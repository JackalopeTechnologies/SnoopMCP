// App.xaml.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host;

using System.Windows;
using System.IO;
using H.NotifyIcon;
using Automation;
using ClientIntegration;

/// <summary>
/// WPF application root for the SnoopMCP tray app. Enforces single-instance, starts the in-process
/// MCP server, realises the tray icon (declared in App.xaml), and tears everything down on Exit or
/// Windows session end.
/// </summary>
// CA1001 suppression: the disposable fields are released in the WPF OnExit override, which is the
// correct teardown hook for the Application singleton. WPF never calls Dispose() on the Application,
// so implementing IDisposable here would be dead code and would not run on shutdown.
#pragma warning disable CA1001
public partial class App
#pragma warning restore CA1001
{
    private const string SingleInstanceMutexName = @"Local\SnoopMCP.Host";
    private const string TrayIconResourceKey = "TrayIcon";
    private const string AppTitle = "SnoopMCP";
    private const string AlreadyRunningMessage = "SnoopMCP is already running — check the system tray.";
    private const string RecoveryTitle = "SnoopMCP — WPF inspection restored";
    private const string RecoveryMessage = "The previous run ended unexpectedly. Open SnoopMCP - diagnostic logs from Start for details. Refresh your coding assistant's connection if needed.";

    private static readonly TimeSpan smExitDisposeTimeout = TimeSpan.FromSeconds(5);

    private Mutex? mInstanceMutex;
    private ServerController? mController;
    private TaskbarIcon? mTrayIcon;
    private HostRunRecord? mRunRecord;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        if (Mutex.TryOpenExisting(SingleInstanceMutexName, out Mutex? existing))
        {
            existing.Dispose();
            MessageBox.Show(AlreadyRunningMessage, AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
        }
        else
        {
            StartOrRelaunch(e);
        }
    }

    private void StartOrRelaunch(StartupEventArgs e)
    {
        // Relaunch before acquiring the single-instance mutex: the elevated child must be able to acquire it.
        bool relaunch = !ElevationInfo.IsElevated() && ElevationInfo.CanElevate()
            && AutostartTask.Exists() && AutostartTask.RunNow();
        if (relaunch)
        {
            HostLog.Info("Relaunching elevated via the autostart task.");
            Shutdown();
        }
        else
        {
            mInstanceMutex = new Mutex(initiallyOwned: false, SingleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                HostLog.Info("Another SnoopMCP instance is already running; exiting.");
                MessageBox.Show(AlreadyRunningMessage, AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
            }
            else
            {
                StartAtCurrentIntegrity(e);
            }
        }
    }

    // Called only by the instance that owns the single-instance handle.
    private void StartAtCurrentIntegrity(StartupEventArgs e)
    {
        mController = new ServerController(e.Args);
        mTrayIcon = (TaskbarIcon?) FindResource(TrayIconResourceKey) ?? throw new InvalidOperationException("Tray icon resource not found.");
        mTrayIcon.DataContext = new TrayViewModel(mController, ExitFromTray,
            (title, message) => mTrayIcon.ShowNotification(title, message),
            ShowOwnedDialog,
            InteractionGate.ForCurrentUser(),
            ElevationInfo.IsElevated());
        mTrayIcon.ForceCreate();
        RecordStartup();
        SessionEnding += OnSessionEnding;
        _ = mController.StartAsync();
        HostLog.Info("SnoopMCP host started.");
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        HostLog.Info($"Windows session ending: {e.ReasonSessionEnding}.");
        Shutdown();
    }

    private void ExitFromTray()
    {
        HostLog.Info("Exit requested from the SnoopMCP tray menu.");
        Shutdown();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        HostLog.Error($"Unhandled host exception; terminating={e.IsTerminating}.", e.ExceptionObject as Exception);
    }

    private void RecordStartup()
    {
        try
        {
            mRunRecord = HostRunRecord.ForCurrentUser();
            if (mRunRecord.Begin())
            {
                HostLog.Info("Previous host run ended without a clean shutdown. Cause unknown; check crash/task history.");
                mTrayIcon?.ShowNotification(RecoveryTitle, RecoveryMessage);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HostLog.Error("Could not record host startup.", ex);
        }
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
            HostLog.Info($"Host shutdown: exit code {e.ApplicationExitCode}.");
            mTrayIcon?.Dispose();
            bool disposed = mController is null || mController.DisposeAsync().AsTask().Wait(smExitDisposeTimeout);
            if (!disposed)
            {
                HostLog.Warn("Server dispose timed out on exit.");
            }
            if (disposed && e.ApplicationExitCode == 0)
            {
                mRunRecord?.Complete();
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
