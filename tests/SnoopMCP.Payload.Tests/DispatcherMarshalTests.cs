// DispatcherMarshalTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.Threading;
using System.Windows.Threading;
using SnoopMCP.Payload;
using SnoopMCP.Protocol.Errors;
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
}
