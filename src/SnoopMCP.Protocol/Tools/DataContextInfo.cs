// DataContextInfo.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// CLR type-shape snapshot of an element's DataContext: type name, namespace,
/// base-type chain, implemented interfaces, and declared CLR properties.
/// </summary>
/// <param name="TypeName">The simple type name of the DataContext.</param>
/// <param name="Namespace">The namespace of the DataContext type, or <see cref="string.Empty"/> when global.</param>
/// <param name="BaseTypes">Full names of every base type from the immediate parent up to <see cref="object"/>.</param>
/// <param name="Interfaces">Full names of every interface implemented by the type.</param>
/// <param name="DeclaredProperties">Declared-only public instance CLR properties on the type.</param>
public sealed record DataContextInfo(
    string TypeName,
    string Namespace,
    IReadOnlyList<string> BaseTypes,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<DeclaredPropertyDto> DeclaredProperties);
