// McpTools.cs
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

namespace SnoopMCP.Host.Tools;

using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SnoopMCP.Host.Injection;
using SnoopMCP.Protocol;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

/// <summary>
/// The MCP tool surface exposed to the LLM client: one tool per inspection operation plus
/// <c>attach</c> / <c>detach</c>. Every inspection tool forwards its typed arguments through
/// <see cref="SessionManager.SendAsync"/> and returns the payload's result element unchanged so the
/// MCP SDK serialises it to the client as-is. Wire tool names come from <see cref="ToolNames"/> so
/// the host holds no magic strings.
/// </summary>
[McpServerToolType]
public sealed class McpTools
{
    private readonly SessionManager mSession;
    private readonly IInjectorService mInjector;

    /// <summary>
    /// Initialises a new <see cref="McpTools"/>.
    /// </summary>
    /// <param name="session">The session manager that owns the attached target.</param>
    /// <param name="injector">The injector used to attach the payload to a target process.</param>
    public McpTools(SessionManager session, IInjectorService injector)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(injector);
        mSession = session;
        mInjector = injector;
    }

    /// <summary>
    /// Enumerates running WPF processes the host can see as candidate attach targets. Host-side,
    /// pre-attach discovery — no injection required — so an LLM client can pick a target by name or
    /// window title instead of needing a PID supplied out-of-band.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while enumerating.</param>
    /// <returns>The set of candidate WPF processes with pid, name, window title, bitness, and attachability.</returns>
    [McpServerTool, Description(
        "List running WPF processes that can be attached to (pid, name, window title, bitness).")]
    public static Task<JsonElement> ListWpfProcesses(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = new ListWpfProcessesResponse(ProcessEnumerator.ListWpfProcesses());
        return Task.FromResult(SerializeResult(response));
    }

    /// <summary>Attaches to a running WPF process by PID, injecting the payload and opening the session.</summary>
    /// <param name="pid">The target process id.</param>
    /// <param name="cancellationToken">A token to observe while attaching.</param>
    /// <returns>Session metadata describing the attached process.</returns>
    [McpServerTool, Description(
        "Attach to a running WPF process by PID. Generates a pipe, injects the payload, opens the session.")]
    public async Task<JsonElement> Attach(int pid, CancellationToken cancellationToken)
    {
        string pipeName = SessionManager.AllocatePipeName();
        ProcessProbeResult probe = await mInjector.ProbeAsync(pid, cancellationToken).ConfigureAwait(false);
        await mInjector.InjectAsync(pid, pipeName, cancellationToken).ConfigureAwait(false);
        await mSession.OpenAsync(pipeName, cancellationToken).ConfigureAwait(false);

        var payload = new
        {
            sessionId = pipeName,
            processName = probe.ProcessName,
            runtimeVersion = probe.RuntimeVersion,
            frameworkVersion = probe.FrameworkVersion,
            bitness = probe.Bitness
        };
        return SerializeResult(payload);
    }

    /// <summary>Detaches from the current session.</summary>
    /// <param name="cancellationToken">A token to observe while detaching.</param>
    /// <returns>An acknowledgement element.</returns>
    [McpServerTool, Description("Detach from the current session.")]
    public async Task<JsonElement> Detach(CancellationToken cancellationToken)
    {
        await mSession.CloseAsync().ConfigureAwait(false);
        return SerializeResult(new { ok = true });
    }

    /// <summary>Enumerates every active visual root in the target process.</summary>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Enumerate every active visual root (window, popup, etc.).")]
    public Task<JsonElement> ListVisualRoots(CancellationToken cancellationToken) =>
        mSession.SendAsync(ToolNames.ListVisualRoots, new ListVisualRootsRequest(), cancellationToken);

    /// <summary>Describes an element by id.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Describe an element by id.")]
    public Task<JsonElement> DescribeElement(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync(ToolNames.DescribeElement, new DescribeElementRequest(id), cancellationToken);

    /// <summary>Enumerates the visual or logical children of an element.</summary>
    /// <param name="id">The parent element id.</param>
    /// <param name="tree">The tree to walk: <c>visual</c> or <c>logical</c>.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Enumerate visual or logical children.")]
    public Task<JsonElement> GetChildren(int id, string tree, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(tree);
        return mSession.SendAsync(ToolNames.GetChildren, new GetChildrenRequest(id, tree), cancellationToken);
    }

    /// <summary>Gets the visual or logical parent of an element.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="tree">The tree to walk: <c>visual</c> or <c>logical</c>.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Get the visual or logical parent.")]
    public Task<JsonElement> GetParent(int id, string tree, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(tree);
        return mSession.SendAsync(ToolNames.GetParent, new GetParentRequest(id, tree), cancellationToken);
    }

    /// <summary>Gets the templated parent of an element, if any.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Get the TemplatedParent if any.")]
    public Task<JsonElement> GetTemplatedParent(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync(ToolNames.GetTemplatedParent, new GetTemplatedParentRequest(id), cancellationToken);

    /// <summary>Finds elements under a root matching an AND-combined predicate.</summary>
    /// <param name="rootId">The root element id whose subtree is searched.</param>
    /// <param name="predicate">The AND-combined predicate; every supplied field must match.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Find elements under rootId matching the AND-combined predicate.")]
    public Task<JsonElement> FindElements(
        int rootId,
        ElementPredicateDto predicate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return mSession.SendAsync(
            ToolNames.FindElements,
            new FindElementsRequest(rootId, predicate),
            cancellationToken);
    }

    /// <summary>Hit tests a root-relative point and returns the deepest visual.</summary>
    /// <param name="rootId">The root element id whose coordinate space the point is expressed in.</param>
    /// <param name="x">The root-relative X coordinate in DIPs.</param>
    /// <param name="y">The root-relative Y coordinate in DIPs.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Hit test a root-relative point and return the deepest visual.")]
    public Task<JsonElement> HitTest(
        int rootId,
        double x,
        double y,
        CancellationToken cancellationToken) =>
        mSession.SendAsync(ToolNames.HitTest, new HitTestRequest(rootId, x, y), cancellationToken);

    /// <summary>Resolves a canonical path string under a root.</summary>
    /// <param name="rootId">The root element id whose subtree is walked.</param>
    /// <param name="pathString">The canonical path string to resolve.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Resolve a canonical path string under rootId.")]
    public Task<JsonElement> ResolvePath(
        int rootId,
        string pathString,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(pathString);
        return mSession.SendAsync(
            ToolNames.ResolvePath,
            new ResolvePathRequest(rootId, pathString),
            cancellationToken);
    }

    /// <summary>Describes the CLR type shape of an element's DataContext.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Describe the CLR type shape of the DataContext.")]
    public Task<JsonElement> DescribeDataContext(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync(ToolNames.DescribeDataContext, new DescribeDataContextRequest(id), cancellationToken);

    /// <summary>Reads a dotted property path off an element's DataContext.</summary>
    /// <param name="id">The element id whose DataContext roots the walk.</param>
    /// <param name="path">The dot-separated property path.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Read a dotted property path off the DataContext.")]
    public Task<JsonElement> ReadDataContextPath(int id, string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return mSession.SendAsync(
            ToolNames.ReadDataContextPath,
            new ReadDataContextPathRequest(id, path),
            cancellationToken);
    }

    /// <summary>Lists the dependency properties reachable on an element.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("List dependency properties on an element.")]
    public Task<JsonElement> ListDependencyProperties(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync(
            ToolNames.ListDependencyProperties,
            new ListDependencyPropertiesRequest(id),
            cancellationToken);

    /// <summary>Gets a dependency property's value and precedence trace.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="propertyName">The dependency property's registered name.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Get DP value and precedence trace.")]
    public Task<JsonElement> GetDependencyProperty(
        int id,
        string propertyName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return mSession.SendAsync(
            ToolNames.GetDependencyProperty,
            new GetDependencyPropertyRequest(id, propertyName),
            cancellationToken);
    }

    /// <summary>Resolves the applied Style and its BasedOn chain.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Resolve the applied Style and its BasedOn chain.")]
    public Task<JsonElement> ResolveStyle(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync(ToolNames.ResolveStyle, new ResolveStyleRequest(id), cancellationToken);

    /// <summary>Resolves the applied ControlTemplate.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Resolve the applied ControlTemplate.")]
    public Task<JsonElement> ResolveTemplate(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync(ToolNames.ResolveTemplate, new ResolveTemplateRequest(id), cancellationToken);

    /// <summary>Inspects the BindingExpression on a property.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="propertyName">The dependency property's registered name carrying the binding.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Inspect the BindingExpression on a property.")]
    public Task<JsonElement> InspectBinding(
        int id,
        string propertyName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return mSession.SendAsync(
            ToolNames.InspectBinding,
            new InspectBindingRequest(id, propertyName),
            cancellationToken);
    }

    /// <summary>Lists every BindingExpression on an element and optionally its descendants.</summary>
    /// <param name="id">The element id at which the audit begins.</param>
    /// <param name="includeDescendants">When <c>true</c>, recurses through the visual subtree.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("List every BindingExpression on an element (and optionally its descendants).")]
    public Task<JsonElement> ListBindings(
        int id,
        bool includeDescendants,
        CancellationToken cancellationToken) =>
        mSession.SendAsync(
            ToolNames.ListBindings,
            new ListBindingsRequest(id, includeDescendants),
            cancellationToken);

    /// <summary>Serialises an element to XAML reflecting its current live state.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Serialize an element to XAML reflecting its current live state.")]
    public Task<JsonElement> ExportXaml(int id, CancellationToken cancellationToken) =>
        mSession.SendAsync(ToolNames.ExportXaml, new ExportXamlRequest(id), cancellationToken);

    private static JsonElement SerializeResult(object payload)
    {
        string json = JsonSerializer.Serialize(payload, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
