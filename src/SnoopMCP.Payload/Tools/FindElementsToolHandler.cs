// FindElementsToolHandler.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

/// <summary>
/// Wire handler for the <c>findElements</c> tool. Resolves the root id and marshals
/// <see cref="ElementFinder.Find"/> onto the WPF dispatcher.
/// </summary>
public sealed class FindElementsToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string FindElementsToolName = "findElements";

    private readonly ElementRegistry mRegistry;
    private readonly ElementFinder mFinder;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="FindElementsToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the root id.</param>
    /// <param name="finder">The element finder.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public FindElementsToolHandler(
        ElementRegistry registry,
        ElementFinder finder,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(finder);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mFinder = finder;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => FindElementsToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        FindElementsRequest request = arguments.Deserialize<FindElementsRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.RootId, out DependencyObject? root);
        if (!resolved || root is null)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Root element id {request.RootId} is not alive.");
        }

        FindElementsResponse response = mMarshal.Invoke(
            () => mFinder.Find(root, request.Predicate),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
