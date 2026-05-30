// CopilotCliWriterTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration.Tests;

using System.Text.Json.Nodes;
using Xunit;

/// <summary>Unit tests for <see cref="CopilotCliWriter"/>.</summary>
public sealed class CopilotCliWriterTests
{
    private const string EndpointName = "snoopmcp";
    private const string ServersKey = "mcpServers";
    private const string TypeKey = "type";
    private const string UrlKey = "url";
    private const string SseType = "sse";
    private const string OtherServerJson =
        "{\"mcpServers\":{\"other\":{\"type\":\"sse\",\"url\":\"http://localhost:9999/mcp\"}}}";

    private static string NewTempConfig()
    {
        return Path.Combine(Path.GetTempPath(), $"snoopmcp-copilot-{Guid.NewGuid():N}.json");
    }

    [Fact]
    public void Register_WritesServerEntry_WhenFileMissing()
    {
        string path = NewTempConfig();
        try
        {
            CopilotCliWriter writer = new(path);
            RegisterResult result = writer.Register(McpEndpoint.Default);
            Assert.True(result.Success);
            JsonNode entry = JsonNode.Parse(File.ReadAllText(path))![ServersKey]![EndpointName]!;
            Assert.Equal(SseType, (string?) entry[TypeKey]);
            Assert.Equal(McpEndpoint.Default.Url, (string?) entry[UrlKey]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Register_PreservesOtherServers_WhenPresent()
    {
        string path = NewTempConfig();
        try
        {
            File.WriteAllText(path, OtherServerJson);
            CopilotCliWriter writer = new(path);
            writer.Register(McpEndpoint.Default);
            JsonNode servers = JsonNode.Parse(File.ReadAllText(path))![ServersKey]!;
            Assert.NotNull(servers["other"]);
            Assert.NotNull(servers[EndpointName]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Register_IsIdempotent_WhenCalledTwice()
    {
        string path = NewTempConfig();
        try
        {
            CopilotCliWriter writer = new(path);
            writer.Register(McpEndpoint.Default);
            writer.Register(McpEndpoint.Default);
            JsonNode entry = JsonNode.Parse(File.ReadAllText(path))![ServersKey]![EndpointName]!;
            Assert.Equal(McpEndpoint.Default.Url, (string?) entry[UrlKey]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Unregister_RemovesOnlySnoopEntry_WhenOthersPresent()
    {
        string path = NewTempConfig();
        try
        {
            CopilotCliWriter writer = new(path);
            File.WriteAllText(path, OtherServerJson);
            writer.Register(McpEndpoint.Default);
            UnregisterResult result = writer.Unregister();
            Assert.True(result.Success);
            JsonNode servers = JsonNode.Parse(File.ReadAllText(path))![ServersKey]!;
            Assert.Null(servers[EndpointName]);
            Assert.NotNull(servers["other"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Unregister_Succeeds_WhenFileMissing()
    {
        string path = NewTempConfig();
        CopilotCliWriter writer = new(path);
        UnregisterResult result = writer.Unregister();
        Assert.True(result.Success);
    }

    [Fact]
    public void GetStatus_ReportsRegistered_AfterRegister()
    {
        string path = NewTempConfig();
        try
        {
            CopilotCliWriter writer = new(path);
            writer.Register(McpEndpoint.Default);
            StatusResult status = writer.GetStatus();
            Assert.True(status.IsRegistered);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetStatus_ReportsNotRegistered_WhenFileMissing()
    {
        string path = NewTempConfig();
        CopilotCliWriter writer = new(path);
        StatusResult status = writer.GetStatus();
        Assert.False(status.IsRegistered);
    }

    [Fact]
    public void Register_FailsSafely_WhenJsonMalformed()
    {
        string path = NewTempConfig();
        try
        {
            File.WriteAllText(path, "{ this is not valid json ");
            CopilotCliWriter writer = new(path);
            RegisterResult result = writer.Register(McpEndpoint.Default);
            Assert.False(result.Success);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
