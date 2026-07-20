// CommandInvoker.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Interaction;

using System.Windows;
using System.Windows.Input;
using Protocol.Errors;
using SnoopMCP.Protocol.Tools;

/// <summary>
/// Resolves and executes the <see cref="ICommand"/> bound to an element — from the element's own
/// <c>Command</c> (any <see cref="ICommandSource"/>) or from a dotted DataContext path — gating on
/// <see cref="ICommand.CanExecute"/>. Runs on the UI thread.
/// </summary>
public sealed class CommandInvoker
{
    /// <summary>
    /// Executes the resolved command, or throws with a structured code when it cannot run.
    /// </summary>
    /// <param name="element">
    /// The element whose bound command is executed. Also supplies the routed-command target: an
    /// <see cref="ICommandSource"/>'s own <see cref="ICommandSource.CommandTarget"/> when set, else
    /// the element itself.
    /// </param>
    /// <param name="path">
    /// Optional dotted DataContext path to an <see cref="ICommand"/>; when null or empty, the
    /// element's own <c>Command</c> (via <see cref="ICommandSource"/>) is used instead.
    /// </param>
    /// <param name="parameter">
    /// Optional command parameter; when null, the element's bound <c>CommandParameter</c> is used
    /// (path-resolved commands have no bound parameter, so this is null unless supplied).
    /// </param>
    /// <returns>A response reporting the observed execution outcome.</returns>
    /// <remarks>
    /// CA1822 disabled: instance method by design so callers (e.g. <c>ExecuteCommandToolHandler</c>)
    /// hold and inject a <see cref="CommandInvoker"/> like the other driving-layer collaborators,
    /// consistent with <c>AutomationPeerDriver</c>. No instance state today; may gain some in a
    /// follow-up phase without an API-shape change.
    /// </remarks>
#pragma warning disable CA1822
    public ExecuteCommandResponse Execute(DependencyObject element, string? path, string? parameter)
#pragma warning restore CA1822
    {
        ArgumentNullException.ThrowIfNull(element);

        (ICommand command, object? boundParameter) = Resolve(element, path);
        object? commandParameter = parameter ?? boundParameter;

        // C8: route through the element's own CommandTarget (falling back to the element itself),
        // NOT Keyboard.FocusedElement. A RoutedCommand carries no execution logic of its own — it
        // raises the Executed/CanExecute routed events starting at the target, and whichever
        // CommandBinding along that route handles them is what actually runs. Driving via focus
        // would silently execute the wrong binding (or none) whenever the target differs from
        // whatever currently has keyboard focus, which is the common case for a driven (non-focused)
        // element.
        IInputElement? target = (element as ICommandSource)?.CommandTarget ?? element as IInputElement;
        if (command is RoutedCommand routed)
        {
            if (!routed.CanExecute(commandParameter, target))
            {
                throw new SnoopMcpException(ErrorCode.CommandNotExecutable, "The routed command's CanExecute returned false.");
            }
            routed.Execute(commandParameter, target);
        }
        else
        {
            if (!command.CanExecute(commandParameter))
            {
                throw new SnoopMcpException(ErrorCode.CommandNotExecutable, "The command's CanExecute returned false.");
            }
            command.Execute(commandParameter);
        }
        return new ExecuteCommandResponse(Executed: true, CanExecute: true, Dispatched: false);
    }

    private static (ICommand Command, object? Parameter) Resolve(DependencyObject element, string? path)
    {
        (ICommand Command, object? Parameter) result = string.IsNullOrEmpty(path)
            ? ResolveFromSource(element)
            : ResolveFromDataContext(element, path);
        return result;
    }

    private static (ICommand Command, object? Parameter) ResolveFromSource(DependencyObject element)
    {
        (ICommand Command, object? Parameter) result;
        if (element is ICommandSource { Command: { } cmd } source)
        {
            result = (cmd, source.CommandParameter);
        }
        else
        {
            throw new SnoopMcpException(ErrorCode.NotDrivable, "Element exposes no Command; supply a DataContext path.");
        }
        return result;
    }

    private static (ICommand Command, object? Parameter) ResolveFromDataContext(DependencyObject element, string path)
    {
        object context = (element as FrameworkElement)?.DataContext
            ?? throw new SnoopMcpException(ErrorCode.NotDrivable, "Element has no DataContext to resolve the command path.");
        if (!DataContextPath.TryWalk(context, path, out object? resolved))
        {
            throw new SnoopMcpException(ErrorCode.BindingPathError, $"DataContext path '{path}' could not be resolved.");
        }

        (ICommand Command, object? Parameter) result = resolved is ICommand pathCommand
            ? (pathCommand, null)
            : throw new SnoopMcpException(ErrorCode.NotDrivable, $"DataContext path '{path}' is not an ICommand.");
        return result;
    }
}
