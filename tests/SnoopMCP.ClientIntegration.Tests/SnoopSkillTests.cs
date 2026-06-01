// SnoopSkillTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration.Tests;

using SnoopMCP.ClientIntegration;
using Xunit;

/// <summary>Tests for <see cref="SnoopSkill"/> installing/removing the snoopmcp-first skill.</summary>
public sealed class SnoopSkillTests : IDisposable
{
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
}
