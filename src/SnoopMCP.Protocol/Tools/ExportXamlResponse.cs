// ExportXamlResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>exportXaml</c> tool: a XAML snapshot of an element's live state.
/// Bindings appear as their evaluated values, not as markup; for the live binding spec use
/// <c>listBindings</c> or <c>inspectBinding</c>.
/// </summary>
/// <param name="Xaml">The serialized XAML (possibly truncated to the soft cap).</param>
/// <param name="ByteCount">The full UTF-8 byte count of the serialized XAML before truncation.</param>
/// <param name="Truncated">Whether the returned <paramref name="Xaml"/> was truncated at the soft cap.</param>
/// <param name="Warning">A human-readable truncation warning, or <c>null</c> when not truncated.</param>
public sealed record ExportXamlResponse(
    string Xaml,
    int ByteCount,
    bool Truncated,
    string? Warning);
