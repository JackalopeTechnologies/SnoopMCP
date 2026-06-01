// SnoopMcpException.cs
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

namespace SnoopMCP.Protocol.Errors;

/// <summary>
///     Exception type carrying a structured <see cref="ErrorCode" /> for protocol-level failures.
/// </summary>
public class SnoopMcpException : Exception
{
    /// <summary>
    ///     Initialises a new instance of the <see cref="SnoopMcpException" /> class with a code and message.
    /// </summary>
    /// <param name="code">The structured error code.</param>
    /// <param name="message">A human-readable description of the failure.</param>
    public SnoopMcpException(ErrorCode code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>
    ///     Initialises a new instance of the <see cref="SnoopMcpException" /> class with a code, message, and inner exception.
    /// </summary>
    /// <param name="code">The structured error code.</param>
    /// <param name="message">A human-readable description of the failure.</param>
    /// <param name="inner">The exception that triggered this one.</param>
    public SnoopMcpException(ErrorCode code, string message, Exception inner) : base(message, inner)
    {
        Code = code;
    }

    /// <summary>Gets the structured error code associated with this exception.</summary>
    public ErrorCode Code { get; }
}
