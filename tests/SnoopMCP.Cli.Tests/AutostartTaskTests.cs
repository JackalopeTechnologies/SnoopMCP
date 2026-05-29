// AutostartTaskTests.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Cli.Tests;

using SnoopMCP.Cli;
using Xunit;

public sealed class AutostartTaskTests
{
    [Fact]
    public void BuildCreateArguments_IsOnLogonLimitedForcedWithHostPath()
    {
        IReadOnlyList<string> args = AutostartTask.BuildCreateArguments(@"C:\app\SnoopMCP.Host.exe");

        Assert.Equal(
            ["/Create", "/TN", "SnoopMCP Host", "/SC", "ONLOGON", "/RL", "LIMITED",
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
    public void BuildCreateArguments_NullHostPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => AutostartTask.BuildCreateArguments(string.Empty));
    }
}
