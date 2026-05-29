// ListBindingsResponse.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>listBindings</c> tool: every binding found on the element (and, when
/// requested, under its visual subtree).
/// </summary>
/// <param name="Bindings">The collected binding summaries.</param>
public sealed record ListBindingsResponse(IReadOnlyList<BindingSummaryDto> Bindings);
