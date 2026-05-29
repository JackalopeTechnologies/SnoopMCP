// BindingTraceLineDto.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// One captured WPF data-binding trace line. Always empty in v1; forwarding live
/// <c>PresentationTraceSources.DataBindingSource</c> output over the pipe is Phase 2.
/// </summary>
/// <param name="Timestamp">When the line was emitted.</param>
/// <param name="Severity">The trace severity, e.g. <c>Error</c>, <c>Warning</c>.</param>
/// <param name="Message">The trace message text.</param>
public sealed record BindingTraceLineDto(string Timestamp, string Severity, string Message);
