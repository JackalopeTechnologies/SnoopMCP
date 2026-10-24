// ClientSkillsTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using ClientIntegration;
using Xunit;

public sealed class ClientSkillsTests : IDisposable
{
    private readonly string mDir = Path.Combine(Path.GetTempPath(), "snoop-clients-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(McpClient.Cursor)]
    [InlineData(McpClient.GeminiCli)]
    [InlineData(McpClient.CopilotCli)]
    [InlineData(McpClient.VsCode)]
    [InlineData(McpClient.Windsurf)]
    [InlineData(McpClient.GrokBuild)]
    public void Registration_InstallsAndRemovesCanonicalSkills(McpClient client)
    {
        string config = Path.Combine(mDir, "config");
        IClientWriter writer = client switch
        {
            McpClient.Cursor => new CursorWriter(config, mDir),
            McpClient.GeminiCli => new GeminiCliWriter(config, mDir),
            McpClient.CopilotCli => new CopilotCliWriter(config, mDir),
            McpClient.VsCode => new VsCodeMcpWriter(config, mDir),
            McpClient.Windsurf => new WindsurfWriter(config, mDir),
            McpClient.GrokBuild => new GrokBuildWriter(config, mDir),
            _ => throw new ArgumentOutOfRangeException(nameof(client))
        };
        Assert.True(writer.Register(McpEndpoint.Default).Success);
        Assert.True(writer.Register(McpEndpoint.Default).Success);
        foreach ((string name, string body) in SnoopSkill.Definitions)
        {
            Assert.Equal(body, File.ReadAllText(Path.Combine(mDir, "skills", name, "SKILL.md")));
        }
        Assert.True(writer.Unregister().Success);
        Assert.True(writer.Unregister().Success);
        Assert.False(Directory.Exists(Path.Combine(mDir, "skills", SnoopSkill.FirstSkillName)));
        Assert.True(File.Exists(config));
    }

    [Fact]
    public void JsonRegistration_PreservesServerSettingsAndPermissions()
    {
        Directory.CreateDirectory(mDir);
        string config = Path.Combine(mDir, "mcp.json");
        File.WriteAllText(config, """
            {"preferences":{"theme":"dark"},"mcpServers":{
              "snoopmcp":{"url":"http://127.0.0.1:6300/mcp","disabled":true,"tools":["inspectBinding"],
                "headers":{"X-Local":"keep"},"timeout":321},
              "other":{"url":"https://other"}}}
            """);
        var writer = new CopilotCliWriter(config, mDir);
        Assert.True(writer.Register(McpEndpoint.Default).Success);
        string text = File.ReadAllText(config);
        Assert.Contains("inspectBinding", text);
        Assert.DoesNotContain("\"*\"", text);
        Assert.Contains("\"disabled\": true", text);
        Assert.Contains("\"timeout\": 321", text);
        Assert.Contains("X-Local", text);
        Assert.Contains("https://other", text);
        Assert.Contains("dark", text);
    }
}
