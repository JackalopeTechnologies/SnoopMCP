// ToolRegistry.cs
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

/// <summary>
///     Lookup table mapping wire-protocol tool names to <see cref="IToolHandler" /> instances.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, IToolHandler> mHandlers = new(StringComparer.Ordinal);

    /// <summary>
    ///     Registers a handler keyed by its <see cref="IToolHandler.ToolName" />.
    /// </summary>
    /// <param name="handler">The handler instance to register.</param>
    public void Register(IToolHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        mHandlers[handler.ToolName] = handler;
    }

    /// <summary>
    ///     Returns the handler registered for <paramref name="toolName" />, or <c>null</c> if none is registered.
    /// </summary>
    /// <param name="toolName">The wire-protocol tool name.</param>
    /// <returns>The registered handler, or <c>null</c> when no handler is registered.</returns>
    public IToolHandler? Find(string toolName)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        mHandlers.TryGetValue(toolName, out IToolHandler? candidate);
        return candidate;
    }
}
