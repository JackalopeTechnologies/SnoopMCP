// ResolvePathRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>resolvePath</c> tool.
/// </summary>
/// <param name="RootId">Element id whose visual subtree is walked.</param>
/// <param name="PathString">A canonical path produced by the path emitter grammar (Task 9).</param>
public sealed record ResolvePathRequest(int RootId, string PathString);
