// ServerController.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Owns the in-process MCP <see cref="WebApplication"/> and its <see cref="ServerState"/>. Start
/// builds and binds a fresh instance; Stop unbinds and disposes it so the port frees without the
/// process exiting. A failed bind (for example, the port is taken) becomes <see cref="ServerState.Faulted"/>
/// rather than throwing, so the tray stays alive.
/// </summary>
public sealed class ServerController : IAsyncDisposable
{
    private readonly string[] mArgs;
    private WebApplication? mApp;

    /// <summary>Raised on the calling thread whenever <see cref="State"/> changes.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Initialises a new <see cref="ServerController"/>.</summary>
    /// <param name="args">Process arguments forwarded to each built server instance.</param>
    public ServerController(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        mArgs = args;
    }

    /// <summary>Gets the current server lifecycle state.</summary>
    public ServerState State { get; private set; } = ServerState.Stopped;

    /// <summary>Gets a value indicating whether a WPF target is currently attached.</summary>
    public bool IsAttached => mApp?.Services.GetService<SessionManager>()?.IsAttached == true;

    /// <summary>Builds and starts a fresh server instance when one is not already running.</summary>
    public async Task StartAsync()
    {
        if (ServerStateInfo.CanStart(State))
        {
            SetState(ServerState.Starting);
            WebApplication app = ServerHost.Build(mArgs);
            // ConfigureAwait(true) is required: the post-await SetState must run on the UI thread,
            // where the tray toggles MenuItem.IsEnabled and calls Shell_NotifyIcon.
            bool started = await TryStartAsync(app).ConfigureAwait(true);
            if (started)
            {
                mApp = app;
                SetState(ServerState.Running);
            }
            else
            {
                await app.DisposeAsync().ConfigureAwait(true);
                SetState(ServerState.Faulted);
            }
        }
    }

    /// <summary>Stops and disposes the running server, freeing the port.</summary>
    public async Task StopAsync()
    {
        if (ServerStateInfo.CanStop(State) && mApp is not null)
        {
            WebApplication app = mApp;
            mApp = null;
            // ConfigureAwait(true) is required: the post-await SetState must run on the UI thread,
            // where the tray toggles MenuItem.IsEnabled and calls Shell_NotifyIcon.
            await app.StopAsync().ConfigureAwait(true);
            await app.DisposeAsync().ConfigureAwait(true);
            SetState(ServerState.Stopped);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (mApp is not null)
        {
            WebApplication app = mApp;
            mApp = null;
            // ConfigureAwait(false) is required: App.OnExit blocks on this via GetAwaiter().GetResult()
            // on the dispatcher thread, so resuming on that captured context would deadlock.
            await app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<bool> TryStartAsync(WebApplication app)
    {
        bool ok = true;
        try
        {
            await app.StartAsync().ConfigureAwait(true);
        }
        catch (Exception)
        {
            ok = false;
        }
        return ok;
    }

    private void SetState(ServerState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
