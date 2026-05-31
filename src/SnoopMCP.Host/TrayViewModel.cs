// TrayViewModel.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using System.ComponentModel;
using System.Windows.Input;
using SnoopMCP.ClientIntegration;

/// <summary>
/// DataContext for the tray <c>TaskbarIcon</c> declared in App.xaml. Surfaces the tooltip text, the
/// Start / Stop / Exit context-menu commands (driven from the <see cref="ServerController"/>), and the
/// Install-into / Uninstall-from / Status commands that register the SnoopMCP endpoint with the
/// supported AI agents via <see cref="ClientRegistration"/>. A per-agent install/uninstall command takes
/// the chosen <see cref="McpClient"/> as its parameter; a null parameter (the "All" item) targets every
/// detected agent. Results are surfaced through the supplied notify callback (a tray balloon).
/// </summary>
public sealed class TrayViewModel : INotifyPropertyChanged
{
    private const string InstallTitle = "SnoopMCP — install";
    private const string UninstallTitle = "SnoopMCP — uninstall";
    private const string StatusTitle = "SnoopMCP — status";
    private const string NoAgentsText = "No matching AI agents were detected.";

    private readonly ServerController mController;
    private readonly Action<string, string> mNotify;
    private readonly RelayCommand mStartCommand;
    private readonly RelayCommand mStopCommand;
    private readonly RelayCommand mExitCommand;
    private readonly RelayCommand<McpClient> mInstallCommand;
    private readonly RelayCommand<McpClient> mUninstallCommand;
    private readonly RelayCommand mStatusCommand;

    /// <summary>Creates the view model.</summary>
    /// <param name="controller">The server controller the Start/Stop menu drives.</param>
    /// <param name="exit">Callback that shuts the whole application down.</param>
    /// <param name="notify">Callback that surfaces a (title, message) notification to the user.</param>
    public TrayViewModel(ServerController controller, Action exit, Action<string, string> notify)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(exit);
        ArgumentNullException.ThrowIfNull(notify);
        mController = controller;
        mNotify = notify;

        mStartCommand = new RelayCommand(() => _ = mController.StartAsync(), () => ServerStateInfo.CanStart(mController.State));
        mStopCommand = new RelayCommand(() => _ = mController.StopAsync(), () => ServerStateInfo.CanStop(mController.State));
        mExitCommand = new RelayCommand(exit);
        mInstallCommand = new RelayCommand<McpClient>(InstallExecute);
        mUninstallCommand = new RelayCommand<McpClient>(UninstallExecute);
        mStatusCommand = new RelayCommand(StatusExecute);

        mController.StateChanged += (_, _) => OnStateChanged();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

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
        IReadOnlyList<StatusResult> results = ClientRegistration.GetStatus(writers);
        mNotify(StatusTitle, Summarize(results.Select(r => r.Message)));
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
        mStartCommand.RaiseCanExecuteChanged();
        mStopCommand.RaiseCanExecuteChanged();
    }
}
