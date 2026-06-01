// HitTestToolHandler.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

/// <summary>
/// Wire handler for the <c>hitTest</c> tool. Resolves the root id and marshals
/// <see cref="HitTester.HitTest"/> onto the WPF dispatcher.
/// </summary>
public sealed class HitTestToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string HitTestToolName = "hitTest";

    private readonly ElementRegistry mRegistry;
    private readonly HitTester mTester;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="HitTestToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the root id.</param>
    /// <param name="tester">The hit tester.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public HitTestToolHandler(
        ElementRegistry registry,
        HitTester tester,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(tester);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mTester = tester;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => HitTestToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        HitTestRequest request = arguments.Deserialize<HitTestRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.RootId, out DependencyObject? root);
        if (!resolved || root is null)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Root element id {request.RootId} is not alive.");
        }

        HitTestResponse response;
        try
        {
            response = mMarshal.Invoke(
                () => mTester.HitTest(root, request.X, request.Y),
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            throw new SnoopMcpException(ErrorCode.InvalidArgument, ex.Message, ex);
        }

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
