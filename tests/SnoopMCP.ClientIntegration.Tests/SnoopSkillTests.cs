// SnoopSkillTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using ClientIntegration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

/// <summary>Tests for <see cref="SnoopSkill"/> installing/removing the SnoopMCP skills.</summary>
public sealed class SnoopSkillTests : IDisposable
{
    private const string SolutionFileName = "SnoopMCP.sln";
    private static readonly string[] smCodexOwners = ["Codex"];

    private readonly string mDir;

    public SnoopSkillTests()
    {
        mDir = Path.Combine(Path.GetTempPath(), "snoopmcp-skill-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(mDir))
        {
            Directory.Delete(mDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Install_WritesSkillMarkdown()
    {
        string skillsDir = Path.Combine(mDir, "skills");

        bool ok = SnoopSkill.Install(skillsDir);

        Assert.True(ok);
        string path = Path.Combine(skillsDir, "snoopmcp-first", "SKILL.md");
        Assert.True(File.Exists(path));
        string text = File.ReadAllText(path);
        Assert.Contains("name: snoopmcp-first", text);
        Assert.Contains("listWpfProcesses", text);
    }

    [Fact]
    public void Install_IsIdempotent()
    {
        string skillsDir = Path.Combine(mDir, "skills");
        SnoopSkill.Install(skillsDir);

        bool ok = SnoopSkill.Install(skillsDir);

        Assert.True(ok);
    }

    [Fact]
    public void Remove_DeletesSkillDir()
    {
        string skillsDir = Path.Combine(mDir, "skills");
        SnoopSkill.Install(skillsDir);

        bool ok = SnoopSkill.Remove(skillsDir);

        Assert.True(ok);
        Assert.False(Directory.Exists(Path.Combine(skillsDir, "snoopmcp-first")));
    }

    [Fact]
    public void Install_WritesBothSkills()
    {
        string skillsDir = Path.Combine(mDir, "skills");

        Assert.True(SnoopSkill.Install(skillsDir));

        Assert.True(File.Exists(Path.Combine(skillsDir, "snoopmcp-first", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(skillsDir, "snoopmcp-uia", "SKILL.md")));
    }

    [Fact]
    public void Remove_PreservesAddedFilesAndUnrelatedSkills()
    {
        Assert.True(SnoopSkill.Install(mDir));
        string added = Path.Combine(mDir, SnoopSkill.FirstSkillName, "notes.txt");
        string other = Path.Combine(mDir, "other", "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(other) ?? throw new InvalidOperationException());
        File.WriteAllText(added, "personal notes");
        File.WriteAllText(other, "other skill");
        Assert.True(SnoopSkill.Remove(mDir));
        Assert.Equal("personal notes", File.ReadAllText(added));
        Assert.Equal("other skill", File.ReadAllText(other));
        Assert.False(File.Exists(Path.Combine(mDir, SnoopSkill.FirstSkillName, "SKILL.md")));
    }

    [Fact]
    public void InstallAndRemove_PreserveUserEdits()
    {
        Assert.True(SnoopSkill.Install(mDir));
        string path = Path.Combine(mDir, SnoopSkill.FirstSkillName, "SKILL.md");
        File.AppendAllText(path, "\nMy local addition");
        string edited = File.ReadAllText(path);
        Assert.False(SnoopSkill.Install(mDir));
        Assert.Equal(edited, File.ReadAllText(path));
        Assert.True(SnoopSkill.Remove(mDir));
        Assert.Equal(edited, File.ReadAllText(path));
    }

    [Fact]
    public void SharedSkills_AreRemovedOnlyAfterLastRegisteredClient()
    {
        Assert.True(SnoopSkill.Install(mDir, "Codex"));
        Assert.True(SnoopSkill.Install(mDir, "Cursor"));
        Assert.True(SnoopSkill.Install(mDir, "Cursor"));
        Assert.True(SnoopSkill.Remove(mDir, "Codex"));
        Assert.True(SnoopSkill.Remove(mDir, "Grok Build"));
        Assert.True(File.Exists(Path.Combine(mDir, SnoopSkill.FirstSkillName, "SKILL.md")));
        Assert.True(SnoopSkill.Remove(mDir, "Cursor"));
        Assert.False(Directory.Exists(Path.Combine(mDir, SnoopSkill.FirstSkillName)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Install_UpgradesOwnedOlderOrInterruptedInstallation(bool interrupted)
    {
        string dir = Path.Combine(mDir, SnoopSkill.FirstSkillName);
        Directory.CreateDirectory(dir);
        const string OlderBody = "previous canonical release";
        string oldHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(OlderBody)));
        string currentBody = SnoopSkill.Definitions[0].Body;
        string newHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(currentBody)));
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), interrupted ? currentBody : OlderBody);
        File.WriteAllText(Path.Combine(dir, ".snoopmcp-install.json"), JsonSerializer.Serialize(new
        {
            Version = 1, Owners = smCodexOwners, Hashes = new[] { oldHash, newHash }
        }));
        Assert.True(SnoopSkill.Install(mDir, "Codex"));
        Assert.Equal(currentBody, File.ReadAllText(Path.Combine(dir, "SKILL.md")));
        Assert.True(SnoopSkill.Remove(mDir, "Codex"));
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void UnownedIdenticalSkill_IsNotClaimedOrDeleted()
    {
        string dir = Path.Combine(mDir, SnoopSkill.FirstSkillName);
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "SKILL.md");
        File.WriteAllText(path, SnoopSkill.Definitions[0].Body);
        Assert.False(SnoopSkill.Install(mDir, "Codex"));
        Assert.True(SnoopSkill.Remove(mDir, "Codex"));
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(Path.Combine(dir, ".snoopmcp-install.json")));
    }

    [Fact]
    public void CorruptOwnershipRecord_PreservesFilesAndReportsFailure()
    {
        Assert.True(SnoopSkill.Install(mDir));
        string marker = Path.Combine(mDir, SnoopSkill.FirstSkillName, ".snoopmcp-install.json");
        File.WriteAllText(marker, "invalid");
        Assert.False(SnoopSkill.Install(mDir));
        Assert.False(SnoopSkill.Remove(mDir));
        Assert.Equal("invalid", File.ReadAllText(marker));
        Assert.True(File.Exists(Path.Combine(mDir, SnoopSkill.FirstSkillName, "SKILL.md")));
    }

    [Fact]
    public void UnwritableDestination_ReturnsFailureWithoutDeletingOtherFiles()
    {
        string blocker = Path.Combine(mDir, SnoopSkill.FirstSkillName);
        File.WriteAllText(blocker, "occupied");
        Assert.False(SnoopSkill.Install(mDir));
        Assert.Equal("occupied", File.ReadAllText(blocker));
    }

    /// <summary>
    /// Drift guard: every embedded skill body in <see cref="SnoopSkill.Definitions"/> must stay in sync with
    /// its corresponding repo <c>skills/&lt;name&gt;/SKILL.md</c> file.
    /// </summary>
    [Fact]
    public void EmbeddedBodies_MatchRepoFiles()
    {
        string repoRoot = FindRepoRoot();
        foreach ((string name, string body) in SnoopSkill.Definitions)
        {
            string repoFile = Path.Combine(repoRoot, "skills", name, "SKILL.md");
            Assert.True(File.Exists(repoFile), $"Missing repo skill file: {repoFile}");
            Assert.Equal(
                File.ReadAllText(repoFile).ReplaceLineEndings("\n").TrimEnd(),
                body.ReplaceLineEndings("\n").TrimEnd());
        }
    }

    /// <summary>Walks up from <see cref="AppContext.BaseDirectory"/> until it finds <c>SnoopMCP.sln</c>.</summary>
    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new InvalidOperationException(
                $"Could not locate {SolutionFileName} above {AppContext.BaseDirectory}.");
        }
        return dir.FullName;
    }
}
