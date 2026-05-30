// App.xaml.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using System.Windows;

/// <summary>
/// WPF application root for the SnoopMCP tray app. Enforces single-instance, starts the in-process
/// MCP server, shows the tray icon, and tears everything down on Exit or Windows session end.
/// </summary>
// CA1001 suppression: the disposable fields are released in the WPF OnExit override, which is the
// correct teardown hook for the Application singleton. WPF never calls Dispose() on the Application,
// so implementing IDisposable here would be dead code and would not run on shutdown.
#pragma warning disable CA1001
public partial class App : Application
#pragma warning restore CA1001
{
    private const string SingleInstanceMutexName = @"Local\SnoopMCP.Host";
    private Mutex? mInstanceMutex;
    private ServerController? mController;
    private TrayIcon? mTray;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        mInstanceMutex = new Mutex(initiallyOwned: false, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
        }
        else
        {
            mController = new ServerController(e.Args);
            mTray = new TrayIcon(mController, Shutdown);
            SessionEnding += OnSessionEnding;
            _ = mController.StartAsync();
        }
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        Shutdown();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        mTray?.Dispose();
        if (mController is not null)
        {
            mController.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        mInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
