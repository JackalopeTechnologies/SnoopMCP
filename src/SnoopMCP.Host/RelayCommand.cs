// RelayCommand.cs
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
