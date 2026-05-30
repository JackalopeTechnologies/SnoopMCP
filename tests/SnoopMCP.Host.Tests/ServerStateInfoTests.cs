// ServerStateInfoTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host.Tests;

using SnoopMCP.Host;
using Xunit;

public class ServerStateInfoTests
{
    [Fact]
    public void CanStart_WhenStopped_IsTrue()
    {
        Assert.True(ServerStateInfo.CanStart(ServerState.Stopped));
    }

    [Fact]
    public void CanStart_WhenFaulted_IsTrue()
    {
        Assert.True(ServerStateInfo.CanStart(ServerState.Faulted));
    }

    [Fact]
    public void CanStart_WhenRunning_IsFalse()
    {
        Assert.False(ServerStateInfo.CanStart(ServerState.Running));
    }

    [Fact]
    public void CanStop_WhenRunning_IsTrue()
    {
        Assert.True(ServerStateInfo.CanStop(ServerState.Running));
    }

    [Fact]
    public void CanStop_WhenStopped_IsFalse()
    {
        Assert.False(ServerStateInfo.CanStop(ServerState.Stopped));
    }

    [Fact]
    public void CanStart_WhenStarting_IsFalse()
    {
        Assert.False(ServerStateInfo.CanStart(ServerState.Starting));
    }

    [Fact]
    public void CanStop_WhenStarting_IsFalse()
    {
        Assert.False(ServerStateInfo.CanStop(ServerState.Starting));
    }

    [Fact]
    public void CanStop_WhenFaulted_IsFalse()
    {
        Assert.False(ServerStateInfo.CanStop(ServerState.Faulted));
    }

    [Theory]
    [InlineData(ServerState.Stopped)]
    [InlineData(ServerState.Starting)]
    [InlineData(ServerState.Running)]
    [InlineData(ServerState.Faulted)]
    public void Tooltip_IsNeverEmpty(ServerState state)
    {
        Assert.False(string.IsNullOrWhiteSpace(ServerStateInfo.Tooltip(state)));
    }

    [Fact]
    public void Tooltip_WhenRunning_MentionsListenAddress()
    {
        Assert.Contains("127.0.0.1:6300", ServerStateInfo.Tooltip(ServerState.Running), StringComparison.Ordinal);
    }

    [Fact]
    public void Tooltip_IsDistinctForEveryState()
    {
        string[] tips =
        [
            ServerStateInfo.Tooltip(ServerState.Stopped),
            ServerStateInfo.Tooltip(ServerState.Starting),
            ServerStateInfo.Tooltip(ServerState.Running),
            ServerStateInfo.Tooltip(ServerState.Faulted)
        ];
        Assert.Equal(tips.Length, new HashSet<string>(tips).Count);
    }
}
