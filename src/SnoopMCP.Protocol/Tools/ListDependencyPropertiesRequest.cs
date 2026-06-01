// ListDependencyPropertiesRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>listDependencyProperties</c> tool.
/// </summary>
/// <param name="Id">Element id whose reachable dependency properties are enumerated.</param>
public sealed record ListDependencyPropertiesRequest(int Id);
