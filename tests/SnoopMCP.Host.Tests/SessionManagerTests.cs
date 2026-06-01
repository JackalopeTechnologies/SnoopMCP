// SessionManagerTests.cs
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

using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Wire;
using Xunit;

#endregion

namespace SnoopMCP.Host.Tests;

public sealed class SessionManagerTests
{
    private static SessionManager CreateManager()
    {
        return new SessionManager(NullLogger<SessionManager>.Instance, NullLoggerFactory.Instance);
    }

    [Fact]
    public void AllocatePipeName_ReturnsUnique()
    {
        SessionManager manager = CreateManager();
        var a = SessionManager.AllocatePipeName();
        var b = SessionManager.AllocatePipeName();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task SendAsync_WithoutOpen_ThrowsSessionLost()
    {
        await using SessionManager manager = CreateManager();

        SnoopMcpException ex =
            await Assert.ThrowsAsync<SnoopMcpException>(() => manager.SendAsync("echo", new { }, default));
        Assert.Equal(ErrorCode.SessionLost, ex.Code);
    }

    [Fact]
    public async Task OpenAsync_ConnectsAndSendsThroughClient()
    {
        await using SessionManager manager = CreateManager();
        var pipeName = SessionManager.AllocatePipeName();

        var serverTask = Task.Run(async () =>
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
        await using SessionManager manager = CreateManager();
        var pipeName = SessionManager.AllocatePipeName();

        var serverTask = Task.Run(async () =>
        {
            await using var server = new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync();
            await Task.Delay(200);
        });

        await manager.OpenAsync(pipeName, default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.OpenAsync(pipeName, default));

        await manager.CloseAsync();
        await serverTask;
    }
}
