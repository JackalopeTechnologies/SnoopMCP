// RelayCommand.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host;

using System.Windows.Input;

/// <summary>
/// Minimal <see cref="ICommand"/> backing the tray menu items. The view model raises
/// <see cref="CanExecuteChanged"/> explicitly when server state changes, which re-evaluates each bound
/// <c>MenuItem.IsEnabled</c>.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action mExecute;
    private readonly Func<bool>? mCanExecute;

    /// <summary>Creates a command.</summary>
    /// <param name="execute">The action to run on execute.</param>
    /// <param name="canExecute">Optional predicate gating execution; always executable when null.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        mExecute = execute;
        mCanExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => mCanExecute?.Invoke() ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter) => mExecute();

    /// <summary>Raises <see cref="CanExecuteChanged"/> so bound controls re-query <see cref="CanExecute"/>.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
