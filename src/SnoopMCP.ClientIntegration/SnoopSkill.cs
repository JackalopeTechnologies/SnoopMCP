// SnoopSkill.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.ClientIntegration;

/// <summary>
/// Installs (and removes) the SnoopMCP skills into a Claude Code skills directory
/// (<c>~/.claude/skills/&lt;name&gt;/SKILL.md</c>). Each embedded body is kept in sync with the
/// corresponding <c>skills/&lt;name&gt;/SKILL.md</c> file in the repo.
/// </summary>
public static class SnoopSkill
{
    /// <summary>The inspection skill's directory name (becomes the Claude Code skill command).</summary>
    public const string FirstSkillName = "snoopmcp-first";

    /// <summary>The driving/UIA skill's directory name (becomes the Claude Code skill command).</summary>
    public const string UiaSkillName = "snoopmcp-uia";

    private const string SkillFileName = "SKILL.md";

    private const string FirstSkillBody =
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

    private const string UiaSkillBody =
        """
        ---
        name: snoopmcp-uia
        description: Use when driving or automating a running WPF application hands-free — clicking, selecting, setting values, navigating, waiting, or screenshotting — via SnoopMCP's UI Automation tools. Complements snoopmcp-first (inspection); this skill is for INTERACTION and background capture, never synthesized mouse/keyboard input.
        ---

        # SnoopMCP — driving a live WPF app (UI Automation)

        SnoopMCP can drive a running WPF app through UI Automation without touching the mouse or stealing
        focus, and capture the window even when it is occluded. Driving is two-tier:

        1. **UIA tier (no attach needed).** Keyed by PID.
           - `getUiaTree(pid, fromElement?, depth)` — discover elements (AutomationId, Name, ControlType, HelpText, bounds, patterns).
           - `findUiaElement(pid, by, value)` — `by` ∈ automationId | name | helpText | controlType (this is the stability order — prefer automationId; treat a VM-type-name Name as a smell).
           - `captureWindow(pid)` — returns the window as a PNG image; works while occluded; a minimized window returns CaptureUnavailable.
           - `waitForUia(pid, by, value, timeoutMs)` — wait for an element instead of sleeping.
           - `invokeUia(element, pattern?)` — click/select/toggle/expand. MUTATES.
           - `setUiaValue(element, value)` — set a text/numeric field. MUTATES.
        2. **Payload tier (requires `attach`).** For controls UIA can't drive and for ground-truth checks — see the driving tools added under attach (peerInvoke, executeCommand, waitForValue).

        ## Rules

        - Never ask SnoopMCP to synthesize mouse/keyboard input — there is no such tool by design. If a control
          is `NotDrivable`, tell the user and ask them to act, or try the payload tier.
        - Mutating tools (`invokeUia`, `setUiaValue`) require the host **interaction gate**. If you get
          `InteractionDisabled`, ask the user to enable "Allow app interaction (driving)" in the SnoopMCP tray.
        - Driving/capturing an elevated (admin) app requires the SnoopMCP host to run elevated — enabling
          autostart registers an elevated logon task (one UAC). This is admin-only.
        - Element handles are short-lived; pass back the `element` reference you received. If it is stale,
          SnoopMCP re-resolves it by locator, or returns `UiaElementStale`/`UiaAmbiguousLocator` — re-find in that case.
        - Endpoint: http://127.0.0.1:6300/mcp. Start the SnoopMCP host (tray app) first.
        """;

    /// <summary>The skills installed for Claude Code, as (directory-name, body) pairs.</summary>
    public static IReadOnlyList<(string Name, string Body)> Definitions { get; } =
    [
        (FirstSkillName, FirstSkillBody),
        (UiaSkillName, UiaSkillBody)
    ];

    /// <summary>Writes all registered skills under <paramref name="skillsDir"/>. Returns false if any write fails.</summary>
    /// <param name="skillsDir">The Claude Code skills directory (e.g. <c>~/.claude/skills</c>).</param>
    public static bool Install(string skillsDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        bool ok = true;
        foreach ((string name, string body) in Definitions)
        {
            try
            {
                string dir = Path.Combine(skillsDir, name);
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, SkillFileName), body);
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

    /// <summary>Removes all registered skill directories if present. Returns false if any removal fails.</summary>
    /// <param name="skillsDir">The Claude Code skills directory (e.g. <c>~/.claude/skills</c>).</param>
    public static bool Remove(string skillsDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(skillsDir);
        bool ok = true;
        foreach ((string name, _) in Definitions)
        {
            string dir = Path.Combine(skillsDir, name);
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
        }
        return ok;
    }
}
