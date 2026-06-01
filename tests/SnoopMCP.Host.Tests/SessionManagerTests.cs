// SessionManagerTests.cs
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

public sealed class SessionManagerTests
{
    private static SessionManager CreateManager()
    {
        return new SessionManager(NullLogger<SessionManager>.Instance, NullLoggerFactory.Instance);
    }

    [Fact]
    public void AllocatePipeName_ReturnsUnique()
    {
        var manager = CreateManager();
        string a = SessionManager.AllocatePipeName();
        string b = SessionManager.AllocatePipeName();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task SendAsync_WithoutOpen_ThrowsSessionLost()
    {
        await using var manager = CreateManager();

        SnoopMcpException ex = await Assert.ThrowsAsync<SnoopMcpException>(
            () => manager.SendAsync("echo", new { }, default));
        Assert.Equal(ErrorCode.SessionLost, ex.Code);
    }

    [Fact]
    public async Task OpenAsync_ConnectsAndSendsThroughClient()
    {
        await using var manager = CreateManager();
        string pipeName = SessionManager.AllocatePipeName();

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
            JsonElement res = JsonDocument.Parse("{\"ok\":true}").RootElement.Clone();
            await WireSerializer.WriteFrameAsync(
                server,
                new RpcResponse { Id = request!.Id, Result = res },
                default);
        });

        await manager.OpenAsync(pipeName, default);
        Assert.True(manager.IsAttached);

        JsonElement got = await manager.SendAsync("anything", new { x = 1 }, default);
        Assert.True(got.GetProperty("ok").GetBoolean());

        await manager.CloseAsync();
        Assert.False(manager.IsAttached);
        await serverTask;
    }

    [Fact]
    public async Task OpenAsync_TwiceWithoutClose_Throws()
    {
        await using var manager = CreateManager();
        string pipeName = SessionManager.AllocatePipeName();

        Task serverTask = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync();
            await Task.Delay(200);
        });

        await manager.OpenAsync(pipeName, default);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.OpenAsync(pipeName, default));

        await manager.CloseAsync();
        await serverTask;
    }
}
