// WireSerializer.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Protocol.Wire;

using System.Buffers.Binary;
using System.IO;
using System.Text.Json;

/// <summary>
/// Length-prefixed JSON framing for SnoopMCP wire payloads.
/// Frame layout: 4-byte little-endian uint32 payload length, then UTF-8 JSON body.
/// </summary>
public static class WireSerializer
{
    private const int FrameLengthBytes = 4;
    private const int MaxFrameSizeBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions smJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>Gets the shared <see cref="JsonSerializerOptions"/> instance used by the framing layer.</summary>
    public static JsonSerializerOptions JsonOptions => smJsonOptions;

    /// <summary>
    /// Serialises <paramref name="payload"/> as UTF-8 JSON and writes it to <paramref name="destination"/>
    /// prefixed by a 4-byte little-endian length header.
    /// </summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="destination">The stream to write into.</param>
    /// <param name="payload">The payload to serialise; must not be null.</param>
    /// <param name="cancellationToken">A token to observe while writing.</param>
    public static async Task WriteFrameAsync<T>(Stream destination, T payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(payload);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload, smJsonOptions);
        if (body.Length > MaxFrameSizeBytes)
        {
            throw new InvalidOperationException($"Frame exceeds {MaxFrameSizeBytes} bytes ({body.Length}).");
        }

        byte[] header = new byte[FrameLengthBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint) body.Length);

        await destination.WriteAsync(header.AsMemory(), cancellationToken).ConfigureAwait(false);
        await destination.WriteAsync(body.AsMemory(), cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a single length-prefixed frame from <paramref name="source"/> and deserialises it as <typeparamref name="T"/>.
    /// Returns <c>default</c> when the stream is exhausted before a complete frame is available.
    /// </summary>
    /// <typeparam name="T">The payload type to deserialise.</typeparam>
    /// <param name="source">The stream to read from.</param>
    /// <param name="cancellationToken">A token to observe while reading.</param>
    /// <returns>The deserialised payload, or <c>default</c> on EOF / truncated input.</returns>
    public static async Task<T?> ReadFrameAsync<T>(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        byte[] header = new byte[FrameLengthBytes];
        int headerRead = await ReadExactAsync(source, header, cancellationToken).ConfigureAwait(false);
        T? result = default;

        bool gotFullHeader = headerRead == FrameLengthBytes;
        if (gotFullHeader)
        {
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(header);
            if (length > MaxFrameSizeBytes)
            {
                throw new InvalidDataException($"Incoming frame {length} bytes exceeds {MaxFrameSizeBytes}.");
            }

            byte[] body = new byte[length];
            int bodyRead = await ReadExactAsync(source, body, cancellationToken).ConfigureAwait(false);
            bool gotFullBody = bodyRead == (int) length;
            if (gotFullBody)
            {
                result = JsonSerializer.Deserialize<T>(body, smJsonOptions);
            }
        }

        return result;
    }

    private static async Task<int> ReadExactAsync(Stream source, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int total = 0;
        bool keepReading = buffer.Length > 0;
        while (keepReading)
        {
            int chunk = await source.ReadAsync(buffer.Slice(total), cancellationToken).ConfigureAwait(false);
            bool eof = chunk == 0;
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
}
