// ListDependencyPropertiesRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>listDependencyProperties</c> tool.
/// </summary>
/// <param name="Id">Element id whose reachable dependency properties are enumerated.</param>
public sealed record ListDependencyPropertiesRequest(int Id);
