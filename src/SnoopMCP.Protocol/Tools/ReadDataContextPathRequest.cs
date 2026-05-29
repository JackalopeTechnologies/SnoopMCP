// ReadDataContextPathRequest.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>readDataContextPath</c> tool.
/// </summary>
/// <param name="Id">Element id whose DataContext is the root of the path walk.</param>
/// <param name="Path">Dot-separated property path, e.g. <c>SelectedCustomer.Address.Street</c>.</param>
public sealed record ReadDataContextPathRequest(int Id, string Path);
