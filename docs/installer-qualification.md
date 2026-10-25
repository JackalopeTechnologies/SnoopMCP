# Windows installer qualification

The client guidance and recovery changes remain in draft until the checks below are complete.
Passing automated tests or a single installed upgrade does not satisfy the full installer lifecycle
and scheduled recovery requirements. Record the tested package version, source commit, commands,
results, and retained evidence when completing each check.

## Verification completed

- Clean Release build with no warnings or errors; 413 tests passed and one documented manual
  capture test was skipped. The final installer recovery changes also passed all 107 client and
  migration tests.
- Package validation rejects mismatched application versions, missing CLR activation policy,
  and invalid old-product removal ordering.
- An isolated MSI failure probe executed the packaged recovery helper, restored a scratch
  preference, and retained its backup. Run `SnoopMCP.Installer/Test-UpgradeRecovery.ps1` with the
  built custom-action assembly and a new scratch directory to reproduce this check.
- An actual upgrade from 1.4.2 to candidate 1.4.3 completed successfully. Client configuration
  and the interaction preference survived byte for byte, and all 42 installed files matched the
  package. The running host reported commit `cfc6b8e73a1f9bb0c2333538d680c37fd2b9ebb3`.
- Live MCP discovery returned 30 tools and both canonical skill resources. Codex discovered both
  installed skills, and repeat registration preserved its parsed configuration.
- The installed scheduled task has unlimited runtime, allows battery operation, and requests
  three failure restarts at one-minute intervals. This is configuration verification, not an
  executed crash-recovery test.

## Required before merge or release

Use an isolated Windows user for destructive lifecycle checks. Preserve the user's normal
installation and other developers' configuration. For every applicable case, verify unrelated
client settings, server options, permissions, other skills, edited Snoop skills, personal skill
support files, logs, interaction preferences, and Start menu entries against the expected ownership
rules.

- [ ] Fresh installation and discovery of the canonical skills.
- [ ] Upgrade from a customized 1.4.2 installation, including legacy cleanup and state restoration.
- [ ] Repeated installation without configuration loss or duplicated registrations.
- [ ] Rollback and cancellation during the relevant upgrade phases, including old-product removal;
  restore preserved state and retain the backup after failure.
- [ ] Uninstallation removes only installer-owned files and registrations; local edits and unrelated
  content survive.
- [ ] Actual scheduled recovery after an unexpected host exit, with bounded restart attempts and
  useful diagnostics.
- [ ] Intentional tray Exit and CLI stop remain stopped rather than triggering recovery.

Check inspection ownership before any host stop or crash experiment. Never interrupt another
inspection session, launch a prohibited second application instance, or enable interaction for
qualification. Distinguish protocol/discovery checks from an actually executed WPF inspection and
use a screenshot when verifying appearance.
