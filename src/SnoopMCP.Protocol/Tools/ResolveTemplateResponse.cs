// ResolveTemplateResponse.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>resolveTemplate</c> tool: the applied <c>ControlTemplate</c>'s type, its
/// key and source (best-effort), the runtime template tree under the control, and the named parts.
/// </summary>
/// <param name="TemplateType">The template's CLR type (full name when available), or <c>null</c> when none is applied.</param>
/// <param name="TemplateKey">The template's target-type name when determinable; recovering <c>x:Key</c> is Phase 2.</param>
/// <param name="TemplateSource">Always <c>null</c> in v1; reliably surfacing this requires resource-tree walking.</param>
/// <param name="TemplateTree">The control's visual children after template expansion, or <c>null</c> when none.</param>
/// <param name="NamedParts">The named parts reachable in the expanded template.</param>
public sealed record ResolveTemplateResponse(
    string? TemplateType,
    string? TemplateKey,
    string? TemplateSource,
    TemplateNodeDto? TemplateTree,
    IReadOnlyList<NamedPartDto> NamedParts);
