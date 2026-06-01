// WireSerializerTests.cs
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

using System.Text.Json;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Wire;
using Xunit;

#endregion

namespace SnoopMCP.Protocol.Tests;

/// <summary>
///     Round-trip and edge-case tests for the length-prefixed <see cref="WireSerializer" />.
/// </summary>
public sealed class WireSerializerTests
{
    [Fact]
    public async Task RoundTrip_Request_PreservesIdToolAndArguments()
    {
        JsonElement args = JsonDocument.Parse("{\"pid\":1234}").RootElement;
        var original = new RpcRequest { Id = 42, Tool = "attach", Arguments = args };

        using var stream = new MemoryStream();
        await WireSerializer.WriteFrameAsync(stream, original, CancellationToken.None);
        stream.Position = 0;

        RpcRequest? roundTripped = await WireSerializer.ReadFrameAsync<RpcRequest>(stream, CancellationToken.None);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.Id, roundTripped!.Id);
        Assert.Equal(original.Tool, roundTripped.Tool);
        Assert.Equal(1234, roundTripped.Arguments.GetProperty("pid").GetInt32());
    }

    [Fact]
    public async Task RoundTrip_Response_WithResult_PreservesPayload()
    {
        JsonElement resultPayload = JsonDocument.Parse("{\"ok\":true,\"count\":3}").RootElement;
        var original = new RpcResponse { Id = 7, Result = resultPayload };

        using var stream = new MemoryStream();
        await WireSerializer.WriteFrameAsync(stream, original, CancellationToken.None);
        stream.Position = 0;

        RpcResponse? roundTripped = await WireSerializer.ReadFrameAsync<RpcResponse>(stream, CancellationToken.None);

        Assert.NotNull(roundTripped);
        Assert.Equal(7, roundTripped!.Id);
        Assert.True(roundTripped.IsSuccess);
        Assert.NotNull(roundTripped.Result);
        Assert.Equal(3, roundTripped.Result!.Value.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task RoundTrip_Response_WithError_PreservesErrorFields()
    {
        var original = new RpcResponse
        {
            Id = 9,
            Error = new RpcError
            {
                Code = ErrorCode.ElementExpired,
                Message = "Element id 42 has been garbage collected.",
                Details = "live=false"
            }
        };

        using var stream = new MemoryStream();
        await WireSerializer.WriteFrameAsync(stream, original, CancellationToken.None);
        stream.Position = 0;

        RpcResponse? roundTripped = await WireSerializer.ReadFrameAsync<RpcResponse>(stream, CancellationToken.None);

        Assert.NotNull(roundTripped);
        Assert.False(roundTripped!.IsSuccess);
        Assert.NotNull(roundTripped.Error);
        Assert.Equal(ErrorCode.ElementExpired, roundTripped.Error!.Code);
        Assert.Equal("Element id 42 has been garbage collected.", roundTripped.Error.Message);
        Assert.Equal("live=false", roundTripped.Error.Details);
    }

    [Fact]
    public async Task ReadFrame_OnEofBeforeHeader_ReturnsDefault()
    {
        using var stream = new MemoryStream();
        RpcRequest? result = await WireSerializer.ReadFrameAsync<RpcRequest>(stream, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task ReadFrame_OnTruncatedBody_ReturnsDefault()
    {
        using var stream = new MemoryStream();
        byte[] header = { 0xFF, 0x00, 0x00, 0x00 };
        await stream.WriteAsync(header);
        stream.Position = 0;

        RpcRequest? result = await WireSerializer.ReadFrameAsync<RpcRequest>(stream, CancellationToken.None);
        Assert.Null(result);
    }
}
