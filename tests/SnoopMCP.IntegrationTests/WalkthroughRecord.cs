// WalkthroughRecord.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.IntegrationTests;

using System.Text.Json;

/// <summary>One captured tool call: the scene it belongs to, the tool name, the request, the response.</summary>
public sealed record WalkthroughRecord(string Scene, string Tool, object Request, JsonElement Response);
