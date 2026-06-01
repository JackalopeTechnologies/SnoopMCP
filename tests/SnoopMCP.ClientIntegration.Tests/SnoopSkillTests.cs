// SnoopSkillTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

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
