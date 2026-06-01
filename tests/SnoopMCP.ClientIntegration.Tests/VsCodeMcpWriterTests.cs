// VsCodeMcpWriterTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

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
        var writer = new VsCodeMcpWriter(mConfigPath, mDir);

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
        var writer = new VsCodeMcpWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        JsonObject root = ReadConfig();
        Assert.Equal("http://x", (string?)root["servers"]!["other"]!["url"]);
        Assert.Empty(root["inputs"]!.AsArray());
        Assert.Equal("http://127.0.0.1:6300/mcp", (string?)root["servers"]!["snoopmcp"]!["url"]);
    }

    [Fact]
    public void Unregister_RemovesOnlySnoopMcp()
    {
        File.WriteAllText(mConfigPath,
            "{\"servers\":{\"snoopmcp\":{\"type\":\"http\",\"url\":\"http://127.0.0.1:6300/mcp\"}," +
            "\"other\":{\"type\":\"http\",\"url\":\"http://x\"}}}");
        var writer = new VsCodeMcpWriter(mConfigPath, mDir);

        writer.Unregister();

        JsonObject root = ReadConfig();
        Assert.False(root["servers"]!.AsObject().ContainsKey("snoopmcp"));
        Assert.True(root["servers"]!.AsObject().ContainsKey("other"));
    }

    [Fact]
    public void Register_OnFileWithNoServersSection_AddsTheSection()
    {
        File.WriteAllText(mConfigPath, "{\"inputs\":[]}");
        var writer = new VsCodeMcpWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        JsonObject root = ReadConfig();
        Assert.Empty(root["inputs"]!.AsArray());
        Assert.Equal("http://127.0.0.1:6300/mcp", (string?)root["servers"]!["snoopmcp"]!["url"]);
    }

    [Fact]
    public void Register_OnMalformedJson_FailsWithoutThrowing()
    {
        File.WriteAllText(mConfigPath, "{ this is not json ");
        var writer = new VsCodeMcpWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.False(result.Success);
    }

    [Fact]
    public void Unregister_OnMissingFile_IsSuccessfulNoOp()
    {
        var writer = new VsCodeMcpWriter(mConfigPath, mDir);

        UnregisterResult result = writer.Unregister();

        Assert.True(result.Success);
        Assert.False(File.Exists(mConfigPath));
    }

    [Fact]
    public void ClientName_IsVsCode()
    {
        Assert.Equal("VS Code", new VsCodeMcpWriter(mConfigPath, mDir).ClientName);
    }
}
