// ValueWaiterTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Payload.Tests;

using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;
using Interaction;
using Payload;
using Protocol.Errors;
using SnoopMCP.Protocol.Tools;
using Xunit;

public sealed class ValueWaiterTests
{
    [WpfFact]
    public async Task WaitAsync_DataContextPath_MatchesAfterChange()
    {
        var vm = new TestVm { Status = "pending" };
        var element = new TextBlock { DataContext = vm };
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromSeconds(2));

        _ = dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(150);
            vm.Status = "done";
        });

        WaitForValueResponse r = await new ValueWaiter().WaitAsync(
            element, null, "Status", "done", 2000, marshal, default);

        Assert.True(r.Matched);
        Assert.Equal("done", r.ActualValue);
    }

    [WpfFact]
    public async Task WaitAsync_DependencyProperty_MatchesAfterChange()
    {
        var element = new TextBlock { Text = "pending" };
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        var marshal = new DispatcherMarshal(dispatcher, TimeSpan.FromSeconds(2));

        _ = dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(150);
            element.Text = "done";
        });

        WaitForValueResponse r = await new ValueWaiter().WaitAsync(
            element, "Text", null, "done", 2000, marshal, default);

        Assert.True(r.Matched);
        Assert.Equal("done", r.ActualValue);
    }

    [WpfFact]
    public async Task WaitAsync_Timeout_ReturnsNotMatched()
    {
        var vm = new TestVm { Status = "pending" };
        var element = new TextBlock { DataContext = vm };
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));

        WaitForValueResponse r = await new ValueWaiter().WaitAsync(
            element, null, "Status", "done", 200, marshal, default);

        Assert.False(r.Matched);
        Assert.Equal("pending", r.ActualValue);
    }

    [WpfFact]
    public async Task WaitAsync_BothOrNeitherSource_ThrowsInvalidArgument()
    {
        var vm = new TestVm { Status = "pending" };
        var element = new TextBlock { DataContext = vm };
        var marshal = new DispatcherMarshal(Dispatcher.CurrentDispatcher, TimeSpan.FromSeconds(2));
        var waiter = new ValueWaiter();

        SnoopMcpException neitherEx = await Assert.ThrowsAsync<SnoopMcpException>(
            () => waiter.WaitAsync(element, null, null, "done", 200, marshal, default));
        SnoopMcpException bothEx = await Assert.ThrowsAsync<SnoopMcpException>(
            () => waiter.WaitAsync(element, "Text", "Status", "done", 200, marshal, default));

        Assert.Equal(ErrorCode.InvalidArgument, neitherEx.Code);
        Assert.Equal(ErrorCode.InvalidArgument, bothEx.Code);
    }

    /// <summary>Tiny DataContext view model for the DataContext-path wait tests.</summary>
    private sealed class TestVm : INotifyPropertyChanged
    {
        private string mStatus = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Status
        {
            get => mStatus;
            set
            {
                mStatus = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }
    }
}
