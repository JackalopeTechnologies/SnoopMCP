// AutostartTaskTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using ClientIntegration;
using System.Xml.Linq;
using Xunit;

public sealed class AutostartTaskTests
{
    [Fact]
    public void BuildCreateArguments_ImportsRecoveryDefinition()
    {
        IReadOnlyList<string> args = AutostartTask.BuildCreateArguments(@"E:\app\task.xml");

        Assert.Equal(
            ["/Create", "/TN", "SnoopMCP Host", "/XML", @"E:\app\task.xml", "/F"],
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

    [Fact]
    public void BuildRunArguments_RunsTheTask()
    {
        Assert.Equal(["/Run", "/TN", "SnoopMCP Host"], AutostartTask.BuildRunArguments());
    }

    [Fact]
    public void TaskDefinition_RemovesTimedAndBatteryStopsAndBoundsRecovery()
    {
        const string Exe = @"E:\Apps & Tools\SnoopMCP.Host.exe";
        const string UserId = "S-1-5-21-123";
        XDocument xml = XDocument.Parse(AutostartTask.BuildTaskXml(Exe, UserId));
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        Assert.Equal(@"E:\Apps & Tools\SnoopMCP.Recovery.exe", xml.Descendants(ns + "Command").Single().Value);
        Assert.All(xml.Descendants(ns + "UserId"), id => Assert.Equal(UserId, id.Value));
        Assert.Equal("InteractiveToken", xml.Descendants(ns + "LogonType").Single().Value);
        Assert.Equal("HighestAvailable", xml.Descendants(ns + "RunLevel").Single().Value);
        Assert.Equal("PT0S", xml.Descendants(ns + "ExecutionTimeLimit").Single().Value);
        Assert.Equal("false", xml.Descendants(ns + "StopIfGoingOnBatteries").Single().Value);
        Assert.Equal("false", xml.Descendants(ns + "DisallowStartIfOnBatteries").Single().Value);
        Assert.Equal("IgnoreNew", xml.Descendants(ns + "MultipleInstancesPolicy").Single().Value);
        // The launcher owns the retry budget; the scheduler must not multiply it.
        Assert.Empty(xml.Descendants(ns + "RestartOnFailure"));
    }
}
