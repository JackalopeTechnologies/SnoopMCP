// HostRecoveryTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using ClientIntegration;
using Xunit;

public sealed class HostRecoveryTests
{
    [Fact]
    public async Task RepeatedCrashes_AllowExactlyThreeDelayedRestarts()
    {
        int launches = 0;
        var delays = new List<TimeSpan>();
        var log = new List<string>();
        int result = await HostRecovery.RunAsync(() => Task.FromResult(-++launches),
            duration => { delays.Add(duration); return Task.CompletedTask; }, log.Add);
        Assert.Equal(-4, result);
        Assert.Equal(4, launches);
        Assert.Equal(3, delays.Count);
        Assert.All(delays, duration => Assert.Equal(TimeSpan.FromMinutes(1), duration));
        Assert.Contains("restart limit reached", log[^1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanExit_StopsSupervisionIncludingAfterRecovery(bool crashFirst)
    {
        int launches = 0;
        int delays = 0;
        int result = await HostRecovery.RunAsync(() => Task.FromResult(++launches == 1 && crashFirst ? -1 : 0),
            _ => { delays++; return Task.CompletedTask; }, _ => { });
        Assert.Equal(0, result);
        Assert.Equal(crashFirst ? 2 : 1, launches);
        Assert.Equal(crashFirst ? 1 : 0, delays);
    }
}
