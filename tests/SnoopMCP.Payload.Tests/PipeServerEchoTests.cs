// PipeServerEchoTests.cs
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
using SnoopMCP.Payload.Tools;
using SnoopMCP.Protocol.Wire;
using Xunit;

#endregion

namespace SnoopMCP.Payload.Tests;

public sealed class PipeServerEchoTests
{
    [Fact]
    public async Task Echo_ViaPipe_RoundTrips()
    {
        const int connectTimeoutMs = 5000;
        var pipeName = $"snoopmcp-echo-{Guid.NewGuid():N}";
        var registry = new ToolRegistry();
        registry.Register(new EchoToolHandler());

        await using var server = new PipeServer(pipeName, registry, NullLogger<PipeServer>.Instance);
        server.Start();

        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(connectTimeoutMs);

        using var argsDoc = JsonDocument.Parse("{\"hello\":\"world\"}");
        var request = new RpcRequest { Id = 1, Tool = "echo", Arguments = argsDoc.RootElement };

        await WireSerializer.WriteFrameAsync(client, request, CancellationToken.None);
        RpcResponse? response = await WireSerializer.ReadFrameAsync<RpcResponse>(client, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(1, response!.Id);
        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Result);

        var echoedRaw = response.Result!.Value.GetProperty("echoed").GetString() ?? string.Empty;
        Assert.Contains("\"hello\"", echoedRaw);
        Assert.Contains("\"world\"", echoedRaw);
    }
}
