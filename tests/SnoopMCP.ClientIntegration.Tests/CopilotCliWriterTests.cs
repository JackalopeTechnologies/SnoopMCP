// CopilotCliWriterTests.cs
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

using System.Text.Json.Nodes;
using Xunit;

#endregion

namespace SnoopMCP.ClientIntegration.Tests;

/// <summary>Unit tests for <see cref="CopilotCliWriter" />.</summary>
public sealed class CopilotCliWriterTests
{
    private static string NewTempConfig()
    {
        return Path.Combine(Path.GetTempPath(), $"snoopmcp-copilot-{Guid.NewGuid():N}.json");
    }

    private static CopilotCliWriter NewWriter(string path)
    {
        return new CopilotCliWriter(path, Path.GetDirectoryName(path)!);
    }

    [Fact]
    public void Register_WritesHttpEntryWithToolsWildcard_WhenFileMissing()
    {
        var path = NewTempConfig();
        try
        {
            CopilotCliWriter writer = NewWriter(path);
            RegisterResult result = writer.Register(McpEndpoint.Default);
            Assert.True(result.Success);
            JsonNode entry = JsonNode.Parse(File.ReadAllText(path))![ServersKey]![EndpointName]!;
            Assert.Equal(HttpType, (string?)entry[TypeKey]);
            Assert.Equal(McpEndpoint.Default.Url, (string?)entry[UrlKey]);
            Assert.Equal("*", (string?)((JsonArray)entry[ToolsKey]!)[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Register_PreservesOtherServers_WhenPresent()
    {
        var path = NewTempConfig();
        try
        {
            File.WriteAllText(path, OtherServerJson);
            CopilotCliWriter writer = NewWriter(path);
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
        var path = NewTempConfig();
        try
        {
            CopilotCliWriter writer = NewWriter(path);
            writer.Register(McpEndpoint.Default);
            writer.Register(McpEndpoint.Default);
            JsonNode entry = JsonNode.Parse(File.ReadAllText(path))![ServersKey]![EndpointName]!;
            Assert.Equal(McpEndpoint.Default.Url, (string?)entry[UrlKey]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Unregister_RemovesOnlySnoopEntry_WhenOthersPresent()
    {
        var path = NewTempConfig();
        try
        {
            CopilotCliWriter writer = NewWriter(path);
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
        var path = NewTempConfig();
        CopilotCliWriter writer = NewWriter(path);
        UnregisterResult result = writer.Unregister();
        Assert.True(result.Success);
    }

    [Fact]
    public void GetStatus_ReportsRegistered_AfterRegister()
    {
        var path = NewTempConfig();
        try
        {
            CopilotCliWriter writer = NewWriter(path);
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
        var path = NewTempConfig();
        CopilotCliWriter writer = NewWriter(path);
        StatusResult status = writer.GetStatus();
        Assert.False(status.IsRegistered);
    }

    [Fact]
    public void Register_FailsSafely_WhenJsonMalformed()
    {
        var path = NewTempConfig();
        try
        {
            File.WriteAllText(path, "{ this is not valid json ");
            CopilotCliWriter writer = NewWriter(path);
            RegisterResult result = writer.Register(McpEndpoint.Default);
            Assert.False(result.Success);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private const string EndpointName = "snoopmcp";
    private const string ServersKey = "mcpServers";
    private const string TypeKey = "type";
    private const string UrlKey = "url";
    private const string ToolsKey = "tools";
    private const string HttpType = "http";

    private const string OtherServerJson =
        "{\"mcpServers\":{\"other\":{\"type\":\"http\",\"url\":\"http://localhost:9999/mcp\"}}}";
}
