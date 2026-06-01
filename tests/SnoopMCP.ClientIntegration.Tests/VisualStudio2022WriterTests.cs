// VisualStudio2022WriterTests.cs
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

public sealed class VisualStudio2022WriterTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;

    public VisualStudio2022WriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-vs2022-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, ".mcp.json");
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
    public void Register_WritesBareUrlUnderServers()
    {
        var writer = new VisualStudio2022Writer(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(mConfigPath));
        JsonElement entry = doc.RootElement.GetProperty("servers").GetProperty("snoopmcp");
        Assert.Equal("http://127.0.0.1:6300/mcp", entry.GetProperty("url").GetString());
        Assert.False(entry.TryGetProperty("type", out _));
    }

    [Fact]
    public void Status_TrueAfterRegister()
    {
        var writer = new VisualStudio2022Writer(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        Assert.True(writer.GetStatus().IsRegistered);
    }

    [Fact]
    public void Unregister_RemovesEntry()
    {
        var writer = new VisualStudio2022Writer(mConfigPath, mDir);
        writer.Register(McpEndpoint.Default);

        writer.Unregister();

        Assert.False(writer.GetStatus().IsRegistered);
    }
}
