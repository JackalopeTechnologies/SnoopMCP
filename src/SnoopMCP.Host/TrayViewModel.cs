// TrayViewModel.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using System.ComponentModel;
using System.Windows.Input;

/// <summary>
/// DataContext for the tray <c>TaskbarIcon</c> declared in App.xaml. Surfaces the tooltip text and the
/// Start / Stop / Exit context-menu commands, driving them from the <see cref="ServerController"/>.
/// Start/Stop availability flows through each command's CanExecute, which is what enables/disables the
/// bound menu items.
/// </summary>
public sealed class TrayViewModel : INotifyPropertyChanged
{
    private readonly ServerController mController;
    private readonly RelayCommand mStartCommand;
    private readonly RelayCommand mStopCommand;
    private readonly RelayCommand mExitCommand;

    /// <summary>Creates the view model.</summary>
    /// <param name="controller">The server controller the menu drives.</param>
    /// <param name="exit">Callback that shuts the whole application down.</param>
    public TrayViewModel(ServerController controller, Action exit)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(exit);
        mController = controller;

        mStartCommand = new RelayCommand(() => _ = mController.StartAsync(), () => ServerStateInfo.CanStart(mController.State));
        mStopCommand = new RelayCommand(() => _ = mController.StopAsync(), () => ServerStateInfo.CanStop(mController.State));
        mExitCommand = new RelayCommand(exit);

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

    private void OnStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToolTipText)));
        mStartCommand.RaiseCanExecuteChanged();
        mStopCommand.RaiseCanExecuteChanged();
    }
}
