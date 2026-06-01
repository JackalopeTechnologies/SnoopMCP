// DispatcherMarshalTests.cs
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

using System.Windows.Threading;
using SnoopMCP.Protocol.Errors;
using Xunit;

#endregion

namespace SnoopMCP.Payload.Tests;

public sealed class DispatcherMarshalTests
{
    [StaFact]
    public void Invoke_FastFunction_ReturnsResult()
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromSeconds(2));

        var result = marshal.Invoke(() => 42 + 8, CancellationToken.None);

        Assert.Equal(50, result);
    }

    [StaFact]
    public void Invoke_OnSameThread_RunsInline()
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromSeconds(2));

        var dispatcherThreadId = Environment.CurrentManagedThreadId;
        var observedThreadId = marshal.Invoke(() => Environment.CurrentManagedThreadId, CancellationToken.None);

        Assert.Equal(dispatcherThreadId, observedThreadId);
    }

    [StaFact]
    public void Invoke_FromForeignThread_RunsOnDispatcherThread()
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromSeconds(2));
        var dispatcherThreadId = Environment.CurrentManagedThreadId;
        var observed = 0;

        var worker = new Thread(() =>
        {
            observed = marshal.Invoke(() => Environment.CurrentManagedThreadId, CancellationToken.None);
        });
        worker.Start();

        DispatcherFrame frame = new DispatcherFrame();
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

        SnoopMcpException ex =
            Assert.Throws<SnoopMcpException>(() => slowMarshal.Invoke(() => 1, CancellationToken.None));
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

        Assert.Throws<InvalidOperationException>(() => marshal.Invoke(() => 1, CancellationToken.None));
    }
}
