# SnoopMCP v1.1 — Walkthrough Design

**Date:** 2026-05-29
**Status:** Decision-ready; awaiting approval to move to implementation planning.
**Decision sought:** Approval of the doc structure, capture mechanism, and scene script so we can write the executable plan.

---

## 1. Goal

A single `docs/walkthrough.md` that demonstrates every v1.1 read-only tool against the real `SampleWpfApp`. The doc reads top-to-bottom for a human and feeds straight into an LLM as in-context demonstration. Every tool call response in the doc is captured from a real attached session against `SampleWpfApp`, so the JSON is ground truth rather than illustrative fiction.

The walkthrough's job is to make SnoopMCP **legible**: a reader (human or LLM) finishes it understanding what each tool returns, why one would reach for it, and how the tools chain to diagnose a real problem.

---

## 2. Scope

**In scope:**
- `docs/walkthrough.md` — the deliverable doc.
- A skip-by-default capture test that writes `tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json` from a live attached session.
- Whatever minimal tweaks `SampleWpfApp` needs so each scene has something deterministic to point at (a named Save button, the broken-binding label, an addressable virtualized ListBox, etc. — see §6).

**Out of scope:**
- A PowerShell driver script (`scripts/walkthrough.ps1`). Declined in brainstorming.
- An "LLM-tutor prompt" file. Declined in brainstorming.
- CI integration for the capture fact. It runs by hand when the sample changes.
- Shipping `SampleWpfApp` to end users via the installer — that is **A3**'s deliverable, not A2's. The walkthrough author writes as though the sample is on the reader's machine; A3 makes that true.

---

## 3. Audience and format

**Audience:** dual — a human reading `docs/walkthrough.md` standalone, and an LLM ingesting it as in-context demonstration of the tool surface. Both expect the same content; the format optimises for one read.

**Format:** **hybrid — scene framing plus transcript**. Each tool gets a mini-scene:

1. One or two sentences of prose framing ("Doug wants to know which style is winning on the Save button.").
2. The tool call shown as a fenced JSON block: tool name + arguments.
3. The captured response, trimmed for readability with `…` elisions where deep trees would bury the point. Full untrimmed JSON lives in `tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json`.
4. One or two sentences anchored to a specific field of the response ("The `winningSource` of `StyleSetter` confirms the danger style is what makes it red, not a local set.").

**Tone:** debugging-with-a-colleague. The opening "drat, what's the PID?" stumble is part of the show — it motivates `listWpfProcesses` before `attach`.

---

## 4. Section structure

The doc is organised by tool category. Each section opens with one paragraph framing the capability before the per-tool mini-scenes.

1. **Opening** (~½ page) — scene setter. Host is running, SampleWpfApp is alive, MCP client is connected. Notes that element IDs in the transcript will differ on the reader's machine and that the response excerpts are trimmed.
2. **Discovery & lifecycle** — `listWpfProcesses` (the PID stumble), `attach`. `detach` is deferred to the closing.
3. **Orientation** — `listVisualRoots`, `describeElement`.
4. **Tree navigation** — `getChildren` (the `visual` vs `logical` split is demonstrated against a `ContentControl` with a DataTemplate — visual surfaces the templated content, logical sees the leaf), `getParent`, `getTemplatedParent` (climb out of `CustomButtonTemplate`).
5. **Search & spatial** — `findElements` (by name, then by `textContains`), `hitTest`, `resolvePath`.
6. **DataContext** — `describeDataContext` (CLR shape), `readDataContextPath` (dotted walk).
7. **Dependency properties** — `listDependencyProperties`, `getDependencyProperty` (precedence trace on `Background`).
8. **Styles & templates** — `resolveStyle` (the `Danger→Primary→Base` `BasedOn` chain), `resolveTemplate` (`PART_Border` / `PART_Content` named parts and the `IsMouseOver` trigger).
9. **Bindings** — `inspectBinding` (the broken `Address.Country.Name` `PathError`), `listBindings` (wide audit under DetailPane).
10. **Snapshot & wrap-up** — `exportXaml` of the DetailPane subtree, then `detach` in one line.
11. **Closing** (~½ page) — recap of the tour, pointer to `samples/SampleWpfApp` source so the reader can poke around, and a one-liner noting the same flow works against the reader's own WPF app.

20 tools across 9 mini-section bodies. Approximate length: 10–15 minutes of human reading; ~6–10 KB of in-context material for an LLM.

---

## 5. Capture mechanism

A new test file `tests/SnoopMCP.IntegrationTests/WalkthroughCaptureTests.cs`:

- **Single `[Fact]`**, `CaptureWalkthroughTranscript`, marked `Skip = "Manual capture; run by hand to refresh walkthrough.md. See test XML doc for the command."` so normal `dotnet test` runs never execute it.
- **Owns its own `IAsyncLifetime`** rather than sharing with `EndToEndTests` — the capture walks a deliberate, scripted path that differs from the gate test's shape-only sweep, and decoupling means test ordering / parallelism cannot affect the capture.
- **Walks the scenes in document order** so the resulting JSON file is grouped the way the doc consumes it.
- **Writes** `tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json` — kept beside its producer so refresh-the-doc is a single, atomic operation. The doc author cherry-picks excerpts into `docs/walkthrough.md` by hand.
- **Transcript schema**: an array of `{ scene: string, tool: string, request: object, response: object }` records, written with `WireSerializer.JsonOptions` so the formatting matches what an MCP client would actually see.
- **The transcript file is committed to git**. It is a curated artifact, not generated build output: the doc references specific fields in specific responses, so committing the JSON pins what those references mean. Regenerating is a deliberate refresh, not a build step, and the diff against the committed copy is itself a useful signal when SampleWpfApp or the payload changes shape.

**The popup-root scene** needs the ComboBox dropdown open so `listVisualRoots` sees a popup root in addition to the main window. The capture fact opens it via in-box **UI Automation** (`System.Windows.Automation.AutomationElement` + `ExpandCollapsePattern`) before the popup-related calls, then collapses it after. UIA is in-box .NET, adds no NuGet dependency, and keeps the capture script self-contained. This is the **only** capture step that reaches the running app via a path other than the MCP surface — the doc explicitly notes that the reader would do this manually (click the ComboBox open) before running the popup scene themselves.

**Refresh command** (documented in the test's XML doc and in `docs/walkthrough.md`'s authoring note):

```text
dotnet test tests/SnoopMCP.IntegrationTests/SnoopMCP.IntegrationTests.csproj `
    --filter FullyQualifiedName~CaptureWalkthroughTranscript `
    -- xunit.execution.RunSkippedTests=true
```

(Exact incantation locked in during implementation — xUnit v3 may need a slightly different flag.)

---

## 6. SampleWpfApp fidelity check

Before the capture fact lands, the implementation pass verifies `SampleWpfApp` exposes everything the scene script depends on:

| Scene depends on | Required in sample |
|---|---|
| `findElements({name: "SaveButton"})` | A button with `x:Name="SaveButton"` (or whatever the existing name is — check before scripting) |
| The broken-binding label | A `TextBlock` bound to `Address.Country.Name` whose source path doesn't resolve |
| `resolveStyle` BasedOn chain | Three styles named (or implicitly typed) `Danger`, `Primary`, `Base` with `BasedOn` pointing up the chain, applied to a button |
| `resolveTemplate` named parts | A `Style.Setter` of `Template = CustomButtonTemplate` whose template has `PART_Border` and `PART_Content` and an `IsMouseOver` trigger |
| Virtualized ListBox | A `ListBox` with `VirtualizingStackPanel.IsVirtualizing="True"`, ≥1000 items, addressable (named or located by predicate) |
| `getChildren` visual/logical contrast | A `ContentControl` whose `ContentTemplate` injects intermediate Visuals — the visual tree shows the template's content, the logical tree shows the bare ContentControl |
| Popup-root scene | A `ComboBox` whose dropdown is openable via UIA |

The implementation pass starts by reading `samples/SampleWpfApp` and reconciling against this table. Any "tell" that's missing or named differently is either added to the sample (small XAML change) or has the scene re-pointed at what's actually there. The reconciliation result is recorded in the first implementation task.

---

## 7. Decisions made (not open questions)

These were open in the verbal design; resolved here so the spec is implementable as-is.

- **Popup mechanism:** UIA inside the capture fact (§5).
- **DataTemplate / visual+logical divergence:** folded into the `getChildren` mini-scene as a `visual` vs `logical` contrast, not a standalone scene.
- **`detach`:** one-line mention in the closing, not its own scene.
- **Trim policy:** response excerpts trimmed to keep each block under ~20 lines where possible, with `…` elisions clearly marked. Full JSON in the transcript file is the source of truth.
- **ID handling:** element IDs are quoted verbatim from the capture run; the opening explicitly notes that the reader's IDs will differ.

---

## 8. Acceptance criteria

The A2 work is done when:

1. `docs/walkthrough.md` exists and exercises all 20 v1.1 tools (the 19 inspection tools plus `listWpfProcesses`), each with a captured-from-reality response excerpt.
2. `tests/SnoopMCP.IntegrationTests/WalkthroughCaptureTests.cs` exists, contains the skip-by-default `CaptureWalkthroughTranscript` fact, and — when un-skipped and run against a live SampleWpfApp — writes `walkthrough-transcript.json` with the full scene set.
3. The capture fact passes when run by hand (the verification step is "remove `Skip`, run it, see the JSON file, put the `Skip` back").
4. `dotnet build SnoopMCP.sln -c Debug -p:TreatWarningsAsErrors=true` stays zero-warning.
5. The standard test suite still runs in 13 seconds without the capture fact executing (Skip-by-default does its job).
6. The walkthrough doc renders correctly on GitHub and the trimmed excerpts read as natural English plus credible JSON.

---

## 9. Known limitations

- **No automated drift check** between `walkthrough-transcript.json` and `docs/walkthrough.md`. If a captured field is referenced in the doc and the next capture renames or removes it, the doc still reads OK but is technically wrong. The reconciliation step lives in the doc-author's head when re-running the capture; a future enhancement could parse the doc's referenced field names and assert they exist in the JSON. Not worth building now.
- **The capture fact requires a desktop session.** It spawns SampleWpfApp and drives it with UIA, which means it cannot be run from a session-0 CI agent. Refresh is a desktop-developer action by design.

---

## 10. Out of scope for v1.1 (recorded so we don't drift)

- A `scripts/walkthrough.ps1` driver. Could land in v1.2 if there's demand to run the walkthrough live against a fresh sample without xUnit in the loop.
- An "LLM-tutor prompt" markdown file. Could land if early users report the walkthrough is too dense to consume without scaffolding.
- A video / animated GIF version. Out of scope and not on any roadmap.
- Translating the walkthrough to address a generic non-`SampleWpfApp` target. The doc is intentionally sample-driven; the generic-target story lives in the README quickstart, not here.
