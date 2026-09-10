# Hierarchy Decorator 2.0 — Performance

**Status of the numbers in this document:** the cache-fill benchmark of §6.0 has been run and its results
are recorded there. The interactive scroll and frame-time benchmarks of §6.1 onward have **not** been run -
every result cell there still reads `not measured`, because they need an interactive editor and the Profiler.
The design claims, the cost model and the benchmark procedure are fixed here first, so that each run has
something to be checked against.

Scope: the Editor decoration path only (`Editor/**`). Companion documents: [ARCHITECTURE.md](ARCHITECTURE.md)
for the design decisions this file measures, [CHANGELOG.md](CHANGELOG.md) for what changed.

---

## 1. Design goals

### 1.1 The single biggest change from 1.x

1.x hooked `EditorApplication.hierarchyWindowItemOnGUI`. That callback is an **IMGUI per-repaint callback**:
Unity calls it once for every visible row, on every repaint of the Hierarchy window. A repaint happens when
the mouse moves over the window, when a row is hovered, when the selection changes, when the scene view
redraws, when an editor animation ticks — many times a second, whether or not anything about the hierarchy
actually changed. Everything 1.x did inside that callback — `GetComponents<Component>()`, prefix matching run
four to six times per row, `GUIStyle.CalcSize`, an allocating `Selection.gameObjects` read, up to `max(n, 7)`
`Handles.DrawLine` calls per depth level — was therefore repeated per row per repaint.

2.0 hooks `Unity.Hierarchy.Editor.HierarchyWindow.BindViewItem`. That is a **UI Toolkit data-binding
callback**, not a paint callback. Unity raises it when a row is bound to a node: when the row scrolls into
view, when the pooled row element is recycled onto a different node, or when the view rebinds a row because
its data changed. It is **not** raised on repaint. Once a row is bound, hovering it, moving the mouse across
the window, repainting the editor, or selecting elsewhere causes the panel to redraw the existing
`VisualElement`s with no plugin code running at all.

That is the whole cost model in one sentence: **a hierarchy repaint costs nothing extra, because a repaint
does not call us.** What costs is a bind — scrolling, and structural change.

### 1.2 What work happens when

| Moment | What runs | Frequency |
|---|---|---|
| Domain load | `HierarchyBootstrap` static ctor subscribes the static events | once per domain |
| `delayCall` after load | legacy-window check, 1.x migration, rebind of already-open windows | once per domain |
| `BindView` (per Hierarchy view) | `StyleInjector` adds the two stylesheets to `HierarchyView.StyleContainer` | once per view, re-run on every `HierarchyView.Reset()` (scene load, prefab stage, handler registration) |
| `BindViewItem` (per row) | `DecoratorHost.Decorate` → cache `Ensure` → five decorators write styles | once per row entering/rebinding, **not** per repaint |
| First bind of a given `EntityId` | `DecorationCache.Ensure` fills the missing facets: component scan, icon resolution, header match | once per object, until invalidated |
| Repaint | nothing of ours | — |
| `ObjectChangeEvents.changesPublished` | `ChangeTracker` maps the event batch onto cache facets, then redecorates the affected *live* rows only | per editor change batch |
| Hover a row | `HierarchyTooltips.OnGetTooltip` appends to the `StringBuilder` Unity hands us; needs the `Name` facet only | per tooltip |

Sources: `Editor/Core/HierarchyBootstrap.cs`, `Editor/Core/StyleInjector.cs`, `Editor/Core/DecoratorHost.cs`,
`Editor/Cache/DecorationCache.cs`, `Editor/Cache/ChangeTracker.cs`, `Editor/Core/HierarchyTooltips.cs`.

### 1.3 Consequences the benchmark should confirm

1. Idle editor with the Hierarchy window open and focused: zero plugin CPU, zero plugin GC.
2. Scrolling: cost is `rows entering the viewport × per-bind cost`, independent of scene size once the cache
   is warm — the cache is a dictionary keyed by `EntityId`, not a list that is walked.
3. A transform drag (the highest-volume change event by far) never triggers a component rescan: the
   `ChangeGameObjectOrComponentProperties` path is O(1) per event by construction
   (`ChangeTracker.MarkPropertyChange`).
4. Turning the Components column on costs no extra scanning: it shares `DecorationCache` with the inline
   strip (`Editor/Columns/ComponentsColumn.cs`).

---

## 2. What is cached, keyed by what, and what invalidates it

### 2.1 The cache

`DecorationCache` (`Editor/Cache/DecorationCache.cs`) is a `Dictionary<EntityId, RowData>` with an initial
capacity of 1024 and a hard ceiling of `MaxEntries = 32768`. `EntityId` is the key end to end; it is never
converted to `int` (ARCHITECTURE.md D11).

`RowData` (`Editor/Cache/RowData.cs`) holds two facets, tracked as a `[Flags] CacheFacet` bitfield:

| Facet | Contents | Filled by |
|---|---|---|
| `Name` | object name, matched header rule index, derived header label | `FillName` → `NameMatcher.FindRule` |
| `Components` | icon slice (`ComponentIconEntry[]` — component reference, `Texture2D`, nicified display name, stack count), overflow count, missing-script count | `FillComponents` → `GetComponentCount` / `GetComponentAtIndex` / `AssetPreview.GetMiniThumbnail` |

`RowData` also carries `SettingsRevision`. `HierarchyDecoratorSettings.Revision` is bumped on every settings
change; `Ensure` compares it and drops the whole row on mismatch, so a settings edit invalidates everything
without a separate broadcast.

`DecoratorHost.RequiredFacets` asks for `Name` always, and `Components` only when component icons or the
missing-script indicator are enabled — so a headers-and-tree-lines-only configuration never scans components.

Two smaller caches sit beside it:

* `ComponentCatalog` (`Editor/Cache/ComponentCatalog.cs`) — `TypeCache`-built type index and MonoScript GUID
  index, discarded on domain reload / recompile.
* `NameMatcher`'s compiled-`Regex` dictionary (`Editor/Utility/NameMatcher.cs`), cleared when the rule list
  changes.
* `LineTextures` (`Editor/Utility/LineTextures.cs`) — four tileable dash/dot strips plus one opaque pixel,
  generated lazily once per domain and destroyed by `HierarchyBootstrap.Uninstall`.

### 2.2 The invalidation table as designed (ARCHITECTURE.md D3, reproduced verbatim)

| `ObjectChangeKind` | Invalidated facet |
|---|---|
| `ChangeGameObjectStructure` | `Components` (icons + missing scripts) for that object only |
| `ChangeGameObjectStructureHierarchy` | `Components` + `Tree` + `Name` for the object **and its subtree** |
| `CreateGameObjectHierarchy` | insert object + subtree; `Tree` of the new parent |
| `DestroyGameObjectHierarchy` | evict object + subtree; `Tree` of the parent |
| `ChangeGameObjectParent` | `Tree` for object, subtree, old parent, new parent |
| `ChangeChildrenOrder` / `ChangeRootOrder` | `Tree` only |
| `ChangeGameObjectOrComponentProperties` | cheap O(1) revalidation only (name hash, enabled bits) — **never** a component rescan, otherwise a transform drag thrashes the cache |
| `UpdatePrefabInstances` | `Components` + `Tree` + `Prefab` for the batch |
| `ChangeScene` | drop that scene's bucket |

`EditorApplication.hierarchyChanged` is kept only as a coarse safety net (a version counter bump), never as
the primary mechanism.

### 2.3 The invalidation table as implemented today

`Editor/Cache/ChangeTracker.cs` is coarser than D3 in one direction, and covers two kinds D3 does not
mention. This is a deliberate simplification recorded in the code comment ("rather than maintaining a
parent→children index purely to walk it, drop everything"), not an accident — but the two tables must not be
confused, and the benchmark measures the code, not the design note.

| `ObjectChangeKind` | What the code actually does |
|---|---|
| `ChangeGameObjectStructure` | `Invalidate(entityId, Components)`, row marked dirty |
| `ChangeGameObjectOrComponentProperties` | GameObject target → `Invalidate(entityId, Name)`, row dirty. Component target → owner GameObject marked dirty only, no facet dropped (enabled state is read live, not cached) |
| `UpdatePrefabInstances` | `Invalidate(id, All)` for every id in the batch, rows dirty |
| `CreateGameObjectHierarchy`, `DestroyGameObjectHierarchy`, `ChangeGameObjectStructureHierarchy`, `ChangeGameObjectParent`, `ChangeChildrenOrder`, `ChangeRootOrder`, `ChangeScene` | **full `DecorationCache.Clear()`** plus `RefreshAllLiveRows()` |
| `DestroyAssetObject`, `ChangeAssetObjectProperties` | `InvalidateAll(Components)` **and** full `DecorationCache.Clear()` + `RefreshAllLiveRows()` |
| everything else in the enum | ignored |

There is no `Tree` facet and no `Prefab` facet in `CacheFacet` — only `Name` and `Components`. Tree topology
is read live from `HierarchyViewModel` on every bind, so it needs no cache entry and no invalidation.

Cache-wide resets (`ChangeTracker.ResetAll`: clear cache + `ComponentCatalog.Invalidate` +
`NameMatcher.ClearRegexCache`) additionally fire on scene open, scene close, prefab stage open/close, undo/redo,
play-mode entry and exit, and `[InitializeOnEnterPlayMode]` (`Editor/Core/HierarchyBootstrap.cs`). `EntityId`s
are reassigned across the play-mode boundary, so a reset there is required for correctness, not caution.

---

## 3. Allocation policy on the bind path

The bind path is `DecoratorHost.OnBindViewItem` → `Decorate` → `DecorationCache.Ensure` → the five decorators
in fixed order: `RowTintDecorator`, `TreeLineDecorator`, `HeaderDecorator`, `IndicatorDecorator`,
`ComponentIconDecorator`.

| Rule | Where it is implemented |
|---|---|
| **Pooled row elements, addressed by index.** Tree-line columns are children of a container whose child 0 is always the horizontal connector; columns follow in order and are addressed by `FirstColumnIndex + column`, so a rebind is a handful of style writes with no lookup. | `Editor/Decorations/TreeLineDecorator.cs` |
| **Find-or-create, never blind `Add()`.** Row `VisualElement`s are pooled by Unity and children of `LeftCustomContainer` / `RightCustomContainer` survive rebinding, so a blind `Add()` accumulates elements forever. Every decorator goes through a linear scan over the handful of existing children. | `Editor/Utility/RowElements.cs` (`Find`, `GetOrCreate`, `HideIfPresent`, `SetVisible`), used by all five decorators and by `Editor/Columns/ComponentsColumn.cs` |
| **State expressed by re-derivation, not by undo.** `UnbindViewItem` only forgets the row; every `Apply` fully re-derives its visuals including hiding them, so a recycled row cannot keep stale styling. | `Editor/Core/DecoratorHost.cs` (`OnUnbindViewItem`), the `IRowDecorator` contract in `Editor/Core/RowContext.cs` |
| **No LINQ anywhere in the package.** Verified: no `using System.Linq` under `Editor/`. Loops are index-based `for` loops over `List<T>` / arrays. | whole package |
| **No reflection.** Component enable state uses the public `EditorUtility.GetObjectEnabled` / `SetObjectEnabled` instead of 1.x's `PropertyInfo`; type enumeration uses `UnityEditor.TypeCache` instead of `AppDomain.GetTypes()`. Verified: no `System.Reflection` and no `AppDomain` under `Editor/`. | `Editor/Decorations/ComponentIconDecorator.cs`, `Editor/Cache/ComponentCatalog.cs` |
| **No `AssetDatabase` call from `BindViewItem`.** The stylesheet load is per *view* (`BindView`), not per row. The MonoScript GUID index is documented as settings-UI-only. See the caveat in §4.5 — this rule is not currently airtight. | `Editor/Core/StyleInjector.cs` (per view), `Editor/Cache/ComponentCatalog.cs` |
| **Line textures generated once per domain.** Dashed and dotted guides are 1×6 / 1×2 (and transposed) tileable strips tinted per element via `unityBackgroundImageTintColor` and repeated by UI Toolkit. Solid lines are a background colour and need no texture at all. Nothing is rasterized on the render path. | `Editor/Utility/LineTextures.cs`, applied by `LineStyler.Apply` in `Editor/Decorations/TreeLineDecorator.cs` |
| **`RowContext` is a `readonly struct` passed by `in`.** No per-row context object. | `Editor/Core/RowContext.cs` |
| **Scratch buffers are static fields.** The dirty-id set, the redecorate scratch list and the tree-line continuation array are all preallocated instance/static fields. | `Editor/Cache/ChangeTracker.cs`, `Editor/Core/DecoratorHost.cs`, `Editor/Decorations/TreeLineDecorator.cs` |
| **Icon geometry set inline, cosmetics in USS.** A failed stylesheet load degrades to unstyled-but-visible rather than zero-sized invisible icons. | `Editor/Utility/IconElements.cs` |

What this policy does **not** claim: that a bind allocates zero bytes. See §4.4.

---

## 4. Known costs that remain

Listed honestly, including the ones that are a deliberate trade rather than an oversight.

### 4.1 `EditorUtility.GetObjectEnabled` per icon, per bind

`ComponentIconDecorator.BindIcon` calls `EditorUtility.GetObjectEnabled(component)` for every displayed icon
on every bind when `fadeDisabledComponents` is on, and `ComponentsColumn.BindCell` does the same for the
column. This is a native round-trip per icon and it is deliberately **not** cached: caching it would make the
icon fade lag behind the Inspector checkbox, because a component's enabled toggle publishes
`ChangeGameObjectOrComponentProperties` for the *Component*, which the tracker deliberately handles in O(1)
without touching the `Components` facet.

Worst case is `maxIconsPerRow` calls per bound row — 12 under the Debug preset, 8 under Developer. The
benchmark's icon-heavy configurations exist mainly to price this.

Mitigation available to a user who does not want it: turn `fadeDisabledComponents` off, which short-circuits
the call entirely.

### 4.2 Full cache drop on subtree-shaped change events

Seven `ObjectChangeKind`s (§2.3) throw away the entire `DecorationCache` rather than walking a subtree.
Creating an object, deleting one, reparenting one, reordering siblings, or changing a scene therefore costs
one cold bind for every row currently visible, plus a cold refill for every row scrolled to afterwards. In a
10,000-object scene the *cache* is what is discarded; the visible cost is bounded by the number of visible
rows, not by scene size, because only live rows are redecorated (`DecoratorHost.RefreshAllLiveRows`).

The reason is stated in the code: the event only ever names the subtree root, so a targeted invalidation
would require maintaining a parent→children index purely to walk it. These are user-paced operations
(one drag, one delete), not per-frame ones. The benchmark should still price the worst realistic case:
dragging a large subtree onto a new parent in the 10k scene.

Two related cliffs worth measuring rather than assuming:

* `DestroyAssetObject` / `ChangeAssetObjectProperties` also trigger a full clear. A script recompile or a
  large asset reimport publishes many of these.
* `DecorationCache` clears itself entirely when it reaches `MaxEntries = 32768` and then refills, so a scene
  above that size will thrash. 10k is comfortably below the ceiling; a 50k-object scene is not, and is out of
  scope for this benchmark.

### 4.3 Component scan on first bind of a row

`DecorationCache.FillComponents` walks every component on the GameObject
(`GetComponentCount` + `GetComponentAtIndex`, which avoid the allocating `GetComponents<T>()` of 1.x) and per
kept component performs:

* `component.GetType()` and a dictionary probe against the rule table;
* `AssetPreview.GetMiniThumbnail(component)` — one P/Invoke, natively cached;
* `ObjectNames.NicifyVariableName(type.Name)` — **allocates a string**;
* for `stackDuplicates`, a linear scan over the icons already collected for this row (O(n²) in components per
  row, with n bounded by the icon limit).

Plus, when the null pre-filter found nothing and the missing-script indicator is on,
`GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject)`, and for alphabetical ordering an
`Array.Sort` over the slice.

This runs once per object per cache lifetime, not per repaint — but it is the dominant cost of a cold scroll,
and it is exactly what the "cold cache" benchmark configuration isolates.

### 4.4 Per-bind string allocation where a label is formatted

The steady-state bind path is allocation-free *except* where a string is composed:

* stacked-icon tooltip `$"{DisplayName} ({StackCount})"` — per bind, per stacked icon
  (`ComponentIconDecorator.BindIcon`);
* stack badge text `StackCount.ToString()` — per bind, per stacked icon;
* overflow label `"+" + overflowCount` and its tooltip — per bind, when the icon limit is exceeded;
* missing-script tooltip `$"{count} missing MonoBehaviour scripts"` — per bind, when count > 1
  (`IndicatorDecorator.Apply`).

None of these fire on the common row (no duplicates, no overflow, no missing scripts), which is why the
target for a plain scroll is still 0 bytes/row. The benchmark must report bytes/row for the icon-heavy
configurations separately rather than assuming the plain-row number generalizes.

### 4.5 `AssetDatabase` reachable from the cache-fill path

`DecorationCache.FillComponents` calls `EnsureRuleTable`, which calls `ComponentCatalog.Resolve` once per
component rule whenever the settings revision changed. `Resolve` performs `AssetDatabase.GUIDToAssetPath` +
`LoadAssetAtPath<MonoScript>` per rule that carries a GUID, and — for a rule that resolves by type name and
has no stored GUID — calls `FindMonoScriptGuid`, which can trigger `EnsureScriptIndex`, i.e. a full
`AssetDatabase.FindAssets("t:MonoScript")` project scan.

This is bounded (the script index is built once per domain, the rule table once per settings revision) but it
contradicts the comment in `ComponentCatalog` that the script index is "only ever triggered from the settings
UI — never from the hierarchy render path". The first bind after a settings change in a project with many
scripts is therefore a plausible hitch, and the benchmark's "enabled, cold cache" run should be taken
immediately after a settings edit at least once to catch it.

### 4.6 Tree-line topology walk per bind

`HierarchyNodes.CollectAncestorContinuations` walks from the node up to the root, and at each level
`IsLastVisibleChild` walks forward through siblings skipping any the view model does not contain. Worst case
per bind is O(depth × siblings). A wide, filtered tree is the pathological shape. This is read live rather
than cached because tree topology has no facet (§2.3), so there is nothing to invalidate when it changes.

### 4.7 Double redecoration on structural change

`ObjectChangeEvents.changesPublished` and `EditorApplication.hierarchyChanged` both fire for most structural
edits, and both end in a redecoration of live rows (`RefreshLiveRows` / `RefreshAllLiveRows`). The cache
absorbs the second pass, so the cost is the decorator style writes, not a rescan — but it is a measurable
doubling on the change path and the benchmark should not attribute it to the cache.

---

## 5. Benchmark methodology

### 5.1 Instrumentation the package ships

Three `ProfilerMarker`s, which cost nothing while the Profiler is off:

| Marker | Covers | Expected behaviour |
|---|---|---|
| `HierarchyDecorator.DecorateRow` | The whole per-row bind path, every decorator included. | Once per row bind. Present while scrolling. |
| `HierarchyDecorator.ScanComponents` | A component-slice cache fill. | Only on a row's first bind, or after an invalidation. |
| `HierarchyDecorator.MatchHeaderRule` | A header-rule cache fill. | Same. |

The single most useful reading in this whole document: scroll down and back up over the same rows, and
`ScanComponents` must **not** reappear for rows that were already bound. If it does, the cache is not doing
its job and every other number here is meaningless.

The generator that produces the scenes below ships as the **Benchmark Scene Generator** sample
(`Samples~/BenchmarkScenes`); import it from the Package Manager and open
`Tools > Hierarchy Decorator > Benchmark Scene Generator`.

### 5.2 Environment to record with every run

A number without these is not a number. Record all of them in the run header:

* Unity version (target: `6000.6.0f1`), OS and build, CPU, RAM, display scale.
* `EditorSettings.useLegacyHierarchy == false` (the new window must be active — 2.0 draws nothing otherwise).
* `EditorApplication.isCompiling == false` at the start of the measurement window.
* Enter Play Mode options (`enterPlayModeOptionsEnabled`, `DisableDomainReload`) — must not change between
  configurations.
* Package version and git commit.
* Hierarchy window size in pixels and the resulting number of virtualized rows (row height is 16 px), plus
  the number of other editor windows open (a docked Scene view repainting changes the editor loop cost).
* Active preset, and every setting that differs from it.

### 5.3 Scenes

Generated from script by the Benchmark sample, never committed as scene assets — the generator is committed
instead.

| Scene | GameObjects | Shape |
|---|---|---|
| S100 | 100 | mixed depth 0–4, a few header-prefixed rows |
| S1k | 1,000 | mixed depth 0–6 |
| S10k | 10,000 | mixed depth 0–8 |

Fix the component distribution across all three (for example: every object a `Transform`; a fixed fraction
carrying 2–6 extra components including at least one duplicate type and at least one object with a
deliberately missing script) and record the exact distribution in the run header. The header-prefix rows must
use the shipped default prefixes — `---` (separator), `=`, `-`, `+` — so header matching is exercised.

### 5.4 Configurations

| # | Name | Setup |
|---|---|---|
| C1 | Plugin disabled — baseline | Package not present in the project manifest. This is the true zero: with the package installed but the master switch off, `BindViewItem` is still subscribed and `DecoratorHost.Decorate` still runs all five decorators with `Active == false`. Capture that variant too if you want to price the always-subscribed callback. |
| C2 | Enabled, cold cache | Default preset. Before each measurement window force a cache drop (open the scene, or trigger `ChangeTracker.ResetAll` from the harness). Every bind is a cache miss. |
| C3 | Enabled, warm cache | Same settings as C2, but scroll the full list once first, then discard warm-up and measure a second traversal. Every bind is a cache hit. |
| C4 | Component icons on | C3 plus component icons enabled — `ComponentIconMode.All`, `includeCustomScripts` on, `fadeDisabledComponents` on, `maxIconsPerRow = 8`, tooltips on. This is the Developer preset. |
| C5 | Maximum preset | The **Debug** built-in preset: headers on, tree lines dashed at full depth with connector, `overrideRowColors` on, icons `All` + custom scripts, `maxIconsPerRow = 12`, tooltips on, fade on, click action `ToggleEnabled`, missing-script indicator on. Additionally enable the optional **Components** column from the Hierarchy header's right-click menu. |

### 5.5 Scenarios per configuration

Each is a separate measurement window; do not blend them.

1. **Idle** — window open and focused, mouse still, nothing changing, ≥ 300 frames. Expected: no plugin
   samples at all.
2. **Hover sweep** — mouse moved across rows without scrolling. Expected: no bind samples, tooltip samples
   only. This is the scenario that proves the IMGUI-vs-bind claim of §1.1.
3. **Scroll** — a scripted, fixed-distance, fixed-rate scroll from top to bottom of the list. This is the
   primary number.
4. **Transform drag** — drag one object in the Scene view for ≥ 3 seconds, flooding
   `ChangeGameObjectOrComponentProperties`.
5. **Add Component** — a single `ChangeGameObjectStructure` on one visible object.
6. **Reparent a subtree** — drag a subtree of ~100 objects onto a new parent. Prices §4.2.
7. **Settings edit** — change one setting while the window is visible. Prices the revision bump plus §4.5.

### 5.6 What to capture

| Metric | How |
|---|---|
| Bind cost | Profiler CPU module, Hierarchy view, Deep Profile on, filtered to `HierarchyDecorator`. Report **Total ms** and **Self ms** for `DecoratorHost.Decorate`, `DecorationCache.Ensure` and each decorator's `Apply`. Deep Profile inflates absolute numbers — use it for attribution, then re-measure the frame time with it off. |
| Rows bound per frame | Deep Profile call count on `DecoratorHost.Decorate`, or a counter in the harness. Required: `ms per row` without it is meaningless. |
| GC allocation | Profiler **GC Alloc** column per sample, and for an exact figure `System.GC.GetAllocatedBytesForCurrentThread()` around the bind in a harness (see below). Report **bytes per bound row**. Target for a plain warm scroll: 0. |
| Editor responsiveness while scrolling | Editor-loop frame time over the scroll window: **median and p95 over ≥ 300 frames**, plus the count of frames over 16.7 ms and over 33 ms. p95, not mean — the tail is what a user feels. |
| Cache memory | `DecorationCache.Count` (internals are visible to `Wooshii.HierarchyDecorator.Editor.Tests`), plus a Memory Profiler package snapshot filtered to `HierarchyDecorator.RowData` and `ComponentIconEntry[]`. Report bytes per cached row and total at each scene size, after a full traversal. |
| Change-event cost | Deep Profile sample for `ChangeTracker.OnChangesPublished`, per event batch, for scenarios 4–7. |

The harness for the exact allocation and cache-count numbers belongs in the test assembly
(`Tests/Editor/`), which already has `InternalsVisibleTo` access — not in `Editor/`, which must stay free of
benchmark code.

### 5.7 Exact Unity Profiler steps

1. Open the benchmark scene generated by the sample and let the editor settle: no compilation, no import,
   no asset preview jobs pending.
2. Set the Hierarchy window to the recorded fixed size and dock it identically for every run. Close every
   window that is not part of the recorded layout.
3. `Window > Analysis > Profiler`.
4. In the Profiler toolbar, set the connection/target dropdown to **Editor** so editor-side samples are
   recorded at all (this is `ProfilerDriver.profileEditor`; without it the plugin is invisible).
5. Enable only the **CPU Usage** module. Switch the bottom pane from Timeline to **Hierarchy**, and turn
   **Collapse EditorLoop** off so editor-side samples stay attributable.
6. For the attribution pass only, enable **Deep Profile** and let the editor recompile. For the frame-time
   pass, leave it off.
7. Press **Record**. Wait 30 frames of warm-up, then perform the scenario's scripted interaction. Stop
   recording.
8. Discard the first 30 recorded frames. Over the remaining ≥ 300 frames:
   * sort the Hierarchy view by Total, search for `HierarchyDecorator`, and read Total ms, Self ms, Calls and
     GC Alloc for each of the samples listed in §5.6;
   * read the editor-loop frame time per frame for the median/p95 figures.
9. Repeat every scenario three times and report the median run, not the best one.
10. Never mix a Deep Profile number with a non-Deep Profile number in the same table row.

---

## 6. Results

Two different things are measured here, and they are kept apart on purpose.

**§6.0 has been run.** It measures the cache fill - the component scan, the icon resolution and the header
match - which is the only genuinely expensive work 2.0 does, and the one number the whole design rests on. It
runs headlessly and is committed, so anyone can reproduce it.

**§6.1 onward have not been run.** They measure UI Toolkit's own layout and repaint cost while a human
scrolls, which needs an interactive editor and the Profiler. Every cell there is still a placeholder.

### 6.0 Cache fill — measured

Environment: Unity 6000.6.0f1, Apple M1 Pro, macOS 26, `-batchmode -nographics`, Editor scripts compiled in
release. Scenes shaped as §5.3 describes (mixed depth, a spread of component counts, a header row every
fiftieth object).

Reproduce with:

```bash
Unity -batchmode -nographics -projectPath <project> -runTests -testPlatform EditMode \
      -testFilter "HierarchyDecorator.Tests.PerformanceBenchmarks" -testResults results.xml -logFile -
```

| Scene | Cold fill (total) | Cold (per object) | Warm fill (total) | Warm (per object) | Cache |
|---|---|---|---|---|---|
| S100 | 0.21 ms | 2.1 µs | 0.06 ms | 0.6 µs | 100 entries, <1 KB |
| S1k | 1.34 ms | 1.3 µs | 0.07 ms | 0.1 µs | 1 000 entries, ~152 KB |
| S10k | 13.55 ms | 1.4 µs | 0.85 ms | 0.1 µs | 10 000 entries, ~3.1 MB |

What these numbers mean in practice:

* **Cold cost is per row, not per scene.** Only visible rows bind, so a full screen of ~60 rows costs about
  **0.08 ms** the first time it is scrolled into view. The 13.55 ms figure for S10k is the whole scene at
  once, which never happens outside this benchmark.
* **Warm cost is ~13× lower and flat** at ~0.1 µs per row - that is the facet-mask early-out in
  `DecorationCache.Ensure`, and it is what every scroll-back, every repaint and every unrelated refresh pays.
  The benchmark asserts warm < 25 % of cold and fails if the cache is ever bypassed.
* **Memory is ~315 bytes per cached row**, dominated by the icon array. A 10 000 object scene fully traversed
  costs about 3 MB; the cache is capped at 32 768 entries.

Not covered by these numbers: UI Toolkit layout and repaint, which is what §6.1 onward are for.

### 6.1 Scroll — median ms per bound row

| Configuration | S100 | S1k | S10k |
|---|---|---|---|
| C1 plugin disabled (baseline) | not measured | not measured | not measured |
| C2 enabled, cold cache | not measured | not measured | not measured |
| C3 enabled, warm cache | not measured | not measured | not measured |
| C4 component icons on | not measured | not measured | not measured |
| C5 maximum preset (Debug) | not measured | not measured | not measured |

### 6.2 Scroll — GC allocated bytes per bound row

| Configuration | S100 | S1k | S10k |
|---|---|---|---|
| C1 plugin disabled (baseline) | not measured | not measured | not measured |
| C2 enabled, cold cache | not measured | not measured | not measured |
| C3 enabled, warm cache | not measured | not measured | not measured |
| C4 component icons on | not measured | not measured | not measured |
| C5 maximum preset (Debug) | not measured | not measured | not measured |

### 6.3 Editor responsiveness while scrolling — frame time median / p95 ms

| Configuration | S100 | S1k | S10k |
|---|---|---|---|
| C1 plugin disabled (baseline) | not measured | not measured | not measured |
| C2 enabled, cold cache | not measured | not measured | not measured |
| C3 enabled, warm cache | not measured | not measured | not measured |
| C4 component icons on | not measured | not measured | not measured |
| C5 maximum preset (Debug) | not measured | not measured | not measured |

### 6.4 Idle and hover — plugin CPU ms per frame

| Scenario | C1 | C2 | C3 | C4 | C5 |
|---|---|---|---|---|---|
| Idle, window focused (S10k) | not measured | not measured | not measured | not measured | not measured |
| Hover sweep, no scroll (S10k) | not measured | not measured | not measured | not measured | not measured |

### 6.5 Change events — ms per event batch (S10k)

| Scenario | C3 warm cache | C5 maximum preset |
|---|---|---|
| Transform drag (`ChangeGameObjectOrComponentProperties` flood) | not measured | not measured |
| Add Component (`ChangeGameObjectStructure`) | not measured | not measured |
| Reparent ~100-object subtree (full cache drop) | not measured | not measured |
| Settings edit (revision bump) | not measured | not measured |

### 6.6 Cache memory after a full traversal

| Configuration | S100 | S1k | S10k |
|---|---|---|---|
| C3 enabled, warm cache — entries | not measured | not measured | not measured |
| C3 enabled, warm cache — bytes | not measured | not measured | not measured |
| C5 maximum preset — entries | not measured | not measured | not measured |
| C5 maximum preset — bytes | not measured | not measured | not measured |

---

Hierarchy Decorator is MIT licensed. Originally created by Damian Slocombe (WooshiiDev),
Copyright (c) 2020 - 2024. See [LICENSE.md](LICENSE.md).
