// ResolveTemplateToolHandler.cs
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
///     Wire handler for the <c>resolveTemplate</c> tool. Resolves the element id and marshals
///     <see cref="TemplateResolver.Resolve" /> onto the WPF dispatcher.
/// </summary>
public sealed class ResolveTemplateToolHandler : IToolHandler
{
    /// <summary>
    ///     Initialises a new <see cref="ResolveTemplateToolHandler" />.
    /// </summary>
    /// <param name="registry">Element registry used to resolve the element id.</param>
    /// <param name="resolver">The template resolver.</param>
    /// <param name="marshal">Dispatcher marshal used to enter the WPF UI thread.</param>
    public ResolveTemplateToolHandler(
        ElementRegistry registry,
        TemplateResolver resolver,
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
    private readonly TemplateResolver mResolver;

    /// <inheritdoc />
    public string ToolName => ResolveTemplateToolName;

    /// <inheritdoc />
    public Task<JsonElement> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        ResolveTemplateRequest request = arguments.Deserialize<ResolveTemplateRequest>(WireSerializer.JsonOptions)
                                         ?? throw new SnoopMcpException(ErrorCode.InvalidArgument,
                                             "Missing request payload.");

        var resolved = mRegistry.TryResolve(request.Id, out DependencyObject? element);
        if (!resolved || element is null)
            throw new SnoopMcpException(
                ErrorCode.ElementExpired,
                $"Element id {request.Id} is not alive.");

        ResolveTemplateResponse response = mMarshal.Invoke(
            () => mResolver.Resolve(element),
            cancellationToken);

        var json = JsonSerializer.Serialize(response, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        JsonElement result = doc.RootElement.Clone();
        return Task.FromResult(result);
    }

    /// <summary>The wire-protocol tool name.</summary>
    public const string ResolveTemplateToolName = "resolveTemplate";
}
