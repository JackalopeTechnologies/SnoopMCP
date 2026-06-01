// ResolvePathToolHandler.cs
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
///     Wire handler for the <c>resolvePath</c> tool. Resolves the root id and marshals
///     <see cref="PathResolver.Resolve" /> onto the WPF dispatcher.
/// </summary>
public sealed class ResolvePathToolHandler : IToolHandler
{
    /// <summary>
    ///     Initialises a new <see cref="ResolvePathToolHandler" />.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the root id.</param>
    /// <param name="resolver">The path resolver.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public ResolvePathToolHandler(
        ElementRegistry registry,
        PathResolver resolver,
        DispatcherMarshal marshal)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(marshal);
        mRegistry = registry;
        mResolver = resolver;
        mMarshal = marshal;
    }

    private readonly DispatcherMarshal mMarshal;

    private readonly ElementRegistry mRegistry;
    private readonly PathResolver mResolver;

    /// <inheritdoc />
    public string ToolName => ResolvePathToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ResolvePathRequest request = arguments.Deserialize<ResolvePathRequest>(WireSerializer.JsonOptions)
                                     ?? throw new SnoopMcpException(ErrorCode.InvalidArgument,
                                         "Missing request payload.");

        var resolved = mRegistry.TryResolve(request.RootId, out DependencyObject? root);
        if (!resolved || root is null)
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Root element id {request.RootId} is not alive.");

        ResolvePathResponse response = mMarshal.Invoke(
            () => mResolver.Resolve(root, request.PathString),
            cancellationToken);

        var json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }

    /// <summary>The wire-protocol tool name.</summary>
    public const string ResolvePathToolName = "resolvePath";
}
