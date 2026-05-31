// RelayCommandOfT.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using System.Windows.Input;

/// <summary>
/// An <see cref="ICommand"/> that forwards a typed value-type parameter (e.g. the chosen agent) to its
/// callback. A missing/absent <c>CommandParameter</c> — as on an "All" menu item — arrives as
/// <see langword="null"/>. The command is always executable.
/// </summary>
/// <typeparam name="T">The value type carried by the command parameter (e.g. an enum).</typeparam>
public sealed class RelayCommand<T> : ICommand
    where T : struct
{
    private readonly Action<T?> mExecute;

    /// <summary>Creates the command.</summary>
    /// <param name="execute">The callback invoked with the (possibly null) typed parameter.</param>
    public RelayCommand(Action<T?> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        mExecute = execute;
    }

    /// <inheritdoc />
    /// <remarks>The command is always enabled, so the event is never raised.</remarks>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc />
    public void Execute(object? parameter) => mExecute(parameter as T?);
}
