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

    [Theory]
    [InlineData(ServerState.Stopped)]
    [InlineData(ServerState.Starting)]
    [InlineData(ServerState.Running)]
    [InlineData(ServerState.Faulted)]
    public void Tooltip_IsNeverEmpty(ServerState state)
    {
        Assert.False(string.IsNullOrWhiteSpace(ServerStateInfo.Tooltip(state)));
    }
}
