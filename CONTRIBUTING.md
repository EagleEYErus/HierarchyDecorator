# Contributing to Hierarchy Decorator 2

This repository *is* the UPM package: `package.json` sits at the repo root, the package id is
`com.wooshii.hierarchydecorator`, and everything ships from `Editor/`. There is no runtime assembly and
nothing is written into a player build.

| | |
|---|---|
| Package id | `com.wooshii.hierarchydecorator` |
| Version | `2.0.0` |
| Minimum Unity | `6000.6.0f1` (`"unity": "6000.6"`, `"unityRelease": "0f1"`) |
| Assembly | `Wooshii.HierarchyDecorator.Editor` (`includePlatforms: ["Editor"]`, no references) |
| Test assembly | `Wooshii.HierarchyDecorator.Editor.Tests` (`defineConstraints: ["UNITY_INCLUDE_TESTS"]`) |

---

## 1. Setting up a development project

### 1.1 Clone

```bash
git clone https://github.com/WooshiiDev/HierarchyDecorator.git
```

Do not open the clone as a Unity project. It is a package, not a project — you need a host project that
references it.

### 1.2 Create a host project

Create an empty Unity **6000.6.0f1** project. The render pipeline and template do not matter; the package
is editor-only.

### 1.3 Add the package as a local package

Two options. Pick based on whether you want the Test Runner to pick the tests up automatically.

**A. Embedded** — clone (or symlink) the repo into the host project so it lands at
`<project>/Packages/com.wooshii.hierarchydecorator/`. An embedded package's tests are discovered without
any extra configuration, which makes this the least friction option for day-to-day work.

**B. Local `file:` reference** — keep the clone anywhere on disk and point `Packages/manifest.json` at it.
`testables` is a **top-level sibling of `dependencies`**, not a key inside it:

```json
{
  "dependencies": {
    "com.wooshii.hierarchydecorator": "file:/absolute/path/to/HierarchyDecorator"
  },
  "testables": ["com.wooshii.hierarchydecorator"]
}
```

A non-embedded package (local `file:`, tarball, registry or Git URL) contributes **zero** tests until it is
listed in `testables`. If the Test Runner shows nothing, this is almost always why.

The Package Manager equivalent of option B is **Install package from disk…** and selecting the repo's
`package.json`; it writes the same `file:` entry, but you still have to add `testables` by hand.

End users install from **Package Manager ▸ Install package from Git URL**. That path is for consumers, not
for development — you cannot edit a Git-installed package.

### 1.4 Check the editor is actually set up to run the package

* **Project Settings ▸ Editor ▸ Hierarchy ▸ Use Legacy Hierarchy must be OFF.** 2.0 targets the new Unity 6.6
  Hierarchy window only. With the legacy window enabled, the extension API is never invoked, the package
  draws nothing, and it logs a one-time hint (`LegacyHierarchyNotice`). In 6000.6.0f1
  `EditorSettings.useLegacyHierarchy` defaults to `false`, so an untouched project is already correct.
* **Tools ▸ Hierarchy Decorator ▸ Settings…** should open the project settings page. The full menu is
  `Settings…`, `Disable All Decorations`, `Re-enable Failed Decorations`, `Import Settings From 1.x`,
  `Reset Settings To Defaults`. (`Benchmark Scene Generator` joins that menu only after the sample of the
  same name is imported.)

### 1.5 Where your settings end up

* Shared ruleset: `ProjectSettings/Packages/com.wooshii.hierarchydecorator/Settings.asset`
* Per-developer choices: `UserSettings/Packages/com.wooshii.hierarchydecorator/UserSettings.asset`

Nothing is ever written to `Assets/`. If you find yourself adding a code path that writes there, stop — that
was a 1.x problem this rewrite exists to remove.

---

## 2. Where the code lives

`Editor/` is split by responsibility, and the flow is strictly one-way: integration → cache → decorators →
visual elements. Nothing downstream calls upstream.

| Folder | Contents | Responsibility |
|---|---|---|
| `Editor/Core/` | `HierarchyBootstrap`, `StyleInjector`, `DecoratorHost`, `RowContext` (+ `IRowDecorator`), `HierarchyNodes`, `HierarchyContextMenu`, `HierarchyTooltips`, `HierarchyDecoratorMenu`, `HierarchyLog`, `PackageInfo`, `PackagePaths`, `AssemblyInfo` | The only place that touches `Unity.Hierarchy.Editor`. `HierarchyBootstrap` owns **every** event subscription in the package; `DecoratorHost` owns per-row dispatch and failure isolation. |
| `Editor/Cache/` | `DecorationCache`, `RowData` (+ the `CacheFacet` enum), `ChangeTracker`, `ComponentCatalog` | Derived per-row data keyed by `EntityId`, and the invalidation driven by `ObjectChangeEvents.changesPublished`. |
| `Editor/Decorations/` | `RowTintDecorator`, `TreeLineDecorator`, `HeaderDecorator`, `IndicatorDecorator`, `ComponentIconDecorator` | One `IRowDecorator` per visual concern. Draw order is the order of `DecoratorHost.s_Decorators`. |
| `Editor/Columns/` | `ComponentsColumn` | The single custom column (`[HierarchyViewColumnDescriptor]` / `[HierarchyViewCellDescriptor]`), hidden by default. Active/Static/Layer/Tag/Visibility are native columns and are deliberately not reimplemented. |
| `Editor/Settings/` | `HierarchyDecoratorSettings` **and** `HierarchyDecoratorUserSettings` (same file), `SettingTypes`, `DefaultSettings`, `Preset` (+ `BuiltInPresets`), `HierarchyDecoratorSettingsProvider`, `ComponentRuleListView` | The serialized schema, the built-in presets (Minimal, Clean, Developer, Designer, Debug) and the UI Toolkit settings page registered at `Project/Hierarchy Decorator` and `Preferences/Hierarchy Decorator`. |
| `Editor/Migration/` | `LegacyMigrator`, `MiniYaml` | Reads a 1.x `Settings.asset` **as text**, because the 1.x classes no longer exist in a 2.0 project. |
| `Editor/UI/` | `HierarchyDecorator.uss`, `HierarchyDecorator_dark.uss`, `HierarchyDecorator_light.uss`, `HierarchyDecoratorSettings.uss` | Theme variables live in the `_dark`/`_light` pair; rules live in the base sheet. |
| `Editor/Utility/` | `NameMatcher`, `LineTextures`, `RowElements`, `IconElements` | Leaf helpers with no dependency on the hierarchy API. |
| `Tests/Editor/` | `NameMatcherTests`, `DecorationCacheTests`, `LegacyMigratorTests`, `MiniYamlTests`, `SettingsAndPresetTests`, `SettingsScope` | EditMode tests. |
| `Samples~/BenchmarkScenes/` | `HierarchyBenchmarkWindow` | Optional sample (`Samples~` is not compiled until imported through the Package Manager). Generates reproducible large hierarchies to point the Profiler at. |

`ARCHITECTURE.md` §3 sketches this layout at design time and its file list has drifted from the tree; the
table above reflects what is actually on disk. When they disagree, the code wins and `ARCHITECTURE.md`
should be corrected.

Read `ARCHITECTURE.md` before your first change. Decisions **D1–D11** explain why things are the way they
are, and §7 lists alternatives that were considered and rejected — re-proposing one of those is fine, but
argue against the recorded reason.

---

## 3. Architecture rules you must not break

These are the row rendering contract from `ARCHITECTURE.md` §5. They are not style preferences; each one
corresponds to a documented behaviour of the Unity 6.6 Hierarchy window, and breaking one produces bugs
that only show up after scrolling, filtering or a scene change.

### 3.1 Rows are pooled — never blind-`Add()` children

`HierarchyViewItem`s are recycled per handler. Anything you add to `LeftCustomContainer`,
`RightCustomContainer` or the item itself **survives rebinding onto a different GameObject**. A blind
`Add()` therefore accumulates elements forever.

Go through `RowElements.Find<T>` / `RowElements.GetOrCreate<T>` (`Editor/Utility/RowElements.cs`), which
look elements up by name over the handful of children — allocation-free, and cheaper than a `UQuery`.

### 3.2 Fully re-derive every visual on every bind

`Apply(in RowContext)` runs for every bound row and must produce the complete visual state, **including the
"off" state**. Hiding is not optional: a recycled row still carries whatever the previous object put on it,
and `RowContext.Active` can be false because the user turned the package off after the row was decorated.

Express state with `EnableInClassList(class, condition)` or by writing the style in both directions
(`StyleKeyword.Null` to hand a property back to USS). Never write a style only in the "on" branch.

### 3.3 Never revert styles in `UnbindViewItem`

Unity's own documentation says not to, because of the recycling above. Unbind is only for forgetting the
row, unregistering callbacks and releasing rented buffers — see `DecoratorHost.OnUnbindViewItem`.

Also note that `item.Node` is already `HierarchyNode.Null` inside unbind. Capture the node during bind if
you need it later; `DecoratorHost` stores it in `BoundRow` for exactly this reason.

### 3.4 Always gate on `HierarchyGameObjectHandler` first

Not every row is a GameObject. Scenes, sub-scenes, entity worlds and third-party node types register their
own handlers, and `SubSceneAuthoring` is a separate handler type rather than a subclass. Calling the wrong
handler returns `null` **silently** instead of throwing, which is precisely why the gate has to come first
rather than being inferred from a null result.

Use `HierarchyNodes.IsGameObject` / `HierarchyNodes.TryGetGameObject`, or `RowContext.IsGameObject`. Never
cast `item.Handler` yourself.

### 3.5 Suppress depth-based decoration while filtering

`HierarchyViewItem.CalculateIndentWidth` early-returns `0` when `view.Filtering` is true, and the list
becomes flat. Anything positioned from depth — tree guide lines above all — would point at nothing. Check
`RowContext.IsFiltering` and hide, as `TreeLineDecorator` does.

### 3.6 `item.RowContainer` may be `null`

It is null when the item is not yet parented into a multi-column row. Null-check before touching it and
degrade gracefully to the name-column container; never assume a full-row background is available.

### 3.7 Two rules that come with the above

* **Decorators are isolated.** `DecoratorHost` calls each `Apply` inside a `try/catch`; a decorator that
  throws is disabled for the session and reported exactly once through `HierarchyLog.Once`. Do not "fix" a
  crash by removing the catch — the isolation is what keeps one broken decoration from making the Hierarchy
  unusable, and what keeps the console from being spammed per repaint.
* **Bind-time work only.** `BindViewItem` fires when a row scrolls into view or its data changes, not every
  repaint — but it still runs per row. Expensive derivation (component enumeration, icon resolution, header
  matching) belongs in `DecorationCache`, invalidated per facet from `ChangeTracker`. Never scan components
  from `Apply`.

---

## 4. Running the EditMode tests

### 4.1 From the Test Runner window

**Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.** The tests live in
`Wooshii.HierarchyDecorator.Editor.Tests`.

If the assembly does not appear at all, the package is neither embedded nor listed in `testables` — see
§1.3.

### 4.2 From the command line

```bash
Unity -batchmode -nographics -runTests \
  -projectPath <project> \
  -testPlatform EditMode \
  -testResults results.xml \
  -logFile -
```

`Unity` is the editor executable — `.../Unity.app/Contents/MacOS/Unity` on macOS, `Unity.exe` on Windows.
`<project>` is the **host Unity project** that references the package, not this repository.

Notes that matter:

* **Do not pass `-quit`.** Unity's regular `-quit` argument is not supported while tests are running. The
  Editor exits on its own when the run completes; adding `-quit` cuts the run short.
* **Exit codes:** `0` = all tests passed, `2` = at least one test failed, `1` = C# compile error (batchmode
  aborts and no results file is written). Treat `1` and `2` as different failures in any script you write.
* `-logFile -` streams the Editor log to stdout, which is what you want interactively and in CI. Pass a path
  instead if you want it kept.
* `results.xml` is NUnit3 XML; the root element carries `total`/`passed`/`failed`/`skipped`/`result`, and
  each `<test-case>` carries its own `result`.
* Useful narrowing flags: `-assemblyNames "Wooshii.HierarchyDecorator.Editor.Tests"`, `-testFilter`,
  `-testCategory`, and `-runSynchronously` (EditMode only, runs everything in one Editor update).
* CI machines need an activated license or the run never starts.

### 4.3 Not measured

* There is **no CI workflow in this repository** — `.github/` contains issue templates and `FUNDING.yml`
  only. The suite has never been run in CI; if you add a workflow, say so in the PR.
* **No performance numbers have been measured.** The measurement apparatus exists — the bind path is
  instrumented with `ProfilerMarker`s (`HierarchyDecorator.DecorateRow`, `HierarchyDecorator.ScanComponents`,
  `HierarchyDecorator.MatchHeaderRule`), which cost nothing while the Profiler is off, and the
  `Benchmark Scene Generator` sample under `Samples~/BenchmarkScenes` builds reproducible 100 / 1 000 /
  10 000 object scenes to record against. Nothing has actually been recorded from either. Do not add
  timings, allocation counts or "N× faster than 1.x" claims to any document until a real run exists to back
  them.

---

## 5. Commit messages

Conventional commits, `type(scope): summary`, imperative mood, lowercase summary, no trailing period.

**Types:** `feat`, `fix`, `docs`, `chore`, `test`, `refactor`.

**Scopes** are the subsystem, normally the folder: `core`, `cache`, `decorations`, `columns`, `settings`,
`migration`, `ui`, `utility`, `package`, `tests`. Omit the scope for repo-wide changes.

From this repository's history:

```
feat(core): Unity 6.6 hierarchy integration layer
feat(cache): per-object decoration cache with targeted invalidation
feat(decorations): headers, separators, tree lines, icons and indicators
feat(migration): import HierarchyDecorator 1.x settings
chore(package): bump version to 0.12.1
docs: document the 2.0 architecture
```

Put the *why* in the body when a change encodes a non-obvious Unity behaviour — and put the same reasoning
in an XML doc comment next to the code, because that is where the next person will look.

---

## 6. Code style

There is no analyzer or `.editorconfig` in the repo; the style below is what the codebase already does
consistently and what review will hold you to.

**Explicit types, never `var`.** There are currently zero uses of `var` anywhere in `Editor/`. Row rendering
is easier to review when every type is on screen. The same applies to tests and samples.

**No LINQ on the render path.** There is currently no `using System.Linq` anywhere in the package. Use
indexed `for` loops over lists and arrays. More generally, keep the bind path allocation-free: `RowContext`
is a `readonly struct` passed by `in`, `DecoratorHost` reuses a scratch list, `TreeLineDecorator` reuses a
`bool[]` and its `VisualElement`s, and dashed/dotted lines use a texture generated once per domain.

**Naming.**

* `m_` for private instance fields, serialized or not — `[SerializeField] private int m_SchemaVersion;`
* `s_` for statics — `private static bool s_Installed;`
* PascalCase for constants, properties, methods and types; no Hungarian prefixes beyond the two above.
* Statics that own Unity objects (textures, elements) need a release path that runs on domain reload — see
  `LineTextures.Release()` being called from `HierarchyBootstrap.Uninstall()`.

**Serialized schema classes are the exception.** The `[Serializable]` data classes in
`Editor/Settings/SettingTypes.cs` (`HeaderRule`, `ComponentRule`, `TreeLineSettings`, `ThemeColor`, …) use
plain public lowercase fields, because those field names *are* the on-disk format. Renaming one is a data
migration, not a refactor: add `[FormerlySerializedAs]`, bump
`HierarchyDecoratorSettings.CurrentSchemaVersion`, and add a `case` to the `OnAfterDeserialize` chain.
Renaming a serialized *type* needs `[MovedFrom]`. `OnAfterDeserialize` can run off the main thread — field
shuffling only, no Unity API calls.

**XML doc comments that explain non-obvious Unity Editor behaviour.** Every type gets a `<summary>`; every
member whose correctness depends on an Editor detail gets the reason, not a restatement of the code. The
existing comments set the bar:

* `StyleInjector` — why the stylesheets are re-added from every `BindView` (`HierarchyView.Reset()` destroys
  and recreates `StyleContainer`).
* `HierarchyBootstrap` — why subscription happens in the static constructor rather than on `delayCall`
  (`BindView` is raised from the window's `CreateGUI`, which can run first).
* `RowElements` — why find-or-create exists at all (rows are pooled).
* `ChangeTracker.MarkPropertyChange` — why only O(1) work is allowed there (a transform drag fires it every
  frame).

Do not comment the obvious. A comment that repeats the method name is noise.

**Editor-only.** No `Runtime/` assembly, no code that could end up in a build. Commit the `.meta` file for
every file you add.

---

## 7. Adding a decorator

1. Implement `IRowDecorator` (`Editor/Core/RowContext.cs`): a stable `Id`, the `CacheFacet`s you read, and
   `Apply(in RowContext)`.
2. Register it in `DecoratorHost.s_Decorators` — array order is draw order.
3. Obey §3 in full: find-or-create, re-derive both directions, gate on `IsGameObject`, respect
   `IsFiltering`, null-check `RowContainer`.
4. Add anything expensive to `DecorationCache` / `RowData` with a facet, and invalidate it from
   `ChangeTracker`.
5. Add the settings to `SettingTypes.cs` and surface them in `HierarchyDecoratorSettingsProvider`.

---

## 8. Before opening a pull request

* Compiles clean in 6000.6.0f1 — no new warnings.
* EditMode tests pass locally; say how you ran them and what the exit code was.
* Manually verified in the Hierarchy: scroll a long list, type in the search field, collapse/expand, enter
  and exit a prefab stage, enter and exit play mode. Those are the four paths recycling bugs hide in.
* Nothing written to `Assets/`.
* `ARCHITECTURE.md` updated if you changed or added a decision.
* `CHANGELOG.md` entry.
* No invented numbers. If you did not measure it, do not write it down.
