// GetDependencyPropertyRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>getDependencyProperty</c> tool.
/// </summary>
/// <param name="Id">Element id whose dependency-property value is read.</param>
/// <param name="PropertyName">The dependency property's registered name, e.g. <c>Background</c>.</param>
public sealed record GetDependencyPropertyRequest(int Id, string PropertyName);
