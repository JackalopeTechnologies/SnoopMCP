// PipeServer.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload;

using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using Tools;
using Protocol.Errors;
using Protocol.Wire;

/// <summary>
/// Named-pipe server that accepts one client at a time and dispatches incoming
/// <see cref="RpcRequest"/> frames to a <see cref="ToolRegistry"/>.
/// </summary>
public sealed class PipeServer : IAsyncDisposable
{
    private const int PipeBufferSize = 64 * 1024;
    private static readonly TimeSpan smInitialRetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan smMaxRetryDelay = TimeSpan.FromSeconds(5);

    private readonly string mPipeName;
    private readonly ToolRegistry mRegistry;
    private readonly CancellationTokenSource mShutdown = new();
    private Task? mAcceptLoop;

    /// <summary>
    /// Initialises a new <see cref="PipeServer"/> bound to the supplied named pipe.
    /// </summary>
    /// <param name="pipeName">The named-pipe instance to create.</param>
    /// <param name="registry">The tool registry to dispatch into.</param>
    public PipeServer(string pipeName, ToolRegistry registry)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        ArgumentNullException.ThrowIfNull(registry);
        mPipeName = pipeName;
        mRegistry = registry;
    }

    /// <summary>
    /// Starts the accept loop on a background task. Returns immediately.
    /// </summary>
    public void Start()
    {
        mAcceptLoop = Task.Run(() => AcceptLoopAsync(mShutdown.Token));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await mShutdown.CancelAsync().ConfigureAwait(false);
        if (mAcceptLoop is not null)
        {
            try
            {
                await mAcceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
        mShutdown.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        bool keepRunning = true;
        TimeSpan retryDelay = smInitialRetryDelay;
        while (keepRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    mPipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    PipeBufferSize,
                    PipeBufferSize);

                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                retryDelay = smInitialRetryDelay;
                await ServeClientAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                keepRunning = false;
            }
            catch (IOException)
            {
                // Transient pipe error; back off before re-creating the server stream so a persistent
                // failure cannot spin the accept loop. The delay grows to a cap and resets on the next
                // successful connection; a shutdown request during the delay stops the loop.
                keepRunning = await DelayBeforeRetryAsync(retryDelay, cancellationToken).ConfigureAwait(false);
                retryDelay = NextRetryDelay(retryDelay);
            }
        }
    }

    /// <summary>
    /// Waits <paramref name="delay"/> before the accept loop re-creates the pipe after a transient
    /// error. Returns <c>false</c> if shutdown was requested during the wait, signalling the loop to stop.
    /// </summary>
    private static async Task<bool> DelayBeforeRetryAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        bool keepRunning = true;
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            keepRunning = false;
        }
        return keepRunning;
    }

    /// <summary>
    /// Doubles <paramref name="current"/> up to <see cref="smMaxRetryDelay"/>, giving exponential
    /// backoff while pipe errors persist.
    /// </summary>
    private static TimeSpan NextRetryDelay(TimeSpan current)
    {
        long doubledTicks = current.Ticks * 2;
        return doubledTicks >= smMaxRetryDelay.Ticks ? smMaxRetryDelay : TimeSpan.FromTicks(doubledTicks);
    }

    private async Task ServeClientAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        bool clientConnected = true;
        while (clientConnected && !cancellationToken.IsCancellationRequested)
        {
            RpcRequest? request = await WireSerializer
                .ReadFrameAsync<RpcRequest>(pipe, cancellationToken)
                .ConfigureAwait(false);

            if (request is null)
            {
                clientConnected = false;
            }
            else
            {
                RpcResponse response = await DispatchAsync(request, cancellationToken).ConfigureAwait(false);
                await WireSerializer.WriteFrameAsync(pipe, response, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<RpcResponse> DispatchAsync(RpcRequest request, CancellationToken cancellationToken)
    {
        RpcResponse response;
        IToolHandler? handler = mRegistry.Find(request.Tool);
        if (handler is null)
        {
            response = new RpcResponse
            {
                Id = request.Id,
                Error = new RpcError
                {
                    Code = ErrorCode.ToolNotFound,
                    Message = $"No handler registered for tool '{request.Tool}'."
                }
            };
        }
        else
        {
            try
            {
                JsonElement result = await handler
                    .ExecuteAsync(request.Arguments, cancellationToken)
                    .ConfigureAwait(false);
                response = new RpcResponse { Id = request.Id, Result = result };
            }
            catch (SnoopMcpException ex)
            {
                response = new RpcResponse
                {
                    Id = request.Id,
                    Error = new RpcError { Code = ex.Code, Message = ex.Message }
                };
            }
            catch (Exception ex)
            {
                response = new RpcResponse
                {
                    Id = request.Id,
                    Error = new RpcError
                    {
                        Code = ErrorCode.Unknown,
                        Message = ex.Message,
                        Details = ex.GetType().FullName
                    }
                };
            }
        }
        return response;
    }
}
