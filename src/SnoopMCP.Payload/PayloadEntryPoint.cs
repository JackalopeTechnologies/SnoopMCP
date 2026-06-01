// PayloadEntryPoint.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#region Usings

using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Payload.Tools;

#endregion

namespace SnoopMCP.Payload;

/// <summary>
///     Static entry point invoked by Snoop's <c>ManagedInjector</c> after the payload assembly is loaded
///     into the target process. The injector convention dictates the signature
///     <c>public static int &lt;MethodName&gt;(string args)</c>; we pass the named-pipe name as <c>args</c>.
/// </summary>
public static class PayloadEntryPoint
{
    /// <summary>
    ///     Called by <c>ManagedInjector</c> from within the target process. This is a thin bootstrap:
    ///     it installs an <see cref="AppDomain.AssemblyResolve" /> handler that probes the payload's own
    ///     directory, THEN delegates to <see cref="Run" />. The split matters — the payload's dependencies
    ///     (<c>SnoopMCP.Protocol</c>, <c>Microsoft.Extensions.Logging.Abstractions</c>) sit next to the
    ///     payload, not in the target app's base directory, so without this handler the CLR fails to
    ///     resolve them when JIT-compiling <see cref="Run" />. The bootstrap itself references only shared
    ///     framework types, so it JITs cleanly before the handler is needed.
    /// </summary>
    /// <param name="args">The named-pipe instance to bind. Must not be empty.</param>
    /// <returns><c>0</c> on success; non-zero when payload initialisation fails.</returns>
    public static int Inject(string args)
    {
        ArgumentException.ThrowIfNullOrEmpty(args);
        AppDomain.CurrentDomain.AssemblyResolve += ResolveFromPayloadDirectory;
        return Run(args.Trim());
    }

    /// <summary>
    ///     Resolves payload dependencies from the payload assembly's own directory. The target process
    ///     only probes its own base directory, so transitive payload dependencies must be loaded by hand.
    /// </summary>
    private static Assembly? ResolveFromPayloadDirectory(object? sender, ResolveEventArgs eventArgs)
    {
        Assembly? resolved = null;
        var requested = new AssemblyName(eventArgs.Name);
        var simpleName = requested.Name ?? string.Empty;
        var isResolvable = simpleName.Length > 0
                           && !simpleName.EndsWith(ResourcesAssemblySuffix, StringComparison.Ordinal);
        if (isResolvable)
        {
            var payloadDirectory = Path.GetDirectoryName(typeof(PayloadEntryPoint).Assembly.Location)
                                   ?? string.Empty;
            var candidate = Path.Combine(payloadDirectory, simpleName + ManagedAssemblyExtension);
            if (File.Exists(candidate)) resolved = Assembly.LoadFrom(candidate);
        }

        return resolved;
    }

    /// <summary>
    ///     Builds the tool registry and starts the pipe server on a background task, returning immediately.
    ///     Invoked only after <see cref="Inject" /> has installed the dependency resolver.
    /// </summary>
    /// <param name="pipeName">The named-pipe instance to bind.</param>
    /// <returns><c>0</c> on success; non-zero when payload initialisation fails.</returns>
    private static int Run(string pipeName)
    {
        var exitCode = 0;
        try
        {
            if (Application.Current is null)
                throw new InvalidOperationException(
                    "Application.Current is null; payload must inject into a running WPF app.");

            var registry = new ElementRegistry();
            var emitter = new PathStringEmitter();
            var describer = new ElementDescriber(registry, emitter);
            var marshal = new DispatcherMarshal(Application.Current.Dispatcher);

            var ownerResolver = new PopupOwnerResolver();
            var rootEnumerator = new RootEnumerator(registry, ownerResolver);
            var childrenEnumerator = new ChildrenEnumerator(describer);
            var parentNavigator = new ParentNavigator(describer);
            var elementFinder = new ElementFinder(describer);
            var hitTester = new HitTester(describer);
            var pathParser = new PathStringParser();
            var pathResolver = new PathResolver(describer, pathParser);
            var dataContextInspector = new DataContextInspector();
            var dpInspector = new DependencyPropertyInspector();
            var styleResolver = new StyleResolver();
            var templateResolver = new TemplateResolver(registry, describer);
            var bindingInspector = new BindingInspector(registry);
            var xamlExporter = new XamlExporter();

            var toolRegistry = new ToolRegistry();
            toolRegistry.Register(new EchoToolHandler());
            toolRegistry.Register(new DescribeElementToolHandler(registry, describer, marshal));
            toolRegistry.Register(new ListVisualRootsToolHandler(rootEnumerator, marshal));
            toolRegistry.Register(new GetChildrenToolHandler(registry, childrenEnumerator, marshal));
            toolRegistry.Register(new GetParentToolHandler(registry, parentNavigator, marshal));
            toolRegistry.Register(new GetTemplatedParentToolHandler(registry, parentNavigator, marshal));
            toolRegistry.Register(new FindElementsToolHandler(registry, elementFinder, marshal));
            toolRegistry.Register(new HitTestToolHandler(registry, hitTester, marshal));
            toolRegistry.Register(new ResolvePathToolHandler(registry, pathResolver, marshal));
            toolRegistry.Register(new DescribeDataContextToolHandler(registry, dataContextInspector, marshal));
            toolRegistry.Register(new ReadDataContextPathToolHandler(registry, dataContextInspector, marshal));
            toolRegistry.Register(new ListDependencyPropertiesToolHandler(registry, dpInspector, marshal));
            toolRegistry.Register(new GetDependencyPropertyToolHandler(registry, dpInspector, marshal));
            toolRegistry.Register(new ResolveStyleToolHandler(registry, styleResolver, marshal));
            toolRegistry.Register(new ResolveTemplateToolHandler(registry, templateResolver, marshal));
            toolRegistry.Register(new InspectBindingToolHandler(registry, bindingInspector, marshal));
            toolRegistry.Register(new ListBindingsToolHandler(registry, bindingInspector, marshal));
            toolRegistry.Register(new ExportXamlToolHandler(registry, xamlExporter, marshal));

            ILogger<PipeServer> logger = NullLogger<PipeServer>.Instance;
            psServer = new PipeServer(pipeName, toolRegistry, logger);
            psServer.Start();
        }
        catch (Exception)
        {
            const int injectionFailedExitCode = 1;
            exitCode = injectionFailedExitCode;
        }

        return exitCode;
    }

    private const string ManagedAssemblyExtension = ".dll";
    private const string ResourcesAssemblySuffix = ".resources";

    private static PipeServer? psServer;
}
