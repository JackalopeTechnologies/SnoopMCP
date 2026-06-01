// TrayViewModel.cs
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

using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using SnoopMCP.ClientIntegration;

#endregion

namespace SnoopMCP.Host;

/// <summary>
///     DataContext for the tray <c>TaskbarIcon</c> declared in App.xaml. Surfaces the status-dot icon and
///     tooltip for the current <see cref="ServerState" />, the Start / Stop / Exit context-menu commands
///     (driven from the <see cref="ServerController" />), and the Install-into / Uninstall-from / Status
///     commands that register the SnoopMCP endpoint with the supported AI agents via
///     <see cref="ClientRegistration" />. A per-agent install/uninstall command takes the chosen
///     <see cref="McpClient" /> as its parameter; a null parameter (the "All" item) targets every detected
///     agent. Install/uninstall results are surfaced through the notify callback (a tray balloon); Status is
///     shown through the dialog callback (an owned modal), matching the per-agent layout users expect.
/// </summary>
public sealed class TrayViewModel : INotifyPropertyChanged
{
    /// <summary>Creates the view model.</summary>
    /// <param name="controller">The server controller the Start/Stop menu drives.</param>
    /// <param name="exit">Callback that shuts the whole application down.</param>
    /// <param name="notify">Callback that surfaces a transient (title, message) tray notification.</param>
    /// <param name="showDialog">Callback that surfaces a modal (title, message) dialog for Status.</param>
    public TrayViewModel(
        ServerController controller,
        Action exit,
        Action<string, string> notify,
        Action<string, string> showDialog)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(exit);
        ArgumentNullException.ThrowIfNull(notify);
        ArgumentNullException.ThrowIfNull(showDialog);
        mController = controller;
        mNotify = notify;
        mShowDialog = showDialog;

        mStartCommand = new RelayCommand(() => _ = mController.StartAsync(),
            () => ServerStateInfo.CanStart(mController.State));
        mStopCommand = new RelayCommand(() => _ = mController.StopAsync(),
            () => ServerStateInfo.CanStop(mController.State));
        mExitCommand = new RelayCommand(exit);
        mInstallCommand = new RelayCommand<McpClient>(InstallExecute);
        mUninstallCommand = new RelayCommand<McpClient>(UninstallExecute);
        mStatusCommand = new RelayCommand(StatusExecute);

        mController.StateChanged += (_, _) => OnStateChanged();
    }

    /// <summary>Gets the tray icon (logo + status dot) for the current server state.</summary>
    public ImageSource IconSource => TrayIconRenderer.ForState(mController.State);

    /// <summary>Gets the tray tooltip text for the current server state.</summary>
    public string ToolTipText => ServerStateInfo.Tooltip(mController.State);

    /// <summary>Gets the command that starts the MCP server.</summary>
    public ICommand StartCommand => mStartCommand;

    /// <summary>Gets the command that stops the MCP server.</summary>
    public ICommand StopCommand => mStopCommand;

    /// <summary>Gets the command that exits the application.</summary>
    public ICommand ExitCommand => mExitCommand;

    /// <summary>Gets the command that installs SnoopMCP into one agent (parameter) or all detected (null).</summary>
    public ICommand InstallCommand => mInstallCommand;

    /// <summary>Gets the command that removes SnoopMCP from one agent (parameter) or all detected (null).</summary>
    public ICommand UninstallCommand => mUninstallCommand;

    /// <summary>Gets the command that reports SnoopMCP's registration status across all agents.</summary>
    public ICommand StatusCommand => mStatusCommand;

    private readonly ServerController mController;
    private readonly RelayCommand mExitCommand;
    private readonly RelayCommand<McpClient> mInstallCommand;
    private readonly Action<string, string> mNotify;
    private readonly Action<string, string> mShowDialog;
    private readonly RelayCommand mStartCommand;
    private readonly RelayCommand mStatusCommand;
    private readonly RelayCommand mStopCommand;
    private readonly RelayCommand<McpClient> mUninstallCommand;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void InstallExecute(McpClient? client)
    {
        IReadOnlyList<IClientWriter> writers = WritersFor(client);
        IReadOnlyList<RegisterResult> results = ClientRegistration.Register(writers, McpEndpoint.Default);
        mNotify(InstallTitle, Summarize(results.Select(r => r.Message)));
    }

    private void UninstallExecute(McpClient? client)
    {
        IReadOnlyList<IClientWriter> writers = WritersFor(client);
        IReadOnlyList<UnregisterResult> results = ClientRegistration.Unregister(writers);
        mNotify(UninstallTitle, Summarize(results.Select(r => r.Message)));
    }

    private void StatusExecute()
    {
        IReadOnlyList<IClientWriter> writers = ClientRegistration.CreateWriters(ClientRegistration.AllClients);
        var report = string.Join(Environment.NewLine, writers.Select(StatusLine));
        mShowDialog(StatusTitle, report);
    }

    private static string StatusLine(IClientWriter writer)
    {
        var presence = writer.IsDetected() ? InstalledMark : AbsentMark;
        var registration = writer.GetStatus().IsRegistered ? RegisteredMark : NotRegisteredMark;
        return $"{writer.ClientName}: {presence}, {registration}";
    }

    private static IReadOnlyList<IClientWriter> WritersFor(McpClient? client)
    {
        return client is McpClient chosen
            ? new[] { ClientRegistration.CreateWriter(chosen) }
            : ClientRegistration.DetectedWriters();
    }

    private static string Summarize(IEnumerable<string> messages)
    {
        var lines = messages.ToList();
        return lines.Count == 0 ? NoAgentsText : string.Join(Environment.NewLine, lines);
    }

    private void OnStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToolTipText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconSource)));
        mStartCommand.RaiseCanExecuteChanged();
        mStopCommand.RaiseCanExecuteChanged();
    }

    private const string InstallTitle = "SnoopMCP — install";
    private const string UninstallTitle = "SnoopMCP — uninstall";
    private const string StatusTitle = "SnoopMCP — agent status";
    private const string NoAgentsText = "No matching AI agents were detected.";
    private const string InstalledMark = "installed";
    private const string AbsentMark = "absent";
    private const string RegisteredMark = "registered";
    private const string NotRegisteredMark = "not registered";
}
