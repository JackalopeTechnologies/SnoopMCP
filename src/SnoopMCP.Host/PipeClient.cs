// PipeClient.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SnoopMCP.Protocol.Errors;
using SnoopMCP.Protocol.Wire;

/// <summary>
/// Host-side counterpart to the payload's <c>PipeServer</c>. Connects to a named pipe, writes
/// length-prefixed <see cref="RpcRequest"/> frames, reads <see cref="RpcResponse"/> frames, and
/// surfaces <see cref="RpcError"/> payloads as <see cref="SnoopMcpException"/>. A single lock
/// serialises calls so request/response framing stays paired on the one-client pipe.
/// </summary>
public sealed partial class PipeClient : IAsyncDisposable
{
    private const int ConnectTimeoutMs = 5000;
    private const string NullJsonLiteral = "null";

    private readonly string mPipeName;
    private readonly ILogger<PipeClient> mLogger;
    private readonly SemaphoreSlim mLock = new(1, 1);
    private NamedPipeClientStream? mStream;
    private long mNextRequestId;

    /// <summary>
    /// Initialises a new <see cref="PipeClient"/> bound to the supplied named pipe.
    /// </summary>
    /// <param name="pipeName">The named-pipe instance to connect to.</param>
    /// <param name="logger">The logger to record diagnostics into.</param>
    public PipeClient(string pipeName, ILogger<PipeClient> logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        ArgumentNullException.ThrowIfNull(logger);
        mPipeName = pipeName;
        mLogger = logger;
    }

    /// <summary>Gets a value indicating whether the underlying pipe stream is connected.</summary>
    public bool IsConnected => mStream is { IsConnected: true };

    /// <summary>
    /// Connects to the named pipe. Throws if already connected.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while connecting.</param>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (mStream is not null)
            {
                throw new InvalidOperationException("Pipe client already connected.");
            }

            var stream = new NamedPipeClientStream(
                ".",
                mPipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await stream.ConnectAsync(ConnectTimeoutMs, cancellationToken).ConfigureAwait(false);
            mStream = stream;
            LogConnected(mPipeName);
        }
        finally
        {
            mLock.Release();
        }
    }

    /// <summary>
    /// Sends a tool request and returns the result JSON. Throws <see cref="SnoopMcpException"/>
    /// when the payload returns an error or the pipe closes before a response arrives.
    /// </summary>
    /// <param name="toolName">The wire tool name to dispatch.</param>
    /// <param name="arguments">The tool-specific argument object; serialised to the request body.</param>
    /// <param name="cancellationToken">A token to observe while sending and receiving.</param>
    /// <returns>The result element returned by the payload.</returns>
    public async Task<JsonElement> SendAsync(
        string toolName,
        object arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        ArgumentNullException.ThrowIfNull(arguments);

        JsonElement result;
        await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            NamedPipeClientStream stream = mStream
                ?? throw new InvalidOperationException("Pipe client not connected.");

            long id = Interlocked.Increment(ref mNextRequestId);
            JsonElement argsElement = ToJsonElement(arguments);
            var request = new RpcRequest { Id = id, Tool = toolName, Arguments = argsElement };

            await WireSerializer.WriteFrameAsync(stream, request, cancellationToken).ConfigureAwait(false);
            RpcResponse? response = await WireSerializer
                .ReadFrameAsync<RpcResponse>(stream, cancellationToken)
                .ConfigureAwait(false);

            result = ExtractResult(response);
        }
        finally
        {
            mLock.Release();
        }

        return result;
    }

    private static JsonElement ExtractResult(RpcResponse? response)
    {
        if (response is null)
        {
            throw new SnoopMcpException(ErrorCode.SessionLost, "Pipe closed before response arrived.");
        }

        if (response.Error is not null)
        {
            throw new SnoopMcpException(response.Error.Code, response.Error.Message);
        }

        JsonElement result = response.Result ?? JsonDocument.Parse(NullJsonLiteral).RootElement.Clone();
        return result;
    }

    private static JsonElement ToJsonElement(object arguments)
    {
        string json = JsonSerializer.Serialize(arguments, WireSerializer.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await mLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (mStream is not null)
            {
                await mStream.DisposeAsync().ConfigureAwait(false);
                mStream = null;
            }
        }
        finally
        {
            mLock.Release();
            mLock.Dispose();
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Connected to pipe {PipeName}.")]
    private partial void LogConnected(string pipeName);
}
