// GetTemplatedParentResponse.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>getTemplatedParent</c> tool.
/// </summary>
/// <param name="TemplatedParent">
/// The templated parent snapshot when the element was generated from a control template;
/// otherwise <c>null</c>.
/// </param>
public sealed record GetTemplatedParentResponse(DescribeElementResponse? TemplatedParent);
