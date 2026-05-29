// ResolveTemplateRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>resolveTemplate</c> tool.
/// </summary>
/// <param name="Id">Element id whose applied WPF <c>ControlTemplate</c> is resolved.</param>
public sealed record ResolveTemplateRequest(int Id);
