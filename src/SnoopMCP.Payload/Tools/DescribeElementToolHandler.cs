// DescribeElementToolHandler.cs
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
///     Wire handler for the <c>describeElement</c> tool. Resolves the supplied id and marshals
///     <see cref="ElementDescriber.Describe" /> onto the WPF dispatcher.
/// </summary>
public sealed class DescribeElementToolHandler : IToolHandler
{
    /// <summary>
    ///     Initialises a new <see cref="DescribeElementToolHandler" />.
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

    private readonly ElementDescriber mDescriber;
    private readonly DispatcherMarshal mMarshal;

    private readonly ElementRegistry mRegistry;

    /// <inheritdoc />
    public string ToolName => DescribeElementToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        DescribeElementRequest request = arguments.Deserialize<DescribeElementRequest>(WireSerializer.JsonOptions)
                                         ?? throw new SnoopMcpException(ErrorCode.InvalidArgument,
                                             "Missing request payload.");

        var resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");

        DescribeElementResponse response = mMarshal.Invoke(
            () => mDescriber.Describe(element),
            cancellationToken);

        var json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }

    /// <summary>The wire-protocol tool name.</summary>
    public const string DescribeElementToolName = "describeElement";
}
