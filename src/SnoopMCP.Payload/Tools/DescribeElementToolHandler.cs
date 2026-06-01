// DescribeElementToolHandler.cs
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
/// Wire handler for the <c>describeElement</c> tool. Resolves the supplied id and marshals
/// <see cref="ElementDescriber.Describe"/> onto the WPF dispatcher.
/// </summary>
public sealed class DescribeElementToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string DescribeElementToolName = "describeElement";

    private readonly ElementRegistry mRegistry;
    private readonly ElementDescriber mDescriber;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="DescribeElementToolHandler"/>.
    /// </summary>
    /// <param name="registry">The element registry that resolves wire ids to live objects.</param>
    /// <param name="describer">The describer that builds the response payload.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public DescribeElementToolHandler(
        ElementRegistry registry,
        ElementDescriber describer,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(describer);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mDescriber = describer;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => DescribeElementToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        DescribeElementRequest request = arguments.Deserialize<DescribeElementRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");
        }

        DescribeElementResponse response = mMarshal.Invoke(
            () => mDescriber.Describe(element),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
