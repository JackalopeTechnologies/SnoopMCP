// WireSerializerTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tests;

using System.IO;
using System.Text.Json;
using Errors;
using Wire;
using Xunit;

/// <summary>
/// Round-trip and edge-case tests for the length-prefixed <see cref="WireSerializer"/>.
/// </summary>
public sealed class WireSerializerTests
{
    [Fact]
    public async Task RoundTrip_Request_PreservesIdToolAndArguments()
    {
        var args = JsonDocument.Parse("{\"pid\":1234}").RootElement;
        var original = new RpcRequest
        {
            Id = 42,
            Tool = "attach",
            Arguments = args
        };

        using var stream = new MemoryStream();
        await WireSerializer.WriteFrameAsync(stream, original, CancellationToken.None);
        stream.Position = 0;

        var roundTripped = await WireSerializer.ReadFrameAsync<RpcRequest>(stream, CancellationToken.None);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.Id, roundTripped.Id);
        Assert.Equal(original.Tool, roundTripped.Tool);
        Assert.Equal(1234, roundTripped.Arguments.GetProperty("pid").GetInt32());
    }

    [Fact]
    public async Task RoundTrip_Response_WithResult_PreservesPayload()
    {
        var resultPayload = JsonDocument.Parse("{\"ok\":true,\"count\":3}").RootElement;
        var original = new RpcResponse
        {
            Id = 7,
            Result = resultPayload
        };

        using var stream = new MemoryStream();
        await WireSerializer.WriteFrameAsync(stream, original, CancellationToken.None);
        stream.Position = 0;

        var roundTripped = await WireSerializer.ReadFrameAsync<RpcResponse>(stream, CancellationToken.None);

        Assert.NotNull(roundTripped);
        Assert.Equal(7, roundTripped.Id);
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

        var roundTripped = await WireSerializer.ReadFrameAsync<RpcResponse>(stream, CancellationToken.None);

        Assert.NotNull(roundTripped);
        Assert.False(roundTripped.IsSuccess);
        Assert.NotNull(roundTripped.Error);
        Assert.Equal(ErrorCode.ElementExpired, roundTripped.Error!.Code);
        Assert.Equal("Element id 42 has been garbage collected.", roundTripped.Error.Message);
        Assert.Equal("live=false", roundTripped.Error.Details);
    }

    [Fact]
    public async Task ReadFrame_OnEofBeforeHeader_ReturnsDefault()
    {
        using var stream = new MemoryStream();
        var result = await WireSerializer.ReadFrameAsync<RpcRequest>(stream, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task ReadFrame_OnTruncatedBody_ReturnsDefault()
    {
        using var stream = new MemoryStream();
        byte[] header = [0xFF, 0x00, 0x00, 0x00];
        await stream.WriteAsync(header);
        stream.Position = 0;

        var result = await WireSerializer.ReadFrameAsync<RpcRequest>(stream, CancellationToken.None);
        Assert.Null(result);
    }
}
