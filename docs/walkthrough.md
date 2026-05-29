# SnoopMCP walkthrough — a guided tour of all 20 tools

This is an end-to-end tour of SnoopMCP's read-only inspection tools, told as a
debugging session. **Doug** is chasing a styling-and-binding bug in a WPF app
and has an MCP client (Claude) wired to SnoopMCP. The client drives the tools;
Doug just describes what he wants in plain English and reads back the JSON.

The target is `samples/SampleWpfApp` — a small customer browser with a few bugs
planted on purpose: a `DeleteButton` whose colour comes from a three-level
`BasedOn` style cascade, a `SaveButton` with a custom control template, a
`DetailPane` whose content is data-templated, and a `BrokenBinding` text block
bound to a path that does not exist. The same flow works against any WPF app;
SampleWpfApp is just a convenient target with known faults.

## How to read this document

- The host is already running and bound to `http://127.0.0.1:6300/mcp`
  (localhost only — see the [README](../README.md) for build/run). The MCP
  client is connected. SampleWpfApp is running.
- Every response block below is a **real capture**, only lightly trimmed for
  length. Long arrays and deep trees are cut with a clearly marked `…`. The
  full, untrimmed JSON for all 25 captured calls lives in
  [`tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json`](../tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json).
- **Element ids are per-run.** The ids you see here (`4` for the Save button,
  `2` for the detail pane, `160` for the Delete button, and so on) came from one
  specific run. On your machine the ids will be different — discover them with
  `findElements`/`listVisualRoots` rather than copying ids from this page.

---

## 1. Discovery & lifecycle

Doug knows the app is running but not its process id, and the host runs on his
machine — the client cannot see his Task Manager. The first job is to find an
attachable WPF process, then open a session against it.

### `listWpfProcesses` — "drat, I need a PID I can't see"

Doug just says "find the WPF apps I can attach to." This tool is host-side and
needs no session, so it works before anything is attached.

```json
{ "tool": "listWpfProcesses", "arguments": {} }
```

```json
{
  "processes": [
    {
      "pid": 26764,
      "processName": "SampleWpfApp",
      "mainWindowTitle": "SnoopMCP Sample App",
      "bitness": "x64",
      "frameworkVersion": "10.0.826.23019",
      "attachable": true
    }
  ]
}
```

`attachable: true` and `bitness: "x64"` are the green light: SnoopMCP v1 only
attaches to x64 WPF targets, and this one qualifies. Doug has his PID: `26764`.

### `attach` — open the session

"Attach to 26764." This injects the payload into the target and opens a session.

```json
{ "tool": "attach", "arguments": { "pid": 26764 } }
```

```json
{
  "sessionId": "snoopmcp-668754cb277740f692b23880fac54fb3",
  "processName": "SampleWpfApp",
  "frameworkVersion": "10.0.826.23019",
  "bitness": "x64"
}
```

The returned `sessionId` confirms the payload loaded and the named pipe is live.
From here on, every tool call is marshalled onto the target's UI dispatcher.

---

## 2. Orientation

With a session open, Doug gets his bearings: what top-level roots exist, and
what is the main window made of. At the end of this section he revisits the root
list with the theme dropdown open, which surfaces a second, popup root.

### `listVisualRoots` — what windows/popups exist

```json
{ "tool": "listVisualRoots", "arguments": {} }
```

```json
{
  "roots": [
    {
      "rootId": 0,
      "kind": "Window",
      "hwnd": 28578460,
      "title": "SnoopMCP Sample App",
      "rootElementId": 1
    }
  ]
}
```

One root for now: a `Window` whose `rootElementId` is `1`. That `1` is the handle
Doug feeds into searches and `describeElement`.

### `describeElement` — identity of the root

"Describe element 1."

```json
{ "tool": "describeElement", "arguments": { "id": 1 } }
```

```json
{
  "id": 1,
  "type": "MainWindow",
  "bounds": { "x": 0, "y": 0, "width": 900, "height": 600 },
  "visibleText": "Search: Light Customer 0000 customer0000@example.com …",
  "isInTemplate": false,
  "hasBindingErrors": false,
  "path": "/MainWindow",
  "childCount": 1,
  "dataContextType": "SampleWpfApp.ViewModels.MainViewModel",
  "isAlive": true
}
```

The node-level summary: it's the `MainWindow`, its `dataContextType` is
`MainViewModel`, and `hasBindingErrors` is `false` at this level (the broken
binding lives deeper, inside the detail pane's template). The `path` value is the
canonical string `resolvePath` round-trips on.

### Revisit with the dropdown open — the Popup root

A human debugging this would click the `ThemeCombo` dropdown open, then list
roots again. SnoopMCP v1 is read-only and cannot click, so the capture used UI
Automation to open the combo; with it open, `listVisualRoots` now reports a
*second* root.

```json
{ "tool": "listVisualRoots", "arguments": {} }
```

```json
{
  "roots": [
    { "rootId": 0, "kind": "Window", "hwnd": 28578460, "title": "SnoopMCP Sample App", "rootElementId": 1 },
    { "rootId": 1, "kind": "Popup", "hwnd": 77009516, "rootElementId": 165, "openedBy": 25 }
  ]
}
```

The new entry has `kind: "Popup"` and an `openedBy` field pointing at the element
id (`25`) that owns it — the dropdown. Popups render in their own HWND/visual
root, which is exactly why a flat window walk misses them.

### `getChildren` into the popup root

"Walk the visual children of the popup root, 165."

```json
{ "tool": "getChildren", "arguments": { "id": 165, "tree": "visual" } }
```

```json
{
  "children": [
    {
      "id": 166,
      "type": "Decorator",
      "bounds": { "x": 0, "y": 0, "width": 165, "height": 66.88 },
      "visibleText": "Light Light Dark Dark High Contrast High Contrast",
      "path": "/PopupRoot/Decorator",
      "childCount": 1,
      "dataContextType": "SampleWpfApp.ViewModels.MainViewModel",
      "isAlive": true
    }
  ]
}
```

The popup's first visual child is a `Decorator` whose `path` begins
`/PopupRoot/…` (a different root prefix from `/MainWindow/…`), and whose
`visibleText` shows the three theme items. The popup contents are reachable with
the same tools as the main window — you just have to start from the popup's own
`rootElementId`.

---

## 3. Tree navigation

WPF has two trees — visual (every rendered node) and logical (the author's
content model) — and they diverge most sharply around data-templated content.
The detail pane (`id: 2`) is the perfect specimen: its `Content` is a `Customer`
object rendered through a `ContentTemplate`.

### `getChildren` — visual vs logical divergence

First the **visual** tree of the detail pane:

```json
{ "tool": "getChildren", "arguments": { "id": 2, "tree": "visual" } }
```

```json
{
  "children": [
    {
      "id": 3,
      "type": "ContentPresenter",
      "isInTemplate": true,
      "dataContextType": "SampleWpfApp.ViewModels.Customer",
      "path": "/MainWindow/…/ContentControl[Name='DetailPane']/ContentPresenter",
      "childCount": 1,
      "isAlive": true
    }
  ]
}
```

Now the **logical** tree of the *same* element:

```json
{ "tool": "getChildren", "arguments": { "id": 2, "tree": "logical" } }
```

```json
{ "children": [] }
```

That empty array is the whole point. Visually, the detail pane has a
`ContentPresenter` child (with `isInTemplate: true` and a `Customer`
`dataContextType`) that the template generated to render the customer.
Logically, it has *no* element children at all — its `Content` is a plain data
object, not a child UIElement. When "where did this control go?" doesn't add up,
this is usually why: you're looking in the wrong tree.

### `getParent` — climb back up

"Who is the visual parent of 3?"

```json
{ "tool": "getParent", "arguments": { "id": 3, "tree": "visual" } }
```

```json
{
  "parent": {
    "id": 2,
    "type": "ContentControl",
    "name": "DetailPane",
    "isInTemplate": false,
    "dataContextType": "SampleWpfApp.ViewModels.MainViewModel",
    "path": "/MainWindow/…/ContentControl[Name='DetailPane']",
    "childCount": 1,
    "isAlive": true
  }
}
```

Back to `DetailPane` (`id: 2`). Note the `dataContextType` flips back to
`MainViewModel` here — the `Customer` context only begins inside the presenter.

### `getTemplatedParent` — climb out of a template

A node generated by a control template points back at the templated control via
its templated parent. Here Doug climbs from inside the Save button's template
(`id: 5`, its `PART_Border`) back to the button itself.

```json
{ "tool": "getTemplatedParent", "arguments": { "id": 5 } }
```

```json
{
  "templatedParent": {
    "id": 4,
    "type": "Button",
    "name": "SaveButton",
    "isInTemplate": false,
    "path": "/MainWindow/…/Button[Name='SaveButton']",
    "childCount": 1,
    "isAlive": true
  }
}
```

`getParent` would keep walking the visual chain; `getTemplatedParent` jumps
straight out to the `SaveButton` (`id: 4`) that owns the template instance.

---

## 4. Search & spatial

Walking the tree by hand is fine for a few hops. To *find* things, Doug searches
by name, by visible text, by screen coordinate, or round-trips a path string.

### `findElements` by name

"Find the element named SaveButton, starting from root 1."

```json
{ "tool": "findElements", "arguments": { "rootId": 1, "predicate": { "name": "SaveButton" } } }
```

```json
{
  "matches": [
    {
      "id": 4,
      "type": "Button",
      "name": "SaveButton",
      "bounds": { "x": 741.5, "y": 512.4, "width": 52.6, "height": 32.6 },
      "path": "/MainWindow/…/Button[Name='SaveButton']",
      "isAlive": true
    }
  ]
}
```

One match: the `SaveButton`, `id: 4` — the same id used in the template scenes.
The predicate also accepts `type`, `automationId`, DP-value, and structural
("has-ancestor"/"in-template-of") forms.

### `findElements` by text

"Find anything showing 'Springfield'." Text search is fuzzy by node — every
element whose visible text contains the string matches, from the container down
to the leaf.

```json
{ "tool": "findElements", "arguments": { "rootId": 1, "predicate": { "textContains": "Springfield" } } }
```

```json
{
  "matches": [
    { "id": 2,   "type": "ContentControl",   "name": "DetailPane", "isInTemplate": false },
    { "id": 3,   "type": "ContentPresenter", "isInTemplate": true,  "dataContextType": "SampleWpfApp.ViewModels.Customer" },
    { "id": 145, "type": "StackPanel",       "isInTemplate": true,  "childCount": 7 },
    { "id": 150, "type": "TextBlock",        "isInTemplate": true,  "visibleText": "Springfield" }
  ]
}
```

Four hits, narrowing from the pane (`id: 2`) down to the actual `TextBlock`
(`id: 150`) whose `visibleText` is exactly `"Springfield"`. (`textContains`
only searches each element's visible text, capped at ~200 chars — see the
README's v1 limitations.)

### `hitTest` — deepest visual at a point

"What's at screen point (50, 50)?" Useful when you can see a pixel misbehaving
but don't know which element owns it.

```json
{ "tool": "hitTest", "arguments": { "rootId": 1, "x": 50, "y": 50 } }
```

```json
{
  "element": {
    "id": 34,
    "type": "Border",
    "name": "Bd",
    "bounds": { "x": 16, "y": 49.96, "width": 280, "height": 450.42 },
    "isInTemplate": true,
    "path": "/MainWindow/…/ListBox[Name='CustomerList']/Border[Name='Bd']",
    "isAlive": true
  }
}
```

The deepest visual under that point is the `Border` named `Bd` (`isInTemplate:
true`) inside `CustomerList`'s own control template — the chrome around the list,
not an item.

### `resolvePath` — path string back to an element

Paths from `describeElement` aren't just labels; they round-trip. Here Doug
feeds the root's own path back in.

```json
{ "tool": "resolvePath", "arguments": { "rootId": 1, "pathString": "/MainWindow" } }
```

```json
{
  "element": {
    "id": 1,
    "type": "MainWindow",
    "bounds": { "x": 0, "y": 0, "width": 900, "height": 600 },
    "path": "/MainWindow",
    "dataContextType": "SampleWpfApp.ViewModels.MainViewModel",
    "isAlive": true
  }
}
```

`/MainWindow` resolves back to `id: 1`. Because ids are per-run and can expire,
a stable path string is the durable way to re-find a node across calls.

---

## 5. DataContext

Bindings resolve against a DataContext, so before chasing a binding Doug asks two
questions: what *shape* is the context, and does a given dotted path actually
reach a value.

### `describeDataContext` — the CLR shape

Run against the broken-binding text block (`id: 152`), whose context is a
`Customer`.

```json
{ "tool": "describeDataContext", "arguments": { "id": 152 } }
```

```json
{
  "dataContext": {
    "typeName": "Customer",
    "namespace": "SampleWpfApp.ViewModels",
    "interfaces": ["System.ComponentModel.INotifyPropertyChanged"],
    "declaredProperties": [
      { "name": "Name",    "type": "System.String",                    "canRead": true, "canWrite": true },
      { "name": "Email",   "type": "System.String",                    "canRead": true, "canWrite": true },
      { "name": "Address", "type": "SampleWpfApp.ViewModels.Address",  "canRead": true, "canWrite": true }
    ]
  }
}
```

The context is a `Customer` exposing `Name`, `Email`, and `Address`. Crucially
there is **no `Country`** member on `Address`'s owner here — the first hint at why
`Address.Country.Name` will fail.

### `readDataContextPath` — reachable path

"Read `SelectedCustomer.Name` off the root's DataContext." (The root's context is
the `MainViewModel`.)

```json
{ "tool": "readDataContextPath", "arguments": { "id": 1, "path": "SelectedCustomer.Name" } }
```

```json
{ "value": "Customer 0000", "valueType": "System.String", "pathReachable": true }
```

`pathReachable: true` with `value: "Customer 0000"` — the happy path resolves to
the selected customer's name.

### `readDataContextPath` — broken path

Now the same tool on the broken binding's `Customer` context (`id: 152`), reading
the exact failing path.

```json
{ "tool": "readDataContextPath", "arguments": { "id": 152, "path": "Address.Country.Name" } }
```

```json
{ "pathReachable": false, "failureAt": "Address.Country" }
```

`pathReachable: false`, and `failureAt` pinpoints the break at `Address.Country`
— `Address` resolves, but `.Country` does not, so evaluation never reaches
`.Name`. This is the root cause in one field, without touching the binding
machinery yet.

---

## 6. Dependency properties

The Delete button is the wrong colour, and Doug wants to know *which* setter
won. Dependency properties have a precedence order; SnoopMCP exposes both the
full list and the resolved value with its trace.

### `listDependencyProperties` — what's available

Run against the Save button (`id: 4`). The full list is long (every DP on a
`Button`); here are the relevant entries.

```json
{ "tool": "listDependencyProperties", "arguments": { "id": 4 } }
```

```jsonc
{
  "properties": [
    { "name": "IsPressed",  "ownerType": "System.Windows.Controls.Primitives.ButtonBase", "valueType": "System.Boolean",                "isAttached": false },
    { "name": "Background", "ownerType": "System.Windows.Controls.Panel",                  "valueType": "System.Windows.Media.Brush",    "isAttached": false },
    { "name": "Template",   "ownerType": "System.Windows.Controls.Control",               "valueType": "System.Windows.Controls.ControlTemplate", "isAttached": false },
    { "name": "Style",      "ownerType": "System.Windows.FrameworkElement",               "valueType": "System.Windows.Style",          "isAttached": false }
    // … ~85 more DPs (IsMouseOver, Padding, Foreground, Visibility, …) …
  ]
}
```

Roughly 90 DPs come back, each with its declaring `ownerType` and `valueType`.
This is the menu of property names you can then drill into with
`getDependencyProperty`.

### `getDependencyProperty` — value + precedence trace

"What's `Background` on the Delete button (`id: 160`), and why?"

```json
{ "tool": "getDependencyProperty", "arguments": { "id": 160, "propertyName": "Background" } }
```

```json
{
  "name": "Background",
  "currentValue": "#FFDC143C",
  "currentValueType": "System.Windows.Media.SolidColorBrush",
  "precedence": [
    { "source": "StyleSetter", "value": "#FFDC143C", "sourceDescription": "Style TargetType=Button (chain depth 0)" },
    { "source": "StyleSetter", "value": "#FF1E90FF", "sourceDescription": "Style TargetType=Button (chain depth 1)" },
    { "source": "StyleSetter", "value": "#FFD3D3D3", "sourceDescription": "Style TargetType=Button (chain depth 2)" },
    { "source": "Default",                            "sourceDescription": "Default value from type metadata" }
  ],
  "winningSource": "Style"
}
```

`currentValue` is `#FFDC143C` (Crimson) and `winningSource` is `Style`. The
`precedence` array is the story: three `StyleSetter` candidates for `Background`
at chain depths 0/1/2 — Crimson, then DodgerBlue (`#FF1E90FF`), then LightGray
(`#FFD3D3D3`) — with the depth-0 setter winning. That cascade is the `BasedOn`
chain, which the next section unpacks.

---

## 7. Styles & templates

### `resolveStyle` — the BasedOn cascade

"Resolve the style on the Delete button (`id: 160`)." Read the response
carefully — it has a subtlety worth understanding.

```json
{ "tool": "resolveStyle", "arguments": { "id": 160 } }
```

```jsonc
{
  "appliedStyleKey": "Button",
  "appliedStyleSource": "Explicit",
  "basedOnChain": [
    { "targetType": "System.Windows.Controls.Button", "depth": 0 },
    { "targetType": "System.Windows.Controls.Button", "depth": 1 },
    { "targetType": "System.Windows.Controls.Button", "depth": 2 }
  ],
  "setters": [
    { "property": "Background", "value": "#FFDC143C" },
    { "property": "Background", "value": "#FF1E90FF" },
    { "property": "Foreground", "value": "#FFFFFFFF" },
    // … FontFamily "Segoe UI", FontSize "14" …
    { "property": "Padding",    "value": "12,6,12,6" },
    { "property": "Background", "value": "#FFD3D3D3" }
  ],
  "triggers": [
    { "kind": "Trigger", "condition": "IsMouseOver=True", "setters": [ { "property": "Background", "value": "#FF4169E1" } ] },
    { "kind": "Trigger", "condition": "IsEnabled=False",  "setters": [ { "property": "Background", "value": "#FF808080" } ] }
  ]
}
```

The subtlety: `appliedStyleKey` reads `"Button"`, not `"DangerButtonStyle"`. The
x:Key the style was declared with is **not** carried on the style at runtime —
WPF keeps the `TargetType`, which is what the resolver reports
(`appliedStyleSource: "Explicit"` because the style was set explicitly, not by
implicit type lookup). The three-level cascade lives in `basedOnChain` (depths
0/1/2), and you can read it directly in the `setters`: `Background` appears three
times — Crimson at the Danger level, DodgerBlue at Primary, LightGray at Base —
which is exactly the precedence trace from the previous section, viewed from the
style side. The two `triggers` (hover → RoyalBlue `#FF4169E1`, disabled → Gray
`#FF808080`) come from the Primary level.

### `resolveTemplate` — runtime tree + named parts

The Save button carries a custom `ControlTemplate`. "Resolve the template on
`id: 4`."

```json
{ "tool": "resolveTemplate", "arguments": { "id": 4 } }
```

```json
{
  "templateType": "System.Windows.Controls.ControlTemplate",
  "templateKey": "Button",
  "templateTree": {
    "elementId": 4, "type": "Button", "name": "SaveButton",
    "children": [
      { "elementId": 5, "type": "Border", "name": "PART_Border",
        "children": [
          { "elementId": 158, "type": "ContentPresenter", "name": "PART_Content",
            "children": [ { "elementId": 159, "type": "TextBlock", "name": "" } ] } ] } ]
  },
  "namedParts": [
    { "partName": "PART_Border",  "partType": "System.Windows.Controls.Border",           "elementId": 5 },
    { "partName": "PART_Content", "partType": "System.Windows.Controls.ContentPresenter", "elementId": 158 }
  ]
}
```

`templateTree` is the *instantiated* template: `Button → PART_Border (Border) →
PART_Content (ContentPresenter) → TextBlock`, each node with a live `elementId`
you can navigate to. `namedParts` lifts out the two `x:Name`d parts —
`PART_Border` (`id: 5`, the one the earlier `getTemplatedParent` started from)
and `PART_Content` — by name and runtime id. (Like `resolveStyle`, `templateKey`
reports `"Button"`, the target type, not the resource key `CustomButtonTemplate`.
The `IsPressed` template trigger declared in the XAML isn't surfaced in this
response; `resolveStyle` is where trigger details show up.)

---

## 8. Bindings

Now the payoff: diagnosing the broken binding directly, then auditing every
binding under the pane.

### `inspectBinding` — one binding, in depth

"Inspect the `Text` binding on the broken text block (`id: 152`)."

```json
{ "tool": "inspectBinding", "arguments": { "id": 152, "propertyName": "Text" } }
```

```json
{
  "bindingPath": "Address.Country.Name",
  "mode": "Default",
  "currentValue": "",
  "state": "PathError",
  "recentTraceLines": []
}
```

There it is: `state: "PathError"` on path `Address.Country.Name`, producing an
empty `currentValue`. This corroborates the `readDataContextPath` finding from
the DataContext section — the binding is wired correctly but its path can't
resolve. `recentTraceLines` is `[]`: in v1 it is always empty (the README lists
this as a known limitation; Phase 2 will wire up `PresentationTraceSources`), so
don't read its emptiness as "no errors."

### `listBindings` — wide audit under a subtree

"List every binding on the detail pane and its descendants (`id: 2`)."

```json
{ "tool": "listBindings", "arguments": { "id": 2, "includeDescendants": true } }
```

```json
{
  "bindings": [
    {
      "elementId": 2,
      "elementType": "ContentControl",
      "property": "Content",
      "bindingPath": "SelectedCustomer",
      "mode": "Default",
      "state": "Active",
      "hasError": false,
      "resolvedSourceType": "SampleWpfApp.ViewModels.MainViewModel",
      "currentValue": "SampleWpfApp.ViewModels.Customer"
    }
  ]
}
```

Where `inspectBinding` is a deep dive on one property, `listBindings` is the
breadth scan. Here it reports the pane's own `Content` binding to
`SelectedCustomer` as `state: "Active"` / `hasError: false`, resolved against
the `MainViewModel` and currently holding a `Customer`. (The template-internal
text-block bindings — including the broken one — are part of the data template's
instantiated content rather than direct bindings on the pane's own descendants,
so the deep dive via `inspectBinding` remains the right tool for those.)

---

## 9. Snapshot & wrap-up

### `exportXaml` — a XAML snapshot of live state

"Export the detail pane (`id: 2`) as XAML." This serialises the element's live
state via `XamlWriter`.

```json
{ "tool": "exportXaml", "arguments": { "id": 2 } }
```

```xml
<ContentControl Name="DetailPane" Margin="12,0,0,0" Grid.Column="1" xmlns="…/presentation" …>
  <ContentControl.ContentTemplate>
    <DataTemplate DataType="{x:Type swavm:Customer}">
      <StackPanel>
        <TextBlock Text="" FontWeight="SemiBold" FontSize="22" />
        <TextBlock Text="" Margin="0,0,0,12" Opacity="0.7" />
        <TextBlock Text="Address" FontWeight="Bold" Margin="0,12,0,4" />
        <!-- … three more TextBlocks (Street/City/PostalCode) … -->
        <TextBlock Text="" Foreground="#FFFF0000" Name="BrokenBinding" Margin="0,12,0,0" />
      </StackPanel>
    </DataTemplate>
  </ContentControl.ContentTemplate>
  <swavm:Customer Name="Customer 0000" Email="customer0000@example.com">
    <swavm:Customer.Address>
      <swavm:Address Street="1 Main St" City="Springfield" PostalCode="10000" />
    </swavm:Customer.Address>
  </swavm:Customer>
</ContentControl>
```

The XML above is the value of the response's `xaml` field; the response also
carries `byteCount: 1100` and `truncated: false`. Two things to notice. First, **bindings appear as their evaluated values, not as
`{Binding …}` markup** — every data-bound `TextBlock.Text` shows as `Text=""`
(the bound values aren't baked into the snapshot), and `BrokenBinding` shows up
empty with its red `Foreground="#FFFF0000"`. For binding *shape* you still want
`listBindings`/`inspectBinding`. Second, the live `Content` — `Customer 0000` and
its nested `Address` — is serialised out as real objects, so the snapshot
captures the actual data the pane is showing.

### `detach` — close the session

"Detach."

```json
{ "tool": "detach", "arguments": {} }
```

```json
{ "ok": true }
```

`ok: true`. The payload is released and the session is closed; the target app
keeps running, untouched.

---

## Closing

That's the whole v1 surface, end to end. Starting from nothing but a running
app, Doug:

- **discovered** the process (`listWpfProcesses`) and **attached** (`attach`);
- **oriented** with `listVisualRoots` / `describeElement`, then caught the
  ComboBox's separate `Popup` root once it opened;
- **navigated** the visual vs logical trees (`getChildren`, `getParent`,
  `getTemplatedParent`) and saw why data-templated content vanishes from the
  logical tree;
- **searched** by name, text, and screen point (`findElements`, `hitTest`) and
  round-tripped a path (`resolvePath`);
- inspected the **DataContext** shape and read paths (`describeDataContext`,
  `readDataContextPath`), localising the failure to `Address.Country`;
- traced **dependency-property** precedence and the **BasedOn** style cascade
  (`getDependencyProperty`, `resolveStyle`) and the Save button's **control
  template** (`resolveTemplate`);
- diagnosed the binding (`inspectBinding` → `PathError`), audited the subtree
  (`listBindings`), snapshotted live XAML (`exportXaml`), and **detached**.

The bug, found three different ways that all agree: `Address.Country.Name` binds
to a `Country` that doesn't exist on the `Customer`'s `Address`.

The target's source is under [`samples/SampleWpfApp/`](../samples/SampleWpfApp/)
— [`MainWindow.xaml`](../samples/SampleWpfApp/MainWindow.xaml) and
[`Resources/Themes.xaml`](../samples/SampleWpfApp/Resources/Themes.xaml) hold the
fixtures used above. Nothing here is specific to SampleWpfApp, though: the same
flow works against your own WPF app — SampleWpfApp is just a target with known
bugs to make the tour reproducible.

### Refreshing this walkthrough

Every response above is a trimmed excerpt from
[`tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json`](../tests/SnoopMCP.IntegrationTests/walkthrough-transcript.json).
That transcript is regenerated by the `CaptureWalkthroughTranscript` fact in
[`tests/SnoopMCP.IntegrationTests/WalkthroughCaptureTests.cs`](../tests/SnoopMCP.IntegrationTests/WalkthroughCaptureTests.cs),
which drives every tool against a live SampleWpfApp in document order. It is
skipped by default; the exact `dotnet test` command to run it by hand is in that
class's XML doc comment. After regenerating, eyeball the new JSON, restore the
`Skip`, and commit the transcript alongside any edits to this page.
