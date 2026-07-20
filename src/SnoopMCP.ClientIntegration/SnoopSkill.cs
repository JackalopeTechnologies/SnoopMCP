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
        description: Use when inspecting, diagnosing, or debugging a running WPF application's live visual tree, dependency properties, data bindings, styles, templates, or DataContext via SnoopMCP — a control renders blank, wrong-sized, or wrong-styled; a binding silently fails; the DataContext isn't what you expect; you need a template part or a property's effective value. For clicking, typing, waiting, or screenshotting the app, use snoopmcp-uia instead.
        ---

        # SnoopMCP — live WPF inspection

        SnoopMCP injects a payload into a running .NET (x64) WPF process and answers questions about its
        live state: the visual and logical trees, dependency-property values with their precedence, bindings,
        styles, templates, and DataContext. Every tool in this skill is read-only and ungated — call them
        freely while diagnosing.

        Driving the app (click, set a value, wait, capture the window) is a separate, mutating tool family
        behind the host's interaction gate, which is **off by default** — the user enables it from the
        SnoopMCP tray menu ("Allow app interaction (driving)"). See the **snoopmcp-uia** skill.

        ## Workflow

        1. `listWpfProcesses()` — pick the target by pid, window title, and bitness. Host-side; no session yet.
        2. `attach(pid)` — required before every tool below. Its response already carries the live visual
           roots; use those ids rather than guessing, or reusing ids from an earlier session. Only one session
           exists at a time: `attach` fails outright while another is open, so `detach()` first.
        3. Inspect (below).
        4. `detach()` when done.

        ## Quick reference

        | To find out | Call |
        |---|---|
        | What windows/popups exist | `listVisualRoots()` |
        | Which element is *that* one | `findElements(id, predicate)` when you know a name/type; `hitTest(id, x, y)` when you only have a location; `resolvePath(id, path)` to re-resolve a path string from an earlier `describeElement` |
        | What a node is | `describeElement(id)` |
        | Tree shape | `getChildren(id, tree)`, `getParent(id, tree)`, `getTemplatedParent(id)` |
        | Why a property holds this value | `getDependencyProperty(id, name)` — value **and** precedence trace; `listDependencyProperties(id)` |
        | Why a binding shows nothing | `inspectBinding(id, propName)` for one binding; `listBindings(id, includeDescendants)` to audit a subtree |
        | What the data actually is | `describeDataContext(id)`, `readDataContextPath(id, path)` |
        | Why it looks like that | `resolveStyle(id)`, `resolveTemplate(id)` |
        | Full live snapshot | `exportXaml(id)` |

        Property names are the plain DP name — `Text`, `Visibility`, `Background`. `listDependencyProperties`
        gives the exact spellings available on an element.

        `findElements` predicate fields, all optional and AND-combined: `type` (case-sensitive substring of
        the full type name), `name` (exact `x:Name`), `automationId` (exact), `textContains` (case-insensitive,
        against capped visible text), `propertyEquals` (stringified DP comparison), and the nested predicates
        `hasAncestor`, `hasDescendant`, `inTemplateOf`. For bulk text searches, also `leafOnly`, `maxResults`,
        and `suppressPath` to keep large result sets bounded and readable.

        ## Common mistakes

        - **Carrying element ids across sessions.** Ids are per-session. If the target exits, the session goes
          with it (`SessionLost`) — `attach` the new pid and re-resolve. Within a live session, an id whose
          element has been garbage-collected returns `ElementExpired` — re-resolve from
          `listVisualRoots`/`findElements`. Worse, a live-looking id can silently describe a different row: a
          virtualizing panel recycles the *same* container for a different data item as it scrolls — same id,
          no error, no `ElementExpired`. If you're holding ids across calls that might span a scroll or data
          change, re-`describeElement` and compare `dataContextHashCode` against the value you saw before; a
          changed value means this id now names different data.
        - **Blaming the binding first.** For a wrong value, read the precedence trace from
          `getDependencyProperty` — a local value, style trigger, or animation may be beating the binding.
        - **Trusting `exportXaml` for binding shape.** It serializes evaluated values, not `{Binding}` markup;
          use `listBindings` when the question is about the binding itself.
        - **Over-trusting predicates.** `textContains` searches only ~200 chars of visible text, and
          `propertyEquals` does not handle attached properties.
        - **Waiting on `recentTraceLines`.** `inspectBinding` always returns it empty today.

        ## Environment

        - One target at a time; x64 WPF on .NET 10+; the host must run at the **same elevation** as the target
          (`AccessDenied` otherwise).
        - Start the SnoopMCP host (tray app) first. Endpoint: http://127.0.0.1:6300/mcp.
        - Calls run on the target's dispatcher with a 5s timeout. `DispatcherTimeout` means the app's UI thread
          was busy or blocked, not that the call was wrong: let the app settle and retry the same call once. If
          it repeats, the UI thread is genuinely stuck — that is the bug to chase, not the tool call.
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
        2. **Payload tier (requires `attach`).** For controls UIA can't drive and for ground-truth checks.
           Element ids are the Snoop element ids from `attach`/`listVisualRoots`/`findElements`, not UIA-tier
           `element` references.
           - `getAutomationPeerInfo(id)` — bridge a Snoop element id to its UIA identity (AutomationId, Name, ClassName, ControlType); correlates the two tiers by `AutomationId`. Read-only.
           - `waitForValue(id, dependencyProperty?, dataContextPath?, expected, timeoutMs)` — poll a DP or DataContext path until it equals `expected` — ground-truth check (VM/DP state, not pixels). Read-only.
           - `peerInvoke(id, pattern, dispatch?)` — drive the real WPF `AutomationPeer` pattern (Invoke | Toggle | SelectionItem | ExpandCollapse) in-process. MUTATES.
           - `executeCommand(id, path?, parameter?, dispatch?)` — execute the `ICommand` bound to an element (or at a DataContext `path`), CanExecute-gated. MUTATES.

        ## An element visibly on screen isn't found by `findUiaElement`/`getUiaTree`

        This is not necessarily a SnoopMCP defect. A UIA tree walk only descends through the automation
        peers a parent peer reports as its children (`AutomationPeer.GetChildrenCore()`); a custom control
        whose peer never implemented child-peer support (or reports none) prunes everything below it from
        every UIA client's search, regardless of scope (`Children`/`Descendants`) or `by` locator — this is
        unrelated to the raw/control/content tree "view" concept, which only affects `TreeWalker`/caching,
        not `FindAll`/`FindFirst`. Suspect this whenever a `name`/`controlType` search comes up empty for
        something you can see rendered.

        **Workaround:** fall back to the payload tier. `attach()` then `findElements(id, { textContains, leafOnly: true })`
        walks the real WPF visual tree via `VisualTreeHelper` and never consults automation peers, so it
        finds elements a pruned peer hides from UIA entirely.

        ## Rules

        - Never ask SnoopMCP to synthesize mouse/keyboard input — there is no such tool by design. If a control
          is `NotDrivable`, tell the user and ask them to act, or try the payload tier.
        - Mutating tools (`invokeUia`, `setUiaValue`, `peerInvoke`, `executeCommand`) require the host
          **interaction gate**. If you get `InteractionDisabled`, ask the user to enable "Allow app interaction
          (driving)" in the SnoopMCP tray.
        - `peerInvoke`/`executeCommand` accept an optional `dispatch="post"` for actions that open a modal
          dialog and would otherwise block the wait indefinitely — it fires the action and returns immediately
          (`Dispatched: true`) without observing the outcome, i.e. `ActionDispatched`; verify the effect
          separately with `waitForValue`/`captureWindow`.
        - Without `dispatch="post"` (the default is `dispatch="wait"`), a mutating call that times out on the
          dispatcher returns `ActionPending` — the action may already have applied before the wait gave up;
          verify with `waitForValue`/`captureWindow` before assuming failure or retrying.
        - Driving/capturing an elevated (admin) app requires the SnoopMCP host to run elevated — enabling
          autostart registers an elevated logon task (one UAC). This is admin-only.
        - Element handles are short-lived; pass back the `element` reference you received. If it is stale,
          SnoopMCP re-resolves it by locator, or returns `UiaElementStale`/`UiaAmbiguousLocator` — re-find in that case.
        - The UIA tools deliberately use two argument shapes, not one: `findUiaElement`/`waitForUia` take a flat
          `pid`/`by`/`value` locator because there is no reference yet; `invokeUia`/`setUiaValue`/`getUiaTree`
          take an `element`/`fromElement` reference object (handle + locator fallback) because there is. This is
          an accepted design decision, not an inconsistency — don't flatten the reference calls or wrap the
          locator calls to make them match.
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
