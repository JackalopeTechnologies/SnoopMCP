// WaitForValueToolHandler.cs
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
/// Wire handler for the <c>waitForValue</c> tool: polls a dependency property or a dotted
/// DataContext path in-process until it matches an expected value or the timeout elapses. This is
/// ground-truth verification (reads VM/DP reality, not pixels). Read-only — ungated, no mutating
/// dispatch mode. The poll loop itself runs off the pipe-handling thread; each read is marshalled
/// onto the UI thread by <see cref="ValueWaiter.WaitAsync"/>.
/// </summary>
public sealed class WaitForValueToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly ValueWaiter mWaiter;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="WaitForValueToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the element id.</param>
    /// <param name="waiter">The value waiter.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread for each poll.</param>
    public WaitForValueToolHandler(ElementRegistry registry, ValueWaiter waiter, DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(waiter);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mWaiter = waiter;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => ToolNames.WaitForValue;

    /// <inheritdoc />
    public async Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        WaitForValueRequest request = arguments.Deserialize<WaitForValueRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
        {
            throw new SnoopMcpException(ErrorCode.ElementExpired, $"Element id {request.Id} is not alive.");
        }

        WaitForValueResponse response = await mWaiter.WaitAsync(
            element,
            request.DependencyProperty,
            request.DataContextPath,
            request.Expected,
            request.TimeoutMs,
            mMarshal,
            cancellationToken).ConfigureAwait(false);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
