// StatusResult.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

/// <summary>Whether a client's config currently registers the SnoopMCP server.</summary>
/// <param name="IsRegistered">True when the SnoopMCP entry is present with the expected URL.</param>
/// <param name="Message">Human-readable detail for status output.</param>
public sealed record StatusResult(bool IsRegistered, string Message);
