// GetChildrenToolHandler.cs
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
/// Wire handler for the <c>getChildren</c> tool. Resolves the parent id and marshals
/// <see cref="ChildrenEnumerator.Enumerate"/> onto the WPF dispatcher.
/// </summary>
public sealed class GetChildrenToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string GetChildrenToolName = "getChildren";

    private readonly ElementRegistry mRegistry;
    private readonly ChildrenEnumerator mEnumerator;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="GetChildrenToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the parent id.</param>
    /// <param name="enumerator">The children enumerator.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public GetChildrenToolHandler(
        ElementRegistry registry,
        ChildrenEnumerator enumerator,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(enumerator);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mEnumerator = enumerator;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => GetChildrenToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        GetChildrenRequest request = arguments.Deserialize<GetChildrenRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} not alive.");
        }

        GetChildrenResponse response = mMarshal.Invoke(
            () => mEnumerator.Enumerate(element, request.Tree),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
