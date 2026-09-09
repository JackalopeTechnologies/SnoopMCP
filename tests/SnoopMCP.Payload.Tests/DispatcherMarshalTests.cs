// DispatcherMarshalTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Threading;
using System.Windows.Threading;
using Payload;
using Protocol.Errors;
using Xunit;

public sealed class DispatcherMarshalTests
{
    [StaFact]
    public void Invoke_FastFunction_ReturnsResult()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromSeconds(2));

        int result = marshal.Invoke(() => 42 + 8, CancellationToken.None);

        Assert.Equal(50, result);
    }

    [StaFact]
    public void Invoke_OnSameThread_RunsInline()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromSeconds(2));

        int dispatcherThreadId = Environment.CurrentManagedThreadId;
        int observedThreadId = marshal.Invoke(() => Environment.CurrentManagedThreadId, CancellationToken.None);

        Assert.Equal(dispatcherThreadId, observedThreadId);
    }

    [StaFact]
    public void Invoke_FromForeignThread_RunsOnDispatcherThread()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromSeconds(2));
        int dispatcherThreadId = Environment.CurrentManagedThreadId;
        int observed = 0;

        var worker = new Thread(() =>
        {
            observed = marshal.Invoke(() => Environment.CurrentManagedThreadId, CancellationToken.None);
        });
        worker.Start();

        DispatcherFrame frame = new();
        var pump = new Thread(() =>
        {
            worker.Join();
            frame.Continue = false;
        });
        pump.Start();
        Dispatcher.PushFrame(frame);
        pump.Join();

        Assert.Equal(dispatcherThreadId, observed);
    }

    [StaFact]
    public void Invoke_ExceedingTimeout_ThrowsDispatcherTimeout()
    {
        var shortTimeout = TimeSpan.FromMilliseconds(100);
        Dispatcher? workerDispatcher = null;
        var ready = new ManualResetEventSlim();
        var workerThread = new Thread(() =>
        {
            workerDispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        workerThread.SetApartmentState(ApartmentState.STA);
        workerThread.IsBackground = true;
        workerThread.Start();
        ready.Wait();

        var slowMarshal = new DispatcherMarshal(workerDispatcher!, shortTimeout);

        workerDispatcher!.BeginInvoke(() => Thread.Sleep(TimeSpan.FromSeconds(1)));

        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(
            () => slowMarshal.Invoke(() => 1, CancellationToken.None));
        Assert.Equal(ErrorCode.DispatcherTimeout, ex.Code);

        workerDispatcher.InvokeShutdown();
        workerThread.Join();
    }

    [StaFact]
    public void Invoke_OnShutDownDispatcher_ThrowsInvalidOperation()
    {
        Dispatcher? workerDispatcher = null;
        var ready = new ManualResetEventSlim();
        var workerThread = new Thread(() =>
        {
            workerDispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        workerThread.SetApartmentState(ApartmentState.STA);
        workerThread.IsBackground = true;
        workerThread.Start();
        ready.Wait();

        workerDispatcher!.InvokeShutdown();
        workerThread.Join();

        var marshal = new DispatcherMarshal(workerDispatcher, TimeSpan.FromSeconds(1));

        Assert.Throws<InvalidOperationException>(
            () => marshal.Invoke(() => 1, CancellationToken.None));
    }

    [WpfFact]
    public void Invoke_WorkThrowsSnoopMcpException_RethrowsItUnwrapped()
    {
        // Same contract as InvokeMutating: the caller sees the original exception, not Task.Wait's
        // AggregateException wrapper, so PipeServer can map it to its structured code.
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));
        Exception? caught = null;
        var worker = new Thread(() =>
        {
            try
            {
                marshal.Invoke<object?>(
                    static () => throw new SnoopMcpException(ErrorCode.ElementExpired, "Element is not alive."),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        worker.Start();
        PumpUntilJoined(worker);

        SnoopMcpException mcp = Assert.IsType<SnoopMcpException>(caught);
        Assert.Equal(ErrorCode.ElementExpired, mcp.Code);
    }

    [WpfFact]
    public void Invoke_TimedOutWorkThatLaterThrows_LeavesNoUnobservedTaskFault()
    {
        // On timeout the read path calls Abort(), which cannot stop work that has already started.
        // If that work then throws, the abandoned operation's Task faults with nobody left to observe
        // it — the same defect class as the ActionPending path, closed with the same fix.
        string marker = $"timeout-fault-{Guid.NewGuid():N}";
        using var probe = UnobservedFaultProbe.ForMessage(marker);
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromMilliseconds(100));
        var workFinished = new ManualResetEventSlim();
        Exception? caught = null;
        var worker = new Thread(() =>
        {
            try
            {
                marshal.Invoke<object?>(
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
                    CancellationToken.None);
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
        Assert.Equal(ErrorCode.DispatcherTimeout, mcp.Code);
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
