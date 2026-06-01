// ClaudeDesktopWriterTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

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
            var root = JsonNode.Parse(File.ReadAllText(path))!;
            JsonNode entry = root[ServersKey]![EndpointName]!;
            Assert.Equal(NpxCommand, (string?) entry[CommandKey]);
            var args = (JsonArray) entry[ArgsKey]!;
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
