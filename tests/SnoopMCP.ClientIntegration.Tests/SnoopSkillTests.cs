// SnoopSkillTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using ClientIntegration;
using Xunit;

/// <summary>Tests for <see cref="SnoopSkill"/> installing/removing the SnoopMCP skills.</summary>
public sealed class SnoopSkillTests : IDisposable
{
    private const string SolutionFileName = "SnoopMCP.sln";

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
