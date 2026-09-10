# HierarchyDecorator 1.x → Hierarchy Decorator 2.0

2.0 is a rewrite on Unity 6.6's public `Unity.Hierarchy` extension API (UI Toolkit). It requires
**Unity 6000.6.0f1** and the **new** Hierarchy window. Your 1.x configuration is imported once, from a plain
text read of the old asset; the 1.x asset itself is never modified and never deleted.

Everything below is derived from `Editor/Migration/LegacyMigrator.cs` and the settings types it writes into.
Where a 1.x field has no 2.0 counterpart, this document says so instead of implying it survived.

---

## 1. Before you upgrade

* **1.x cannot work on Unity 6.6.** Releases that still use `EditorApplication.hierarchyWindowItemOnGUI` do not
  compile there — it is `[Obsolete(..., error: true)]`. Releases that use the `EntityId` callback do compile,
  but that callback only drives the **legacy** IMGUI window, and 6.6 uses the new Hierarchy window by default
  (`EditorSettings.useLegacyHierarchy` defaults to `false`), so 1.x renders nothing.
* **Remove the 1.x package (or `Assets/HierarchyDecorator/` folder) before installing 2.0**, but **keep its
  `Settings.asset`** until the import has run. The importer reads that file as text; it does not need the 1.x
  scripts to be present or compiling.
* Install 2.0 through **Package Manager → Install package from Git URL**.
* 2.0 requires the new Hierarchy window. With **Project Settings → Editor → Hierarchy → Use Legacy Hierarchy**
  enabled, 2.0 draws nothing and logs a one-time hint to the Console.
* Header rules are still name-prefix based, so your `= PLAYER` / `- Enemies` / `+ Spawn Points` GameObjects
  keep working with no scene changes. 2.0 additionally ships a `---` separator rule.

---

## 2. What happens on install

### 2.1 The automatic import

`HierarchyBootstrap` calls `LegacyMigrator.RunIfNeeded()` once per domain load, on the first
`EditorApplication.delayCall`. It imports **only** when all of the following hold:

1. The 2.0 settings have never recorded a completed migration (`LegacyMigrationCompleted == false`).
2. **No 2.0 settings file exists yet** — `ProjectSettings/Packages/com.wooshii.hierarchydecorator/Settings.asset`
   is absent. This is the "fresh install" test, and it is the reason an automatic import can never overwrite a
   ruleset you already built in 2.0.
3. At least one 1.x settings asset is found. `FindLegacyAssets()` scans `Assets/**/*.asset` and keeps every file
   whose text contains the 1.x `Settings.cs` script GUID `00668fd727de9bb4081a8a202ce24c3b` or the class
   identifier `Wooshii.HierarchyDecorator.Editor::HierarchyDecorator.Settings`. The 1.x `EditorPrefs` pointer
   `<ProductName>_HD_GUID` is used only to move its asset to the front of the candidate list — it is a hint,
   never the sole source.

If several candidates are found, the first one is imported and the Console explains how to pick a different one.
On success the report is logged: source path, number of header rules, number of component rules, any skipped
component entries, and any notes.

Every other case is a no-op:

| Situation | What happens |
|---|---|
| No 1.x asset anywhere in `Assets/` | Nothing is imported. The "migration completed" flag is set so the scan does not repeat. |
| A 1.x asset exists **and** 2.0 settings already exist | Nothing is changed. One Console message names the asset and points at the menu item. The flag is set so the message does not repeat. |
| The asset cannot be read or parsed | One Console message. The flag is **not** set, so the next domain reload tries again. |

### 2.2 The manual import

**Tools → Hierarchy Decorator → Import Settings From 1.x** runs the same importer at any time, on an existing
2.0 setup. It is the only way to import when 2.0 settings already exist.

* No 1.x asset found → a dialog says so and nothing happens.
* Exactly one candidate → that one is used.
* More than one candidate → a file picker opens so you choose.
* A confirmation dialog states that header rules and component rules will be **replaced** and that the 1.x asset
  is not modified. After the import the open Hierarchy rows are refreshed and the report is shown and logged.

### 2.3 What the import never does

* It never loads the 1.x asset through the `AssetDatabase`, so 1.x's own `OnEnable` (which wipes built-in
  show/exclude flags on a Unity version mismatch) cannot run and corrupt the source data. The file is parsed as
  text by `Editor/Migration/MiniYaml.cs`.
* **It never modifies or deletes the 1.x asset**, its `.meta`, or anything else under `Assets/`. Re-running the
  import is safe and repeatable.
* It writes exactly one file: the 2.0 project settings at
  `ProjectSettings/Packages/com.wooshii.hierarchydecorator/Settings.asset`. Per-developer choices live in
  `UserSettings/Packages/com.wooshii.hierarchydecorator/UserSettings.asset`.
* The 1.x `EditorPrefs` key `<ProductName>_HD_GUID` is read and left in place; nothing deletes it.
* Presets (Minimal, Clean, Developer, Designer, Debug) are only applied when you pick one in the settings UI, so
  an import is never overwritten by a preset behind your back.

---

## 3. Value-by-value mapping

### 3.1 Header styles — `styleData.styles[i]` → `HeaderRules[i]`

Rules are rebuilt in list order, and list order is still match precedence: the first matching rule wins, with no
longest-prefix or specificity rule. If the 1.x asset contains no styles, the 2.0 defaults are kept untouched.

| 1.x field | 2.0 field | Notes |
|---|---|---|
| `prefix` | `HeaderRule.pattern` | Copied verbatim. Falls back to `=` if the key is absent. |
| `isRegex` | `HeaderRule.useRegex` | The implicit `^` anchor is kept. 2.0 compiles the pattern once (`Compiled \| CultureInvariant`, 50 ms timeout) instead of matching 4–6× per row per repaint. |
| `noSpaceAfterPrefix` | `HeaderRule.requireSpaceAfterPrefix` | **Inverted**: `requireSpaceAfterPrefix = !noSpaceAfterPrefix`. The literal match itself is unchanged — `StartsWith(prefix)` plus, unless opted out, exactly one following space. |
| `name` | `HeaderRule.name` | Falls back to `Header <index>`. |
| `fontSize` | `HeaderRule.fontSize` | Clamped to 6–24. |
| `fontAlignment` (`TextAnchor`) | `HeaderRule.alignment` (`Left`/`Center`/`Right`) | Mapped by `anchor % 3`. The vertical third of the anchor is dropped: a hierarchy row is a single line. |
| `fontStyle` (`FontStyle`) | `HeaderRule.bold` | `Bold` and `BoldAndItalic` → `true`. **Italic is lost** — 2.0 has no italic option. |
| `textFormatting` | `HeaderRule.textCase` | `ToUpper→Upper`, `ToLower→Lower`, `DontChange→AsTyped`. 2.0 uses the invariant culture. |
| `modes[0].fontColour` | `HeaderRule.textColor.light` | `modes[0]` is the light skin. |
| `modes[0].backgroundColour` | `HeaderRule.backgroundColor.light` | |
| `modes[1].fontColour` | `HeaderRule.textColor.dark` | `modes[1]` is the dark skin. |
| `modes[1].backgroundColour` | `HeaderRule.backgroundColor.dark` | |
| *(missing `modes` entry)* | 2.0 default header colors | Light `0.176` gray on `0.667` gray; dark white on `0.176` gray. |
| `styleData.displayIcons` | `HeaderRule.showComponentIcons` on **every** imported rule | 1.x had one global "show icons on styled rows" switch; 2.0 makes it per rule, so you can differentiate afterwards. |
| — | `HeaderRule.showTreeLines = false` | Matches 1.x, which suppressed breadcrumbs on any styled row. |
| — | `HeaderRule.enabled = true` | 1.x had no per-style enable switch. |
| — | `HeaderRule.kind = Header`, plus an inserted `---` **Separator** rule | 1.x had no separator concept, so 2.0's default separator rule is inserted at index 0 (unless one is already present) — otherwise `---` would fall through to the `-` sub-header rule. It is counted in the report. |
| `font` (`Font` asset) | **not migrated** | 2.0 draws labels with UI Toolkit and has no per-rule font asset. |
| `style` (`GUIStyle`), `capturedGroups` | **dropped** | Derived caches that 1.x serialized by accident; they carry no user intent. |

New 2.0 fields the import leaves at their defaults: `keepPrefixInLabel` (off), `letterSpacing` (0), `showLine`
(off), `lineStyle` (Solid), `lineThickness` (1).

`styleData.displayTags` and `styleData.displayLayers` are **not** migrated — see §4.

### 3.2 Row background — `styleData` → `Rows`

| 1.x field | 2.0 field | Notes |
|---|---|---|
| `twoToneBackground` | `Rows.overrideAlternatingColors` | Copied verbatim; absent key → `false`. 1.x defaulted this to **on**, so an imported project keeps two-tone rows, while a fresh 2.0 install has it **off** (Unity 6.6 alternates rows itself). |
| `lightMode.colorOne` | `Rows.evenColor.light` | |
| `darkMode.colorOne` | `Rows.evenColor.dark` | |
| `lightMode.colorTwo` | `Rows.oddColor.light` | |
| `darkMode.colorTwo` | `Rows.oddColor.dark` | |

Behavioral difference: 2.0 sets a background color on Unity's own row container and derives parity from the view
model's row index. 1.x painted a rectangle over the row and derived parity from `rect.y % 32`, which is why it
then had to re-draw the icon, label, prefab arrow, override bar and foldout by hand.

### 3.3 Breadcrumbs → tree lines — `globalData` → `TreeLines`

| 1.x field | 2.0 field | Notes |
|---|---|---|
| `showBreadcrumbs` | `TreeLines.enabled` | Master switch. Absent → `true`. |
| `instanceBreadcrumbs.displayHorizontal` | `TreeLines.showConnector` | The short horizontal stub into the row. |
| `fullDepthBreadcrumbs.show` | `TreeLines.fullDepth` | Guide lines for every ancestor level. |
| `instanceBreadcrumbs.style` | `TreeLines.style` | `Solid→Solid`, `Dash→Dashed`, `Dotted→Dotted`. |
| `instanceBreadcrumbs.color` (alpha) | `TreeLines.opacity` | The alpha becomes opacity, clamped to 0.05–1. |
| `instanceBreadcrumbs.color` (RGB) | `TreeLines.color.light` **and** `.dark` | Stored with alpha forced to 1, and the same color for both skins, because 1.x had a single color. |
| `instanceBreadcrumbs.show` | **not read** | 2.0 has one enable switch (the master one) rather than two. |
| `fullDepthBreadcrumbs.{color, style, displayHorizontal}` | **not read** | 2.0 draws the whole tree with one style and one color; the full-depth record only contributes its `show` flag. |

### 3.4 Components — `components` → `ComponentIcons`, `Indicators`, `ComponentRules`

| 1.x field | 2.0 field | Notes |
|---|---|---|
| `enableIcons` | `ComponentIcons.enabled` | Absent → `true`. |
| `clickToToggleComponent` | `ComponentIcons.clickAction` | `true → ToggleEnabled`, `false → Select` (select-and-ping; 1.x had no such mode). Absent → `true`. Toggling is now undoable and uses the public `EditorUtility.Get/SetObjectEnabled` instead of reflection. |
| `showMissingScriptWarning` | `Indicators.showMissingScripts` | Absent → `true`. |
| `stackDuplicateIcons` | `ComponentIcons.stackDuplicates` | Absent → `false`. 2.0 adds a count badge to the stacked icon. |
| `showAll` bit 1 (`Unity`) | `ComponentIcons.mode` | Set → `All` (everything except what a rule hides), clear → `Selected` (only what a rule shows). Absent → `3`. |
| `showAll` bit 2 (`Custom`) | `ComponentIcons.includeCustomScripts` | **Caveat:** `includeCustomScripts` only has an effect in `All` mode. A 1.x asset with `showAll == 2` (custom scripts only) therefore becomes "show only components an explicit rule shows". Re-check the icon settings after importing such a project. |
| `unityGroups[].components[].{shown, excluded}` | a `ComponentRule` with `display = Show` / `Hide` | Entries with **neither** flag set are skipped — they are 1.x's untouched machine-generated rows, not user intent. |
| …their `name` (assembly-qualified) | `ComponentRule.typeName` + `assemblyName` (+ `monoScriptGuid` when one exists) | Resolved with `Type.GetType` during the import and re-keyed to the namespace-qualified name plus the simple assembly name, because assembly-qualified names are not stable across Unity versions. If the type no longer resolves, the text before the first comma is stored as `typeName` so the rule stays visible in the settings UI instead of vanishing. A blank name counts as unresolved and is reported as a skipped entry. |
| `customGroups[].components[].{shown, excluded}` + `script.guid` | a `ComponentRule` with `monoScriptGuid` | The MonoScript GUID is the identity, so the rule survives a class rename, a namespace change, an asmdef rename and file moves. |
| `allCustomComponents.components[]` | same treatment | Only entries with `shown` or `excluded` set. |
| `unityGroups[].name`, `customGroups[].name`, group membership | **not migrated** | See §4. |
| `content`, `displayName`, `isBuiltIn`, `hasToggle`, `unityVersion`, `unityCount` | **dropped** | Derived caches; 2.0 rebuilds all of it from `TypeCache`. |

New 2.0 icon fields the import leaves at their defaults: `hideTransform` (on — Transform and RectTransform icons
are suppressed), `maxIconsPerRow` (8) with `showOverflowIndicator`, `showTooltips` (on),
`fadeDisabledComponents` (on), `order` (Natural).

---

## 4. What is **not** migrated, and why

| 1.x data | Why it is gone |
|---|---|
| `globalData.showActiveToggles`, `activeToggleType` (Checkbox/Dot) | **Native Unity 6.6.** Every row in the new window has a real toggle (`HierarchyViewItem.Toggle`), and Active is a native, resizable column. Re-drawing a checkbox at a hard-coded `rect.x = 32` has no purpose now. |
| `globalData.activeSwiping`, `swipeSameState`, `swipeSelectionOnly`, `depthMode` | **Deferred.** The click-drag "swipe" gesture across many checkboxes has no native equivalent and is genuinely useful, but it is not implemented in 2.0.0, so there is nothing to import into. (The 1.x implementation was also partly broken: the plain-click path never wrote `SetActive` while swiping was enabled, and the shift path read `Event.current.keyCode` instead of `Event.shift`, so it never engaged.) |
| `globalData.tagSettings.*`, `layerSettings.*`, `tagLayerLayout`, `hideUntagged` | **Native Unity 6.6.** Tag and Layer are native columns — enable them from the Hierarchy header's right-click menu. 1.x's 48 px three-cell grid, its overflow trimming and its `GenericMenu` pickers all existed because IMGUI had nowhere else to put them. |
| `layerSettings.applyChildLayers` | **Removed intentionally.** It was a behavior of 1.x's own layer dropdown, which no longer exists. It also only ever went one level deep and did not record the children for undo. |
| `tagSettings/layerSettings.colorSettings` (solid color, random/hashed color, hue/saturation/brightness) | **Removed intentionally.** It colored labels 2.0 no longer draws. The hashed-color cache in 1.x was static, non-serialized and only filled from the settings tab, so it silently fell back to the default color after most domain reloads anyway. |
| `styleData.displayTags`, `displayLayers` | **Removed intentionally.** They meant "still show tag/layer on a header row". Tags and layers are now per-column, not per-row, so there is no per-rule equivalent to map onto. `displayIcons` does survive, as a per-rule flag (§3.1). |
| `styleData.showSceneItemHighlight`, `sceneItemHighlight.{color, lineThickness}` | **Removed intentionally.** Scene rows are a first-class node type in 6.6 (`HierarchySceneHandler`) that Unity styles itself; 2.0's decorators deliberately act on GameObject rows only. 1.x drew this bar by scanning every open scene for every non-GameObject row and painting at `rect.x - 48`. |
| `components.customGroups[].name` and group membership, `allCustomComponents` structure | **Removed intentionally.** 2.0 replaces named groups with one flat, searchable rule list keyed by MonoScript GUID; only the per-member `shown`/`excluded` bits carry real user intent and those *are* imported. 1.x's group structure was derived data with a serialization defect that lost groups on restart (upstream issue #119). |
| `components.unityGroups[].name` (General / 2D / Animation / Audio / Mesh / Physics / Network / UI) | **Removed intentionally.** The names came from a hard-coded filter table and the groups were regenerated on every asset import. 2.0 enumerates component types from `TypeCache` and offers a search box instead of categories. |
| `globalData.showAllComponents` | Dead field in 1.x — serialized but never read. |
| Per-style `font` asset, italic font style, vertical text anchor | No 2.0 equivalent (§3.1). |
| `EditorPrefs` key `<ProductName>_HD_GUID` | Read as a hint during the scan, then left alone. 2.0 does not use `EditorPrefs` at all. |

---

## 5. Feature-by-feature regression table

Every major 1.x feature, classified exactly once.

| 1.x feature | Verdict | Reason |
|---|---|---|
| Header / prefix styles (`=`, `-`, `+`) | **Preserved** | Same matching contract, same shipped defaults, same first-match-wins ordering; rules are imported one-for-one. |
| Regex header rules | **Improved** | Precompiled once with a match timeout and ordinal/invariant comparison; a pattern with no capture group now matches instead of silently doing nothing; invalid patterns are reported in the settings UI. |
| Text formatting (upper / lower / unchanged) | **Preserved** | Imported as `textCase`, now invariant-culture. |
| Prefix stripping in the drawn label | **Improved** | 1.x had two divergent implementations (measured width and drawn text could disagree); 2.0 derives the label once, in `NameMatcher`, and caches it on the row. |
| Per-style font asset and italic | **Removed intentionally** | UI Toolkit labels use the editor font; the option carried little value and no clean USS mapping. |
| "Show component icons on styled rows" | **Preserved** | Promoted from one global switch to a per-rule `showComponentIcons`. |
| "Show tag / layer on styled rows" | **Replaced by native Unity** | Tag and Layer are columns now; the per-row switch has no meaning. |
| Two-tone / alternating row background | **Replaced by native Unity** | 6.6 alternates rows itself. 2.0 keeps a color **override** only, applied to Unity's row container instead of painting over the row. |
| Row re-implementation (GameObject/prefab icon, prefab text colors, prefab arrow, added-override badge, foldout triangle) | **Replaced by native Unity** | 2.0 decorates and never repaints, so none of this has to be rebuilt — which also fixes the hidden prefab override bar (#111) and the hidden SubScene caret (#86) by construction. |
| Selection / hover / inactive tint | **Replaced by native Unity** | It only existed to repair what the two-tone paint destroyed. UI Toolkit supplies the states. |
| Scene-header highlight bar | **Removed intentionally** | Unity styles scene rows itself in 6.6; 2.0 decorates GameObject rows only. |
| Breadcrumbs / tree guide lines | **Improved** | Pooled `VisualElement`s positioned from Unity's real indent geometry (4 px override bar + 14 px per level), with a repeating texture for dashed and dotted styles instead of dozens of `Handles.DrawLine` calls per row per repaint. The `rect.x < 60` bail-out is gone. |
| Separate instance / full-depth breadcrumb styling | **Removed intentionally** | 2.0 draws the tree with one style and one color; only the full-depth *enable* flag survives. |
| Active-state toggle (checkbox / dot) | **Replaced by native Unity** | Native row toggle plus a native Active column. |
| Swipe-toggle gesture | **Deferred** | No native equivalent and worth having, but not implemented in 2.0.0. |
| Shift multi-toggle over the selection | **Removed intentionally** | It never worked in 1.x — the loop called `SetActive` on the same object N times instead of the selected ones. |
| Component icon strip | **Improved** | Derived once per bind from a cache instead of per repaint, right-aligned at the end of the Name column, with a per-row cap and an overflow indicator (#141), fading for disabled components (#84), and an optional dedicated column. |
| Click an icon to toggle the component | **Improved** | Public `EditorUtility.GetObjectEnabled` / `SetObjectEnabled` instead of reflection, and the change is recorded for undo (1.x only called `SetDirty`). Works with stacked icons too (#147). |
| Right-click an icon for Unity's component context menu | **Removed intentionally** | It was reflection into the non-public `EditorUtility.DisplayObjectContextMenu`. The row's own native context menu remains, and 2.0 appends its own submenu (convert to header, clear decoration, settings). |
| Stack duplicate icons | **Preserved** | Now with a count badge, and built at bind time rather than string-concatenated into a tooltip every frame. |
| Missing-script warning | **Preserved** | Drawn as a badge next to the row; the detection is a by-product of the cached component scan. |
| Per-component show / exclude | **Preserved** | Imported into a three-state rule list. Only non-default rules are stored, so the settings file stays small. |
| Display mode flags (Unity / Custom) | **Preserved** | Imported as `mode` + `includeCustomScripts` (see the caveat in §3.4). |
| Unity component category groups (2D, Animation, Audio, …) | **Removed intentionally** | Replaced by a searchable flat list over `TypeCache`; the categories were a hard-coded filter table regenerated on every asset import. |
| Custom component groups + MonoScript drag-and-drop | **Removed intentionally** | Replaced by GUID-keyed rules; the group model was the source of upstream #119. |
| Auto-registering unknown MonoBehaviours from the render path | **Removed intentionally** | Mutating the settings asset from inside `OnGUI` is exactly what caused the duplicate-registration spam of #134. |
| Component identity by `Type.AssemblyQualifiedName` | **Improved** | Stored as MonoScript GUID + namespace-qualified name + assembly name, resolved GUID-first for user scripts. |
| Component enable state via reflected `enabled` property | **Improved** | Replaced by the public `EditorUtility` API. |
| Depth / last-sibling / has-children computation | **Replaced by native Unity** | Read from the hierarchy view model instead of walking the `Transform` parent chain per row per repaint. |
| IMGUI per-row dispatch (`hierarchyWindowItem*OnGUI`) | **Replaced by native Unity** | `HierarchyWindow.BindViewItem` / `UnbindViewItem` fire when a row scrolls into view or changes, not on every repaint. |
| Per-repaint component enumeration (`ComponentList`) | **Improved** | One bind-time snapshot in `DecorationCache`, invalidated per facet from `ObjectChangeEvents`. |
| Component data rebuild on every asset import | **Improved** | The catalog is invalidated explicitly rather than rebuilt (with a `Debug.LogWarning`) on every import batch. |
| Settings asset in `Assets/` + `EditorPrefs` GUID pointer | **Improved** | Shared rules in `ProjectSettings/`, per-developer choices in `UserSettings/` (git-ignored). Answers upstream #25 and removes the `FindAssets("t:Settings")` collision. |
| Settings UI (hand-rolled IMGUI tabs, ~2400 lines) | **Improved** | One UI Toolkit page under Project Settings → Hierarchy Decorator, plus presets. |
| Preferences window entry | **Preserved** | Preferences → Hierarchy Decorator now holds the per-developer options; the shared ruleset lives in Project Settings. |
| Tab registration attribute + full-AppDomain type scan | **Removed intentionally** | 2.0.0 has a single settings page and no third-party tab API. |

---

## 6. What you will notice after upgrading

* **Alternating rows look different.** On a fresh install the two-tone override is off, because Unity 6.6 draws
  alternating rows natively. After an import it stays on if 1.x had it on (1.x defaulted it to on), but it now
  only recolors the row container — selection, hover, the prefab override bar and the foldout arrow are Unity's
  again.
* **Component icons moved.** They are right-aligned at the end of the Name column instead of occupying a fixed
  grid at the far right of the window. There is also an optional **Components** column, hidden by default and
  enabled from the Hierarchy header's right-click menu.
* **At most 8 icons per row by default**, with an overflow indicator, and Transform / RectTransform icons are
  hidden. 1.x drew as many as fit and dropped the rest silently.
* **Tag and layer are native columns.** Turn them on from the Hierarchy header's right-click menu. They are
  resizable and reorderable, and long tag names are no longer clipped into a 48 px slot.
* **The active checkbox is Unity's.** Every row has one, and there is a native Active column; the swipe gesture
  is gone for now.
* **Headers no longer hide row chrome.** The foldout triangle, prefab arrow, override bar and SubScene caret stay
  visible on decorated rows.
* **Tree lines disappear while you search.** Unity flattens the list and reports indent 0 during filtering, so
  depth-based decoration is suppressed on purpose.
* **Regex rules without a capture group now match** (1.x silently ignored them); the label becomes the name minus
  the matched span.
* **Nothing is written to `Assets/` any more.** After importing, the old `Settings.asset` is dead weight and can
  be deleted whenever you like.
* **Nothing renders with the legacy Hierarchy window.** If you see no decorations and a one-time Console hint,
  check Project Settings → Editor → Hierarchy → Use Legacy Hierarchy.
* **Performance:** the expensive work (component enumeration, icon resolution, header matching) moved from every
  repaint to once per row bind, with per-facet cache invalidation. **No before/after numbers have been measured
  yet.** The intended methodology is to compare 1.x on the legacy window against 2.0 on the new window in
  generated 100 / 1k / 10k GameObject scenes, measuring editor frame time with the cursor moving over the
  Hierarchy window; until that is run, treat the improvement as a design claim, not a measurement.

---

## 7. Rolling back to 1.x

Nothing about 2.0 blocks a rollback:

* **Your 1.x asset is still there.** The importer only read it. If it is still in `Assets/`, reinstalling 1.x
  picks it up exactly as before.
* **Remove 2.0's own configuration** (optional) by deleting
  `ProjectSettings/Packages/com.wooshii.hierarchydecorator/Settings.asset` and
  `UserSettings/Packages/com.wooshii.hierarchydecorator/UserSettings.asset`. Nothing else belongs to the package.
* **Reinstall 1.x by tag.** In Package Manager → *Install package from Git URL*:
  `https://github.com/WooshiiDev/HierarchyDecorator.git#v0.12.0` — the last tagged 1.x release. (`0.12.1` exists
  on `master` as a version bump but was never tagged.) For an embedded copy, `git checkout v0.12.0` in the
  package folder.
* **Remember why 1.x stopped working.** On Unity 6.6 it renders nothing under the default new Hierarchy window.
  A rollback is only useful together with either Project Settings → Editor → Hierarchy → **Use Legacy Hierarchy**,
  or an older Editor version.
* 2.0 and 1.x share the package name `com.wooshii.hierarchydecorator`, so install one or the other, never both.

---

## Attribution

Hierarchy Decorator was created by **Damian Slocombe (WooshiiDev)** and is distributed under the MIT License,
`Copyright (c) 2020 - 2024 Damian Slocombe`. 2.0 is a modernization of that work and keeps the same license and
the same copyright notice; see `LICENSE.md`.
