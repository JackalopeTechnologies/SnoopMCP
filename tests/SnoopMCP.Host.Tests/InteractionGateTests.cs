// InteractionGateTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using Host.Automation;
using Xunit;

public sealed class InteractionGateTests : IDisposable
{
    private readonly string mPath = Path.Combine(Path.GetTempPath(), "gate-" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        if (File.Exists(mPath))
        {
            File.Delete(mPath);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void IsEnabled_DefaultsFalse_WhenNoFile()
    {
        var gate = new InteractionGate(mPath);
        Assert.False(gate.IsEnabled);
    }

    [Fact]
    public void SetEnabled_Persists_AndIsReadBySecondInstance()
    {
        new InteractionGate(mPath).SetEnabled(true);
        Assert.True(new InteractionGate(mPath).IsEnabled);
        new InteractionGate(mPath).SetEnabled(false);
        Assert.False(new InteractionGate(mPath).IsEnabled);
    }
}
