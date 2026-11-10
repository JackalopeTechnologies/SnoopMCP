# Windows installer qualification

Qualification was executed on disposable GitHub-hosted Windows machines and on the developer's
installed copy on October 24, 2026. The isolated machines own all processes, scheduled tasks, and
configuration used by destructive tests. The developer installation was backed up before upgrade;
no inspection session was attached and its interaction preference was preserved.

## Installer lifecycle

The eight cases in `Test-InstallerLifecycle.ps1` passed in
[CI run 40154446949](https://github.com/JackalopeTechnologies/SnoopMCP/actions/runs/40154446949).
They also passed for the final installer correction in
[CI run 40154712438](https://github.com/JackalopeTechnologies/SnoopMCP/actions/runs/40154712438).

| Case | Result |
| --- | --- |
| Fresh install, repeated registration, repair, and uninstall | Passed |
| Upgrade from customized published 1.4.2 | Passed |
| Failure and cancellation after old-product removal | Both passed |
| Failure and cancellation late in the new installation | Both passed |
| Failure and cancellation inside legacy removal | Both passed |

The checks compare client settings, server options, permissions, unrelated skills, edited Snoop
skills, personal skill support files, logs, interaction preferences, and owned shortcuts. Failed
upgrades restore the old files and scheduled task, including its customized description, and retain
the recovery backup. Successful uninstallation removes only owned files and registrations.

The legacy package is pinned by SHA-256:
`4776DF42B57E8003E85CEC058335B09B76C5CDF8A3D366AC2868F4AC01470C0A`.
Each CI artifact records the tested candidate commit and package hash in `candidate.json`.

## Crash recovery and intentional shutdown

`Test-InstalledHost.ps1` passed in CI runs
[40154132665](https://github.com/JackalopeTechnologies/SnoopMCP/actions/runs/40154132665) and
[40154446949](https://github.com/JackalopeTechnologies/SnoopMCP/actions/runs/40154446949):

- Three unexpected host exits were each followed by a new healthy host after one minute.
- A fourth unexpected exit exhausted the retry limit and remained stopped for 80 seconds.
- CLI stop and the installed tray's Exit command each remained stopped for 80 seconds.
- Diagnostics recorded unexpected shutdown and exhaustion of the retry limit.
- The recovery launcher exited after intentional shutdown; no false crash marker remained.

The tray check invokes the installed application's actual `ExitCommand` on its WPF dispatcher
using a test-only startup hook. It is command-execution evidence, not a physical menu click or an
appearance test. Interaction stays disabled and no WPF inspection session is attached.

Run 40154712438 repeated the three recoveries and retry-limit check, but reported a process
immediately after CLI stop. That run did not identify its PID or exit state. The qualification
script now releases its retained process handles after exit, explicitly requires the original host
to have exited when CLI stop returns, and records PID/exit state on any subsequent failure. The
80-second no-restart assertion is unchanged. The current PR checks carry the result of this recheck.

The scheduled launcher supervises only its own child. Recovery covers nonzero host exits; it does
not promise recovery if the launcher or operating system is terminated, or if an external tool
forces a zero exit code. Deliberate Exit remains stopped until manually started or the next logon.

## Installed-machine verification

The final package was installed successfully with Windows Installer exit code 0:

- Application version: `1.4.3+0fe0144b07fa8d2260c3c20d6fb8b6ab97de6aa6`.
- MSI SHA-256: `3F89F76AEFB51016BCD1AB94EF5007281BB2FACD38FC828D3FD5DD4B81A3D20B`.
- All 46 installed package files matched the staging directory, including all injector launchers.
- Eight existing client configurations retained their settings. Claude concurrently updated its
  feature caches and usage counters; these changes were recorded separately from preferences.
- All 1,314 unrelated skill files were unchanged, and four installed Snoop skill directories
  matched the two canonical sources and contained ownership receipts.
- The interaction preference was byte-identical to its backup. The host was unattached.
- Health and MCP initialization reported the exact version above; discovery returned 30 tools
  and two canonical guidance resources. This Codex chat successfully called `list_wpf_processes`.
- The installed `SnoopMCP Host` scheduled task ran `SnoopMCP.Recovery.exe`, whose child was the host.

The install exposed an independent injector version mismatch: old launchers reported 6.1.2.1,
while the reproducible build reports 1.0.0.0. Windows Installer skipped their replacement and old
product removal deleted them. The package now forces copying its owned files regardless of their
previous independent versions; full installed-file comparison confirmed the correction.

Local evidence and the private configuration backup are retained in
`E:\CodexScratch\snoopmcp-install-20261024-final`. They are not committed or uploaded.

## Build, protocol, and discovery evidence

The full local Release suite passed 420 tests with one documented manual capture test skipped;
the build had zero warnings and errors. The later scheduled-task ownership cases passed all five
focused tests. Package validation and the final candidate build also passed without warnings.

The integration suite inspected the sample WPF application. The installed-machine checks above
verified files, configuration, connection, discovery, and read-only process enumeration; they did
not execute a new WPF inspection or verify appearance. A fresh Codex application server previously
discovered both installed skills enabled. An older failed client session may require reconnecting
or restarting the client; a healthy endpoint alone does not establish client discovery.
