// ServerLoggingTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.IntegrationTests;

using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Host;
using Xunit;

/// <summary>
/// Proves the server's file-logging provider is wired into the running app's logging pipeline - the
/// gap that previously left MCP tool exceptions invisible (only "An error occurred invoking '…'"
/// reached the client, and nothing reached any log). Starts the real host pointed at a temp log
/// file, emits an error through the app's own logger factory, and confirms the entry - message,
/// category, and exception detail - was persisted to that file.
/// </summary>
public sealed class ServerLoggingTests
{
    [Fact]
    public async Task StartedServer_PersistsLoggerOutputToConfiguredFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string logPath = Path.Combine(Path.GetTempPath(), $"snoopmcp-server-{Guid.NewGuid():N}.log");
        WebApplication app = ServerHost.Build([], GetFreePort(), logPath);
        await app.StartAsync(ct);
        try
        {
            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("IntegrationProbe");
            logger.LogError(new InvalidOperationException("probe-detail"), "probe-marker");

            string contents = await File.ReadAllTextAsync(logPath, ct);
            Assert.Contains("probe-marker", contents);
            Assert.Contains("IntegrationProbe", contents);
            Assert.Contains("probe-detail", contents);
            Assert.Contains(nameof(InvalidOperationException), contents);
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

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
