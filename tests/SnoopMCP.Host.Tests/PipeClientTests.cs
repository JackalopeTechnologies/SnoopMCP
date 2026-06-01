// PipeClientTests.cs
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

public sealed class PipeClientTests
{
    [Fact]
    public async Task SendAsync_ReturnsResultJson()
    {
        var pipeName = $"snoopmcp-test-{Guid.NewGuid():N}";

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
        var pipeName = $"snoopmcp-test-{Guid.NewGuid():N}";

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
            var response = new RpcResponse
            {
                Id = request!.Id, Error = new RpcError { Code = ErrorCode.ElementExpired, Message = "gone" }
            };
            await WireSerializer.WriteFrameAsync(server, response, default);
        });

        await using var client = new PipeClient(pipeName, NullLogger<PipeClient>.Instance);
        await client.ConnectAsync(default);

        SnoopMcpException ex =
            await Assert.ThrowsAsync<SnoopMcpException>(() => client.SendAsync("anything", new { }, default));
        Assert.Equal(ErrorCode.ElementExpired, ex.Code);
        await serverTask;
    }
}
