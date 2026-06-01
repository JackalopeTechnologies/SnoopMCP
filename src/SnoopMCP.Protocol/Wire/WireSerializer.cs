// WireSerializer.cs
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

using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

#endregion

namespace SnoopMCP.Protocol.Wire;

/// <summary>
///     Length-prefixed JSON framing for SnoopMCP wire payloads.
///     Frame layout: 4-byte little-endian uint32 payload length, then UTF-8 JSON body.
/// </summary>
public static class WireSerializer
{
    /// <summary>Gets the shared <see cref="JsonSerializerOptions" /> instance used by the framing layer.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    ///     Serialises <paramref name="payload" /> as UTF-8 JSON and writes it to <paramref name="destination" />
    ///     prefixed by a 4-byte little-endian length header.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="destination">The stream to write into.</param>
    /// <param name="payload">The payload to serialise; must not be null.</param>
    /// <param name="cancellationToken">A token to observe while writing.</param>
    public static async Task WriteFrameAsync<T>(Stream destination, T payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(payload);

        var body = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        if (body.Length > MaxFrameSizeBytes)
            throw new InvalidOperationException($"Frame exceeds {MaxFrameSizeBytes} bytes ({body.Length}).");

        var header = new byte[FrameLengthBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)body.Length);

        await destination.WriteAsync(header.AsMemory(), cancellationToken).ConfigureAwait(false);
        await destination.WriteAsync(body.AsMemory(), cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Reads a single length-prefixed frame from <paramref name="source" /> and deserialises it as
    ///     <typeparamref name="T" />.
    ///     Returns <c>default</c> when the stream is exhausted before a complete frame is available.
    /// </summary>
    /// <typeparam name="T">The payload type to deserialise.</typeparam>
    /// <param name="source">The stream to read from.</param>
    /// <param name="cancellationToken">A token to observe while reading.</param>
    /// <returns>The deserialised payload, or <c>default</c> on EOF / truncated input.</returns>
    public static async Task<T?> ReadFrameAsync<T>(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var header = new byte[FrameLengthBytes];
        var headerRead = await ReadExactAsync(source, header, cancellationToken).ConfigureAwait(false);
        T? result = default;

        var gotFullHeader = headerRead == FrameLengthBytes;
        if (gotFullHeader)
        {
            var length = BinaryPrimitives.ReadUInt32LittleEndian(header);
            if (length > MaxFrameSizeBytes)
                throw new InvalidDataException($"Incoming frame {length} bytes exceeds {MaxFrameSizeBytes}.");

            var body = new byte[length];
            var bodyRead = await ReadExactAsync(source, body, cancellationToken).ConfigureAwait(false);
            var gotFullBody = bodyRead == (int)length;
            if (gotFullBody) result = JsonSerializer.Deserialize<T>(body, JsonOptions);
        }

        return result;
    }

    private static async Task<int> ReadExactAsync(Stream source, Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;
        var keepReading = buffer.Length > 0;
        while (keepReading)
        {
            var chunk = await source.ReadAsync(buffer.Slice(total), cancellationToken).ConfigureAwait(false);
            var eof = chunk == 0;
            if (eof)
            {
                keepReading = false;
            }
            else
            {
                total += chunk;
                keepReading = total < buffer.Length;
            }
        }

        return total;
    }

    private const int FrameLengthBytes = 4;
    private const int MaxFrameSizeBytes = 16 * 1024 * 1024;
}
