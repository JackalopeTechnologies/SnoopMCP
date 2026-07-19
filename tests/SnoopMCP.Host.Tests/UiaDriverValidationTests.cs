// UiaDriverValidationTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using Host.Automation;
using Protocol.Errors;
using Xunit;

/// <summary>
/// Desktop-free guards on <see cref="UiaDriver"/>'s pure-validation paths: the checks that throw
/// before any UI Automation call is made, so these run in the normal non-interactive gate and catch
/// regressions of the pid/argument safety contracts without needing a live target window.
/// </summary>
public sealed class UiaDriverValidationTests
{
    [Fact]
    public void GetTreeAsync_FromElementPidMismatch_ThrowsInvalidArgument()
    {
        var driver = new UiaDriver(new ElementHandleCache());
        var fromElement = new UiaElementRef(5678, "5678:1", "automationId", "X");

        // GetTreeAsync throws synchronously (before returning a Task); the block body forces the
        // Assert.Throws(Action) overload instead of the obsolete Func<Task> one, since the compiler
        // would otherwise pick the Task-returning overload from the expression-lambda's return type.
        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(() =>
        {
            _ = driver.GetTreeAsync(1234, fromElement, 1, default);
        });

        Assert.Equal(ErrorCode.InvalidArgument, ex.Code);
    }

    [Fact]
    public void GetTreeAsync_DepthLessThanOne_ThrowsInvalidArgument()
    {
        var driver = new UiaDriver(new ElementHandleCache());

        // Same Action-overload workaround as above — GetTreeAsync throws before returning a Task.
        SnoopMcpException ex = Assert.Throws<SnoopMcpException>(() =>
        {
            _ = driver.GetTreeAsync(1234, null, 0, default);
        });

        Assert.Equal(ErrorCode.InvalidArgument, ex.Code);
    }

    [Fact]
    public async Task WaitForAsync_NegativeTimeout_ThrowsInvalidArgument()
    {
        var driver = new UiaDriver(new ElementHandleCache());

        SnoopMcpException ex = await Assert.ThrowsAsync<SnoopMcpException>(
            () => driver.WaitForAsync(1234, "automationId", "X", -1, default));

        Assert.Equal(ErrorCode.InvalidArgument, ex.Code);
    }
}
