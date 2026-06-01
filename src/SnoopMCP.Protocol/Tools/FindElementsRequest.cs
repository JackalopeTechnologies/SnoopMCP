// FindElementsRequest.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire request for the <c>findElements</c> tool.
/// </summary>
/// <param name="RootId">The element id whose subtree (including the root itself) is searched.</param>
/// <param name="Predicate">AND-combined predicate; every supplied field must match.</param>
public sealed record FindElementsRequest(int RootId, ElementPredicateDto Predicate);
