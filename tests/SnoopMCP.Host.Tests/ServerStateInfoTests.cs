// ServerStateInfoTests.cs
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

using Xunit;

#endregion

namespace SnoopMCP.Host.Tests;

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
