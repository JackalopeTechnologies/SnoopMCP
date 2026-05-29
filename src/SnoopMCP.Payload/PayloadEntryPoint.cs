// PayloadEntryPoint.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload;

using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Payload.PathStrings;
using SnoopMCP.Payload.Tools;

/// <summary>
/// Static entry point invoked by Snoop's <c>ManagedInjector</c> after the payload assembly is loaded
/// into the target process. The injector convention dictates the signature
/// <c>public static int &lt;MethodName&gt;(string args)</c>; we pass the named-pipe name as <c>args</c>.
/// </summary>
public static class PayloadEntryPoint
{
    private static PipeServer? psServer;

    /// <summary>
    /// Called by <c>ManagedInjector</c> from within the target process.
    /// Starts the pipe server on a background task and returns immediately.
    /// </summary>
    /// <param name="args">The named-pipe instance to bind. Must not be empty.</param>
    /// <returns><c>0</c> on success; non-zero when payload initialisation fails.</returns>
    public static int Inject(string args)
    {
        ArgumentException.ThrowIfNullOrEmpty(args);
        int exitCode = 0;
        try
        {
            string pipeName = args.Trim();

            if (Application.Current is null)
            {
                throw new InvalidOperationException(
                    "Application.Current is null; payload must inject into a running WPF app.");
            }

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
}
