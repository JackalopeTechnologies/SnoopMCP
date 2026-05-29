// RegisterResult.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>Outcome of registering the SnoopMCP server in a client's config.</summary>
/// <param name="Success">True when the config now contains the SnoopMCP entry.</param>
/// <param name="Message">Human-readable detail for logs / status output.</param>
public sealed record RegisterResult(bool Success, string Message);
