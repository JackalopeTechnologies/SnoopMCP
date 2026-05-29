// DependencyPropertyDto.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire description of a single dependency property reachable on an element.
/// </summary>
/// <param name="Name">The dependency property's registered name.</param>
/// <param name="OwnerType">The CLR full name of the type that registered the property.</param>
/// <param name="ValueType">The CLR full name of the property's value type.</param>
/// <param name="IsAttached">
/// <c>true</c> when the property is an attached property currently set on the element;
/// <c>false</c> when it is reachable via the element's own type chain.
/// </param>
public sealed record DependencyPropertyDto(
    string Name,
    string OwnerType,
    string ValueType,
    bool IsAttached);
