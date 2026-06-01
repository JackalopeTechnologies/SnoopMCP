// ListDependencyPropertiesToolHandler.cs
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
/// Wire handler for the <c>listDependencyProperties</c> tool. Resolves the element id and marshals
/// <see cref="DependencyPropertyInspector.ListProperties"/> onto the WPF dispatcher.
/// </summary>
public sealed class ListDependencyPropertiesToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string ListDependencyPropertiesToolName = "listDependencyProperties";

    private readonly ElementRegistry mRegistry;
    private readonly DependencyPropertyInspector mInspector;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="ListDependencyPropertiesToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the element id.</param>
    /// <param name="inspector">The dependency-property inspector.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public ListDependencyPropertiesToolHandler(
        ElementRegistry registry,
        DependencyPropertyInspector inspector,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mInspector = inspector;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => ListDependencyPropertiesToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ListDependencyPropertiesRequest request = arguments.Deserialize<ListDependencyPropertiesRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        ListDependencyPropertiesResponse response = mMarshal.Invoke(
            () => mInspector.ListProperties(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
