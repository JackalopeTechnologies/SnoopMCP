// ListVisualRootsToolHandler.cs
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

namespace SnoopMCP.Payload.Tools;

using System.Text.Json;
using SnoopMCP.Payload.Inspection;
using SnoopMCP.Protocol.Tools;
using SnoopMCP.Protocol.Wire;

/// <summary>
/// Wire handler for the <c>listVisualRoots</c> tool. Marshals
/// <see cref="RootEnumerator.Enumerate"/> onto the WPF dispatcher and serialises the result.
/// </summary>
public sealed class ListVisualRootsToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string ListVisualRootsToolName = "listVisualRoots";

    private readonly RootEnumerator mEnumerator;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="ListVisualRootsToolHandler"/>.
    /// </summary>
    /// <param name="enumerator">The root enumerator.</param>
    /// <param name="marshal">The dispatcher marshal.</param>
    public ListVisualRootsToolHandler(RootEnumerator enumerator, DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(enumerator);
        ArgumentNullException.ThrowIfNull(marshal);
        mEnumerator = enumerator;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => ListVisualRootsToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ListVisualRootsResponse response = mMarshal.Invoke(
            () => mEnumerator.Enumerate(),
            cancellationToken);

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
