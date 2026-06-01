// RegisterResult.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

/// <summary>Outcome of registering the SnoopMCP server in a client's config.</summary>
/// <param name="Success">True when the config now contains the SnoopMCP entry.</param>
/// <param name="Message">Human-readable detail for logs / status output.</param>
public sealed record RegisterResult(bool Success, string Message);
