// CommandInvokerTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Interaction;
using Payload;
using Tools;
using Protocol.Errors;
using Protocol.Wire;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class CommandInvokerTests
{
    [WpfFact]
    public void Execute_ButtonWithCommand_RunsWhenCanExecute()
    {
        bool ran = false;
        var cmd = new RelayTestCommand(_ => ran = true, _ => true);
        var button = new Button { Command = cmd };
        var invoker = new CommandInvoker();

        ExecuteCommandResponse r = invoker.Execute(button, path: null, parameter: null);

        Assert.True(r.CanExecute);
        Assert.True(r.Executed);
        Assert.False(r.Dispatched);
        Assert.True(ran);
    }

    [WpfFact]
    public void Execute_CanExecuteFalse_DoesNotRun_AndReportsBlocked()
    {
        var cmd = new RelayTestCommand(_ => throw new Xunit.Sdk.XunitException("should not run"), _ => false);
        var button = new Button { Command = cmd };
        var invoker = new CommandInvoker();

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(() => invoker.Execute(button, null, null));

        Assert.Equal(ErrorCode.CommandNotExecutable, ex.Code);
    }

    [WpfFact]
    public void Execute_RoutedCommand_RoutesToCommandTarget_NotFocusedElement()
    {
        // C8: RoutedCommand must route through (element as ICommandSource).CommandTarget, not
        // Keyboard.FocusedElement. RoutedCommand.Execute(parameter, target) raises the Executed event
        // starting at `target`, so `target`'s own CommandBinding handles it — no visual tree needed,
        // and no focus is ever set in this test, which is the point: routing does not depend on focus.
        var routed = new RoutedCommand("Test", typeof(Button));
        bool targetCanExecuteQueried = false;
        bool targetExecuted = false;
        var target = new Button();
        target.CommandBindings.Add(new CommandBinding(
            routed,
            (_, e) =>
            {
                targetExecuted = true;
                e.Handled = true;
            },
            (_, e) =>
            {
                targetCanExecuteQueried = true;
                e.CanExecute = true;
            }));
        var source = new Button { Command = routed, CommandTarget = target };
        var invoker = new CommandInvoker();

        ExecuteCommandResponse r = invoker.Execute(source, path: null, parameter: null);

        Assert.True(r.CanExecute);
        Assert.True(r.Executed);
        Assert.False(r.Dispatched);
        Assert.True(targetCanExecuteQueried);
        Assert.True(targetExecuted);
    }

    [WpfFact]
    public void Execute_RoutedCommand_CanExecuteFalse_DoesNotRun_AndReportsBlocked()
    {
        // C8: the routed branch's gate uses the 2-arg routed.CanExecute(parameter, target) overload,
        // a distinct code path from the plain ICommand.CanExecute(parameter) exercised by
        // Execute_CanExecuteFalse_DoesNotRun_AndReportsBlocked above. The Executed handler throws if
        // it ever runs, so a passing test proves both "did not execute" and "reported blocked".
        var routed = new RoutedCommand("Test", typeof(Button));
        var target = new Button();
        target.CommandBindings.Add(new CommandBinding(
            routed,
            (_, _) => throw new Xunit.Sdk.XunitException("should not run"),
            (_, e) => e.CanExecute = false));
        var source = new Button { Command = routed, CommandTarget = target };
        var invoker = new CommandInvoker();

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(() => invoker.Execute(source, null, null));

        Assert.Equal(ErrorCode.CommandNotExecutable, ex.Code);
    }

    [WpfFact]
    public void Execute_DataContextPath_ResolvesCommand_RunsWhenCanExecute()
    {
        bool ran = false;
        var cmd = new RelayTestCommand(_ => ran = true, _ => true);
        var viewModel = new TestViewModel { MyCommand = cmd };
        var element = new ContentControl { DataContext = viewModel };
        var invoker = new CommandInvoker();

        ExecuteCommandResponse r = invoker.Execute(element, path: nameof(TestViewModel.MyCommand), parameter: null);

        Assert.True(r.CanExecute);
        Assert.True(r.Executed);
        Assert.False(r.Dispatched);
        Assert.True(ran);
    }

    [WpfFact]
    public void Execute_DataContextPath_MissingSegment_ThrowsBindingPathError()
    {
        var viewModel = new TestViewModel();
        var element = new ContentControl { DataContext = viewModel };
        var invoker = new CommandInvoker();

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(
            () => invoker.Execute(element, path: "NoSuchProp", parameter: null));

        Assert.Equal(ErrorCode.BindingPathError, ex.Code);
    }

    [WpfFact]
    public void Execute_DataContextPath_NullFinalSegment_ThrowsBindingPathError()
    {
        // Regression: DataContextPath.TryWalk must treat a null FINAL-segment value the same as a
        // missing segment (BindingPathError), matching the original WalkPath contract (`?? throw`
        // on every segment, including the last). MyCommand is left unset (null) on purpose.
        var viewModel = new TestViewModel();
        var element = new ContentControl { DataContext = viewModel };
        var invoker = new CommandInvoker();

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(
            () => invoker.Execute(element, path: nameof(TestViewModel.MyCommand), parameter: null));

        Assert.Equal(ErrorCode.BindingPathError, ex.Code);
    }

    [WpfFact]
    public void Execute_DataContextPath_ResolvesNonCommand_ThrowsNotDrivable()
    {
        var viewModel = new TestViewModel();
        var element = new ContentControl { DataContext = viewModel };
        var invoker = new CommandInvoker();

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(
            () => invoker.Execute(element, path: nameof(TestViewModel.NotACommand), parameter: null));

        Assert.Equal(ErrorCode.NotDrivable, ex.Code);
    }

    [WpfFact]
    public void ExecuteCommand_WaitMode_RunsSynchronously_AndReportsExecuted()
    {
        bool ran = false;
        var cmd = new RelayTestCommand(_ => ran = true, _ => true);
        var button = new Button { Command = cmd };
        var registry = new ElementRegistry();
        int id = registry.GetOrAssign(button);
        var invoker = new CommandInvoker();
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));
        var handler = new ExecuteCommandToolHandler(registry, invoker, marshal);
        JsonElement arguments = ToArguments(new ExecuteCommandRequest(id, null, null, null));

        JsonElement result = handler.ExecuteAsync(arguments, default).GetAwaiter().GetResult();
        ExecuteCommandResponse? response = result.Deserialize<ExecuteCommandResponse>(WireSerializer.JsonOptions);

        Assert.NotNull(response);
        Assert.True(response!.Executed);
        Assert.True(response.CanExecute);
        Assert.False(response.Dispatched);
        Assert.True(ran);
    }

    [WpfFact]
    public void ExecuteCommand_PostMode_ReturnsImmediately_DispatchedTrue_CanExecuteTrue_ExecutedNull()
    {
        // Post mode validates before posting, so CanExecute is an observed fact (true) while Executed
        // stays null: the action itself has not run by the time the response is written.
        var cmd = new RelayTestCommand(_ => { }, _ => true);
        var button = new Button { Command = cmd };
        var registry = new ElementRegistry();
        int id = registry.GetOrAssign(button);
        var invoker = new CommandInvoker();
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));
        var handler = new ExecuteCommandToolHandler(registry, invoker, marshal);
        JsonElement arguments = ToArguments(new ExecuteCommandRequest(id, null, null, "post"));

        JsonElement result = handler.ExecuteAsync(arguments, default).GetAwaiter().GetResult();
        ExecuteCommandResponse? response = result.Deserialize<ExecuteCommandResponse>(WireSerializer.JsonOptions);

        Assert.NotNull(response);
        Assert.True(response!.Dispatched);
        Assert.Null(response.Executed);
        Assert.True(response.CanExecute);
    }

    [WpfFact]
    public void ExecuteCommand_PostMode_CanExecuteFalse_ReportsCommandNotExecutable_AndDoesNotRun()
    {
        // Raygun group 290708134232: CanExecute is a dispatcher-thread query, not a mutation, so post
        // mode can ask it before posting and refuse with the structured code instead of Dispatched=true.
        var cmd = new RelayTestCommand(_ => throw new Xunit.Sdk.XunitException("should not run"), _ => false);
        var button = new Button { Command = cmd };
        var registry = new ElementRegistry();
        int id = registry.GetOrAssign(button);
        var invoker = new CommandInvoker();
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));
        var handler = new ExecuteCommandToolHandler(registry, invoker, marshal);
        JsonElement arguments = ToArguments(new ExecuteCommandRequest(id, null, null, "post"));

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(
            () => handler.ExecuteAsync(arguments, default).GetAwaiter().GetResult());
        PumpDispatcher();

        Assert.Equal(ErrorCode.CommandNotExecutable, ex.Code);
    }

    [WpfFact]
    public void ExecuteCommand_PostMode_ExecuteThrows_LeavesNoUnobservedTaskFault()
    {
        // Guard for the fire path that survives validation: a command that passes CanExecute and then
        // throws inside Execute is the one failure post mode still cannot report. It must be traced
        // inside the payload, never left as an unobserved dispatcher-operation fault.
        string marker = $"execute-fault-{Guid.NewGuid():N}";
        using var probe = UnobservedFaultProbe.ForMessage(marker);
        var cmd = new RelayTestCommand(_ => throw new InvalidOperationException(marker), _ => true);
        var button = new Button { Command = cmd };
        var registry = new ElementRegistry();
        int id = registry.GetOrAssign(button);
        var invoker = new CommandInvoker();
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));
        var handler = new ExecuteCommandToolHandler(registry, invoker, marshal);
        JsonElement arguments = ToArguments(new ExecuteCommandRequest(id, null, null, "post"));

        JsonElement result = handler.ExecuteAsync(arguments, default).GetAwaiter().GetResult();
        PumpDispatcher();
        ExecuteCommandResponse? response = result.Deserialize<ExecuteCommandResponse>(WireSerializer.JsonOptions);

        Assert.NotNull(response);
        Assert.True(response!.Dispatched);
        Assert.False(
            probe.CollectAndCheckFired(),
            "The posted command's fault reached TaskScheduler.UnobservedTaskException in the host process.");
    }

    /// <summary>Pumps the current dispatcher until everything queued at or above Input priority has run.</summary>
    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    [WpfFact]
    public void ExecuteCommand_WaitMode_FromForeignThread_CanExecuteFalse_SurfacesCommandNotExecutable()
    {
        // Raygun group 290708134232: CommandInvoker.Execute threw CommandNotExecutable inside the
        // dispatcher delegate. Through the real pipe path (a foreign thread, as PipeServer's request
        // loop is) the handler must surface that SnoopMcpException itself — not an AggregateException
        // wrapper that PipeServer's catch-all would report as ErrorCode.Unknown.
        var cmd = new RelayTestCommand(_ => throw new Xunit.Sdk.XunitException("should not run"), _ => false);
        var button = new Button { Command = cmd };
        var registry = new ElementRegistry();
        int id = registry.GetOrAssign(button);
        var invoker = new CommandInvoker();
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));
        var handler = new ExecuteCommandToolHandler(registry, invoker, marshal);
        JsonElement arguments = ToArguments(new ExecuteCommandRequest(id, null, null, null));
        Exception? caught = null;
        var worker = new Thread(() =>
        {
            try
            {
                handler.ExecuteAsync(arguments, default).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        worker.Start();

        var frame = new DispatcherFrame();
        var pump = new Thread(() =>
        {
            worker.Join();
            frame.Continue = false;
        });
        pump.Start();
        Dispatcher.PushFrame(frame);
        pump.Join();

        SnoopMcpException mcp = Assert.IsType<SnoopMcpException>(caught);
        Assert.Equal(ErrorCode.CommandNotExecutable, mcp.Code);
    }

    private static JsonElement ToArguments(ExecuteCommandRequest request)
    {
        string json = JsonSerializer.Serialize(request, WireSerializer.JsonOptions);
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private sealed class RelayTestCommand : ICommand
    {
        private readonly Action<object?> mExecute;
        private readonly Predicate<object?> mCanExecute;

        public RelayTestCommand(Action<object?> execute, Predicate<object?> canExecute)
        {
            mExecute = execute;
            mCanExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => mCanExecute(parameter);

        public void Execute(object? parameter) => mExecute(parameter);
    }

    /// <summary>Tiny DataContext view model for the dotted-path Resolve tests.</summary>
    private sealed class TestViewModel
    {
        public ICommand? MyCommand { get; set; }

        public object NotACommand { get; } = "not a command";
    }
}
