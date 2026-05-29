// InspectBindingRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>inspectBinding</c> tool.
/// </summary>
/// <param name="Id">Element id whose binding is inspected.</param>
/// <param name="PropertyName">The dependency property's registered name carrying the binding, e.g. <c>Text</c>.</param>
public sealed record InspectBindingRequest(int Id, string PropertyName);
