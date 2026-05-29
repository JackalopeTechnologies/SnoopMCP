// PathStep.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Payload.PathStrings;

/// <summary>
/// A single segment in a canonical path string of the form
/// <c>/TypeName[Name='X', AutomationId='Y'][n]/...</c>.
/// </summary>
/// <param name="TypeName">The CLR type's short name (no namespace).</param>
/// <param name="Attributes">Zero or more attribute predicates (e.g. <c>Name</c>, <c>AutomationId</c>).</param>
/// <param name="Index">Optional 0-based index disambiguating same-typed siblings.</param>
public sealed record PathStep(
    string TypeName,
    IReadOnlyDictionary<string, string> Attributes,
    int? Index);
