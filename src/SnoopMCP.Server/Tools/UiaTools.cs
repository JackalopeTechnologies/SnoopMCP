// UiaTools.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tools;

using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Automation;
using Protocol.Errors;
using Protocol.Wire;

/// <summary>
/// MCP tools that drive and observe a live WPF window via out-of-process UI Automation and
/// PrintWindow capture. Session-free (keyed by pid; no attach required). Every tool first confirms the
/// target hosts WPF via <see cref="WpfTargetGuard"/> — see each tool's remarks for exactly where in its
/// body that check runs. Read-only tools are otherwise ungated; the two mutating tools
/// (<c>invokeUia</c>, <c>setUiaValue</c>) additionally require the host <see cref="InteractionGate"/>
/// to be enabled, and check the gate BEFORE the WPF guard so a gate-off call always reports
/// <see cref="ErrorCode.InteractionDisabled"/> rather than an attach failure. This class is also a
/// second boundary (alongside <see cref="McpTools"/>) where SnoopMCP's own
/// <see cref="SnoopMcpException"/> is promoted to <see cref="McpException"/> (see <see cref="Promote"/>)
/// so the curated code + message reach the model instead of the SDK's sanitised fallback text.
/// </summary>
[McpServerToolType]
public sealed class UiaTools
{
    private const string ImagePngMimeType = "image/png";

    private readonly IUiaDriver mDriver;
    private readonly IScreenCapture mCapture;
    private readonly InteractionGate mGate;

    /// <summary>Initialises the tool surface.</summary>
    /// <param name="driver">The UIA driver used by the discovery and driving tools.</param>
    /// <param name="capture">The background window capture used by <see cref="CaptureWindow"/>.</param>
    /// <param name="gate">The host interaction gate guarding the mutating tools.</param>
    public UiaTools(IUiaDriver driver, IScreenCapture capture, InteractionGate gate)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(gate);
        mDriver = driver;
        mCapture = capture;
        mGate = gate;
    }

    /// <summary>Walks the UIA tree of a WPF process to a bounded depth.</summary>
    /// <param name="pid">The target process id.</param>
    /// <param name="fromElement">An element to root the walk at, or null to walk from the top-level window.</param>
    /// <param name="depth">The maximum depth to walk, at least 1.</param>
    /// <param name="cancellationToken">A token to observe while walking.</param>
    /// <returns>The discovered elements.</returns>
    [McpServerTool, Description("Walk the UIA tree of a WPF process (by pid) to a bounded depth. Read-only.")]
    public async Task<JsonElement> GetUiaTree(
        int pid, int depth, UiaElementRef? fromElement = null, CancellationToken cancellationToken = default)
    {
        JsonElement result;
        try
        {
            WpfTargetGuard.EnsureWpf(pid);
            IReadOnlyList<UiaElementInfo> tree =
                await mDriver.GetTreeAsync(pid, fromElement, depth, cancellationToken).ConfigureAwait(false);
            result = SerializeResult(new { elements = tree });
        }
        catch (SnoopMcpException ex) { throw Promote(ex); }
        catch (Exception ex) when (ex is not McpException and not OperationCanceledException)
        {
            // Only an McpException's message reaches the client; everything else is replaced by the
            // SDK's bare "An error occurred invoking '…'" (issue #81). A response that fails to
            // serialize (issue #73) lands here, so it is classified before it can escape untyped.
            throw ToolErrorFilter.Describe(ex, null);
        }
        return result;
    }

    /// <summary>Finds UIA elements under a WPF process by locator.</summary>
    /// <param name="pid">The target process id.</param>
    /// <param name="by">The locator kind: automationId, name, helpText, or controlType.</param>
    /// <param name="value">The locator value to match.</param>
    /// <param name="cancellationToken">A token to observe while searching.</param>
    /// <returns>The matching elements.</returns>
    [McpServerTool, Description(
        "Find UIA elements under a WPF process by locator (automationId|name|helpText|controlType). Read-only.")]
    public async Task<JsonElement> FindUiaElement(int pid, string by, string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(by);
        ArgumentNullException.ThrowIfNull(value);
        JsonElement result;
        try
        {
            WpfTargetGuard.EnsureWpf(pid);
            IReadOnlyList<UiaElementInfo> hits =
                await mDriver.FindAsync(pid, by, value, cancellationToken).ConfigureAwait(false);
            result = SerializeResult(new { matches = hits });
        }
        catch (SnoopMcpException ex) { throw Promote(ex); }
        catch (Exception ex) when (ex is not McpException and not OperationCanceledException)
        {
            // Only an McpException's message reaches the client; everything else is replaced by the
            // SDK's bare "An error occurred invoking '…'" (issue #81). A response that fails to
            // serialize (issue #73) lands here, so it is classified before it can escape untyped.
            throw ToolErrorFilter.Describe(ex, null);
        }
        return result;
    }

    /// <summary>Captures a WPF window's rendered content, even if occluded, as a native MCP image.</summary>
    /// <param name="pid">The target process id.</param>
    /// <param name="cancellationToken">A token to observe before capturing.</param>
    /// <returns>A tool result whose content is a single PNG image block.</returns>
    [McpServerTool, Description("Capture a WPF window (by pid) as a PNG image, even if occluded. Read-only.")]
    public Task<CallToolResult> CaptureWindow(int pid, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallToolResult result;
        try
        {
            WpfTargetGuard.EnsureWpf(pid);
            CaptureResult capture = mCapture.Capture(pid);
            byte[] png = Convert.FromBase64String(capture.Base64);
            result = new CallToolResult { Content = [ImageContentBlock.FromBytes(png, ImagePngMimeType)] };
        }
        catch (SnoopMcpException ex) { throw Promote(ex); }
        catch (Exception ex) when (ex is not McpException and not OperationCanceledException)
        {
            // Only an McpException's message reaches the client; everything else is replaced by the
            // SDK's bare "An error occurred invoking '…'" (issue #81). A response that fails to
            // serialize (issue #73) lands here, so it is classified before it can escape untyped.
            throw ToolErrorFilter.Describe(ex, null);
        }
        return Task.FromResult(result);
    }

    /// <summary>Polls until a UIA element matches the locator or the timeout elapses.</summary>
    /// <param name="pid">The target process id.</param>
    /// <param name="by">The locator kind: automationId, name, helpText, or controlType.</param>
    /// <param name="value">The locator value to match.</param>
    /// <param name="timeoutMs">The maximum time to wait, in milliseconds.</param>
    /// <param name="cancellationToken">A token to observe while polling.</param>
    /// <returns>The matching element.</returns>
    [McpServerTool, Description("Poll until a UIA element matches the locator or the timeout elapses. Read-only.")]
    public async Task<JsonElement> WaitForUia(
        int pid, string by, string value, int timeoutMs, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(by);
        ArgumentNullException.ThrowIfNull(value);
        JsonElement result;
        try
        {
            WpfTargetGuard.EnsureWpf(pid);
            UiaElementInfo hit =
                await mDriver.WaitForAsync(pid, by, value, timeoutMs, cancellationToken).ConfigureAwait(false);
            result = SerializeResult(hit);
        }
        catch (SnoopMcpException ex) { throw Promote(ex); }
        catch (Exception ex) when (ex is not McpException and not OperationCanceledException)
        {
            // Only an McpException's message reaches the client; everything else is replaced by the
            // SDK's bare "An error occurred invoking '…'" (issue #81). A response that fails to
            // serialize (issue #73) lands here, so it is classified before it can escape untyped.
            throw ToolErrorFilter.Describe(ex, null);
        }
        return result;
    }

    /// <summary>MUTATES the target application: invokes an element's action pattern.</summary>
    /// <param name="element">The element to act on.</param>
    /// <param name="pattern">The pattern to use (Invoke/SelectionItem/Toggle/ExpandCollapse), or null to auto-select.</param>
    /// <param name="cancellationToken">A token to observe while invoking.</param>
    /// <returns>An acknowledgement element.</returns>
    [McpServerTool, Description(
        "MUTATES the target application: invokes an element's action pattern " +
        "(Invoke/SelectionItem/Toggle/ExpandCollapse). Requires the host interaction gate to be enabled.")]
    public async Task<JsonElement> InvokeUia(
        UiaElementRef element, string? pattern = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(element);
        RequireGate();
        JsonElement result;
        try
        {
            WpfTargetGuard.EnsureWpf(element.Pid);
            await mDriver.InvokeAsync(element, pattern, cancellationToken).ConfigureAwait(false);
            result = SerializeResult(new { ok = true });
        }
        catch (SnoopMcpException ex) { throw Promote(ex); }
        catch (Exception ex) when (ex is not McpException and not OperationCanceledException)
        {
            // Only an McpException's message reaches the client; everything else is replaced by the
            // SDK's bare "An error occurred invoking '…'" (issue #81). A response that fails to
            // serialize (issue #73) lands here, so it is classified before it can escape untyped.
            throw ToolErrorFilter.Describe(ex, null);
        }
        return result;
    }

    /// <summary>MUTATES the target application: sets an element's value via ValuePattern.</summary>
    /// <param name="element">The element whose value is set.</param>
    /// <param name="value">The new value.</param>
    /// <param name="cancellationToken">A token to observe while setting.</param>
    /// <returns>An acknowledgement element.</returns>
    [McpServerTool, Description(
        "MUTATES the target application: sets an element's value via ValuePattern. " +
        "Requires the host interaction gate to be enabled.")]
    public async Task<JsonElement> SetUiaValue(UiaElementRef element, string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(value);
        RequireGate();
        JsonElement result;
        try
        {
            WpfTargetGuard.EnsureWpf(element.Pid);
            await mDriver.SetValueAsync(element, value, cancellationToken).ConfigureAwait(false);
            result = SerializeResult(new { ok = true });
        }
        catch (SnoopMcpException ex) { throw Promote(ex); }
        catch (Exception ex) when (ex is not McpException and not OperationCanceledException)
        {
            // Only an McpException's message reaches the client; everything else is replaced by the
            // SDK's bare "An error occurred invoking '…'" (issue #81). A response that fails to
            // serialize (issue #73) lands here, so it is classified before it can escape untyped.
            throw ToolErrorFilter.Describe(ex, null);
        }
        return result;
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

    private static McpException Promote(SnoopMcpException ex) => new($"[{ex.Code}] {ex.Message}", ex);

    private static JsonElement SerializeResult(object payload)
    {
        string json = JsonSerializer.Serialize(payload, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
