// RelayCommand.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SampleWpfApp.ViewModels;

using System.Windows.Input;

public sealed class RelayCommand : ICommand
{
    private readonly Action mExecute;
    private readonly Func<bool>? mCanExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        mExecute = execute;
        mCanExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => mCanExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => mExecute();
}
