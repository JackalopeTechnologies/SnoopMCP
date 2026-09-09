// ExecuteCommandToolHandler.cs
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
/// Wire handler for <c>executeCommand</c>: resolves and executes the <c>ICommand</c> bound to an
/// element in-process. Supports two dispatch modes (see <see cref="ExecuteCommandRequest.Dispatch"/>):
/// <c>"wait"</c> (default) awaits the execution via <see cref="DispatcherMarshal.InvokeMutating{T}"/>
/// and reports the observed outcome; <c>"post"</c> validates first via
/// <see cref="CommandInvoker.Prepare"/> on the UI thread (so an unresolvable command or a false
/// <c>CanExecute</c> still surfaces as a structured error), then fires the execution via
/// <see cref="DispatcherMarshal.Post"/> and returns immediately without observing it — for commands
/// that would otherwise block the mutating wait indefinitely (e.g. one that opens a modal dialog).
/// </summary>
public sealed class ExecuteCommandToolHandler : IToolHandler
{
    private const string DispatchPost = "post";

    private readonly ElementRegistry mRegistry;
    private readonly CommandInvoker mInvoker;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="ExecuteCommandToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the element id.</param>
    /// <param name="invoker">The command invoker.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public ExecuteCommandToolHandler(ElementRegistry registry, CommandInvoker invoker, DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mInvoker = invoker;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => ToolNames.ExecuteCommand;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ExecuteCommandRequest request = arguments.Deserialize<ExecuteCommandRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
        {
            throw new SnoopMcpException(ErrorCode.ElementExpired, $"Element id {request.Id} is not alive.");
        }

        ExecuteCommandResponse response;
        bool isPost = string.Equals(request.Dispatch, DispatchPost, StringComparison.OrdinalIgnoreCase);
        if (isPost)
        {
            // Fire-and-forget applies to the EXECUTION only: it may pump a nested message loop (e.g. a
            // modal dialog) that would never return to a waiting caller. Resolution and CanExecute are
            // UI-thread queries, not mutations, so they run first as a bounded read-only dispatcher
            // call and their failures reach the client as structured errors instead of vanishing
            // behind Dispatched=true. CanExecute is therefore an observed fact (true) while Executed
            // stays null: the command has not run by the time this returns — verify the effect
            // separately (e.g. waitForValue).
            Action fire = mMarshal.Invoke(
                () => mInvoker.Prepare(element, request.Path, request.Parameter),
                cancellationToken);
            mMarshal.Post(fire);
            response = new ExecuteCommandResponse(Executed: null, CanExecute: true, Dispatched: true);
        }
        else
        {
            // A dispatcher timeout here throws ActionPending — correct, since the mutation may already
            // have applied by the time the wait gives up. CommandInvoker.Execute returns the full
            // response directly (unlike a void peer action), so the dispatched work is a plain
            // expression lambda with no `return` keyword of its own — nothing here counts against this
            // method's single-return shape.
            response = mMarshal.InvokeMutating(() => mInvoker.Execute(element, request.Path, request.Parameter), cancellationToken);
        }

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return Task.FromResult(doc.RootElement.Clone());
    }
}
