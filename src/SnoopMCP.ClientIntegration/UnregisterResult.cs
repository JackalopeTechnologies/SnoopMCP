// UnregisterResult.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>Outcome of removing the SnoopMCP server from a client's config.</summary>
/// <param name="Success">True when the SnoopMCP entry is absent afterward (including no-op).</param>
/// <param name="Message">Human-readable detail for logs / status output.</param>
public sealed record UnregisterResult(bool Success, string Message);
