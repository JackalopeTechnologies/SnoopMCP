// CodexWriterTests.cs
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

using SnoopMCP.ClientIntegration;
using Tomlyn;
using Tomlyn.Model;
using Xunit;

public sealed class CodexWriterTests : IDisposable
{
    private readonly string mDir;
    private readonly string mConfigPath;

    public CodexWriterTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-codex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
        mConfigPath = Path.Combine(mDir, "config.toml");
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private TomlTable ReadConfig() => Toml.ToModel(File.ReadAllText(mConfigPath));

    private static TomlTable Servers(TomlTable root) => (TomlTable)root["mcp_servers"];

    private static string EntryUrl(TomlTable root, string name) => (string)((TomlTable)Servers(root)[name])["url"];

    [Fact]
    public void Register_OnMissingFile_CreatesConfigWithUrlEntry()
    {
        var writer = new CodexWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        Assert.Equal("http://127.0.0.1:6300/mcp", EntryUrl(ReadConfig(), "snoopmcp"));
    }

    [Fact]
    public void Register_EnablesRmcpFeatureFlag()
    {
        var writer = new CodexWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        TomlTable root = ReadConfig();
        var features = (TomlTable)root["features"];
        Assert.True((bool)features["experimental_use_rmcp_client"]);
    }

    [Fact]
    public void Register_PreservesOtherTablesAndTopLevelKeys()
    {
        File.WriteAllText(mConfigPath, "model = \"gpt-5\"\n\n[mcp_servers.other]\nurl = \"http://x\"\n");
        var writer = new CodexWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        TomlTable root = ReadConfig();
        Assert.Equal("gpt-5", (string)root["model"]);
        Assert.Equal("http://x", EntryUrl(root, "other"));
        Assert.Equal("http://127.0.0.1:6300/mcp", EntryUrl(root, "snoopmcp"));
    }

    [Fact]
    public void Register_OnFileWithNoServersSection_AddsTheSection()
    {
        File.WriteAllText(mConfigPath, "model = \"gpt-5\"\n");
        var writer = new CodexWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.True(result.Success, result.Message);
        TomlTable root = ReadConfig();
        Assert.Equal("gpt-5", (string)root["model"]);
        Assert.Equal("http://127.0.0.1:6300/mcp", EntryUrl(root, "snoopmcp"));
    }

    [Fact]
    public void Register_WhenAlreadyPresent_IsIdempotent()
    {
        var writer = new CodexWriter(mConfigPath, mDir);
        writer.Register(McpEndpoint.Default);

        RegisterResult second = writer.Register(McpEndpoint.Default);

        Assert.True(second.Success);
        Assert.Single(Servers(ReadConfig()));
    }

    [Fact]
    public void Register_OnMalformedToml_FailsWithoutThrowing()
    {
        File.WriteAllText(mConfigPath, "this is not = valid = toml =");
        var writer = new CodexWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.False(result.Success);
    }

    [Fact]
    public void Unregister_RemovesOnlySnoopMcp_PreservingOthers()
    {
        File.WriteAllText(mConfigPath,
            "[mcp_servers.snoopmcp]\nurl = \"http://127.0.0.1:6300/mcp\"\n\n[mcp_servers.other]\nurl = \"http://x\"\n");
        var writer = new CodexWriter(mConfigPath, mDir);

        UnregisterResult result = writer.Unregister();

        Assert.True(result.Success, result.Message);
        TomlTable servers = Servers(ReadConfig());
        Assert.False(servers.ContainsKey("snoopmcp"));
        Assert.True(servers.ContainsKey("other"));
    }

    [Fact]
    public void Unregister_OnMissingFile_IsSuccessfulNoOp()
    {
        var writer = new CodexWriter(mConfigPath, mDir);

        UnregisterResult result = writer.Unregister();

        Assert.True(result.Success);
        Assert.False(File.Exists(mConfigPath));
    }

    [Fact]
    public void GetStatus_ReflectsRegistration()
    {
        var writer = new CodexWriter(mConfigPath, mDir);
        Assert.False(writer.GetStatus().IsRegistered);

        writer.Register(McpEndpoint.Default);

        Assert.True(writer.GetStatus().IsRegistered);
    }

    [Fact]
    public void Register_WhenUrlChanged_OverwritesExistingEntry()
    {
        File.WriteAllText(mConfigPath, "[mcp_servers.snoopmcp]\nurl = \"http://old:1/mcp\"\n");
        var writer = new CodexWriter(mConfigPath, mDir);

        writer.Register(McpEndpoint.Default);

        Assert.Equal("http://127.0.0.1:6300/mcp", EntryUrl(ReadConfig(), "snoopmcp"));
    }

    [Fact]
    public void Unregister_WhenEntryAbsentButFileExists_IsSuccessfulNoOp()
    {
        File.WriteAllText(mConfigPath, "[mcp_servers.other]\nurl = \"http://x\"\n");
        var writer = new CodexWriter(mConfigPath, mDir);

        UnregisterResult result = writer.Unregister();

        Assert.True(result.Success, result.Message);
        Assert.True(Servers(ReadConfig()).ContainsKey("other"));
    }

    [Fact]
    public void Register_OnModelConflict_FailsWithoutThrowing()
    {
        File.WriteAllText(mConfigPath,
            "[mcp_servers]\nsnoopmcp = \"not-a-table\"\n\n[mcp_servers.snoopmcp]\nurl = \"http://y\"\n");
        var writer = new CodexWriter(mConfigPath, mDir);

        RegisterResult result = writer.Register(McpEndpoint.Default);

        Assert.False(result.Success);
        Assert.False(writer.GetStatus().IsRegistered);
    }
}
