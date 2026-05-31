// App.xaml.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using System.Windows;
using H.NotifyIcon;

/// <summary>
/// WPF application root for the SnoopMCP tray app. Enforces single-instance, starts the in-process
/// MCP server, realises the tray icon (declared in App.xaml), and tears everything down on Exit or
/// Windows session end.
/// </summary>
// CA1001 suppression: the disposable fields are released in the WPF OnExit override, which is the
// correct teardown hook for the Application singleton. WPF never calls Dispose() on the Application,
// so implementing IDisposable here would be dead code and would not run on shutdown.
#pragma warning disable CA1001
public partial class App : Application
#pragma warning restore CA1001
{
    private const string SingleInstanceMutexName = @"Local\SnoopMCP.Host";
    private const string TrayIconResourceKey = "TrayIcon";
    private const string AppTitle = "SnoopMCP";
    private const string AlreadyRunningMessage = "SnoopMCP is already running — check the system tray.";

    private static readonly TimeSpan smExitDisposeTimeout = TimeSpan.FromSeconds(5);

    private Mutex? mInstanceMutex;
    private ServerController? mController;
    private TaskbarIcon? mTrayIcon;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        mInstanceMutex = new Mutex(initiallyOwned: false, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            // Without feedback a second launch just vanishes; tell the user where the running one is.
            HostLog.Info("Another SnoopMCP instance is already running; exiting.");
            MessageBox.Show(AlreadyRunningMessage, AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
        }
        else
        {
            mController = new ServerController(e.Args);
            mTrayIcon = (TaskbarIcon)FindResource(TrayIconResourceKey);
            mTrayIcon.DataContext = new TrayViewModel(mController, Shutdown,
                (title, message) => mTrayIcon.ShowNotification(title, message),
                ShowOwnedDialog);
            mTrayIcon.ForceCreate();
            SessionEnding += OnSessionEnding;
            _ = mController.StartAsync();
            HostLog.Info("SnoopMCP host started.");
        }
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        Shutdown();
    }

    // A tray app has no main window, so an ownerless MessageBox opens unactivated and the closing
    // context menu's input dismisses it before the user can read it. Give it a tiny, off-screen,
    // topmost owner that is activated first, so the dialog stays modal and on top until OK is clicked.
    private static void ShowOwnedDialog(string title, string message)
    {
        var owner = new Window
        {
            Width = 1,
            Height = 1,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            Topmost = true,
            ShowActivated = true
        };
        try
        {
            owner.Show();
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            owner.Close();
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        // Order matters: the mutex release and base.OnExit run in finally so a faulting or hanging
        // dispose can never strand the single-instance mutex or skip WPF's own shutdown.
        try
        {
            mTrayIcon?.Dispose();
            if (mController is not null && !mController.DisposeAsync().AsTask().Wait(smExitDisposeTimeout))
            {
                HostLog.Warn("Server dispose timed out on exit.");
            }
        }
        catch (Exception ex)
        {
            HostLog.Error("Teardown faulted on exit.", ex);
        }
        finally
        {
            mInstanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
