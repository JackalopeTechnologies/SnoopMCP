// ClaudeDesktopWriterTests.cs
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

using System.Text.Json.Nodes;
using Xunit;

/// <summary>Unit tests for <see cref="ClaudeDesktopWriter"/>.</summary>
public sealed class ClaudeDesktopWriterTests
{
    private const string EndpointName = "snoopmcp";
    private const string ServersKey = "mcpServers";
    private const string CommandKey = "command";
    private const string ArgsKey = "args";
    private const string NpxCommand = "npx";
    private const string OtherServerJson =
        "{\"mcpServers\":{\"other\":{\"command\":\"node\",\"args\":[\"x.js\"]}}}";

    private static string NewTempConfig()
    {
        return Path.Combine(Path.GetTempPath(), $"snoopmcp-claudedesktop-{Guid.NewGuid():N}.json");
    }

    private static ClaudeDesktopWriter NewWriter(string path)
    {
        return new ClaudeDesktopWriter(path, Path.GetDirectoryName(path)!);
    }

    [Fact]
    public void Register_WritesServerEntry_WhenFileMissing()
    {
        string path = NewTempConfig();
        try
        {
            ClaudeDesktopWriter writer = NewWriter(path);
            RegisterResult result = writer.Register(McpEndpoint.Default);
            Assert.True(result.Success);
            JsonNode root = JsonNode.Parse(File.ReadAllText(path))!;
            JsonNode entry = root[ServersKey]![EndpointName]!;
            Assert.Equal(NpxCommand, (string?) entry[CommandKey]);
            JsonArray args = (JsonArray) entry[ArgsKey]!;
            Assert.Contains(McpEndpoint.Default.Url, args.Select(a => (string?) a));
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
            ClaudeDesktopWriter writer = NewWriter(path);
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
            ClaudeDesktopWriter writer = NewWriter(path);
            writer.Register(McpEndpoint.Default);
            writer.Register(McpEndpoint.Default);
            JsonNode entry = JsonNode.Parse(File.ReadAllText(path))![ServersKey]![EndpointName]!;
            Assert.Equal(NpxCommand, (string?) entry[CommandKey]);
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
            ClaudeDesktopWriter writer = NewWriter(path);
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
        ClaudeDesktopWriter writer = NewWriter(path);
        UnregisterResult result = writer.Unregister();
        Assert.True(result.Success);
    }

    [Fact]
    public void GetStatus_ReportsRegistered_AfterRegister()
    {
        string path = NewTempConfig();
        try
        {
            ClaudeDesktopWriter writer = NewWriter(path);
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
        ClaudeDesktopWriter writer = NewWriter(path);
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
            ClaudeDesktopWriter writer = NewWriter(path);
            RegisterResult result = writer.Register(McpEndpoint.Default);
            Assert.False(result.Success);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
