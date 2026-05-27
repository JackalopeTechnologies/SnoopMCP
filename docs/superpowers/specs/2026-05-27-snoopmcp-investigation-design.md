# SnoopMCP — Initial Investigation

**Date:** 2026-05-27
**Status:** Decision-ready report; awaiting approval to move to implementation planning.
**Decision sought:** Approval of Approach B and the v1 read-only scope so we can move to a written implementation plan.

---

## 1. Executive summary

WPF styling bugs are disproportionately expensive because the runtime behavior of dependency-property resolution, style/template lookup, and binding evaluation is not visible from the source — it emerges from a value-precedence system, a resource-lookup walk, and live data flowing through `BindingExpression` instances. Existing tools (Snoop, the VS Live Visual Tree) expose this information through GUIs designed for humans; none produce structured output an LLM can consume.

**Recommendation: Approach B.** Reuse the cross-process injection machinery from [snoopwpf](https://github.com/snoopwpf/snoopwpf), but ship our own payload DLL that hosts an MCP server inside the target process and exposes WPF introspection as a small set of read-only, LLM-shaped tools. The parent host process is what an MCP client connects to; it relays JSON-RPC to the payload.

**Scope cap for v1:** read-only. No property writes, no method invocation, no scripting. Phase 2 considers reintroducing those once the read surface proves out.

**Runtime target:** modern .NET (8/9+). .NET Framework is explicitly out of scope.

---

## 2. Problem statement

Styling failures in WPF are hard to diagnose because the visible symptom is decoupled from the underlying cause by several layers of indirection:

- **Value precedence.** A `Background` brush may have been set by a local value, a style setter, a trigger, a template binding, an animation, or inherited from a parent. The XAML source shows one of these; the rendered output reflects the winner, and the loser is invisible.
- **Style resolution.** Whether a style is applied depends on an explicit `Style="{StaticResource …}"` reference, an implicit type-keyed lookup that walks up the logical tree, a `BasedOn` chain, and the theme dictionaries currently merged. A missing key, a mistyped name, or an unexpected implicit override can silently swap which style wins.
- **Template behavior.** A `ControlTemplate` may be missing a named part the control's code expects, or have a trigger that doesn't fire because its `Property` doesn't match the resolved DP.
- **Binding state.** Bindings fail silently by default. A path that doesn't resolve, a `DataContext` that's null, or a converter that throws produces a blank cell, not a stack trace.

Diagnosing these from the source costs hours. Live introspection during a running session — where the LLM can ask "what is the effective `Background` on this Border, and which source won?" — shortcuts the loop substantially.

**Scope:**

- Generic any-WPF-app (must inject from outside; no source modification required to the target)
- Modern .NET only (8/9+); .NET Framework out of scope
- MCP as the wire protocol — the artifact is an MCP server
- Read-only in v1

---

## 3. Tool landscape survey

| Tool | Maintained | Mechanism | Programmatic surface | Fit for LLM use |
|---|---|---|---|---|
| **[Snoop](https://github.com/snoopwpf/snoopwpf)** | Active | Out-of-process DLL injection (`ManagedInjector`) | PowerShell tab inside injected process — text I/O, not JSON | Poor as-is, excellent as a parts donor |
| **WPF Inspector** | Stale | Similar to Snoop | None designed for automation | Poor |
| **VS Live Visual Tree** | Active (in-VS) | Debugger-attached | None; GUI-mediated | Poor — requires VS attached, no MCP path |
| **XAML Spy / DevExpress** | Commercial | DLL injection | None public | Poor — closed source, licensing |
| **ClrMD (`Microsoft.Diagnostics.Runtime`)** | Active | External heap reader via diagnostics port | Fully programmatic | Architecturally interesting, see §4.C |

Snoop is the only mature tool that solves all the hard injection problems on .NET 8+: cross-process attach, AssemblyLoadContext isolation, x86/x64 split builds, Dispatcher marshaling, hwnd discovery for popups and adorners. Every other path either duplicates that work or compromises on capability. The remaining design question is *how much of Snoop to take.*

---

## 4. Architectural options

### A. Wrap Snoop as-is

Drive the existing Snoop binary externally. Two flavors: fork the GUI to add a headless mode, or send PowerShell scripts to its existing in-process script tab and parse the text output.

- **+** Maximum reuse; minimum new code.
- **−** Snoop's output is text formatted for humans. JSON serialization isn't free — we'd be writing parsers either way.
- **−** Coupling SnoopMCP to Snoop's GUI release cadence and internal types is fragile.
- **−** The PowerShell hosting was designed as a power-user feature, not an automation surface; there is no stability contract.

### B. Borrow Snoop's injection, ship our own payload *(recommended)*

Vendor (or git-submodule) Snoop's `ManagedInjector` and the small amount of bootstrap glue. Ship a payload DLL — `SnoopMCP.Payload.dll` — that the injector loads into the target. The payload hosts an MCP server speaking JSON-RPC over a named pipe; a separate `SnoopMCP.Host.exe` is what an MCP client connects to (stdio in, named-pipe to payload out).

- **+** Clean MCP boundary. Tool surface is designed for the three diagnostic priorities you picked, not retrofitted onto a GUI tool's outputs.
- **+** Structured output by construction. Every tool emits typed JSON.
- **+** Reuses Snoop's genuinely hard parts (injection, dispatcher, lifecycle) without inheriting parts that don't fit (GUI, edit affordances, PowerShell hosting).
- **+** Future-proof: writes, method invocation, scripting can be added as Phase 2 tools without re-architecting.
- **−** More code to own. Inspection logic, identity tracking, dispatcher marshaling discipline are all ours.
- **−** We track Snoop's injector evolution; vendoring means periodic re-syncs.

### C. ClrMD only — external heap inspection, no injection

Use `Microsoft.Diagnostics.Runtime` to attach to the target's diagnostics port and walk its managed heap from outside. No DLL injection.

- **+** No code runs in the target. Nothing can be corrupted.
- **−** Fundamentally read-only by construction — not just by choice — because we have no Dispatcher access.
- **−** **Most of our query priorities are unreachable.** `Style` resolution requires walking the resource tree and calling lookup methods. `BindingExpression` state requires reading internal flags whose semantics aren't documented for external readers. Value-precedence resolution requires the WPF property engine, which we'd have to reimplement against heap snapshots.
- **−** High engineering cost relative to capability delivered. Worth mentioning in the report; not viable as the recommendation.

### D. VS Live Visual Tree bridge

Drive Visual Studio externally; consume its Live Visual Tree.

- **−** Requires VS attached as a debugger. The tool isn't standalone.
- **−** No clean MCP story — we'd be screen-scraping a GUI through an extension.
- **−** Dismissed.

### Scoring against priorities

| Criterion (weighted) | A (wrap Snoop) | **B (borrow injection)** | C (ClrMD) | D (VS) |
|---|---|---|---|---|
| Effective DP value + precedence trace | ✓ via text parsing | **✓ native** | ✗ requires reimpl | ✓ via VS UI scrape |
| Style/template resolution | ✓ via text parsing | **✓ native** | ✗ very hard | ✓ via VS UI scrape |
| Binding state + errors | ✓ partial | **✓ native** | ✗ internal state | ✓ partial |
| Structured (JSON) output | ✗ | **✓** | ✓ | ✗ |
| MCP-shaped surface | ✗ | **✓** | ✓ | ✗ |
| Maintenance footprint | Medium (Snoop coupling) | **Medium (vendored injector)** | High | High (VS extension) |
| Read-write extensibility | Possible | **Clean** | Impossible | Awkward |

---

## 5. Recommendation — Approach B

**Adopt Approach B.** Specifically:

- **Vendor by copy** Snoop's `ManagedInjector` and the minimum bootstrap glue needed to load a managed DLL into a running .NET 8+ WPF process. Copy the source into our tree with attribution (see §9 for the licensing path). Snoop doesn't publish these as a library; pulling source we can audit is safer than reaching into a GUI app's internals at runtime.
- **Ship two binaries:**
  - `SnoopMCP.Host.exe` — the MCP server entry point. Speaks MCP over stdio to the client. Owns the lifecycle of an attached session. Translates MCP tool calls into requests on a named pipe to the payload.
  - `SnoopMCP.Payload.dll` — loaded into the target process by the injector. Hosts the pipe server. Marshals every WPF call onto the target's Dispatcher. Owns the element-identity registry and the introspection logic.
- **Communication:** JSON-RPC over a named pipe. The host process is the MCP boundary; the pipe is an internal implementation detail the LLM never sees.

**Risks called out explicitly:**

- Snoop's `ManagedInjector` evolves; we accept a vendoring re-sync cost. Budget one re-sync per year of active Snoop development.
- Injecting into a WPF process running as Administrator from a non-Administrator host will fail. Document the elevation requirement; don't try to work around it silently.
- Payload load failures (assembly conflicts in the target's load context) are the most likely v1 failure mode. The host must surface these clearly with the target's runtime version, the payload's runtime version, and the specific assembly that conflicted.

---

## 6. v1 scope

### Navigation is the hard part

WPF tree navigation is the place v1 is most likely to fail in practice. Visual and logical trees diverge; real windows have thousands of nodes most of which have no name; popups and adorners live in separate visual trees from the controls that opened them; `ItemsControl`s virtualize; and template children sit visually inside but not logically under their host. Brute-force tree walking blows the LLM's context window before reaching anything useful. v1 therefore invests deliberately in navigation primitives — rich predicates, hit-testing, parent traversal, path strings, virtualization-aware enumeration, popup back-references — so the LLM can locate elements by *shape* and *content*, not by walking. Search-first, walk-second.

### In

- Enumerate visual roots (all active `PresentationSource`s — windows, popups, tooltip layers, adorner layers), each annotated with the element that opened it where applicable
- Walk visual and logical trees from any root; navigate upward via visual / logical parent and out of templates via `TemplatedParent`
- Locate elements via rich predicates: type, x:Name, AutomationId, visible-text-contains, property-value-equals, has-ancestor, has-descendant, in-template-of
- Hit-test a root-relative point on a visual root and return the deepest element there
- Emit canonical path strings on every element description; resolve a path string back to an element under a given root
- Per-node identity returns type, x:Name, AutomationId, visible bounds (root-relative), short visible-text snippet, `IsInTemplate` flag, and `HasBindingErrors` flag
- Virtualization-aware `getChildren` — realized children plus realized/virtualized counts; never silently hides items
- Describe DataContext shape at any node (type, base types, interfaces, declared CLR properties — names and types only)
- DP introspection: list DPs on an element, read effective DP value with **full precedence trace** (which source won, what the losers were, in order)
- CLR property reads on the DataContext via dotted paths (`SelectedCustomer.Address.Street`)
- Style resolution: applied `Style` source/key, full `BasedOn` chain, all setters, trigger summary
- Template resolution: applied `ControlTemplate`, its element tree with stable ids, named template parts
- Binding inspection: source object, path, mode, resolved source, current value, `BindingExpression` state, recent binding trace lines if available

### Out (deferred to Phase 2)

- **Writes** — `setDependencyProperty`, `setDataContextPath`. The Snoop "try changing it" feature. Defer to validate the read surface first.
- **Method invocation** — `invokeMethod`, `executeCommand`. Biggest footgun; needs guardrails design.
- **Scripting** — a `runScript` tool that evaluates arbitrary C#/DLR in the target. Maximum power, maximum risk; needs a security model.
- **Cross-process discovery** — `listWpfProcesses`. Orthogonal; user can `Get-Process` for v1.
- **Persistent reattach** across app restarts.
- **Snapshots/diffs** — capturing a tree state and comparing two snapshots. Useful eventually; not v1.

---

## 7. Proposed MCP tool surface

Seventeen tools, grouped into four buckets. Every tool is read-only. The navigation bucket is the largest because LLM-shaped exploration is search-first, not walk-first (see §6).

### Attach / discovery

```text
attach(pid: int) -> { sessionId, processName, runtimeVersion,
                      frameworkVersion, bitness }
detach() -> { ok: bool }
listVisualRoots() -> [ { rootId,
                         kind: "Window"|"Popup"|"Tooltip"|"Adorner"|"Other",
                         hwnd, title, rootElementId,
                         openedBy: elementId | null } ]
```

`openedBy` carries the element id that owns / opened the secondary root (the ComboBox for its dropdown, the host for a Tooltip, etc.). Null for top-level windows.

### Tree navigation

```text
describeElement(id) ->
    { type, name, automationId,
      bounds: { x, y, width, height },          // root-relative
      visibleText,                              // capped at ~200 chars
      isInTemplate, hasBindingErrors,
      path,                                     // canonical path string
      childCount, dataContextType, hashCode, isAlive }

getChildren(id, tree: "visual"|"logical") ->
    { children: [ describeElement ],
      virtualization: { isVirtualizing: bool,
                        realizedItems: int,
                        totalItems: int | null } | null }

getParent(id, tree: "visual"|"logical") -> describeElement | null

getTemplatedParent(id) -> describeElement | null

findElements(rootId, predicate) -> [ describeElement ]
   predicate fields (all optional, AND-combined):
     type             string                substring match on full type name
     name             string                exact x:Name match
     automationId     string                exact AutomationProperties.AutomationId
     textContains     string                case-insensitive in visibleText
     propertyEquals   { property, value }   DP current value equals
     hasAncestor      predicate             recursive
     hasDescendant    predicate             recursive
     inTemplateOf     predicate             TemplatedParent matches

hitTest(rootId, x: double, y: double) -> describeElement | null
   coordinates are root-relative

resolvePath(rootId, pathString) -> describeElement | error
   path grammar:
     /TypeName[Name='X', AutomationId='Y', Text='Z'][n]/...
   each step matches by type + optional attribute predicates + optional index
```

### WPF-aware introspection (targeted)

```text
listDependencyProperties(id) -> [ { name, ownerType, valueType, isAttached } ]

getDependencyProperty(id, propName) ->
    { name, currentValue, currentValueType,
      precedence: [ { source: "Local"|"Animated"|"StyleSetter"|"TemplateTrigger"|...,
                      value, valueType, sourceDescription } ],
      winningSource }

resolveStyle(id) ->
    { appliedStyleKey, appliedStyleSource,
      basedOnChain: [ { key, source } ],
      setters: [ { property, value } ],
      triggers: [ { kind, condition, setters: [ ... ] } ] }

resolveTemplate(id) ->
    { templateType, templateKey, templateSource,
      templateTree: <id-bearing element tree>,
      namedParts: [ { partName, partType, elementId } ] }

inspectBinding(id, propName) ->
    { bindingPath, mode, resolvedSourceType, resolvedSourceHashCode,
      currentValue, state: "Active"|"Detached"|"PathError"|"SourceUnreachable"|...,
      recentTraceLines: [ { timestamp, severity, message } ] }
```

### DataContext / CLR introspection (targeted)

```text
describeDataContext(id) ->
    { typeName, namespace, baseTypes: [ ... ], interfaces: [ ... ],
      declaredProperties: [ { name, type, canRead, canWrite } ] }

readDataContextPath(id, path) ->
    { value, valueType, pathReachable, failureAt? }
```

**Total: 17 tools.** Navigation grew from 3 → 7 in response to the challenges in §6. Other buckets unchanged. Compact enough to fit in the LLM's working set; each call returns useful, bounded JSON.

---

## 8. Architecture sketch

### Process model

```text
                stdio (MCP / JSON-RPC)
   [ MCP client ] <-------------------> [ SnoopMCP.Host.exe ]
                                                |
                                                |  named pipe (JSON-RPC, internal)
                                                v
                                       [ target WPF process ]
                                       [ SnoopMCP.Payload.dll  ] <-- injected by
                                                ^                    vendored
                                                |                    ManagedInjector
                                                |  Dispatcher.Invoke
                                                v
                                       [ live WPF objects ]
```

### Element identity

The payload allocates stable integer ids on first description of each element. The id maps to a `WeakReference` to the live `DependencyObject`. The LLM passes ids back in subsequent calls. When the underlying element is GC'd, `describeElement` returns `isAlive: false`; downstream calls return a structured `ElementExpired` error rather than throwing. Ids are stable for the lifetime of the session, not across reattaches.

### Threading discipline

Every WPF read marshals onto the target's UI Dispatcher via `Dispatcher.Invoke`. The pipe handler thread never touches WPF objects directly. Inspection methods are written defensively: a stuck UI thread becomes a per-call timeout (default 5s), surfaced as `DispatcherTimeout`, not a hang of the MCP server.

### Failure modes the host must distinguish

| Failure | Surface |
|---|---|
| Attach to non-existent / non-WPF / mismatched-bitness process | `AttachFailed` with reason |
| Payload load conflict in target's AssemblyLoadContext | `PayloadLoadFailed` with conflicting assembly, both versions |
| Dispatcher timeout on a single tool call | `DispatcherTimeout`, session remains attached |
| Target process exited | `SessionLost`, session terminated |
| Elevation mismatch (target Admin, host not) | `AccessDenied`, with elevation guidance |
| Element id expired (GC'd) | `ElementExpired` per call |

### Logging

Payload emits structured log lines to the host over a side channel on the same pipe. Host forwards them to stderr (or an MCP `notification` if the client supports it). No logging into the target's own `Trace` listeners — that pollutes the app under test.

---

## 9. Open questions and Phase 2

**Phase 2 capability candidates (in rough priority order):**

1. **Writes.** `setDependencyProperty(id, propName, value)` and `setDataContextPath(id, path, value)`. The "try changing it" feature is genuinely the most valuable thing Snoop offers. Add a confirmation/preview pattern so the LLM has to explicitly opt into mutation.
2. **Method invocation.** `executeCommand(id, parameter)` for `ICommand`s is reasonably safe and high-value (lets the LLM trigger a command and observe the effect). Arbitrary `invokeMethod` is more dangerous; treat separately.
3. **Scripting.** `runScript(csx)` would unlock arbitrary diagnosis. Needs a security model. Possibly gated behind an explicit `--allow-scripting` flag on the host.
4. **Cross-process discovery.** `listWpfProcesses()` — easy, removes friction.
5. **Persistent reattach.** Survive target restarts in a watched mode.
6. **Snapshots and diffs.** Capture full tree state to disk; diff two snapshots. Useful for "what changed between window-shown and the moment the bug appears."
7. **Binding trace recording.** Subscribe to `PresentationTraceSources.DataBindingSource` and surface binding errors as MCP notifications in real time.
8. **Web inspector UI.** Host-side ASP.NET Core endpoint serving a browser-based tree explorer that reuses the existing 17 MCP tools internally. Three useful tiers: (A) tool-explorer page that proxies any tool and renders the JSON response; (B) full tree + properties browser with `findElements`-backed search; (C) live inspector with WebSocket updates, hover-to-highlight (requires payload adorner support), and per-element screenshot capture (requires payload `RenderTargetBitmap` support). Tier A is small (~2 tasks); Tier C requires payload changes that ride with the existing Phase 2 mutation work. Localhost only.

**Open questions:**

- **Injector source of truth.** Vendor by copy, vendor by git submodule, or reach into Snoop via reflection on a NuGet package? Recommendation: copy with attribution and a `THIRD_PARTY_NOTICES` entry, contingent on Snoop's current license permitting it (verify before the smoke test in §10). Snoop has historically used a permissive license; copying with notice is the simplest path if that holds.
- **Bitness.** The host needs to launch the right architecture of payload (x64 / arm64 / x86). v1: support x64 only, error clearly on mismatch.
- **Multiple targets per host.** v1: one attached target at a time. Multi-target is Phase 2 and probably not worth the complexity.
- **MCP transport on the host.** stdio in v1 (standard for MCP servers). Don't introduce HTTP/SSE unless needed.

---

## 10. Next steps

1. **Approve this report.** Confirm Approach B and the v1 scope, or request changes.
2. **Validate the injector on .NET 8+ with a smoke test.** Before committing to the architecture, spike a 1-day test: take Snoop's current main, attach to a stock .NET 8 WPF app, confirm the injection path still works. If Snoop's own builds attach to your sample app, this is settled.
3. **Move to writing-plans.** Produce a step-by-step implementation plan with task breakdown, build sequence, and verification points.

The validation in step 2 is the only thing that could meaningfully change the recommendation; everything else is execution.
