// ListVisualRootsToolHandler.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using Inspection;
using SnoopMCP.Protocol.Tools;
using Protocol.Wire;

/// <summary>
/// Wire handler for the <c>listVisualRoots</c> tool. Marshals
/// <see cref="RootEnumerator.Enumerate"/> onto the WPF dispatcher and serialises the result.
/// </summary>
public sealed class ListVisualRootsToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string ListVisualRootsToolName = "listVisualRoots";

    private readonly RootEnumerator mEnumerator;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="ListVisualRootsToolHandler"/>.
    /// </summary>
    /// <param name="enumerator">The root enumerator.</param>
    /// <param name="marshal">The dispatcher marshal.</param>
    public ListVisualRootsToolHandler(RootEnumerator enumerator, DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(enumerator);
        ArgumentNullException.ThrowIfNull(marshal);
        mEnumerator = enumerator;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => ListVisualRootsToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ListVisualRootsResponse response = mMarshal.Invoke(
            () => mEnumerator.Enumerate(),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
