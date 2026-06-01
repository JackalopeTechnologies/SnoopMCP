// PipeClientTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SnoopMCP.Host;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Wire;
using Xunit;

public sealed class PipeClientTests
{
    [Fact]
    public async Task SendAsync_ReturnsResultJson()
    {
        string pipeName = $"snoopmcp-test-{Guid.NewGuid():N}";

        Task serverTask = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync();

            RpcRequest? request = await WireSerializer.ReadFrameAsync<RpcRequest>(server, default);
            Assert.NotNull(request);
            Assert.Equal("echo", request!.Tool);

            JsonElement result = JsonDocument.Parse("{\"echoed\":\"ok\"}").RootElement.Clone();
            var response = new RpcResponse { Id = request.Id, Result = result };
            await WireSerializer.WriteFrameAsync(server, response, default);
        });

        await using var client = new PipeClient(pipeName, NullLogger<PipeClient>.Instance);
        await client.ConnectAsync(default);
        JsonElement got = await client.SendAsync("echo", new { hello = "world" }, default);

        Assert.Equal("ok", got.GetProperty("echoed").GetString());
        await serverTask;
    }

    [Fact]
    public async Task SendAsync_ErrorResponse_ThrowsSnoopMcpException()
    {
        string pipeName = $"snoopmcp-test-{Guid.NewGuid():N}";

        Task serverTask = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync();

            RpcRequest? request = await WireSerializer.ReadFrameAsync<RpcRequest>(server, default);
            var response = new RpcResponse
            {
                Id = request!.Id,
                Error = new RpcError { Code = ErrorCode.ElementExpired, Message = "gone" }
            };
            await WireSerializer.WriteFrameAsync(server, response, default);
        });

        await using var client = new PipeClient(pipeName, NullLogger<PipeClient>.Instance);
        await client.ConnectAsync(default);

        SnoopMcpException ex = await Assert.ThrowsAsync<SnoopMcpException>(
            () => client.SendAsync("anything", new { }, default));
        Assert.Equal(ErrorCode.ElementExpired, ex.Code);
        await serverTask;
    }
}
