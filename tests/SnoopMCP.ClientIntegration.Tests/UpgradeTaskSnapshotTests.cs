// UpgradeTaskSnapshotTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using InstallerActions;
using Xunit;

public sealed class UpgradeTaskSnapshotTests : IDisposable
{
    private readonly string mRoot = Path.Combine(Path.GetTempPath(), "snoop-task-backup-" + Guid.NewGuid().ToString("N"));

    public UpgradeTaskSnapshotTests() => Directory.CreateDirectory(mRoot);

    public void Dispose()
    {
        Directory.Delete(mRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("SnoopMCP.Host.exe")]
    [InlineData("SnoopMCP.Recovery.exe")]
    public void Rollback_RestoresExactPriorTaskWithoutStartingIt(string taskExecutable)
    {
        ArgumentException.ThrowIfNullOrEmpty(taskExecutable);
        string host = Path.Combine(mRoot, "SnoopMCP.Host.exe");
        string target = Path.Combine(mRoot, taskExecutable);
        string original = TaskXml(target, enabled: false);
        var backup = new UpgradeTaskSnapshot(mRoot);
        backup.Capture(host, () => original);
        string? current = null;
        backup.Restore(() => current, xml => current = xml, () => throw new InvalidOperationException());
        Assert.Equal(original, current);
        current = TaskXml(target, enabled: true);
        backup.Restore(() => current, xml => current = xml, () => throw new InvalidOperationException());
        Assert.Equal(original, current);
        Assert.True(File.Exists(Path.Combine(mRoot, "autostart.xml")));
    }

    [Fact]
    public void Rollback_RemovesNewOwnedTaskWhenThereWasNoPriorTask()
    {
        string host = Path.Combine(mRoot, "SnoopMCP.Host.exe");
        var backup = new UpgradeTaskSnapshot(mRoot);
        backup.Capture(host, () => null);
        string? current = TaskXml(host, enabled: true);
        backup.Restore(() => current, _ => throw new InvalidOperationException(), () => current = null);
        Assert.Null(current);
    }

    [Fact]
    public void Rollback_PreservesAReplacementTaskAndKeepsBackup()
    {
        string host = Path.Combine(mRoot, "SnoopMCP.Host.exe");
        var backup = new UpgradeTaskSnapshot(mRoot);
        backup.Capture(host, () => TaskXml(host, enabled: false));
        string current = TaskXml(Path.Combine(mRoot, "other.exe"), enabled: true);
        Assert.Throws<IOException>(() => backup.Restore(() => current,
            _ => throw new InvalidOperationException(), () => throw new InvalidOperationException()));
        Assert.True(File.Exists(Path.Combine(mRoot, "autostart.xml")));
    }

    [Fact]
    public void Capture_RejectsUnownedTaskAndAccessFailureBeforeCleanup()
    {
        string host = Path.Combine(mRoot, "SnoopMCP.Host.exe");
        var backup = new UpgradeTaskSnapshot(mRoot);
        Assert.Throws<IOException>(() => backup.Capture(host, () => TaskXml(Path.Combine(mRoot, "other.exe"), enabled: true)));
        Assert.Throws<UnauthorizedAccessException>(() => backup.Capture(host, () => throw new UnauthorizedAccessException()));
        Assert.False(File.Exists(Path.Combine(mRoot, "autostart.xml")));
    }

    private static string TaskXml(string host, bool enabled)
        => $"<Task xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"><Settings><Enabled>{enabled}</Enabled></Settings><Actions><Exec><Command>{System.Security.SecurityElement.Escape(host)}</Command></Exec></Actions></Task>";
}
