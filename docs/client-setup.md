# Coding-client setup and recovery

Use the installer finish-page registration option, the tray's **Install into** menu, or
`SnoopMCP.Cli register-clients --detected-only`. Individual flags select specific clients; for example:

```text
SnoopMCP.Cli register-clients --codex --cursor --grok-build --gemini-cli
```

This configures clients already on this Windows computer. It does not install coding assistants or
configure a remote worker to reach this computer. Snoop's endpoint remains local:
`http://127.0.0.1:6300/mcp`.

| Client | Connection configuration | Canonical skill discovery location |
| --- | --- | --- |
| Claude Code | `~/.claude.json` | `~/.claude/skills` |
| Codex | `$CODEX_HOME/config.toml`, otherwise `~/.codex/config.toml` | `~/.agents/skills` |
| Grok Build | `$GROK_HOME/config.toml`, otherwise `~/.grok/config.toml` | `~/.agents/skills` |
| Cursor | `~/.cursor/mcp.json` | `~/.agents/skills` |
| Google Gemini CLI | `~/.gemini/settings.json` | `~/.agents/skills` |
| GitHub Copilot CLI | `$COPILOT_HOME/mcp-config.json`, otherwise `~/.copilot/mcp-config.json` | `~/.agents/skills` |
| VS Code | `%APPDATA%/Code/User/mcp.json` | `~/.agents/skills` |
| Windsurf | `~/.codeium/windsurf/mcp_config.json` | `~/.codeium/windsurf/skills` |
| Visual Studio 2022 | `~/.mcp.json` | Connection registration only |
| Claude Desktop | `%APPDATA%/Claude/claude_desktop_config.json` | Connection registration only; requires Node.js for its existing bridge |

`~` means the current user's home directory. The shared location is documented by
[OpenAI](https://learn.chatgpt.com/docs/build-skills),
[Cursor](https://cursor.com/docs/skills),
[Google](https://geminicli.com/docs/cli/skills/),
[GitHub](https://docs.github.com/en/copilot/concepts/customizations/agent-skills),
[VS Code](https://code.visualstudio.com/docs/agent-customization/agent-skills), and
[Grok Build](https://docs.x.ai/build/features/skills-plugins-marketplaces).
[Grok's connection format](https://docs.x.ai/build/features/mcp-servers) and
[Windsurf's skill directory](https://docs.windsurf.com/windsurf/cascade/skills) are also documented.
Locations were checked on October 24, 2026. No older experimental Codex feature flag is needed.

## Installation and ownership

The application embeds `skills/snoopmcp-first/SKILL.md` and `skills/snoopmcp-uia/SKILL.md` directly
when built. These repository files are the only maintained instruction bodies.

Each managed skill has a `.snoopmcp-install.json` ownership record. Repeated registration upgrades
unedited files and records which clients use the shared copy. Removing one client keeps the copy
needed by the others. Upgrades record both content versions before replacement, allowing a retry
after interruption. Concurrent registration is serialized for each skill root.

Unmarked custom skills, edited managed skills, linked directories, unrelated files, and malformed
ownership records are preserved. A conflict or write failure reports incomplete skill setup rather
than silently claiming success. Exact known bodies from the earlier Claude installer can be adopted
and upgraded; other unmarked files are left alone. Uninstall never recursively deletes a skill folder.

Connection registration updates Snoop's URL and fills missing connection fields while retaining
server options such as timeouts, tool filters, disabled state, headers, and approval preferences.
The obsolete Codex flag is neither added nor changed if already present. Claude permission settings
are never edited, including approvals written by an older installer. Unregistration preserves a
same-named connection whose URL has been replaced, and leaves the client configuration file in place.

Older Windows packages run destructive cleanup even during an upgrade. The new package snapshots
the affected client configuration and permission files, complete Claude skill folders, and non-package
files in the installed application folder before removing the old version. It reads the old package's
file inventory so old binaries are not restored over new ones. Files are restored after installation
and on rollback. Future packages skip client unregistration during upgrades altogether.

The transaction backup is private to the installing user under `%LOCALAPPDATA%/SnoopMCP.Upgrade`.
A failed backup aborts before old-version removal. A successful commit removes only the backup's own
files; rollback retains the backup for recovery. Linked paths abort migration rather than risk the
legacy cleanup following them. Avoid editing client settings while a Windows installer upgrade is running.
Package builds verify the actual backup/removal/restore action order. Tests simulate the old cleanup
and rollback; these checks do not replace an installed Windows upgrade/uninstall qualification.

## Verify guidance separately from connection and inspection

1. Confirm both skill files exist in the documented location. In Codex, look for `snoopmcp-first`
   and `snoopmcp-uia` in the skills selector, or invoke `$snoopmcp-first`. Codex normally detects
   skill changes automatically; restart it if the changes do not appear. Preserve any deliberate
   `[[skills.config]] enabled = false` preference.
2. Check `/health`. Success proves host reachability, not client connection or tool discovery.
   Check the client's Snoop connection, `/mcp` URL, enablement, allowed/disabled tool lists, and logs.
   Refresh the connection after registration or host recovery; restart the client if necessary.
   Grok offers `grok inspect` and `grok mcp doctor snoopmcp`; Gemini offers `gemini skills list --all`.
3. Search the client's available tools for Snoop. Tool names may be snake_case and server-prefixed.
   A successful `listWpfProcesses` call proves a callable connection; it is not a WPF inspection.
4. Follow application instance rules and reuse the intended running app. Check session ownership
   before attaching. The health response's `attached` flag does not identify the owner. Never detach
   someone else's session or restart the host to take it over.
5. Inspect live bindings, dependency properties, styles, templates, and DataContext with Snoop.
   Capture a screenshot to verify appearance. General application tools remain useful where suitable.
   Report the executed inspection separately from file/configuration checks.

The interaction gate is independent of client permissions and remains under user control.
Inspection, waits, and screenshots do not need it. Never enable it automatically or bypass it through
another tool. An enabled gate is not blanket authorization to operate an application or hardware.

## Find and recover Snoop

Search Windows Start for **SnoopMCP - WPF inspection**. It starts the host through its existing
elevated-task path when available. The host runs in the notification area; the tray menu contains
connection setup, status, and Exit. **SnoopMCP - diagnostic logs** opens the log directory.

The startup task explicitly permits unlimited runtime and battery operation. A failed scheduled run
is retried up to three times at one-minute intervals. This is bounded crash recovery, not a promise
to survive every operating-system failure or a manual termination. An intentional tray Exit stays
stopped; `SnoopMCP.Cli stop` ends the scheduled task before terminating remaining instances to avoid
an unwanted restart. Autostart registration may require Windows administrator approval as before.

The host logs tray exits, Windows session shutdown, and fatal exceptions. An interrupted-run marker
allows the next launch to report an unclean stop, including forced termination where no exception
could be logged. The message does not invent a cause: consult `logs/host.log`, server logs, and Windows
task/crash history. Logs and interaction preferences survive uninstall and reinstall.

Installing/upgrading replaces the startup task with the new definition. To repair an existing task
without replacing or stopping the host, use the updated installed `SnoopMCP.Cli install-autostart`.
Keep the host's installed path; a build-output command should not repoint the task to temporary binaries.
