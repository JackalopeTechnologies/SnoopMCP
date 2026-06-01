// RpcError.cs
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

namespace SnoopMCP.Protocol.Wire;

using SnoopMCP.Protocol.Errors;

/// <summary>
/// Error payload returned alongside an <see cref="RpcResponse"/> when a tool invocation fails.
/// </summary>
public sealed class RpcError
{
    /// <summary>Gets the structured error code.</summary>
    public ErrorCode Code { get; init; } = ErrorCode.Unknown;

    /// <summary>Gets the human-readable error message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets optional supplementary detail (stack trace fragment, diagnostic context, etc.).</summary>
    public string? Details { get; init; }
}
