// ReadDataContextPathRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>readDataContextPath</c> tool.
/// </summary>
/// <param name="Id">Element id whose DataContext is the root of the path walk.</param>
/// <param name="Path">Dot-separated property path, e.g. <c>SelectedCustomer.Address.Street</c>.</param>
public sealed record ReadDataContextPathRequest(int Id, string Path);
