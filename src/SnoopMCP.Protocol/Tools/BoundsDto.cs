// BoundsDto.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Element bounds in the visual root's coordinate space.
/// </summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width in DIPs.</param>
/// <param name="Height">Height in DIPs.</param>
public sealed record BoundsDto(double X, double Y, double Width, double Height);
