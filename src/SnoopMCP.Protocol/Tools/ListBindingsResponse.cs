// ListBindingsResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>listBindings</c> tool: every binding found on the element (and, when
/// requested, under its visual subtree).
/// </summary>
/// <param name="Bindings">The collected binding summaries.</param>
public sealed record ListBindingsResponse(IReadOnlyList<BindingSummaryDto> Bindings);
