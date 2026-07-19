// AutostartTaskTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using ClientIntegration;
using Xunit;

public sealed class AutostartTaskTests
{
    [Fact]
    public void BuildCreateArguments_IsOnLogonHighestForcedWithHostPath()
    {
        IReadOnlyList<string> args = AutostartTask.BuildCreateArguments(@"C:\app\SnoopMCP.Host.exe");

        Assert.Equal(
            ["/Create", "/TN", "SnoopMCP Host", "/SC", "ONLOGON", "/RL", "HIGHEST",
             "/TR", @"C:\app\SnoopMCP.Host.exe", "/F"],
            args);
    }

    [Fact]
    public void BuildDeleteArguments_TargetsTheTaskWithForce()
    {
        IReadOnlyList<string> args = AutostartTask.BuildDeleteArguments();

        Assert.Equal(["/Delete", "/TN", "SnoopMCP Host", "/F"], args);
    }

    [Fact]
    public void BuildQueryArguments_QueriesTheTask()
    {
        IReadOnlyList<string> args = AutostartTask.BuildQueryArguments();

        Assert.Equal(["/Query", "/TN", "SnoopMCP Host"], args);
    }

    [Fact]
    public void BuildCreateArguments_NullOrEmptyHostPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => AutostartTask.BuildCreateArguments(string.Empty));
    }
}
