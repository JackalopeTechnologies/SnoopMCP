// ResolvePathToolHandler.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using Inspection;
using Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using Protocol.Wire;

/// <summary>
/// Wire handler for the <c>resolvePath</c> tool. Resolves the root id and marshals
/// <see cref="PathResolver.Resolve"/> onto the WPF dispatcher.
/// </summary>
public sealed class ResolvePathToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string ResolvePathToolName = "resolvePath";

    private readonly ElementRegistry mRegistry;
    private readonly PathResolver mResolver;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="ResolvePathToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the root id.</param>
    /// <param name="resolver">The path resolver.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public ResolvePathToolHandler(
        ElementRegistry registry,
        PathResolver resolver,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mResolver = resolver;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => ResolvePathToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ResolvePathRequest request = arguments.Deserialize<ResolvePathRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.RootId, out DependencyObject? root);
        if (!resolved || root is null)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Root element id {request.RootId} is not alive.");
        }

        ResolvePathResponse response = mMarshal.Invoke(
            () => mResolver.Resolve(root, request.PathString),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
