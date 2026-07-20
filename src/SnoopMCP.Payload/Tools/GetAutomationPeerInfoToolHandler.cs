// GetAutomationPeerInfoToolHandler.cs
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
/// Wire handler for the <c>getAutomationPeerInfo</c> tool. Resolves the element id and marshals
/// <see cref="PeerInfoReader.Read"/> onto the WPF dispatcher. Read-only (ungated) — uses the
/// ordinary <see cref="DispatcherMarshal.Invoke{T}"/>, not the mutating variant.
/// </summary>
public sealed class GetAutomationPeerInfoToolHandler : IToolHandler
{
    private readonly ElementRegistry mRegistry;
    private readonly PeerInfoReader mReader;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="GetAutomationPeerInfoToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the element id.</param>
    /// <param name="reader">The automation peer info reader.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public GetAutomationPeerInfoToolHandler(ElementRegistry registry, PeerInfoReader reader, DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mReader = reader;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => ToolNames.GetAutomationPeerInfo;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        GetAutomationPeerInfoRequest request = arguments.Deserialize<GetAutomationPeerInfoRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
        {
            throw new SnoopMcpException(ErrorCode.ElementExpired, $"Element id {request.Id} is not alive.");
        }

        GetAutomationPeerInfoResponse response = mMarshal.Invoke(
            () => mReader.Read(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
