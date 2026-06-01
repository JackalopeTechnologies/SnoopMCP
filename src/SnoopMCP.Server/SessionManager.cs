// SessionManager.cs
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
using Microsoft.Extensions.Logging;
using SnoopMCP.Protocol.Errors;

#endregion

namespace SnoopMCP.Host;

/// <summary>
///     Owns the lifecycle of a single attached target. Allocates the pipe name, holds the
///     <see cref="PipeClient" />, and dispatches tool calls through it. A <see cref="SessionManager" />
///     represents at most one attached target at a time; opening a session while one is already open
///     throws, which the MCP layer surfaces to the client as a tool error.
/// </summary>
public sealed partial class SessionManager : IAsyncDisposable
{
    /// <summary>
    ///     Initialises a new <see cref="SessionManager" />.
    /// </summary>
    /// <param name="logger">The logger to record lifecycle events into.</param>
    /// <param name="loggerFactory">The factory used to create the per-session <see cref="PipeClient" /> logger.</param>
    public SessionManager(ILogger<SessionManager> logger, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        mLogger = logger;
        mLoggerFactory = loggerFactory;
    }

    /// <summary>Gets a value indicating whether a session is open and its pipe is connected.</summary>
    public bool IsAttached => mClient is { IsConnected: true };

    /// <summary>Gets the pipe name of the open session, or <c>null</c> when none is open.</summary>
    public string? PipeName { get; private set; }

    private readonly SemaphoreSlim mLock = new(1, 1);
    private readonly ILogger<SessionManager> mLogger;
    private readonly ILoggerFactory mLoggerFactory;
    private PipeClient? mClient;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        mLock.Dispose();
    }

    /// <summary>
    ///     Allocates a unique pipe name for a new session.
    /// </summary>
    /// <returns>A process-unique pipe name.</returns>
    public static string AllocatePipeName()
    {
        return $"snoopmcp-{Guid.NewGuid():N}";
    }

    /// <summary>
    ///     Opens a session on the supplied pipe by connecting a fresh <see cref="PipeClient" />. Throws
    ///     when a session is already open.
    /// </summary>
    /// <param name="pipeName">The pipe name the payload is serving on.</param>
    /// <param name="cancellationToken">A token to observe while connecting.</param>
    public async Task OpenAsync(string pipeName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);

        await mLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (mClient is not null)
                throw new InvalidOperationException(
                    "A session is already open; call CloseAsync before opening a new one.");

            var client = new PipeClient(pipeName, mLoggerFactory.CreateLogger<PipeClient>());
            await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            mClient = client;
            PipeName = pipeName;
            LogSessionOpened(pipeName);
        }
        finally
        {
            mLock.Release();
        }
    }

    /// <summary>
    ///     Closes the open session, disposing its <see cref="PipeClient" />. A no-op when no session is open.
    /// </summary>
    public async Task CloseAsync()
    {
        await mLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (mClient is not null)
            {
                await mClient.DisposeAsync().ConfigureAwait(false);
                mClient = null;
                PipeName = null;
                LogSessionClosed();
            }
        }
        finally
        {
            mLock.Release();
        }
    }

    /// <summary>
    ///     Dispatches a tool call through the open session's pipe. Throws
    ///     <see cref="SnoopMcpException" /> with <see cref="ErrorCode.SessionLost" /> when no session is open.
    /// </summary>
    /// <param name="toolName">The wire tool name to dispatch.</param>
    /// <param name="arguments">The tool-specific argument object.</param>
    /// <param name="cancellationToken">A token to observe while sending and receiving.</param>
    /// <returns>The result element returned by the payload.</returns>
    public Task<JsonElement> SendAsync(string toolName, object arguments, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        ArgumentNullException.ThrowIfNull(arguments);

        PipeClient client = mClient
                            ?? throw new SnoopMcpException(
                                ErrorCode.SessionLost,
                                "No attached session. Call attach(pid) first.");
        return client.SendAsync(toolName, arguments, cancellationToken);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Session opened on pipe {PipeName}.")]
    private partial void LogSessionOpened(string pipeName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Session closed.")]
    private partial void LogSessionClosed();
}
