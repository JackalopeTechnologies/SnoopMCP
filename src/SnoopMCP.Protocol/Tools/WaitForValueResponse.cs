// WaitForValueResponse.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// Wire response for the <c>waitForValue</c> tool.
/// </summary>
/// <param name="Matched">Whether the polled value matched <c>Expected</c> before the timeout elapsed.</param>
/// <param name="ActualValue">The last observed value, or <c>null</c> when it could not be read.</param>
public sealed record WaitForValueResponse(bool Matched, string? ActualValue);
