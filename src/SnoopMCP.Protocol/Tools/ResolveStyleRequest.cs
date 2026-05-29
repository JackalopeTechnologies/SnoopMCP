// ResolveStyleRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>resolveStyle</c> tool.
/// </summary>
/// <param name="Id">Element id whose applied WPF <c>Style</c> is resolved.</param>
public sealed record ResolveStyleRequest(int Id);
