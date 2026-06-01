// StyleSetterDto.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// A single style or trigger setter: the dependency-property name it targets and the value it sets,
/// stringified via invariant culture.
/// </summary>
/// <param name="Property">The targeted dependency property's registered name, e.g. <c>Background</c>.</param>
/// <param name="Value">The setter's value, stringified via invariant culture, or <c>null</c>.</param>
public sealed record StyleSetterDto(string Property, string? Value);
