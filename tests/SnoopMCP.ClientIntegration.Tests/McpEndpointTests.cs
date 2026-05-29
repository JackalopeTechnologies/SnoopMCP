// McpEndpointTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration.Tests;

using SnoopMCP.ClientIntegration;
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
