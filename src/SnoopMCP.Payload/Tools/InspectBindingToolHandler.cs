// InspectBindingToolHandler.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

/// <summary>
/// Wire handler for the <c>inspectBinding</c> tool. Resolves the element id and marshals
/// <see cref="BindingInspector.Inspect"/> onto the WPF dispatcher.
/// </summary>
public sealed class InspectBindingToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string InspectBindingToolName = "inspectBinding";

    private readonly ElementRegistry mRegistry;
    private readonly BindingInspector mInspector;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="InspectBindingToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the element id.</param>
    /// <param name="inspector">The binding inspector.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public InspectBindingToolHandler(
        ElementRegistry registry,
        BindingInspector inspector,
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
    public string ToolName => InspectBindingToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        InspectBindingRequest request = arguments.Deserialize<InspectBindingRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        InspectBindingResponse response = mMarshal.Invoke(
            () => mInspector.Inspect(element, request.PropertyName),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
