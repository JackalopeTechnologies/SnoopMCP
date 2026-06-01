// RpcResponse.cs
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

#endregion

namespace SnoopMCP.Protocol.Wire;

/// <summary>
///     Wire-format response envelope returned by an in-process payload to the MCP server.
/// </summary>
public sealed class RpcResponse
{
    /// <summary>Gets the correlation id of the request this response answers.</summary>
    public long Id { get; init; }

    /// <summary>Gets the result payload, populated when the call succeeded.</summary>
    public JsonElement? Result { get; init; }

    /// <summary>Gets the error payload, populated when the call failed.</summary>
    public RpcError? Error { get; init; }

    /// <summary>Gets a value indicating whether this response represents a successful invocation.</summary>
    public bool IsSuccess => Error is null;
}
