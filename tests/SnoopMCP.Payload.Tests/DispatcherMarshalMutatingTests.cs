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
}
