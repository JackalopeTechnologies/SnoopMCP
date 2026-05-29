// ListVisualRootsRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>listVisualRoots</c> tool. Carries no fields; the response
/// is a snapshot of every active <c>PresentationSource</c> in the target process.
/// </summary>
public sealed record ListVisualRootsRequest();
