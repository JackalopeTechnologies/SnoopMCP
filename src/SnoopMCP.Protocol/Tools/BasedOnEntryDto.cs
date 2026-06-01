// BasedOnEntryDto.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tools;

/// <summary>
/// One link in a style's <c>BasedOn</c> chain. Depth 0 is the directly-applied style; each
/// successive entry is its parent via the WPF <c>Style.BasedOn</c> property.
/// </summary>
/// <param name="TargetType">The style's target type (full name when available), e.g. <c>System.Windows.Controls.Button</c>.</param>
/// <param name="Depth">The link's distance from the applied style; the applied style is depth 0.</param>
public sealed record BasedOnEntryDto(string TargetType, int Depth);
