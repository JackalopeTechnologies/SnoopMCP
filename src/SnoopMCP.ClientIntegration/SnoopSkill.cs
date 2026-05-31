// SnoopSkill.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Installs (and removes) the minimal <c>snoopmcp-first</c> skill into a Claude Code skills directory
/// (<c>~/.claude/skills/snoopmcp-first/SKILL.md</c>). The skill body is kept in sync with
/// <c>skills/snoopmcp-first/SKILL.md</c> in the repo.
/// </summary>
public static class SnoopSkill
{
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

    /// <summary>Writes the skill under <paramref name="skillsDir"/>. Returns false on IO failure.</summary>
    /// <param name="skillsDir">The Claude Code skills directory (e.g. <c>~/.claude/skills</c>).</param>
    public static bool Install(string skillsDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        bool ok;
        try
        {
            string dir = Path.Combine(skillsDir, SkillName);
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
        bool ok = true;
        string dir = Path.Combine(skillsDir, SkillName);
        if (Directory.Exists(dir))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                ok = false;
            }
            catch (UnauthorizedAccessException)
            {
                ok = false;
            }
        }
        return ok;
    }
}
