// WalkthroughRecord.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.IntegrationTests;

using System.Text.Json;

/// <summary>One captured tool call: the scene it belongs to, the tool name, the request, the response.</summary>
public sealed record WalkthroughRecord(string Scene, string Tool, object Request, JsonElement Response);
