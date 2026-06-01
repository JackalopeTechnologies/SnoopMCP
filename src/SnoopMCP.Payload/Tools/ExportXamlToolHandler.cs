// ExportXamlToolHandler.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#region Usings

using System.Text.Json;
using System.Windows;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

#endregion

namespace SnoopMCP.Payload.Tools;

/// <summary>
///     Wire handler for the <c>exportXaml</c> tool. Resolves the element id and marshals
///     <see cref="XamlExporter.Export" /> onto the WPF dispatcher.
/// </summary>
public sealed class ExportXamlToolHandler : IToolHandler
{
    /// <summary>
    ///     Initialises a new <see cref="ExportXamlToolHandler" />.
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

    private readonly XamlExporter mExporter;
    private readonly DispatcherMarshal mMarshal;

    private readonly ElementRegistry mRegistry;

    /// <inheritdoc />
    public string ToolName => ExportXamlToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ExportXamlRequest request = arguments.Deserialize<ExportXamlRequest>(WireSerializer.JsonOptions)
                                    ?? throw new SnoopMcpException(ErrorCode.InvalidArgument,
                                        "Missing request payload.");

        var resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");

        ExportXamlResponse response = mMarshal.Invoke(
            () => mExporter.Export(element),
            cancellationToken);

        var json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }

    /// <summary>The wire-protocol tool name.</summary>
    public const string ExportXamlToolName = "exportXaml";
}
