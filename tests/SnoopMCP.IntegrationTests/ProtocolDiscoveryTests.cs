// ProtocolDiscoveryTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.IntegrationTests;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using ModelContextProtocol.Protocol;
using Host;
using Host.Automation;
using Host.Guidance;
using Xunit;

/// <summary>Checks discovery and safety on the real HTTP endpoint without attaching to any application.</summary>
public sealed class ProtocolDiscoveryTests : IAsyncLifetime
{
    private const string LegacyVersion = "2025-11-25";
    private const string ModernVersion = "2026-07-28";
    private readonly string mDirectory = Path.Combine(Path.GetTempPath(), $"snoopmcp-discovery-{Guid.NewGuid():N}");
    private readonly HttpClient mClient = new();
    private WebApplication? mApp;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(mDirectory);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        mApp = ServerHost.Build([], port, Path.Combine(mDirectory, "server.log"),
            new InteractionGate(Path.Combine(mDirectory, "gate.json")));
        await mApp.StartAsync(TestContext.Current.CancellationToken);
        mClient.BaseAddress = new Uri($"http://127.0.0.1:{port}/");
        mClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        mClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
    }

    public async ValueTask DisposeAsync()
    {
        mClient.Dispose();
        if (mApp is not null)
        {
            await mApp.StopAsync(CancellationToken.None);
            await mApp.DisposeAsync();
        }
        Directory.Delete(mDirectory, recursive: true);
    }

    [Theory]
    [InlineData(LegacyVersion)]
    [InlineData(ModernVersion)]
    public async Task Discovery_AdvertisesToolsResourcesAndCanonicalGuidance(string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        JsonElement response = version == ModernVersion
            ? await PostAsync("server/discover", version)
            : await PostAsync("initialize", version, new JsonObject
            {
                ["protocolVersion"] = version,
                ["clientInfo"] = new JsonObject { ["name"] = "SnoopMCP tests", ["version"] = "1.0" },
                ["capabilities"] = new JsonObject()
            });
        JsonElement result = response.GetProperty("result");
        Assert.True(result.GetProperty("capabilities").TryGetProperty("tools", out _));
        Assert.True(result.GetProperty("capabilities").TryGetProperty("resources", out _));
        Assert.Contains(SkillResources.InspectionUri, result.GetProperty("instructions").GetString());
        Assert.Contains(SkillResources.InteractionUri, result.GetProperty("instructions").GetString());
        if (version == ModernVersion)
        {
            Assert.Contains(result.GetProperty("supportedVersions").EnumerateArray(), item => item.GetString() == version);
            Assert.Contains("SnoopMCP", result.GetProperty("_meta").GetProperty(MetaKeys.ServerInfo).GetProperty("name").GetString());
        }
        else
        {
            Assert.Equal(version, result.GetProperty("protocolVersion").GetString());
            Assert.Contains("SnoopMCP", result.GetProperty("serverInfo").GetProperty("name").GetString());
        }
    }

    [Theory]
    [InlineData(LegacyVersion)]
    [InlineData(ModernVersion)]
    public async Task Resources_ReturnExactCanonicalSkillsWithoutChangingTheGate(string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        JsonElement response = await PostAsync("resources/list", version);
        JsonElement resources = response.GetProperty("result").GetProperty("resources");
        Assert.Equal(2, resources.GetArrayLength());
        foreach (JsonElement resource in resources.EnumerateArray())
        {
            string name = resource.GetProperty("name").GetString() ?? throw new InvalidOperationException();
            string uri = resource.GetProperty("uri").GetString() ?? throw new InvalidOperationException();
            Assert.Contains(uri, new[] { SkillResources.InspectionUri, SkillResources.InteractionUri });
            JsonElement read = await PostAsync("resources/read", version, new JsonObject { ["uri"] = uri });
            JsonElement result = read.GetProperty("result");
            JsonElement contents = Assert.Single(result.GetProperty("contents").EnumerateArray());
            string canonical = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "skills", name, "SKILL.md"),
                TestContext.Current.CancellationToken);
            Assert.Equal(canonical, contents.GetProperty("text").GetString());
            Assert.Equal("text/markdown", contents.GetProperty("mimeType").GetString());
            if (version == ModernVersion)
            {
                Assert.Equal(TimeSpan.FromMinutes(5).TotalMilliseconds, result.GetProperty("ttlMs").GetDouble());
                Assert.Equal("private", result.GetProperty("cacheScope").GetString());
            }
        }
        Assert.False(File.Exists(Path.Combine(mDirectory, "gate.json")));
    }

    [Theory]
    [InlineData(LegacyVersion)]
    [InlineData(ModernVersion)]
    public async Task ToolSafetyHints_DoNotBypassTheInteractionGate(string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        JsonElement response = await PostAsync("tools/list", version);
        JsonElement tools = response.GetProperty("result").GetProperty("tools");
        string[] mutations = ["attach", "detach", "peer_invoke", "execute_command", "invoke_uia", "set_uia_value"];
        foreach (string name in mutations)
        {
            Assert.Contains(tools.EnumerateArray(), tool => tool.GetProperty("name").GetString() == name);
        }
        foreach (JsonElement tool in tools.EnumerateArray())
        {
            bool mutates = mutations.Contains(tool.GetProperty("name").GetString(), StringComparer.Ordinal);
            JsonElement annotations = tool.GetProperty("annotations");
            Assert.Equal(!mutates, annotations.GetProperty("readOnlyHint").GetBoolean());
            Assert.Equal(mutates, annotations.GetProperty("destructiveHint").GetBoolean());
        }
        JsonElement call = await PostAsync("tools/call", version, new JsonObject
        {
            ["name"] = "peer_invoke",
            ["arguments"] = new JsonObject { ["id"] = 1, ["pattern"] = "Invoke" }
        });
        Assert.True(call.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Contains("InteractionDisabled", call.ToString());
        Assert.False(File.Exists(Path.Combine(mDirectory, "gate.json")));
    }

    private async Task<JsonElement> PostAsync(string method, string version, JsonObject? parameters = null)
    {
        parameters ??= new JsonObject();
        if (version == ModernVersion)
        {
            parameters["_meta"] = new JsonObject
            {
                [MetaKeys.ProtocolVersion] = version,
                [MetaKeys.ClientInfo] = new JsonObject { ["name"] = "SnoopMCP tests", ["version"] = "1.0" },
                [MetaKeys.ClientCapabilities] = new JsonObject()
            };
        }
        var request = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method, ["params"] = parameters };
        using var message = new HttpRequestMessage(HttpMethod.Post, "mcp")
        {
            Content = new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json")
        };
        message.Headers.Add("MCP-Protocol-Version", version);
        if (version == ModernVersion)
        {
            message.Headers.Add("Mcp-Method", method);
            string? name = (parameters["name"] ?? parameters["uri"])?.GetValue<string>();
            if (name is not null)
            {
                message.Headers.Add("Mcp-Name", name);
            }
        }
        using HttpResponseMessage response = await mClient.SendAsync(message, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            body = body.Split('\n').Single(line => line.StartsWith("data: ", StringComparison.Ordinal))["data: ".Length..];
        }
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.False(document.RootElement.TryGetProperty("error", out _), body);
        return document.RootElement.Clone();
    }
}
