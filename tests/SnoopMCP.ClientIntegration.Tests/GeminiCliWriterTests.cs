// GeminiCliWriterTests.cs
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

namespace SnoopMCP.ClientIntegration.Tests;

using System.Text.Json;
using SnoopMCP.ClientIntegration;
using Xunit;

public sealed class GeminiCliWriterTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;

    public GeminiCliWriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-gemini-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Register_WritesHttpUrlField()
    {
        var writer = new GeminiCliWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mConfigPath));
        JsonElement entry = doc.RootElement.GetProperty("mcpServers").GetProperty("snoopmcp");
        Assert.Equal("http://127.0.0.1:6300/mcp", entry.GetProperty("httpUrl").GetString());
        Assert.False(entry.TryGetProperty("type", out _));
        Assert.False(entry.TryGetProperty("url", out _));
    }

    [Fact]
    public void Register_PreservesOtherKeys()
    {
        File.WriteAllText(mConfigPath, "{\"theme\":\"dark\",\"mcpServers\":{\"other\":{\"httpUrl\":\"http://x\"}}}");
        var writer = new GeminiCliWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mConfigPath));
        Assert.Equal("dark", doc.RootElement.GetProperty("theme").GetString());
        JsonElement servers = doc.RootElement.GetProperty("mcpServers");
        Assert.True(servers.TryGetProperty("other", out _));
        Assert.True(servers.TryGetProperty("snoopmcp", out _));
    }

    [Fact]
    public void Status_TrueAfterRegister()
    {
        var writer = new GeminiCliWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        Assert.True(writer.GetStatus().IsRegistered);
    }

    [Fact]
    public void Unregister_RemovesEntry()
    {
        var writer = new GeminiCliWriter(mConfigPath, mDir);
        writer.Register(McpEndpoint.Default);

        writer.Unregister();

        Assert.False(writer.GetStatus().IsRegistered);
    }
}
