// UpgradeSnapshotTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using InstallerActions;
using Xunit;

/// <summary>Preservation through the destructive cleanup shipped in older installers.</summary>
public sealed class UpgradeSnapshotTests : IDisposable
{
    private readonly string mRoot = Path.Combine(Path.GetTempPath(), "snoop-upgrade-" + Guid.NewGuid().ToString("N"));

    public UpgradeSnapshotTests() => Directory.CreateDirectory(mRoot);

    public void Dispose()
    {
        Directory.Delete(mRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Upgrade_RestoresPreferencesAndPersonalFilesWithoutRestoringOldBinaries()
    {
        string config = Write("profile/.claude.json", "{\"mcpServers\":{\"snoopmcp\":{\"timeout\":777}},\"theme\":\"dark\"}");
        string permission = Write("profile/.claude/settings.json", "{\"permissions\":{\"deny\":[\"*\"]}}");
        string skill = Write("profile/.claude/skills/snoopmcp-first/SKILL.md", "locally customized skill");
        string notes = Write("profile/.claude/skills/snoopmcp-first/notes.txt", "personal notes");
        string unrelated = Write("profile/.claude/skills/other/SKILL.md", "unrelated");
        string binary = Write("installed/host.exe", "old binary");
        string gate = Write("installed/interaction-gate.json", "{\"Enabled\":false}");
        string log = Write("installed/logs/host.log", "history");
        string installed = Path.GetDirectoryName(binary)!;
        var snapshot = new UpgradeSnapshot(Path.Combine(mRoot, "backup"));
        snapshot.Capture([config, permission], [Path.GetDirectoryName(skill)!, installed],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { binary });

        // Simulate legacy unregistration and recursive directory cleanup, then install a newer binary.
        File.WriteAllText(config, "{\"mcpServers\":{}}");
        File.Delete(permission);
        Directory.Delete(Path.GetDirectoryName(skill)!, recursive: true);
        Directory.Delete(installed, recursive: true);
        Write("installed/host.exe", "new binary");

        snapshot.Restore();
        Assert.Contains("777", File.ReadAllText(config));
        Assert.Contains("deny", File.ReadAllText(permission));
        Assert.Equal("locally customized skill", File.ReadAllText(skill));
        Assert.Equal("personal notes", File.ReadAllText(notes));
        Assert.Equal("unrelated", File.ReadAllText(unrelated));
        Assert.Equal("new binary", File.ReadAllText(binary));
        Assert.Equal("{\"Enabled\":false}", File.ReadAllText(gate));
        Assert.Equal("history", File.ReadAllText(log));

        // The backup remains until commit and can restore again after a later rollback failure.
        File.WriteAllText(config, "rollback cleanup");
        snapshot.Restore();
        Assert.Contains("777", File.ReadAllText(config));
        snapshot.Delete();
        Assert.False(Directory.Exists(Path.Combine(mRoot, "backup")));
    }

    [Fact]
    public void Capture_DoesNotReplaceAnExistingBackup()
    {
        string config = Write("config", "original");
        var snapshot = new UpgradeSnapshot(Path.Combine(mRoot, "backup"));
        snapshot.Capture([config], [], new HashSet<string>());
        File.WriteAllText(config, "changed");
        Assert.Throws<IOException>(() => snapshot.Capture([config], [], new HashSet<string>()));
        snapshot.Restore();
        Assert.Equal("original", File.ReadAllText(config));
    }

    [Fact]
    public void Cleanup_RemovesOnlySnapshotOwnedFiles()
    {
        string config = Write("config", "original");
        var snapshot = new UpgradeSnapshot(Path.Combine(mRoot, "backup"));
        snapshot.Capture([config], [], new HashSet<string>());
        string extra = Write("backup/notes.txt", "keep");
        snapshot.Delete();
        Assert.Equal("keep", File.ReadAllText(extra));
        Assert.Equal("original", File.ReadAllText(config));
    }

    [Fact]
    public void LegacyPaths_IncludePermissionAndEveryOldClientConfiguration()
    {
        string profile = Path.Combine(mRoot, "profile");
        string roaming = Path.Combine(mRoot, "roaming");
        string[] paths = LegacyClientPaths.Configurations(profile, roaming).Select(Path.GetFullPath).ToArray();
        Assert.Contains(Path.Combine(profile, ".claude", "settings.json"), paths);
        Assert.Contains(Path.Combine(profile, ".codex", "config.toml"), paths);
        Assert.Contains(Path.Combine(profile, ".cursor", "mcp.json"), paths);
        Assert.Contains(Path.Combine(profile, ".gemini", "settings.json"), paths);
        Assert.Contains(Path.Combine(roaming, "Code", "User", "mcp.json"), paths);
        Assert.Contains(Path.Combine(roaming, "Claude", "claude_desktop_config.json"), paths);
    }

    [Fact]
    public void RestoreFailure_KeepsBackupAvailableForRecovery()
    {
        string config = Write("config", "original");
        string backup = Path.Combine(mRoot, "backup");
        var snapshot = new UpgradeSnapshot(backup);
        snapshot.Capture([config], [], new HashSet<string>());
        File.Delete(config);
        Directory.CreateDirectory(config);
        Assert.Throws<IOException>(snapshot.Restore);
        Assert.True(File.Exists(Path.Combine(backup, "manifest.xml")));
        Assert.Equal("original", File.ReadAllText(Path.Combine(backup, "1.data")));
        Directory.Delete(config);
        snapshot.Restore();
        Assert.Equal("original", File.ReadAllText(config));
    }

    private string Write(string relative, string content)
    {
        string path = Path.GetFullPath(Path.Combine(mRoot, relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
