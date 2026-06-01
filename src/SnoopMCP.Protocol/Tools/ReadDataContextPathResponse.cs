// ReadDataContextPathResponse.cs
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

namespace SnoopMCP.Protocol.Tools;

/// <summary>
///     Wire response for the <c>readDataContextPath</c> tool.
/// </summary>
/// <param name="Value">
///     The leaf value stringified via invariant culture, or <c>null</c> when the value itself is null
///     or the path could not be reached.
/// </param>
/// <param name="ValueType">
///     The CLR full name of the leaf value's runtime type, or <c>null</c> when the value is null
///     or the path could not be reached.
/// </param>
/// <param name="PathReachable"><c>true</c> when every segment resolved; otherwise <c>false</c>.</param>
/// <param name="FailureAt">
///     When not reachable, the path traversed up to and including the offending segment (empty string
///     when the DataContext itself is null); otherwise <c>null</c>.
/// </param>
public sealed record ReadDataContextPathResponse(
    string? Value,
    string? ValueType,
    bool PathReachable,
    string? FailureAt);
