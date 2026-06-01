// HitTestToolHandler.cs
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
/// Wire handler for the <c>hitTest</c> tool. Resolves the root id and marshals
/// <see cref="HitTester.HitTest"/> onto the WPF dispatcher.
/// </summary>
public sealed class HitTestToolHandler : IToolHandler
{
    /// <summary>The wire-protocol tool name.</summary>
    public const string HitTestToolName = "hitTest";

    private readonly ElementRegistry mRegistry;
    private readonly HitTester mTester;
    private readonly DispatcherMarshal mMarshal;

    /// <summary>
    /// Initialises a new <see cref="HitTestToolHandler"/>.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the root id.</param>
    /// <param name="tester">The hit tester.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public HitTestToolHandler(
        ElementRegistry registry,
        HitTester tester,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(tester);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mTester = tester;
        mMarshal = marshal;
    }

    /// <inheritdoc />
    public string ToolName => HitTestToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        HitTestRequest request = arguments.Deserialize<HitTestRequest>(WireSerializer.JsonOptions)
            ?? throw new SnoopMcpException(ErrorCode.InvalidArgument, "Missing request payload.");

        bool resolved = mRegistry.TryResolve(request.RootId, out DependencyObject? root);
        if (!resolved || root is null)
        {
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Root element id {request.RootId} is not alive.");
        }

        HitTestResponse response;
        try
        {
            response = mMarshal.Invoke(
                () => mTester.HitTest(root, request.X, request.Y),
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            throw new SnoopMcpException(ErrorCode.InvalidArgument, ex.Message, ex);
        }

        string json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }
}
