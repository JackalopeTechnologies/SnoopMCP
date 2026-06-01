// VsCodeMcpWriterTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#region Usings

using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

#endregion

namespace SnoopMCP.ClientIntegration.Tests;

public sealed class VsCodeMcpWriterTests : IDisposable
{
    public VsCodeMcpWriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-vsc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, "mcp.json");
    }

    private readonly string mConfigPath;
    private readonly string mDir;

    public void Dispose()
    {
        if (Directory.Exists(mDir)) Directory.Delete(mDir, true);
        GC.SuppressFinalize(this);
    }

    private JsonObject ReadConfig()
    {
        var text = File.ReadAllText(mConfigPath);
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
