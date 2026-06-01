// UnregisterResult.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

/// <summary>Outcome of removing the SnoopMCP server from a client's config.</summary>
/// <param name="Success">True when the SnoopMCP entry is absent afterward (including no-op).</param>
/// <param name="Message">Human-readable detail for logs / status output.</param>
public sealed record UnregisterResult(bool Success, string Message);
