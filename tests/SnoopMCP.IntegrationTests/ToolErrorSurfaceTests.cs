// ToolErrorSurfaceTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.IntegrationTests;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Host;
using Protocol.Errors;
using Xunit;

/// <summary>
/// End-to-end proof that a failing tool call reaches the CLIENT with a typed, self-describing
/// message rather than the SDK's sanitised "An error occurred invoking '…'" (issue #81).
/// <para>
/// This exercises the one path no try/catch inside a tool can reach: the SDK's argument marshaller
/// rejects the call while binding parameters, before the tool body runs. That is exactly how #72
/// presented — a bare failure that read as a broken AutomationPeer when the call had simply been
/// rejected upstream — so the assertion here is on the real wire response, not on a mapping function.
/// </para>
/// </summary>
public sealed class ToolErrorSurfaceTests
{
    [Fact]
    public async Task CallTool_WithMissingRequiredArgument_ReturnsTypedErrorNotSanitisedText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int port = GetFreePort();
        string logPath = Path.Combine(Path.GetTempPath(), $"snoopmcp-errors-{Guid.NewGuid():N}.log");
        WebApplication app = ServerHost.Build([], port, logPath);
        await app.StartAsync(ct);
        try
        {
            // describe_element genuinely requires 'id'; omitting it fails inside the SDK's marshaller.
            const string Body = """
                {"jsonrpc":"2.0","id":1,"method":"tools/call",
                 "params":{"name":"describe_element","arguments":{}}}
                """;

            string response = await PostAsync(port, Body, ct);

            // The SDK always prefixes "An error occurred invoking '<tool>'" and appends the
            // exception's own message ONLY when it is an McpException. Before the fix the text
            // stopped at that prefix, naming neither the failure nor its reason; the assertions
            // below are on the part that carries the cause.
            Assert.Contains(nameof(ErrorCode.InvalidArgument), response, StringComparison.Ordinal);
            Assert.Contains("missing required argument", response, StringComparison.Ordinal);
            Assert.Contains("id", response, StringComparison.Ordinal);
        }
        finally
        {
            await app.StopAsync(ct);
            await app.DisposeAsync();
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
    }

    private static async Task<string> PostAsync(int port, string body, CancellationToken ct)
    {
        using var client = new HttpClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using HttpResponseMessage message = await client
            .PostAsync(new Uri($"http://127.0.0.1:{port}/mcp"), content, ct)
            .ConfigureAwait(false);
        return await message.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
