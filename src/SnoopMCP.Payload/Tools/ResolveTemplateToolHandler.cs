// ResolveTemplateToolHandler.cs
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
/// Wire handler for the <c>resolveTemplate</c> tool. Resolves the element id and marshals
/// <see cref="TemplateResolver.Resolve"/> onto the WPF dispatcher.
/// </summary>
public sealed class ResolveTemplateToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string ResolveTemplateToolName = "resolveTemplate";

    private readonly ElementRegistry mRegistry;
    private readonly TemplateResolver mResolver;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="ResolveTemplateToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the element id.</param>
    /// <param name="resolver">The template resolver.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public ResolveTemplateToolHandler(
        ElementRegistry registry,
        TemplateResolver resolver,
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
    public string ToolName => ResolveTemplateToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ResolveTemplateRequest request = arguments.Deserialize<ResolveTemplateRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        ResolveTemplateResponse response = mMarshal.Invoke(
            () => mResolver.Resolve(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
