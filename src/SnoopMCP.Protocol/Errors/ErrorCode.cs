// ErrorCode.cs
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
///     Structured error codes returned across the SnoopMCP wire protocol.
/// </summary>
public enum ErrorCode
{
    /// <summary>Default value; indicates an unclassified failure.</summary>
    Unknown = 0,

    /// <summary>The host failed to attach to the target process.</summary>
    AttachFailed = 1,

    /// <summary>The in-process payload assembly failed to load.</summary>
    PayloadLoadFailed = 2,

    /// <summary>A dispatcher-bound operation timed out.</summary>
    DispatcherTimeout = 3,

    /// <summary>The active session was lost (host crashed or detached).</summary>
    SessionLost = 4,

    /// <summary>The caller is not authorised to perform the requested action.</summary>
    AccessDenied = 5,

    /// <summary>A previously-handed-out element id no longer resolves to a live object.</summary>
    ElementExpired = 6,

    /// <summary>One or more tool arguments are missing or invalid.</summary>
    InvalidArgument = 7,

    /// <summary>The requested tool name is not registered.</summary>
    ToolNotFound = 8,

    /// <summary>The wire payload violated the protocol contract.</summary>
    ProtocolError = 9,

    /// <summary>A binding path expression could not be evaluated.</summary>
    BindingPathError = 10,

    /// <summary>A textual path could not be parsed.</summary>
    PathParseError = 11
}
