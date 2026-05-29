// FindElementsRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>findElements</c> tool.
/// </summary>
/// <param name="RootId">The element id whose subtree (including the root itself) is searched.</param>
/// <param name="Predicate">AND-combined predicate; every supplied field must match.</param>
public sealed record FindElementsRequest(int RootId, ElementPredicateDto Predicate);
