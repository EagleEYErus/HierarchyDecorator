## v2.0.1 — Defects found by using it

Six defects in the default configuration, all found by inspecting a live Hierarchy window rather than by
reading the code, and all verified fixed the same way. Nothing here changes the architecture.

### Fixed

- **A selected row lost its selection highlight.** A header fill, a separator and the optional row-colour
  override all write an inline background onto the row container, and an inline style outranks every
  stylesheet — including the rule that draws Unity's selection. The `--selected` class was on the row and had
  no effect. Measured on a live window: a selected header row painted `RGBA(0.176, 0.176, 0.176)`, its own
  fill, where an undecorated row painted the editor's selection colour. Rows Unity has selected are now left
  unfilled; the row is rebound when the selection changes, so this costs one `HierarchyView.IsSelected` call
  per bound row and no subscription.
- **Hovering a component icon did nothing in the dark theme, and blackened the icon in the light one.** The
  hover rule used `-unity-background-image-tint-color`, which *multiplies* the image: the dark theme's white
  was a no-op and the light theme's `rgb(40, 40, 40)` scaled every channel to a sixth. It is a backing plate
  now — a translucent rounded rectangle behind the icon, which reads the same in both themes.
- **Clicking a component icon stole the row's selection and its drag.** The handler ran on `PointerDownEvent`
  and called `StopPropagation`, but row selection and the drag-and-drop arming both sit on ancestors in that
  event's bubble phase. A row could not be selected or dragged by any pixel the icon strip covered. The
  action moved to `ClickEvent`, which arrives after selection has happened, and propagation is no longer
  stopped; modified and repeated clicks are left entirely to Unity.
- **The missing-script badge was invisible on header rows and hid the object icon on the others.** It tested
  `Icon.resolvedStyle.display`, which still describes the previous frame — the header decoration hides that
  icon a few lines earlier in the same dispatch — so the badge was written onto an element that had just been
  hidden. It reads the inline value now, and is drawn as an 11px mark in the corner of the icon rather than
  at the slot's full size over it.
- **The Children column showed another row's count.** Its cell descriptor only matches
  `HierarchyGameObjectHandler`, so nothing ran when a pooled cell was rebound to a scene row, and the cell
  kept both the text and the flag that makes Unity show it. Both columns now reset their cell on unbind.
- **The Components column header showed a circled "i" that did nothing.** That was Unity's Inspector-window
  icon, used as a column icon; it reads as a help button, but a column header is inert and our columns are
  not sortable. The icon is gone.

### Changed

- The default component-icon click action is **Toggle Enabled** rather than **Select**. `Select` puts the
  same GameObject in the Inspector that clicking the row already would, so with it the icons looked inert.
  `Select` is still available.

### Known limitation, measured

`HierarchyViewColumnDescriptor.Tooltip` has no effect in Unity 6000.6.0f1: the string reaches no element of
the column header, and neither `MakeHeader` nor `BindHeader` is ever invoked — assigning `MakeHeader` makes
the column disappear from the window altogether. A custom column therefore cannot explain itself on hover.
The property is still set, so the tooltip appears by itself if Unity wires it up.

## v2.0.0 | Unreleased — Rebuilt on the Unity 6.6 Hierarchy

Hierarchy Decorator has been rebuilt on Unity 6.6's public `Unity.Hierarchy` hierarchy extension API (UI Toolkit), replacing the IMGUI row callback 1.x was written against.

### Breaking Changes

- Minimum Unity is now **6000.6** (`"unity": "6000.6"`). Earlier versions are not supported.
- The **new** Hierarchy window is required. With `Project Settings > Editor > Hierarchy > Use Legacy Hierarchy` enabled, 2.0 draws nothing and logs a one-time hint explaining how to switch. The legacy IMGUI window is not supported and no IMGUI fallback is shipped.
- Settings no longer live in `Assets/`. The shared ruleset is written to `ProjectSettings/Packages/com.wooshii.hierarchydecorator/Settings.asset` and per-developer choices to `UserSettings/Packages/com.wooshii.hierarchydecorator/UserSettings.asset`. The `Assets/HierarchyDecorator/Settings.asset` lookup, its `AssetDatabase.FindAssets("t:Settings")` search and the `EditorPrefs` GUID cache are gone.
- The 1.x public API is removed. `HierarchyManager`, `HierarchyDrawer`, `HierarchyInfo`, `HierarchyItem`, `ComponentItem`, the `Settings` ScriptableObject, `RegisterTabAttribute` and the `GUIDrawer` settings framework no longer exist, and code written against them will not compile. 2.0 ships no public extension API: the internal `IRowDecorator` seam will only be published once it has settled.

### Added

**Integration**

- Rendering through `HierarchyWindow.BindView` / `BindViewItem` / `UnbindViewItem`. Decorations are `VisualElement`s placed in the containers Unity exposes for that purpose (`LeftCustomContainer`, `RightCustomContainer`, `RowContainer`); nothing is painted over a finished row.
- A per-object decoration cache keyed by `EntityId`, filled at bind time and invalidated per facet from `ObjectChangeEvents.changesPublished`. Component enumeration, icon resolution and header matching run once per row, not once per repaint.
- Decorator error isolation: a decoration that throws is disabled for the rest of the session and logged exactly once, so a single bug can neither break the Hierarchy nor spam the console.
- Theme-aware stylesheets (`HierarchyDecorator_dark.uss` / `_light.uss`) injected into `HierarchyView.StyleContainer` on every view bind, scoped under a marker class so they cannot leak into other windows that reuse `HierarchyView`.

**Decorations**

- Headers and separators from name-prefix rules, with alignment, text case, bold, font size, letter spacing, paired light/dark colors, an optional solid/dashed/dotted line and thickness, an option to keep the prefix in the label, and per-rule toggles for component icons and tree lines. Regex patterns are supported and compiled once instead of being re-evaluated per repaint.
- Tree guide lines (1.x "breadcrumbs"): full depth or immediate parent only, optional connector, solid/dashed/dotted, opacity and a paired light/dark color. Hidden automatically while the Hierarchy search filter is active, because Unity flattens the list and drops indentation there.
- A component icon strip at the right-hand end of the Name column: `All` or `Selected` mode, include or exclude user scripts, hide `Transform`, an icon limit with an overflow indicator, duplicate stacking, dimming for disabled components, natural or alphabetical order, tooltips, and a click action (none / select the component / toggle it enabled).
- A missing-script badge, with the count repeated in the row tooltip.
- An optional **Components** column — a real Unity 6.6 hierarchy column, hidden by default and enabled from the Hierarchy header menu. It shares the decoration cache with the inline strip, so turning it on costs no extra component scanning.
- An opt-in override of the alternating row colors, per theme. Off by default, because Unity 6.6 draws alternating rows itself.
- A Hierarchy context submenu: `Hierarchy Decorator > Convert To > <rule>`, `Clear Decoration` and `Settings...`. It acts on the selection, or on the clicked row when that row is outside the selection, and every rename is recorded for undo.
- Row tooltips now also carry the real GameObject name behind a header label.

**Settings**

- Two UI Toolkit settings pages: `Project Settings > Hierarchy Decorator` for the shared ruleset, `Preferences > Hierarchy Decorator` for per-developer choices. Both are `SerializedObject`-bound, so undo behaves normally, and edits apply to the live Hierarchy as you type.
- Presets: Minimal, Clean, Developer, Designer and Debug, plus user presets captured from the current settings. A preset covers the feature toggles only, never the header or component rules a team authors.
- A "Displayed Components" list: every component type in the project with a three-state rule (Default / Show / Hide). A rule is stored only when it differs from the default, so a project that never touches the list writes nothing.
- Component identity is stored twice — MonoScript GUID plus namespace-qualified type name plus assembly name — and types are enumerated with `TypeCache` rather than `AppDomain.GetTypes()`. A rule survives a class rename, namespace change or assembly rename, and repairs whichever half of the key went stale.
- A versioned settings schema with an ordered migration chain, so a future format change does not reset configuration.
- `Tools > Hierarchy Decorator`: `Settings...`, `Disable All Decorations`, `Re-enable Failed Decorations`, `Import Settings From 1.x`, `Reset Settings To Defaults`.

**Migration**

- An importer for 1.x settings. It reads the old asset as text, never modifies or deletes it, maps prefix styles to header rules and icon selections to component rules, and reports what was imported and what was skipped. It runs once automatically on first load and can be re-run from the Tools menu.

**Tests**

- An EditMode suite over the package's pure-logic layers: the name matcher (prefix, trailing-space and regex rules), the YAML reader the importer uses, the 1.x migration, the decoration cache, the `ObjectChangeKind` mapping, the component catalog, the settings schema and presets, and the search filters. 61 tests, run against Unity 6000.6.0f1 (`total=64 passed=61 failed=0`; the three skipped are the `[Explicit]` benchmarks below).

### Search

- `hd:header`, `hd:separator`, `hd:none` and `hdrule:<name>` filters in the Hierarchy search box. The documented extension point for this is internal, but the GameObject node handler forwards unknown filters to `SceneQueryEngine`, which collects filters from every loaded assembly through `TypeCache` — so the public `[SceneQueryEngineFilter]` is all it takes. They compose with Unity's own filters and appear in the query-builder dropdown. `has:MissingScript` is deliberately **not** shipped: Unity already answers it with `missing:script`.

### Changed

- **Active toggles** are Unity's native row toggle now. 1.x's `ToggleDrawer` is not ported.
- **Tag and Layer display** are Unity's native, resizable and reorderable Tag and Layer columns now. 1.x's `TagLayerInfo` grid, overflow math and dropdown pickers are not ported.
- **Alternating rows** are native and on by default. 2.0 only offers an override of the colors.
- Inactive tint, prefab text colors, the prefab arrow, the prefab override bar and the foldout are drawn by Unity again; 2.0 never re-implements them. Selection is Unity's too — a selected row is skipped by anything that would fill its background. Hover is the one state 2.0 cannot preserve on a filled row, because a pseudo-state cannot be read from script.
- Component enable state is read and written through the public `EditorUtility.GetObjectEnabled` / `SetObjectEnabled` instead of reflection over `Component.enabled`.
- Header matching keeps 1.x's exact rule — `StartsWith` on the prefix plus, unless the rule opts out, a single following space — so existing scenes keep rendering as they did. The shipped defaults are `---` (separator), `=`, `-` and `+`.
- The two divergent prefix-stripping implementations in 1.x are unified into one, so the drawn label and the matched label can no longer disagree.

### Verified

Run against Unity 6000.6.0f1 in a real editor, not asserted:

- Light and dark theme, including switching the Editor theme while the Hierarchy is open: the correct theme stylesheet is swapped in, header colors invert, component icons re-resolve.
- Prefab Mode, including nested depth: guide lines are drawn from the prefab root, not the scene root.
- Multi-scene: two additively loaded scenes, headers decorated in both.
- Play Mode: decorated before entering, during, and after returning to Edit Mode.
- Undo and redo of a header conversion, including that Unity's own row label keeps the real object name.
- A GameObject with a genuinely missing MonoBehaviour script: the badge is drawn, healthy rows are untouched, rendering does not fall over.
- Fresh install into an empty project: compiles with no errors or warnings, writes nothing into `Assets/`, and the settings match the preset the UI says is active.
- Cache fill benchmark at 100 / 1 000 / 10 000 GameObjects — see PERFORMANCE.md §6.0.

Not verified: UI Toolkit's own layout and repaint cost while scrolling, which needs an interactive Profiler session.

### Second review pass

- Deleting every header rule now sticks. The defaults were re-seeded whenever the list was empty, so a team that does not use the prefix convention could not turn the feature off.
- A settings file written by a newer version of the package is left alone and reported, instead of having its schema version silently stamped down.
- A fresh install actually applies the preset the dropdown claims is active.
- Custom preset names are made unique, and Delete is disabled for the built-in presets — duplicates made Apply and Delete act on the wrong one.
- Switching the Editor theme invalidates cached component icons, which are skin-dependent.
- The "already handled this frame" guard is a frame stamp rather than a sticky flag; it could arm on a change that never produced a hierarchy event and then swallow a real one much later.
- Settings writes are debounced properly: dragging a slider for two seconds rewrote the whole settings file about a hundred times, and the file is flushed before a domain reload and on quit.
- Component icon and display name are cached per type instead of resolved per instance — filling sixty rows cost hundreds of native calls and throwaway strings for a few dozen distinct types.
- The component rule list no longer does AssetDatabase work on every keystroke, and stopped rebuilding every pooled row while filtering.
- A centred header on a nested object is now centred where a centred header should be. The indent is a `translate`, which does not change the laid-out box, so the label drifted right by the indent — verified fixed in a live editor at three different depths.
- The Components column respects header rules and its icons are clickable, like the inline strip.
- Adds tests for the ObjectChangeKind mapping, built on `ObjectChangeEventStream.Builder`, so the four cache fixes above cannot silently regress (61 EditMode tests).

### Also added after the first review pass

- A **Children** column: descendant count including collapsed subtrees, one native call per visible row, hidden by default.
- Scene rows are no longer inert — `Hierarchy Decorator > Ping Scene Asset` in the context menu and the scene path in the row tooltip (upstream issue #127).
- The settings page reports header rules whose regular expression is invalid, and component rules whose type no longer resolves. Both were previously silent.
- The missing-script badge is drawn on `OverlayIcon`, which overlaps the object icon and costs no horizontal space, falling back to its own element when Unity is using that slot for a prefab-override indicator.

### Removed

- The IMGUI renderer: `HierarchyGUI`, `StyleDrawer`'s row re-implementation, `StateDrawer`, `ToggleDrawer`, `TagLayerInfo`, `BreadcrumbsDrawer` and the static `GUIStyle` bank in `Style.cs`. A large part of that code existed only to repair the row the plugin itself had painted over.
- The hand-rolled IMGUI settings UI (~2400 lines): `SettingsEditor`, the tab classes, `GUIDrawer` / `DrawerGroup` and the `ReorderableList` version workarounds, replaced by UI Toolkit and `SerializedObject` binding.
- `ReflectionUtility` and the `AppDomain` type scans it fed.
- The active-swipe toggle gesture and the scene-row highlight added in 0.12.0 are not part of 2.0.

### Fixed

- **#148 — does not compile on Unity 6.3+.** Those errors came from members Unity marked obsolete-as-error. 2.0 references none of them: no `EditorApplication.hierarchyWindowItemOnGUI`, no `GetInstanceID`, no `int` instance-id conversions — `EntityId` is used end to end.
- **#144 — nothing renders under the new Hierarchy window.** 1.x drew from an IMGUI callback the new window never invokes. 2.0 draws from `HierarchyWindow.BindViewItem`, the new window's own per-row extension point, which is also why the new window is now a requirement rather than an option.
- **#111 — the two-tone background hid the prefab override bar.** 2.0 never paints over a finished row. A header or a row tint sets a background color on the row container Unity lays out, and Unity's own override bar sits inside that row and keeps drawing on top of it.
- **#86 — the SubScene caret was hidden.** Every decorator gates on `item.Handler is HierarchyGameObjectHandler` before touching a row, and SubScene rows use a different handler, so they are left untouched. On GameObject rows nothing is drawn over the foldout either.
- **#119 — custom groups were lost on restart.** The group model and its rebuild-on-deserialize step are gone. Component rules are plain serialized data identified by MonoScript GUID plus namespace-qualified type name plus assembly and resolved through `TypeCache`; no load-time path can rename a rule or clear its selection.
- **#142 — the editor hung with large selections and the two-tone background.** Two-tone is Unity's native alternating rows now, and the optional color override writes one background color per row at bind time. `BindViewItem` fires when a row scrolls into view or its data changes, not every repaint, and nothing in the render path reads `Selection`, so cost no longer scales with the size of the selection.

### Performance

- Cache fill measured on Unity 6000.6.0f1 / Apple M1 Pro, headless and reproducible from a committed `[Explicit]` test — see [PERFORMANCE.md §6.0](PERFORMANCE.md). Cold **~1.4 µs per row**, warm **~0.1 µs per row**, **~315 bytes** per cached row; a full screen of ~60 rows costs about **0.08 ms** the first time it scrolls into view and about **0.006 ms** on the way back. The benchmark fails if a warm pass is not far cheaper than a cold one, so bypassing the cache cannot pass silently.
- UI Toolkit's own layout and repaint cost while scrolling is **not measured** — it needs an interactive Profiler session, and PERFORMANCE.md §6.1 onward are left as empty procedure. The **Benchmark Scene Generator** sample builds the 100 / 1k / 10k scenes for it, and the `HierarchyDecorator.DecorateRow`, `.ScanComponents` and `.MatchHeaderRule` profiler markers are shipped so the cache can be verified rather than assumed.

### Migration

- Upgrading from 1.x: see [MIGRATION.md](MIGRATION.md) for the full settings mapping and for what the importer cannot carry over.

## v0.12.0

### Changes

- Added an optional scene highlight in the hierarchy #116 @emptybraces

### Fixes

- Fixed incorrect scene ID comparison. Now using handles, which should work for Unity 6. #116 @empybraces
- Removed `Wooshii.HierarchyDecorator` assm to stop `Assembly for Assembly Definition File will not be compiled, because it has no scripts associated with it.` spam. 

## v0.11.2

### Fixes
- GameObjects named - or ------ would cause index out of bounds exceptions, breaking the hierarchy past that object - #136 @hypevhs 

## v0.11.1

### Changes
- No longer using `EditorApplication.update` to initialise any data. 
- An event `HierarchyDecorator.OnSettings` is now called when the settings scriptable is cached.

### Fixes
- When renaming, deleting or moving assets, HierarchyDecorator now caches component data to avoid data loss.

## v0.11.0 | Community Features & Fixes

### Features
- Option to change the active toggle type - #122 @emptybraces 
- Add context menu to ComponentIconInfo - #105 @lekakid
- Click To Toggle Components - #110 @IndieSeal 
- Add regex support for prefix - #129 @edunad 

## Fixes
- Changing a Transform to a RectTransform would result in the loss of a component reference - #121 @emptybraces
- Fixed Indentation incorrectly shifting when the tag or layer was hidden in horizontal layout - #118 @emptybraces
- Fixed an inconsistent bug which occurred after opening a project, throwing constant exceptions - #115 @emptybraces
- Added missing prefab asset colouring - #124 @ToxPlayers
- Issue #123: Out of Bounds if GameObject only has a prefix and nothing after it - #128 @kaekaes 
- Issue #126: Safe substring checks added to prefix - #128 @kaekaes 

**Full Changelog**: https://github.com/WooshiiDev/HierarchyDecorator/compare/v0.10.1...v0.11.0

## v0.10.1

 - Removed unnecessary files in the package. 

## v0.10.0 | Tags, toggle icon components

### Features

 - Tags can now be displayed in the hierarchy.
 - Options to display tags and layers above each other or horizontally.
 - Disabling/enabling components is now possible by clicking their respective icon.
 - Add feature and toggle for no space after prefix - #103 @sqirradotdev 
 
### Changes

 - Warning icon is now always shown at the end of the icon list.
 - "Stack MonoBehaviours" setting has now been renamed to "Stack Duplicate Icons".
 - Stacked icon tooltips now display all components it represents.
 - Tags & Layers now hide when the hierarchy width is too small.

### Fixes
 - With the new API changes, errors with missing instances have been resolved. 
 - Breadcrumbs now correctly align with siblings, parents and children.
 - Breadcrumbs no longer overlap toggles when using search filter.
 - Game objects that only have hidden children will no longer show a foldout.
 - Settings no longer attempt to draw deleted styles.
 - GameObjects now display prefab states correctly when using two-tone background.
 - Hierarchy settings locked on inspectors, will reinitialise correctly after Unity reloads.
 - Settings editor now updates on Undo.
 - Fix for Scene getting dirtied every frame - #98 @Podden 

### API Changes

**General**

The hierarchy iteration has been overhauled replacing the gameobject references with data containers. This has brought large performance gains, especially with component icons.
 - `HierarchyItem` is a container for GameObjects, to easily cache components and data.
 - `ComponentItem` contains data for each component on a Game Object.
 - `Components` is a collection wrapper, containing the components on a `HierarchyItem`.
 - `HierarchyManager` is now the container for both drawing features and tracking individual hiearachy instances.

**Hierarchy Info**

 - Hierarchy Info now provides `ValidateGrid()` to check if the grid size is valid. This can also be used to prepare data that requires rect or grid information before drawing. 
 - If `ValidateGrid()` returns false, this tells the info element that the info cannot be drawn and will return early.

### Others
 - Removed resources and example scenes due to being unused/useless
 - Fix breadcrumbs last sibling bug when deleting last sibling - #101 @gustaflindqvist (This has been replaced with new cache but thank you for this at the time)
 
Cheers ~ Wooshii

## v0.9.1 | Hotfix - Duplicate component types

This is to fix a use case of types being named the same that may exist in the same project.

Unfortunately, as usual, you will need to delete your settings instance.
 - Looking to find a solution to this.
 
 Cheers
~ Wooshii

## v0.9.0 | Expanded settings, breadcrumbs, new icon settings & further API additions

### New Features

**Active Swiping**
 Toggles now have active swiping! Automatically toggle instances by click dragging over their check boxes. Active swiping also has depth settings, and can be limited to just your current selection @Razenpok @medallyon 
 
 **Breadcrumbs**
Breadcrumbs have been added to have some extra visual indication of the hierarchy tree. Change colors, single or full depth breadcrumbs, with different styles.

**Two Tone**
Two-tone alternate colors can be changed for both light and dark mode.

**Style case**
 You can now set the case of styles choosing from Upper, Lower & No Change @SanielX

### Icons
 - Overhaul to the entire tab and functionality.
 - Enable all of Unity's built in components and custom ones with direct toggles. @SiarheiPilat @nankink
 - Optional toggle to show a single script icon for MonoBehaviours. @SiarheiPilat @nankink
 - You can now search for components!
 - Custom scripts can be grouped and dragged over from the project view.
 - A list of components to exclude has been added to disable them even if show all is enabled.
 
 ### Fixes
  - Fixed performance degradation with components as their icons were not being cached. 
  - Fixed a bug where component data was not being cached correctly.
  - Fixed a bug where alternating background colors could flicker.
  - Fixed a bug where foldouts would rarely begin flickering.

### API Additions
 - An [Attribute](https://github.com/WooshiiDev/HierarchyDecorator/blob/v0.9.0/HierarchyDecorator/Scripts/Editor/Attributes/RegisterTabAttribute.cs) have been added to handle the order of SettingTabs.
 - Using custom [GUIDrawers ](https://github.com/WooshiiDev/HierarchyDecorator/blob/v0.9.0/HierarchyDecorator/Scripts/Editor/GUI/GUIDrawer.cs)to automatically layout the settings window with ease.
 - ComponentTypes are grouped for categories using [ComponentGroup.cs](https://github.com/WooshiiDev/HierarchyDecorator/blob/v0.9.0/HierarchyDecorator/Scripts/Editor/Data/Types/ComponentGroup.cs)
 
 ### Future Changes
 
This should be the final pre-1.0 version. 
 Next the main focuses will include any missing features that have not been added thus far, and getting the API finalized. It would be nice if users and teams could use this tool to create their own additions easily.
 
 Thank you for the patience and time it has taken to release this, I appreciate everyone who takes the time to support this 💪 

Please take note, issues may exist as this is still in development, so please create an [issue](https://github.com/WooshiiDev/HierarchyDecorator/issues) if anything persists.

Thanks
 -- Wooshii 



## 0.8.9

### Bug Fixes
 - Fixed a bug where built in Unity MonoBehaviour's would not display icons when toggled on.
 
Cheers
~ Wooshii

## 0.8.8

### Bug Fixes
 - Fixed a bug where custom components would not update when changing the target script.
 
Cheers
~ Wooshii

## 0.8.7

### Changes
 - Added missing namespace to [GUIHelper.cs](https://github.com/WooshiiDev/HierarchyDecorator/blob/master/HierarchyDecorator/Scripts/Editor/Util/GUIHelper.cs) to avoid conflicts with other packages and/or scripts.
 
Cheers
~ Wooshii

## 0.8.6

### Bug Fixes
 - @AtaTrkgl | Updated the SceneManagement API for 2021.2 or newer.

## 0.8.5

This release provides fixes to custom info overlapping instance labels when the hierarchy gets small, and also provides some serialization issues with settings.

Please note this release may reset your settings but does not change or add features. 
It is recommended to copy your custom settings and replace them after using this release. 

### Bug Fixes
 - EditorPref PREF_GUID for quick loading settings is now project specific. (f96723a446b91d6e4eb5054ec87bce841a4b533b)
 - Fixed a bug where settings attempted to be cached before serialization (43e12763e6b858a748c8441c164a7eb94bcae5e7)
 - Fixed a bug where custom GUIStyles had no names assigned causing look up errors. (9ccd2102347d922688cc1e70cf21bb9ed9a945c2)
 
### Changes
 - Added OnDrawInit to setup data before drawing HierarchyInfo GUI. (0a05a8bd945f816dead8504c81bdd8942b504bdd) (07873266418b00d26b36ef69dac0a383c72d53bb)
 - HierarchyInfo (Layers, Component Icons) will now disappear when overlapping with instance labels in the hierarchy. (f6a3121345ee675a31c2e8857c289cba6a34b362)

Cheers
~ Wooshii

## 0.8.4 

This is the another update for v0.8 to round off some final changes and fixes required.

### Fixes
 - Fixed older version issues with icons or GUI rects appearing in the settings.
  - Unity versions using the old UI will not use Toggle Mixed GUIStyles. This is because of Unity having no dark mode version of that style in the old UI.
  
### Changes
 - Version checks now happen in `OnEnable` for Settings. This is primarily for version support, but also moves some irrelevant code from `ISerializationCallback` methods.
 - Renamed PrefixTab file to StyleTab for correct naming.
 - Small adjustments to GUI for Unity's old UI.
 
Cheers
~ Wooshii

## 0.8.3 | 2021.X Support, Foldout Flicker Fix

This release fixes a flickering bug that was seen across multiple versions of Hierarchy Decorator when expanding or closing instances if they had children. 

Unity 2021.1 and higher now has support with some fixes to the settings GUI.

### Fixes

 - StyleDrawer now stores cache for foldouts and will draw them without flickering.
 - Unity 2021.1 and higher now draws styles correctly in the settings style tab
 - Fixed a bug in Unity 2021.1 and higher where Hierarchy Decorator could not add new styles.

### Changes
 
  - Clean up of the Rects used in the reorderable for the style tab

Cheers,
~Wooshii

## v0.8.2 | Unity 2020 Foldout GUI support

This small release is a hotfix for toggles in Unity 2020 and higher. There were some interaction issues with foldouts when attempting to show or hide children. This release should fix that issue. 

Currently, an issue exists where foldouts will flicker for a short period when expanding or closing them. This is because of the manual draw but it is known. It is on the priority list to be fixed and is being worked on currently. 

Cheers,
~Wooshii

## v0.8.1 | Missing Script Warning

Please note, due to changes you will need to update your HierarchyDecorator version

### Changes
- Added new setting of showing a warning when missing components are detected
- Darkened the inactive colour of instances in the hierarchy when two-tone is enabled.
- Layer drawer label is now brighter 

## v0.8.0 | Settings changes, style previews & public API
Please Note, due to API changes you will need to update your HierarchyDecorator version completely.

### Improvements
 - Styles are now previewed in the "Style" tab of settings.
 - Styles are now reorderable and have a cleaner, functional GUI.
 - Styles now have toggles specifically for styled instances for displaying layers and icons.
 - HierarchyDecorator has an improved check for unity component types, not just checking component counts, but version changes. If the Unity version has changed, it will update all components again.

### Changes
 - The settings class has been fully changed with settings all contained within data classes. This allows easier reference and manipulation of settings, while also containing them correctly.
 - All internal classes have been made public. This change has been added just in case anyone wants to access Hierarchy Decorator for any reason whatsoever. This will change throughout development however, so caution is advised.
 - Icons will now disable toggles on icon categories if "Show All Icons" is enabled. This is to stop confusion between enabling all icons and individual icons being accessible at the same time.

## 0.7.0.0: Fixed persisting foldout issues

## v0.6.0.0
 Hierarchy Core Improvements & Redesign

 Additions Changes
 - Hierarchy now has a new feature system, with easy toggles on options. Will be reflected in the settings in the near future.
 - Hierarchy features/info displayed will now reposition based on what is hidden or displayed keeping space clean.
 - Hovering over component icons will now display what component they are. 
 - The normal hierarchy data now draws under the custom features. This will be provided as an option in the settings in the future.
 - Can now toggle on/off full width styling for the two tone background and styles. If turned off, this will draw custom styles within the normal rect for each instance.

 Fixes/Bugs
 - Fixed a bug where the ScriptableObject will not be created from the git repository.
 - Removed the Settings ScriptableObject from the project. The settings will still exist within the package.
 - Fixed a bug where the settings would revert after editing other setting tabs.
 - Fixed bugs related to saving and updating where settings would revert if AssetDatabase.SaveAssets() was not called. All settings are now handled through serialized properties.
 - General optimisation and clean up of `HierarchyDecorator.cs` has been done.

## v0.5.1.0
Readded custom component icons back to settings

 - Added functionality to icon settings switching between one and two column view if window gets too small
 - Improved visualisation of icon settings

Some of the settings still need tweaked slightly for various reasons (window size, ease of access etc.).
Will improve throughout v0.5 before jumping on to Hierarchy redesigns.
Later reworking of `ComponentTypes` is required as it's not the most flexible structure.

## v0.5.0.0
 Settings redesign!

 - Removed single tab view due to it being a waste of space, and slow navigation.
 - Combined tabs into a single view with foldout displays.
 - Component Icon Types all display in settings as foldouts.
 - Component Icons can now all be enabled or disabled per type.
 - Prefixes can now all be expanded or hidden in settings.

## v0.4.6.5
 - Removed Style Tab as it served very little purpose, settings will be added elsewhere
 - Fixed a bug with static references targeting the previously destroyed SerializableObject for settings

## v0.4.6.4
 - Settings now correctly create new ScriptableObjects when one does not exist
 - Temporary defaults for Settings have been added until a more refined setup is made

## v0.4.6.3
Fixed General Settings not saving

 - Lightened up the names of some of the classes as they were a little over the top

## v0.4.6.2
Can now move settings asset around the project.

 - Path to current settings now saved in `EditorPrefs`. Will only create a new settings asset if one cannot be found within the project files
 - Checkboxes for objects have been hidden in the Prefab Editor to stop the overlap of the show/hide child toggles
 - Continuation of clean up, this time for the HierarchySettings class, see `AssetUtility.cs`

## v0.4.6.1
Restructure of hierarchy decorator calls for correct two tone display

 - Seperated label GUI into it's own method
 - Darkened the selection and added a white tint when selected with two tone on
 - Fixed a bug that stopped the foldouts appearing correctly
 - Added padding to about tab
 - Tweaked some colours in default prefixes
 - Pull Request from @KreliStudio to add Enable/Disable buttons for all icon catergories

## v0.4.6
- Fixed a bug where the bottom-most instance in the hierarchy could not be occasionally selected
- Added MonoBehaviour Icon Support. This can be toggled on/off to show all MonoBehaviour types as icons.
- Added Custom MonoBehaviour icons support. Within the Icon Tab, there is now a Custom Icon Catergory, that will allow users to select MonoBehaviours Directly. This will not only display the script if it's an existing component, 
but the custom icon for them too.

## v0.4.5.1
- Custom GUIStyles now appear as a dropdown in the prefix style selection for easier switching

## v0.4.5
- Setting tabs are now setup in their own individual classes
- Improved caching throughout due to this
- LayerMasks can now be changed from the Hierarchy (multi or single select)
- LayerMask display now has it's own settings catergory in the global settings tab

## v0.4.4
- Added changelog to GitHub

## v0.4.3
- Added null checks for components to fix console errors and hierarchy drawing
- Cleaned up light parts of the hierarchy decorator code
- Moved all global settings into `Global Settings` class
- Fixed a bug that caused errors when prefixes or styles were empty
- Fixed a bug that caused duplication of component types

## v0.4.2 
- Finally fixed foldouts to work properly as normal

## v0.4.1
- Did basic fixes to hierarchy now that background overlays everything
- Now draws toggles boxes using the UnityEngine style
- Redraws GameObject foldouts, but not always correctly
- Redraws default hierarchy Prefab/GameObject Icon beside GameObject names
- Added more component catergories to give easier selection, will revisit this in the future
for a more intuitive way of doing them

## v0.4.0
- Added component icons
- Added component icon toggles
- Made prefix background widths full hierarchy size
- Added Two Tone background option
- Can now select between dark mode and light mode settings in prefix settings

## v0.3.1 
- Created and finished general scriptable object settings design
- Overridden preferences GUI with scriptable object GUI

## v0.3.0
- Added Options in Preferences
- Began first pass of editor for scriptable object

## v0.2.0

- Added toggles for easier disable/enable
- Added layer visualisation in the hierarchy

## v0.1.0:
- Prefix styles added for headers and catergorisation
