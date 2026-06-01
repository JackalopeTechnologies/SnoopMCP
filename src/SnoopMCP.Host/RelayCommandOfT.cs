// RelayCommandOfT.cs
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

using System.Windows.Input;

#endregion

namespace SnoopMCP.Host;

/// <summary>
///     An <see cref="ICommand" /> that forwards a typed value-type parameter (e.g. the chosen agent) to its
///     callback. A missing/absent <c>CommandParameter</c> — as on an "All" menu item — arrives as
///     <see langword="null" />. The command is always executable.
/// </summary>
/// <typeparam name="T">The value type carried by the command parameter (e.g. an enum).</typeparam>
public sealed class RelayCommand<T> : ICommand
    where T : struct
{
    /// <summary>Creates the command.</summary>
    /// <param name="execute">The callback invoked with the (possibly null) typed parameter.</param>
    public RelayCommand(Action<T?> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        mExecute = execute;
    }

    private readonly Action<T?> mExecute;

    /// <inheritdoc />
    /// <remarks>The command is always enabled, so the event is never raised.</remarks>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter)
    {
        return true;
    }

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        mExecute(parameter as T?);
    }
}
