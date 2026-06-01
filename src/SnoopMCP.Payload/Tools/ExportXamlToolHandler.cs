// ExportXamlToolHandler.cs
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
/// Wire handler for the <c>exportXaml</c> tool. Resolves the element id and marshals
/// <see cref="XamlExporter.Export"/> onto the WPF dispatcher.
/// </summary>
public sealed class ExportXamlToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string ExportXamlToolName = "exportXaml";

    private readonly ElementRegistry mRegistry;
    private readonly XamlExporter mExporter;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="ExportXamlToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the element id.</param>
    /// <param name="exporter">The XAML exporter.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public ExportXamlToolHandler(
        ElementRegistry registry,
        XamlExporter exporter,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mExporter = exporter;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => ExportXamlToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ExportXamlRequest request = arguments.Deserialize<ExportXamlRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        ExportXamlResponse response = mMarshal.Invoke(
            () => mExporter.Export(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
