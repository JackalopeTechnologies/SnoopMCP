// SnoopSkill.cs
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

namespace SnoopMCP.ClientIntegration;

/// <summary>
///     Installs (and removes) the minimal <c>snoopmcp-first</c> skill into a Claude Code skills directory
///     (<c>~/.claude/skills/snoopmcp-first/SKILL.md</c>). The skill body is kept in sync with
///     <c>skills/snoopmcp-first/SKILL.md</c> in the repo.
/// </summary>
public static class SnoopSkill
{
    /// <summary>Writes the skill under <paramref name="skillsDir" />. Returns false on IO failure.</summary>
    /// <param name="skillsDir">The Claude Code skills directory (e.g. <c>~/.claude/skills</c>).</param>
    public static bool Install(string skillsDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        bool ok;
        try
        {
            var dir = Path.Combine(skillsDir, SkillName);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, SkillFileName), SkillBody);
            ok = true;
        }
        catch (IOException)
        {
            ok = false;
        }
        catch (UnauthorizedAccessException)
        {
            ok = false;
        }

        return ok;
    }

    /// <summary>Removes the skill directory if present. Returns false on IO failure.</summary>
    /// <param name="skillsDir">The Claude Code skills directory (e.g. <c>~/.claude/skills</c>).</param>
    public static bool Remove(string skillsDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        var ok = true;
        var dir = Path.Combine(skillsDir, SkillName);
        if (Directory.Exists(dir))
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
                ok = false;
            }
            catch (UnauthorizedAccessException)
            {
                ok = false;
            }

        return ok;
    }

    /// <summary>The skill directory name (becomes the Claude Code skill command).</summary>
    public const string SkillName = "snoopmcp-first";

    private const string SkillFileName = "SKILL.md";

    private const string SkillBody =
        """
        ---
        name: snoopmcp-first
        description: Use when inspecting, diagnosing, or debugging a running WPF application's visual tree, data bindings, styles, templates, or DataContext via SnoopMCP. SnoopMCP attaches to a live .NET WPF process and exposes read-only inspection tools over MCP.
        ---

        # SnoopMCP — live WPF inspection

        SnoopMCP is a read-only MCP server that injects into a running .NET (x64) WPF process and lets you
        inspect its live visual tree, dependency properties, bindings, styles, templates, and DataContext. It
        does not modify the target app.

        ## Workflow

        1. `listWpfProcesses` — find the target by PID and window title.
        2. `attach` with the PID — required before any inspection tool.
        3. Inspect: `listVisualRoots`, `describeElement`, `getChildren`/`getParent`, `findElements`, `hitTest`,
           `resolvePath`, `describeDataContext`, `readDataContextPath`, `listDependencyProperties`,
           `getDependencyProperty`, `resolveStyle`, `resolveTemplate`, `inspectBinding`, `listBindings`,
           `exportXaml`.
        4. `detach` when done.

        ## Notes

        - One target at a time; x64 WPF on .NET 10+ only.
        - All tools are read-only — safe to call freely while diagnosing.
        - Endpoint: http://127.0.0.1:6300/mcp. Start the SnoopMCP host (tray app) first.
        """;
}
