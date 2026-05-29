// ResolveStyleToolHandler.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

/// <summary>
/// Wire handler for the <c>resolveStyle</c> tool. Resolves the element id and marshals
/// <see cref="StyleResolver.Resolve"/> onto the WPF dispatcher.
/// </summary>
public sealed class ResolveStyleToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string ResolveStyleToolName = "resolveStyle";

    private readonly ElementRegistry mRegistry;
    private readonly StyleResolver mResolver;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="ResolveStyleToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the element id.</param>
    /// <param name="resolver">The style resolver.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public ResolveStyleToolHandler(
        ElementRegistry registry,
        StyleResolver resolver,
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
    public string ToolName => ResolveStyleToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ResolveStyleRequest request = arguments.Deserialize<ResolveStyleRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        ResolveStyleResponse response = mMarshal.Invoke(
            () => mResolver.Resolve(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
