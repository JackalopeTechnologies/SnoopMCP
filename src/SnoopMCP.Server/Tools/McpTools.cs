// McpTools.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tools;

using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Automation;
using Injection;
using Protocol;
using Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using Protocol.Wire;

/// <summary>
/// The MCP tool surface exposed to the LLM client: one tool per inspection operation plus
/// <c>attach</c> / <c>detach</c>. Every inspection tool forwards its typed arguments through
/// <see cref="Dispatch"/> and returns the payload's result element unchanged so the MCP SDK
/// serialises it to the client as-is. Wire tool names come from <see cref="ToolNames"/> so the host
/// holds no magic strings. This class is also the one boundary where SnoopMCP's own
/// <see cref="SnoopMcpException"/> is promoted to <see cref="McpException"/> (see <see cref="Promote"/>)
/// so the curated code + message reach the model instead of the SDK's sanitised fallback text.
/// Read-only relays (<c>getAutomationPeerInfo</c>, <c>waitForValue</c>) are ungated; the two mutating
/// relays (<c>peerInvoke</c>, <c>executeCommand</c>) additionally require the host
/// <see cref="InteractionGate"/> to be enabled, checked BEFORE <see cref="Dispatch"/> so a gate-off
/// call always reports <see cref="ErrorCode.InteractionDisabled"/> rather than a session/attach
/// failure - the host is the trust boundary here; the payload handlers are not gate-aware.
/// </summary>
[McpServerToolType]
public sealed class McpTools
{
    private const string RootsProperty = "roots";
    private const string ElementExpiredHint =
        " Element ids are per-session and reset on attach; call list_visual_roots to get current root ids.";

    private readonly SessionManager mSession;
    private readonly IInjectorService mInjector;
    private readonly InteractionGate mGate;

    /// <summary>
    /// Initialises a new <see cref="McpTools"/>.
    /// </summary>
    /// <param name="session">The session manager that owns the attached target.</param>
    /// <param name="injector">The injector used to attach the payload to a target process.</param>
    /// <param name="gate">The host interaction gate guarding the mutating relay tools.</param>
    public McpTools(SessionManager session, IInjectorService injector, InteractionGate gate)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(injector);
        ArgumentNullException.ThrowIfNull(gate);
        mSession = session;
        mInjector = injector;
        mGate = gate;
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
        JsonElement result;
        try
        {
            string pipeName = SessionManager.AllocatePipeName();
            ProcessProbeResult probe = await mInjector.ProbeAsync(pid, cancellationToken).ConfigureAwait(false);
            await mInjector.InjectAsync(pid, pipeName, cancellationToken).ConfigureAwait(false);
            await mSession.OpenAsync(pipeName, cancellationToken).ConfigureAwait(false);

            // Seed the payload's element registry with the live visual roots and hand them back, so a
            // freshly attached caller has valid root element ids immediately - element ids are
            // per-session and a previous session's ids are dead after re-attach. This spares the common
            // stale-id trap of reusing an old root id before calling list_visual_roots.
            JsonElement? visualRoots = await TrySeedVisualRootsAsync(cancellationToken).ConfigureAwait(false);

            var payload = new
            {
                sessionId = pipeName,
                processName = probe.ProcessName,
                runtimeVersion = probe.RuntimeVersion,
                frameworkVersion = probe.FrameworkVersion,
                bitness = probe.Bitness,
                visualRoots
            };
            result = SerializeResult(payload);
        }
        catch (SnoopMcpException ex)
        {
            throw Promote(ex);
        }
        return result;
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
        Dispatch(ToolNames.ListVisualRoots, new ListVisualRootsRequest(), cancellationToken);

    /// <summary>Describes an element by id.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Describe an element by id.")]
    public Task<JsonElement> DescribeElement(int id, CancellationToken cancellationToken) =>
        Dispatch(ToolNames.DescribeElement, new DescribeElementRequest(id), cancellationToken);

    /// <summary>Enumerates the visual or logical children of an element.</summary>
    /// <param name="id">The parent element id.</param>
    /// <param name="tree">The tree to walk: <c>visual</c> or <c>logical</c>.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Enumerate visual or logical children.")]
    public Task<JsonElement> GetChildren(int id, string tree, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(tree);
        return Dispatch(ToolNames.GetChildren, new GetChildrenRequest(id, tree), cancellationToken);
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
        return Dispatch(ToolNames.GetParent, new GetParentRequest(id, tree), cancellationToken);
    }

    /// <summary>Gets the templated parent of an element, if any.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Get the TemplatedParent if any.")]
    public Task<JsonElement> GetTemplatedParent(int id, CancellationToken cancellationToken) =>
        Dispatch(ToolNames.GetTemplatedParent, new GetTemplatedParentRequest(id), cancellationToken);

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
        return Dispatch(
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
        Dispatch(ToolNames.HitTest, new HitTestRequest(rootId, x, y), cancellationToken);

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
        return Dispatch(
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
        Dispatch(ToolNames.DescribeDataContext, new DescribeDataContextRequest(id), cancellationToken);

    /// <summary>Reads a dotted property path off an element's DataContext.</summary>
    /// <param name="id">The element id whose DataContext roots the walk.</param>
    /// <param name="path">The dot-separated property path.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Read a dotted property path off the DataContext.")]
    public Task<JsonElement> ReadDataContextPath(int id, string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Dispatch(
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
        Dispatch(
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
        return Dispatch(
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
        Dispatch(ToolNames.ResolveStyle, new ResolveStyleRequest(id), cancellationToken);

    /// <summary>Resolves the applied ControlTemplate.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Resolve the applied ControlTemplate.")]
    public Task<JsonElement> ResolveTemplate(int id, CancellationToken cancellationToken) =>
        Dispatch(ToolNames.ResolveTemplate, new ResolveTemplateRequest(id), cancellationToken);

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
        return Dispatch(
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
        Dispatch(
            ToolNames.ListBindings,
            new ListBindingsRequest(id, includeDescendants),
            cancellationToken);

    /// <summary>Serialises an element to XAML reflecting its current live state.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description("Serialize an element to XAML reflecting its current live state.")]
    public Task<JsonElement> ExportXaml(int id, CancellationToken cancellationToken) =>
        Dispatch(ToolNames.ExportXaml, new ExportXamlRequest(id), cancellationToken);

    /// <summary>Bridges a Snoop element id to its UIA identity via its AutomationPeer.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description(
        "Bridge a Snoop element id to its UIA identity (AutomationId/Name/ClassName/ControlType). Read-only.")]
    public Task<JsonElement> GetAutomationPeerInfo(int id, CancellationToken cancellationToken) =>
        Dispatch(ToolNames.GetAutomationPeerInfo, new GetAutomationPeerInfoRequest(id), cancellationToken);

    /// <summary>Polls a dependency property or DataContext path until it matches an expected value.</summary>
    /// <param name="id">The element id whose DP or DataContext is polled.</param>
    /// <param name="expected">The expected value, compared via ordinal string equality.</param>
    /// <param name="timeoutMs">The maximum time to poll, in milliseconds.</param>
    /// <param name="dependencyProperty">The DP registered name to poll, or null.</param>
    /// <param name="dataContextPath">The dotted DataContext path to poll, or null.</param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    /// <remarks>
    /// The two poll targets are an either/or pair and are declared last, with defaults, so each is
    /// published as optional. A nullable parameter without a default is published as REQUIRED (the SDK
    /// derives that from the missing default, not from nullability), which previously forced callers to
    /// supply both halves of the pair — see issue #72.
    /// </remarks>
    [McpServerTool, Description(
        "Poll a DP or DataContext path until it equals an expected value (ground-truth check). Read-only.")]
    public Task<JsonElement> WaitForValue(
        int id,
        string expected,
        int timeoutMs,
        string? dependencyProperty = null,
        string? dataContextPath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return Dispatch(
            ToolNames.WaitForValue,
            new WaitForValueRequest(id, dependencyProperty, dataContextPath, expected, timeoutMs),
            cancellationToken);
    }

    /// <summary>MUTATES the target: drives an element's AutomationPeer pattern in-process.</summary>
    /// <param name="id">The element id whose AutomationPeer is driven.</param>
    /// <param name="pattern">The peer pattern to invoke: Invoke | Toggle | SelectionItem | ExpandCollapse.</param>
    /// <param name="dispatch">
    /// Dispatch mode: null/"wait" (default) waits; "post" fires-and-forgets. In "post" mode
    /// CanExecute/errors are NOT surfaced — the caller must verify the effect separately (e.g. waitForValue).
    /// </param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description(
        "MUTATES the target: drive an element's AutomationPeer pattern in-process. Requires the " +
        "interaction gate. Optional dispatch='post' fires-and-forgets a dialog-opening action; in " +
        "'post' mode the outcome (including errors) is not surfaced — verify the effect separately.")]
    public Task<JsonElement> PeerInvoke(
        int id,
        string pattern,
        string? dispatch = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);
        RequireGate();
        return Dispatch(ToolNames.PeerInvoke, new PeerInvokeRequest(id, pattern, dispatch), cancellationToken);
    }

    /// <summary>MUTATES the target: executes the ICommand bound to an element (CanExecute-gated).</summary>
    /// <param name="id">The element id: an ICommandSource or the root for <paramref name="path"/>.</param>
    /// <param name="path">Optional dotted DataContext path to an ICommand; null uses the element's own Command.</param>
    /// <param name="parameter">Optional command parameter; null uses the element's CommandParameter.</param>
    /// <param name="dispatch">
    /// Dispatch mode: null/"wait" (default) waits; "post" fires-and-forgets. In "post" mode
    /// CanExecute/errors are NOT surfaced — the caller must verify the effect separately (e.g. waitForValue).
    /// </param>
    /// <param name="cancellationToken">A token to observe while dispatching.</param>
    /// <returns>The payload's result element.</returns>
    [McpServerTool, Description(
        "MUTATES the target: execute the ICommand bound to an element (CanExecute-gated). Requires the " +
        "interaction gate. Optional dispatch='post' fires-and-forgets; in 'post' mode the outcome " +
        "(including CanExecute==false or errors) is not surfaced — verify the effect separately.")]
    public Task<JsonElement> ExecuteCommand(
        int id,
        string? path = null,
        string? parameter = null,
        string? dispatch = null,
        CancellationToken cancellationToken = default)
    {
        RequireGate();
        return Dispatch(
            ToolNames.ExecuteCommand,
            new ExecuteCommandRequest(id, path, parameter, dispatch),
            cancellationToken);
    }

    /// <summary>
    /// Dispatches a tool call through the open session and, on failure, promotes SnoopMCP's curated
    /// <see cref="SnoopMcpException"/> to <see cref="McpException"/> so the SDK propagates its code +
    /// message to the client rather than sanitising it to "An error occurred invoking '…'".
    /// </summary>
    private async Task<JsonElement> Dispatch(string toolName, object arguments, CancellationToken cancellationToken)
    {
        JsonElement result;
        try
        {
            result = await mSession.SendAsync(toolName, arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (SnoopMcpException ex)
        {
            throw Promote(ex);
        }
        return result;
    }

    private async Task<JsonElement?> TrySeedVisualRootsAsync(CancellationToken cancellationToken)
    {
        JsonElement? roots = null;
        try
        {
            JsonElement response = await mSession
                .SendAsync(ToolNames.ListVisualRoots, new ListVisualRootsRequest(), cancellationToken)
                .ConfigureAwait(false);
            if (response.TryGetProperty(RootsProperty, out JsonElement array))
            {
                roots = array.Clone();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Seeding the visual roots is a best-effort convenience: the session is already open and
            // valid without them, and the caller can fall back to list_visual_roots. Never fail attach.
            roots = null;
        }
        return roots;
    }

    /// <summary>Throws a promoted <see cref="ErrorCode.InteractionDisabled"/> unless the host gate is enabled.</summary>
    private void RequireGate()
    {
        if (!mGate.IsEnabled)
        {
            throw Promote(new SnoopMcpException(
                ErrorCode.InteractionDisabled,
                "Driving is disabled. Ask the user to enable interaction in the SnoopMCP tray menu."));
        }
    }

    private static McpException Promote(SnoopMcpException ex)
    {
        string hint = ex.Code == ErrorCode.ElementExpired ? ElementExpiredHint : string.Empty;
        return new McpException($"[{ex.Code}] {ex.Message}{hint}", ex);
    }

    private static JsonElement SerializeResult(object payload)
    {
        string json = JsonSerializer.Serialize(payload, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
