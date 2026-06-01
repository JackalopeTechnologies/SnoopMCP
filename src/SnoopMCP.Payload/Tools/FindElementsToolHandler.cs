// FindElementsToolHandler.cs
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
