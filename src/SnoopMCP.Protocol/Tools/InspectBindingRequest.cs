// InspectBindingRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>inspectBinding</c> tool.
/// </summary>
/// <param name="Id">Element id whose binding is inspected.</param>
/// <param name="PropertyName">The dependency property's registered name carrying the binding, e.g. <c>Text</c>.</param>
public sealed record InspectBindingRequest(int Id, string PropertyName);
