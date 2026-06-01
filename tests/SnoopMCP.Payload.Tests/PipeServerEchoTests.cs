// PipeServerEchoTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Payload;
using Tools;
using Protocol.Wire;
using Xunit;

public sealed class PipeServerEchoTests
{
    [Fact]
    public async Task Echo_ViaPipe_RoundTrips()
    {
        const int ConnectTimeoutMs = 5000;
        string pipeName = $"snoopmcp-echo-{Guid.NewGuid():N}";
        var registry = new ToolRegistry();
        registry.Register(new EchoToolHandler());

        await using var server = new PipeServer(pipeName, registry, NullLogger<PipeServer>.Instance);
        server.Start();

        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(ConnectTimeoutMs);

        using var argsDoc = JsonDocument.Parse("{\"hello\":\"world\"}");
        var request = new RpcRequest
        {
            Id = 1,
            Tool = "echo",
            Arguments = argsDoc.RootElement
        };

        await WireSerializer.WriteFrameAsync(client, request, CancellationToken.None);
        var response = await WireSerializer.ReadFrameAsync<RpcResponse>(client, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(1, response.Id);
        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Result);

        string echoedRaw = response.Result!.Value.GetProperty("echoed").GetString() ?? string.Empty;
        Assert.Contains("\"hello\"", echoedRaw);
        Assert.Contains("\"world\"", echoedRaw);
    }
}
