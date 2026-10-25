// HostRunRecordTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using ClientIntegration;
using Xunit;

public sealed class HostRunRecordTests : IDisposable
{
    private readonly string mPath = Path.Combine(Path.GetTempPath(), "snoop-run-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        File.Delete(mPath);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void CleanExit_DoesNotReportUnexpectedStop()
    {
        var record = new HostRunRecord(mPath);
        Assert.False(record.Begin());
        record.Complete();
        Assert.False(record.Begin());
    }

    [Fact]
    public void MissingExit_IsReportedOnNextStartup()
    {
        Assert.False(new HostRunRecord(mPath).Begin());
        Assert.True(new HostRunRecord(mPath).Begin());
    }
}
