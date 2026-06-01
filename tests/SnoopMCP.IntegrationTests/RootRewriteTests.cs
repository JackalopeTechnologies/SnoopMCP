// RootRewriteTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.IntegrationTests;

using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Host;
using Xunit;

/// <summary>
/// Verifies the ServerHost middleware that rewrites a bare-root request ("/") to the "/mcp" handler,
/// by starting the real host on a free loopback port and comparing routing behaviour. This is the
/// only test that exercises the actual request pipeline (Kestrel + MapMcp + the rewrite), so a
/// regression in middleware ordering or the path comparison is caught here.
/// </summary>
public sealed class RootRewriteTests
{
    private static readonly TimeSpan smClientTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task RootRequest_RoutesToMcpHandler_AndNonRootIsUnaffected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int port = GetFreePort();
        WebApplication app = ServerHost.Build([], port);
        await app.StartAsync(ct);
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = smClientTimeout };

            // /health is a normal mapped endpoint and must NOT be rewritten.
            using HttpResponseMessage health = await client.GetAsync("/health", ct);
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);

            // An unknown path is a genuine 404 — the control that proves "/" below isn't just always non-404.
            using HttpResponseMessage bogus = await client.GetAsync("/does-not-exist", ct);
            Assert.Equal(HttpStatusCode.NotFound, bogus.StatusCode);

            // POST "/" is rewritten to "/mcp": it must reach the MCP handler (never a 404) and return
            // exactly what POST "/mcp" returns for the same request.
            using HttpResponseMessage root = await client.PostAsync("/", new StringContent(string.Empty), ct);
            using HttpResponseMessage mcp = await client.PostAsync("/mcp", new StringContent(string.Empty), ct);
            Assert.NotEqual(HttpStatusCode.NotFound, root.StatusCode);
            Assert.Equal(mcp.StatusCode, root.StatusCode);
        }
        finally
        {
            await app.StopAsync(ct);
            await app.DisposeAsync();
        }
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
