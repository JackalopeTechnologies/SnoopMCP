// DeclaredPropertyDto.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// One CLR property declared directly on a type — name, type, and read/write capability.
/// </summary>
/// <param name="Name">The property name.</param>
/// <param name="Type">The full name of the property's CLR type.</param>
/// <param name="CanRead">Whether the property has a public getter.</param>
/// <param name="CanWrite">Whether the property has a public setter.</param>
public sealed record DeclaredPropertyDto(
    string Name,
    string Type,
    bool CanRead,
    bool CanWrite);
