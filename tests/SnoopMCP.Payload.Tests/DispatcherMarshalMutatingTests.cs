// DispatcherMarshalMutatingTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Threading;
using System.Windows.Threading;
using Payload;
using Protocol.Errors;
using Xunit;

public sealed class DispatcherMarshalMutatingTests
{
    [WpfFact]
    public void InvokeMutating_ReturnsResult_WhenFast()
    {
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));

        int result = marshal.InvokeMutating(() => 42, default);

        Assert.Equal(42, result);
    }

    [WpfFact]
    public void InvokeMutating_Timeout_ThrowsActionPending_NotDispatcherTimeout()
    {
        // A foreign-thread call whose work blocks past the timeout. Run the marshal from a worker
        // thread targeting this test's dispatcher, then pump briefly so the queued work starts.
        var dispatcher = Dispatcher.CurrentDispatcher;
        var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromMilliseconds(100));
        Exception? caught = null;
        Func<object?> slowWork = static () =>
        {
            Thread.Sleep(1000);
            return null;
        };
        var worker = new Thread(() =>
        {
            try
            {
                marshal.InvokeMutating(slowWork, default);
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        worker.Start();

        // Pump the dispatcher so the queued work starts, then let the timeout fire.
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(() =>
        {
            Thread.Sleep(400);
            frame.Continue = false;
        });
        Dispatcher.PushFrame(frame);
        worker.Join(2000);

        SnoopMcpException mcp = Assert.IsType<SnoopMcpException>(caught);
        Assert.Equal(ErrorCode.ActionPending, mcp.Code);
    }

    [WpfFact]
    public void Post_ReturnsImmediately_AndWorkRunsOnDispatcherThread()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromSeconds(2));
        var ran = new ManualResetEventSlim();
        int observedThreadId = -1;

        marshal.Post(() =>
        {
            observedThreadId = Environment.CurrentManagedThreadId;
            ran.Set();
        });

        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(() =>
        {
            ran.Wait(TimeSpan.FromSeconds(2));
            frame.Continue = false;
        });
        Dispatcher.PushFrame(frame);

        Assert.True(ran.IsSet);
        Assert.Equal(Environment.CurrentManagedThreadId, observedThreadId);
    }

    [WpfFact]
    public void InvokeMutating_WorkThrowsSnoopMcpException_RethrowsItUnwrapped()
    {
        // A fault inside the dispatched work must reach the caller as the original SnoopMcpException
        // so PipeServer maps it to its structured code. Task.Wait's AggregateException wrapper would
        // fall through to the catch-all and reach the client as ErrorCode.Unknown instead.
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));
        Exception? caught = null;
        var worker = new Thread(() =>
        {
            try
            {
                marshal.InvokeMutating<object?>(
                    static () => throw new SnoopMcpException(ErrorCode.CommandNotExecutable, "CanExecute returned false."),
                    default);
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        worker.Start();
        PumpUntilJoined(worker);

        SnoopMcpException mcp = Assert.IsType<SnoopMcpException>(caught);
        Assert.Equal(ErrorCode.CommandNotExecutable, mcp.Code);
    }

    [WpfFact]
    public void Post_WorkThrows_LeavesNoUnobservedTaskFault()
    {
        // Raygun handoff (2026-09-09): every peerInvoke/executeCommand fault posted fire-and-forget
        // surfaced in the HOST application's crash reporter as an UnobservedTaskException, because the
        // posted DispatcherOperation's faulted Task was discarded and never observed.
        string marker = $"post-fault-{Guid.NewGuid():N}";
        using var probe = UnobservedFaultProbe.ForMessage(marker);
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));
        var ran = new ManualResetEventSlim();

        marshal.Post(() =>
        {
            ran.Set();
            throw new InvalidOperationException(marker);
        });
        PumpUntilSet(ran);

        Assert.False(
            probe.CollectAndCheckFired(),
            "The posted work's fault reached TaskScheduler.UnobservedTaskException in the host process.");
    }

    [WpfFact]
    public void InvokeMutating_TimedOutWorkThatLaterThrows_LeavesNoUnobservedTaskFault()
    {
        // The ActionPending path abandons the operation after the wait expires. If the still-running
        // work then throws, the abandoned operation's Task faults with nobody left to observe it.
        string marker = $"pending-fault-{Guid.NewGuid():N}";
        using var probe = UnobservedFaultProbe.ForMessage(marker);
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromMilliseconds(100));
        var workFinished = new ManualResetEventSlim();
        Exception? caught = null;
        var worker = new Thread(() =>
        {
            try
            {
                marshal.InvokeMutating<object?>(
                    () =>
                    {
                        try
                        {
                            Thread.Sleep(400);
                            throw new InvalidOperationException(marker);
                        }
                        finally
                        {
                            workFinished.Set();
                        }
                    },
                    default);
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        worker.Start();
        PumpUntilSet(workFinished);
        Assert.True(worker.Join(TimeSpan.FromSeconds(2)));

        Assert.True(workFinished.IsSet, "The dispatched work never ran; the timeout raced ahead of the pump.");
        SnoopMcpException mcp = Assert.IsType<SnoopMcpException>(caught);
        Assert.Equal(ErrorCode.ActionPending, mcp.Code);
        Assert.False(
            probe.CollectAndCheckFired(),
            "The abandoned operation's fault reached TaskScheduler.UnobservedTaskException in the host process.");
    }

    /// <summary>Pumps the current dispatcher until <paramref name="worker"/> has exited.</summary>
    private static void PumpUntilJoined(Thread worker)
    {
        var frame = new DispatcherFrame();
        var pump = new Thread(() =>
        {
            worker.Join();
            frame.Continue = false;
        });
        pump.Start();
        Dispatcher.PushFrame(frame);
        pump.Join();
    }

    /// <summary>Pumps the current dispatcher until <paramref name="signal"/> is set (or 2 s elapse).</summary>
    private static void PumpUntilSet(ManualResetEventSlim signal)
    {
        var frame = new DispatcherFrame();
        var pump = new Thread(() =>
        {
            signal.Wait(TimeSpan.FromSeconds(2));
            frame.Continue = false;
        });
        pump.Start();
        Dispatcher.PushFrame(frame);
        pump.Join();
    }
}
