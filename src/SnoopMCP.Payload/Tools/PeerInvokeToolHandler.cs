// PeerInvokeToolHandler.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using Interaction;
using Protocol;
using Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using Protocol.Wire;

/// <summary>
/// Wire handler for <c>peerInvoke</c>: drives the element's AutomationPeer pattern in-process.
/// Supports two dispatch modes (see <see cref="PeerInvokeRequest.Dispatch"/>): <c>"wait"</c> (default)
/// awaits the action via <see cref="DispatcherMarshal.InvokeMutating{T}"/> and reports the observed
/// outcome; <c>"post"</c> fires the action via <see cref="DispatcherMarshal.Post"/> and returns
/// immediately without observing it — for actions (e.g. those opening a modal dialog) that would
/// otherwise block the mutating wait indefinitely.
/// </summary>
public sealed class PeerInvokeToolHandler : IToolHandler
{
    private const string DispatchPost = "post";

    private readonly ElementRegistry mRegistry;
    private readonly AutomationPeerDriver mDriver;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="PeerInvokeToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the element id.</param>
    /// <param name="driver">The AutomationPeer driver.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public PeerInvokeToolHandler(ElementRegistry registry, AutomationPeerDriver driver, DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mDriver = driver;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => ToolNames.PeerInvoke;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        PeerInvokeRequest request = arguments.Deserialize<PeerInvokeRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
        {
            throw new SnoopMcpException(ErrorCode.ElementExpired, $"Element id {request.Id} is not alive.");
        }

        PeerInvokeResponse response;
        bool isPost = string.Equals(request.Dispatch, DispatchPost, StringComparison.OrdinalIgnoreCase);
        if (isPost)
        {
            // Fire-and-forget: the action may pump a nested message loop (e.g. a modal dialog) that
            // would never return to a waiting caller. The action has not run by the time this returns,
            // so Ok is null (not false) — verify the effect separately (e.g. waitForValue).
            mMarshal.Post(() => mDriver.Invoke(element, request.Pattern));
            response = new PeerInvokeResponse(Ok: null, Dispatched: true);
        }
        else
        {
            // A dispatcher timeout here throws ActionPending — correct, since the mutation may already
            // have applied by the time the wait gives up. The work is delegated to a dedicated method
            // (rather than an inline lambda body) so its own `return` does not count against this
            // method's single-return shape (STR0002/STR0005 scan lambda bodies as part of the enclosing
            // method); the call site below is an expression lambda with no `return` keyword of its own.
            response = mMarshal.InvokeMutating(() => InvokeAndReportOk(element, request.Pattern), cancellationToken);
        }

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }

    private PeerInvokeResponse InvokeAndReportOk(DependencyObject element, string pattern)
    {
        mDriver.Invoke(element, pattern);
        return new PeerInvokeResponse(Ok: true, Dispatched: false);
    }
}
