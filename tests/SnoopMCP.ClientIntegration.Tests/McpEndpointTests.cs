// McpEndpointTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using ClientIntegration;
using Xunit;

public sealed class McpEndpointTests
{
    [Fact]
    public void Default_IsTheSnoopMcpHttpEndpoint()
    {
        McpEndpoint endpoint = McpEndpoint.Default;

        Assert.Equal("snoopmcp", endpoint.Name);
        Assert.Equal("http", endpoint.Type);
        Assert.Equal("http://127.0.0.1:6300/mcp", endpoint.Url);
    }
}
