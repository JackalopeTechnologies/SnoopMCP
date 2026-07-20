// ErrorCodeTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Protocol.Tests;

using Errors;
using Xunit;

/// <summary>
/// Stability tests for the wire-encoded <see cref="ErrorCode"/> values.
/// </summary>
public sealed class ErrorCodeTests
{
    [Fact]
    public void PhaseADrivingCodes_HaveStableNumbers()
    {
        Assert.Equal(12, (int)ErrorCode.InteractionDisabled);
        Assert.Equal(13, (int)ErrorCode.NotDrivable);
        Assert.Equal(14, (int)ErrorCode.ValueReadOnly);
        Assert.Equal(15, (int)ErrorCode.CaptureUnavailable);
        Assert.Equal(16, (int)ErrorCode.UiaElementStale);
        Assert.Equal(17, (int)ErrorCode.UiaAmbiguousLocator);
        Assert.Equal(18, (int)ErrorCode.TargetUnresponsive);
    }

    [Fact]
    public void PhaseBDrivingCodes_HaveStableNumbers()
    {
        Assert.Equal(19, (int)ErrorCode.ActionPending);
        Assert.Equal(20, (int)ErrorCode.ActionDispatched);
        Assert.Equal(21, (int)ErrorCode.CommandNotExecutable);
    }
}
