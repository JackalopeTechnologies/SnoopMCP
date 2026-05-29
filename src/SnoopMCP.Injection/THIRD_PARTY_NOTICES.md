# Third-Party Notices

`SnoopMCP.Injection` builds its injector binaries from the **snoopwpf** project,
referenced as a git submodule under `external/snoopwpf/`. No snoopwpf source is
copied into this tree; the submodule pin records the exact commit we build
against, and the build output is staged (not committed) under this project's
`injector/` output folder.

- Upstream repository: https://github.com/snoopwpf/snoopwpf
- Our fork: https://github.com/JackalopeTechnologies/snoopwpf
- Pinned branch: `snoopmcp`
- Pinned commit: `81a5cd67acb17978585e6f94de633cee81a5be19`
- License: **Microsoft Public License (Ms-PL)** — see `external/snoopwpf/License.txt`

The Ms-PL permits use, reproduction, and distribution (including of compiled
binaries) provided the license terms travel with any distributed copies. The
built injector binaries (`Snoop.InjectorLauncher.*.exe`,
`Snoop.GenericInjector.*.dll`, and their dependencies) are subject to the Ms-PL.

## What we build and why

For v1, `snoopmcp` is identical to upstream `develop` at the pinned commit — no
patches. The fork exists so SnoopMCP can pin a known-good commit and, if needed,
land .NET-version or Jackalope-specific patches on the `snoopmcp` branch without
waiting on upstream review.

SnoopMCP uses **only** Snoop's cross-process injection machinery:

- `Snoop.GenericInjector` — native C++ DLL that hosts the CLR in the target via
  `ICLRRuntimeHost::ExecuteInDefaultAppDomain`.
- `Snoop.InjectorLauncher` — managed launcher exe that loads the native DLL into
  the target process.

None of Snoop's GUI, inspection, or PowerShell code is used.

## Maintenance

The fork carries:

- `develop` — tracks upstream `snoopwpf/snoopwpf:develop`.
- `snoopmcp` — our pin branch (what the submodule tracks).

To sync upstream into the fork's `snoopmcp`:

    git -C external/snoopwpf fetch upstream
    git -C external/snoopwpf checkout develop
    git -C external/snoopwpf merge upstream/develop
    git -C external/snoopwpf push origin develop
    git -C external/snoopwpf checkout snoopmcp
    git -C external/snoopwpf merge develop
    git -C external/snoopwpf push origin snoopmcp

To bump SnoopMCP's submodule pin afterward:

    git submodule update --remote external/snoopwpf
    git add external/snoopwpf
    git commit -F msg.txt
