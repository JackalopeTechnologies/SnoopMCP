// VsCodeMcpWriterTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using SnoopMCP.ClientIntegration;
using Xunit;

public sealed class VsCodeMcpWriterTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;

    public VsCodeMcpWriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-vsc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, "mcp.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private JsonObject ReadConfig()
    {
        string text = File.ReadAllText(mConfigPath);
        return JsonNode.Parse(text) as JsonObject ?? throw new JsonException("not an object");
    }

    [Fact]
    public void Register_UsesServersKey_WithHttpEntry()
    {
        var writer = new VsCodeMcpWriter(mConfigPath);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        JsonObject root = ReadConfig();
        JsonNode entry = root["servers"]!["snoopmcp"]!;
        Assert.Equal("http", (string?)entry["type"]);
        Assert.Equal("http://127.0.0.1:6300/mcp", (string?)entry["url"]);
    }

    [Fact]
    public void Register_PreservesExistingServersAndInputs()
    {
        File.WriteAllText(mConfigPath,
            "{\"servers\":{\"other\":{\"type\":\"http\",\"url\":\"http://x\"}},\"inputs\":[]}");
        var writer = new VsCodeMcpWriter(mConfigPath);

        writer.Register(McpEndpoint.Default);

        JsonObject root = ReadConfig();
        Assert.Equal("http://x", (string?)root["servers"]!["other"]!["url"]);
        Assert.NotNull(root["inputs"]);
        Assert.Equal("http://127.0.0.1:6300/mcp", (string?)root["servers"]!["snoopmcp"]!["url"]);
    }

    [Fact]
    public void Unregister_RemovesOnlySnoopMcp()
    {
        File.WriteAllText(mConfigPath,
            "{\"servers\":{\"snoopmcp\":{\"type\":\"http\",\"url\":\"http://127.0.0.1:6300/mcp\"}," +
            "\"other\":{\"type\":\"http\",\"url\":\"http://x\"}}}");
        var writer = new VsCodeMcpWriter(mConfigPath);

        writer.Unregister();

        JsonObject root = ReadConfig();
        Assert.False(root["servers"]!.AsObject().ContainsKey("snoopmcp"));
        Assert.True(root["servers"]!.AsObject().ContainsKey("other"));
    }

    [Fact]
    public void ClientName_IsVsCode()
    {
        Assert.Equal("VS Code", new VsCodeMcpWriter(mConfigPath).ClientName);
    }
}
