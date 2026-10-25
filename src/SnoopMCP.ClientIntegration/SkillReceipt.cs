// SkillReceipt.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

/// <summary>Ownership and accepted content fingerprints for one installer-managed skill.</summary>
internal sealed class SkillReceipt
{
    public int Version { get; set; } = 1;
    public HashSet<string> Owners { get; set; } = [];
    public HashSet<string> Hashes { get; set; } = [];
}
