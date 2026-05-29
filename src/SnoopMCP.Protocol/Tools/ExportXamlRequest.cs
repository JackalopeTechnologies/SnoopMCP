// ExportXamlRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>exportXaml</c> tool.
/// </summary>
/// <param name="Id">Element id whose live state is serialized to XAML.</param>
public sealed record ExportXamlRequest(int Id);
