// PipeServer.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload;

using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Tools;
using Protocol.Errors;
using Protocol.Wire;

/// <summary>
/// Named-pipe server that accepts one client at a time and dispatches incoming
/// <see cref="RpcRequest"/> frames to a <see cref="ToolRegistry"/>.
/// </summary>
public sealed partial class PipeServer : IAsyncDisposable
{
    private const int PipeBufferSize = 64 * 1024;

    private readonly string mPipeName;
    private readonly ToolRegistry mRegistry;
    private readonly ILogger<PipeServer> mLogger;
    private readonly CancellationTokenSource mShutdown = new();
    private Task? mAcceptLoop;

    /// <summary>
    /// Initialises a new <see cref="PipeServer"/> bound to the supplied named pipe.
    /// </summary>
    /// <param name="pipeName">The named-pipe instance to create.</param>
    /// <param name="registry">The tool registry to dispatch into.</param>
    /// <param name="logger">The logger to record diagnostics into.</param>
    public PipeServer(string pipeName, ToolRegistry registry, ILogger<PipeServer> logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(logger);
        mPipeName = pipeName;
        mRegistry = registry;
        mLogger = logger;
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Pipe IO exception in accept loop; will retry.")]
    private partial void LogPipeIoException(Exception ex);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Unhandled exception in tool '{Tool}'.")]
    private partial void LogToolException(Exception ex, string tool);

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        bool keepRunning = true;
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
                await ServeClientAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                keepRunning = false;
            }
            catch (IOException ex)
            {
                LogPipeIoException(ex);
            }
        }
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
                LogToolException(ex, request.Tool);
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
