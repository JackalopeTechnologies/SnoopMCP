// StatusResult.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>Whether a client's config currently registers the SnoopMCP server.</summary>
/// <param name="IsRegistered">True when the SnoopMCP entry is present with the expected URL.</param>
/// <param name="Message">Human-readable detail for status output.</param>
public sealed record StatusResult(bool IsRegistered, string Message);
