// ReadDataContextPathResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>readDataContextPath</c> tool.
/// </summary>
/// <param name="Value">
/// The leaf value stringified via invariant culture, or <c>null</c> when the value itself is null
/// or the path could not be reached.
/// </param>
/// <param name="ValueType">
/// The CLR full name of the leaf value's runtime type, or <c>null</c> when the value is null
/// or the path could not be reached.
/// </param>
/// <param name="PathReachable"><c>true</c> when every segment resolved; otherwise <c>false</c>.</param>
/// <param name="FailureAt">
/// When not reachable, the path traversed up to and including the offending segment (empty string
/// when the DataContext itself is null); otherwise <c>null</c>.
/// </param>
public sealed record ReadDataContextPathResponse(
    string? Value,
    string? ValueType,
    bool PathReachable,
    string? FailureAt);
