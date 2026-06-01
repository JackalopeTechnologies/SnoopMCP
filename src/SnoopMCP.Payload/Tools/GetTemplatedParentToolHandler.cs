// GetTemplatedParentToolHandler.cs
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
/// Wire handler for the <c>getTemplatedParent</c> tool. Resolves the child id and marshals
/// <see cref="ParentNavigator.GetTemplatedParent"/> onto the WPF dispatcher.
/// </summary>
public sealed class GetTemplatedParentToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string GetTemplatedParentToolName = "getTemplatedParent";

    private readonly ElementRegistry mRegistry;
    private readonly ParentNavigator mNavigator;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="GetTemplatedParentToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the child id.</param>
    /// <param name="navigator">The parent navigator.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public GetTemplatedParentToolHandler(
        ElementRegistry registry,
        ParentNavigator navigator,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mNavigator = navigator;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => GetTemplatedParentToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        GetTemplatedParentRequest request = arguments.Deserialize<GetTemplatedParentRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} not alive.");
        }

        GetTemplatedParentResponse response = mMarshal.Invoke(
            () => mNavigator.GetTemplatedParent(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
