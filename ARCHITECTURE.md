# Hierarchy Decorator 2.0 — Architecture

This document explains what 1.x was, why it could not survive Unity 6.6, and how 2.0 is built.
Everything stated about Unity APIs here was verified against the assemblies shipped with
**Unity 6000.6.0f1** (metadata dump + IL inspection + a live Editor probe), not against memory or blog posts.

---

## 1. The old architecture (HierarchyDecorator 1.x)

```
[InitializeOnLoad] HierarchyDecorator
        └── EditorApplication.delayCall → HierarchyManager.Initialize()
                    └── EditorApplication.hierarchyWindowItemOnGUI += OnGUI
                                    │  (per visible row, per repaint, IMGUI)
                                    ▼
                        TryGetValidInstance(id) → HierarchyItem
                                    ▼
                        item.OnGUIBegin()  → 3× PrefabUtility calls + GetComponents<Component>()
                                    ▼
                        Drawers        { StyleDrawer }               ← paints over Unity's row
                        Info           { TagLayerInfo, ComponentIconInfo }
                        OverlayDrawers { StateDrawer, ToggleDrawer, BreadcrumbsDrawer }
```

### Limitations that made a port impossible

1. **The entry point no longer exists.** In 6.6 `EditorApplication.hierarchyWindowItemOnGUI` is
   `[Obsolete(..., error: true)]` — it does not compile. Its successor
   `hierarchyWindowItemByEntityIdOnGUI` still exists, but its only call site is
   `UnityEditor.GameObjectTreeViewGUI.UserCallbackRowGUI`, i.e. the **legacy** IMGUI window.
   `UnityEditor.HierarchyModule` contains zero references to it.
2. **The legacy window is no longer the default.** `EditorSettings.useLegacyHierarchy` defaults to `false`
   in 6000.6.0f1 (measured on a virgin project). Unity's release notes: *"The Editor now uses the new
   Hierarchy window by default."* So a compiling 1.x on 6.6 draws **nothing**.
3. **The central design sin.** `twoToneBackground` painted over Unity's own row, which forced 1.x to
   re-implement the entire row: GameObject/prefab icon, prefab text colours, prefab arrow, override badge,
   foldout triangle, selection tint, hover tint, inactive tint. Roughly 40 % of the render code existed only
   to repair damage the plugin itself caused.
4. **Everything was recomputed every repaint.** `GetComponents<Component>()` per row per repaint; style
   prefix matching executed 4–6× per row per repaint; `GUIStyle.CalcSize` per row; `Selection.gameObjects`
   (allocating) per row; up to `max(n, 7)` `Handles.DrawLine` calls per depth level per row.
5. **Cache invalidation was inverted.** `OnHierarchyChange` did `if (lookup.Count < 100) return;` — small
   scenes never invalidated at all, large scenes threw the whole cache away on any change.
6. **~2400 of 7789 lines were hand-rolled IMGUI settings UI** — rect math, closure-based `ShowIf`,
   `ReorderableList` version workarounds.
7. **Reflection where public APIs now exist** (`Component.enabled` via `PropertyInfo`,
   `AppDomain.GetTypes()` scans).
8. **Settings lived in `Assets/HierarchyDecorator/Settings.asset`**, discovered through
   `AssetDatabase.FindAssets("t:Settings")` (which matches *any* project type named `Settings`) plus a GUID
   cached in `EditorPrefs` under `{productName}_HD_GUID`.

---

## 2. What Unity 6.6 gives us instead

The new Hierarchy window is a UI Toolkit window with a **first-class public extension API**
(`UnityEditor.HierarchyModule` / `UnityEngine.HierarchyModule` / `UnityEngine.HierarchyCoreModule`).

| Need | Unity 6.6 API |
|---|---|
| Per-row decoration | `Unity.Hierarchy.Editor.HierarchyWindow.BindViewItem` / `UnbindViewItem` (static events) |
| Stylesheet injection | `HierarchyWindow.BindView` → `HierarchyView.StyleContainer` |
| Row anatomy | `HierarchyViewItem.{Toggle, Icon, OverlayIcon, Name, LeftCustomContainer, RightCustomContainer, OverrideBarContainer, RowContainer}` |
| Custom columns | `[HierarchyViewColumnDescriptor]` / `[HierarchyViewCellDescriptor]` + `HierarchyViewColumnDescriptor` / `HierarchyViewCellDescriptor` / `HierarchyViewCell` |
| Context menu | `HierarchyWindow.PopulateContextMenu` |
| Tooltips | `HierarchyWindow.GetTooltip` |
| Tree topology | `HierarchyViewModel.{GetDepth, GetParent, GetNextSibling, HasVisibleChildren, Contains, GetFlags}` |
| Node identity | `HierarchyGameObjectHandler.GetGameObject(in HierarchyNode)` / `GetEntityId(...)` |
| Active / Static / Layer / Tag / Visibility / Picking | **native columns** — 2.0 does not reimplement them |
| Alternating rows | **native**, on by default |
| Component enable state | `EditorUtility.GetObjectEnabled` / `SetObjectEnabled` (public — no reflection) |

Verified row anatomy (empirically, in a live 6000.6.0f1 Editor):

```
HierarchyViewItem                     (flex-direction: row)
└── .hierarchy-item__container
    ├── .hierarchy-item__override-bar-container      4 px prefab override bar
    ├── .hierarchy-item__left-container              ← translate(x = 14 * depth)
    │   ├── Toggle  .unity-tree-view__item-toggle
    │   ├── .hierarchy-item__icon                    16 px
    │   ├── .hierarchy-item__overlay-icon            16 px, margin-left: -16px (on top of Icon)
    │   ├── HierarchyViewItemName  (Label + rename TextField)   ← item.Name is the inner Label
    │   └── .hierarchy-item__left-custom-section     ← LeftCustomContainer
    └── .hierarchy-item__right-container             ← RightCustomContainer (right-aligned)
```

Constants read from IL: `k_IndentWidth = 14`, `k_OverrideBarWidth = 4`, `HierarchyView.k_ItemHeight = 16`.
The indent is a **`translate`, not padding**, and it is applied to `LeftContainer` only.

---

## 3. The 2.0 architecture

Strict one-way flow. Nothing downstream ever calls upstream.

```
   Unity 6.6 integration            data / cache                feature logic            rendering
 ┌──────────────────────┐    ┌──────────────────────┐    ┌────────────────────┐   ┌───────────────────┐
 │ HierarchyBootstrap   │    │ DecorationCache      │    │ IRowDecorator[]    │   │ VisualElement     │
 │  (InitializeOnLoad)  │───▶│  EntityId → RowData  │───▶│  HeaderDecorator   │──▶│ slots on the      │
 │ HierarchyHooks       │    │  facet dirty bits    │    │  TreeLineDecorator │   │ HierarchyViewItem │
 │  BindView/BindItem   │    │ ComponentCatalog     │    │  IconsDecorator    │   │ + our USS         │
 │ ChangeTracker        │    │ IconCache            │    │  IndicatorDecorator│   └───────────────────┘
 │  ObjectChangeEvents  │    └──────────────────────┘    └────────────────────┘
 └──────────────────────┘                    ▲
                                             │
                                   ┌─────────┴──────────┐
                                   │ configuration      │
                                   │ ProjectSettings +  │
                                   │ UserSettings +     │
                                   │ Presets + Migration│
                                   └────────────────────┘
```

### Folder map

```
Editor/
  Core/         HierarchyBootstrap, HierarchyHooks, HierarchyNodes, RowContext, DecoratorHost, HierarchyLog
  Cache/        DecorationCache, RowData, CacheFacets, ChangeTracker, ComponentCatalog, ComponentIconCache
  Decorations/  IRowDecorator, HeaderDecorator, SeparatorDecorator, TreeLineDecorator,
                RowTintDecorator, ComponentIconDecorator, IndicatorDecorator
  Columns/      ComponentsColumn
  Settings/     HierarchyDecoratorSettings (project), HierarchyDecoratorUserSettings (user),
                HeaderRule, ComponentRule, TreeLineOptions, Preset, PresetLibrary,
                HierarchyDecoratorSettingsProvider, HierarchyDecoratorUserSettingsProvider
  Migration/    LegacySettingsLocator, LegacySettingsReader (YAML), LegacyMigrator
  UI/           HierarchyDecorator.uss, HierarchyDecorator_dark.uss, HierarchyDecorator_light.uss,
                Settings UXML/USS
  Utility/      LineTextures, ThemeColors, NameMatcher, PooledElements
Tests/Editor/   EditMode tests
```

---

## 4. Major design decisions

### D1 — Target the new window only; Unity 6.6 is the minimum

`unity: 6000.6`. The extension API exists from 6.5, but in 6.5 the new window is a **preview that is off by
default**, so 6.5 users would install a plugin that appears to do nothing. Supporting the legacy IMGUI window
in parallel would mean carrying the entire 1.x renderer plus its "repaint the whole row" pathology.
2.0 therefore **detects** the legacy window and tells the user how to switch, rather than rendering into it.

### D2 — Decorate, never repaint

2.0 never draws a GameObject icon, prefab arrow, foldout, selection tint or row background that Unity already
draws. Decorations are additive `VisualElement`s placed in the containers Unity exposes for exactly this
purpose. This single decision deletes `HierarchyGUI`, `StateDrawer`, `StyleDrawer`'s row re-implementation and
`Style.cs` from the 1.x codebase (~1000 lines) and fixes issues #111 (override bar hidden) and #86
(SubScene caret hidden) by construction.

### D3 — Bind-time work only, with an explicit cache

`BindViewItem` fires when a row scrolls into view or its data changes — **not** every repaint. All expensive
derivation (component enumeration, icon resolution, missing-script detection, header matching) happens once
per row and is stored in `DecorationCache`, keyed by `EntityId`.

Invalidation is driven by `UnityEditor.ObjectChangeEvents.changesPublished`, mapped **per facet**:

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

### D4 — Theming through two stylesheets plus custom properties

Editor USS has **no** `.unity-theme-dark` selector mechanism; Unity itself ships a `_dark.uss` / `_light.uss`
pair holding only a `:root { --var: value }` block and picks one with `EditorGUIUtility.isProSkin`. 2.0 does
the same and namespaces every custom property `--hd-*` (the bare `--hierarchy-*` prefix is already contested
between the built-in hierarchy sheet and `com.unity.entities`).

Sheets are added to `HierarchyView.StyleContainer` from **every** `BindView` callback, never cached:
`HierarchyView.Reset()` destroys and recreates `StyleContainer`, and `Reset()` runs on every
`SetSourceHierarchy` (scene load, prefab stage enter/exit, handler registration).

All selectors are scoped under a marker class we add to `StyleContainer`, because `.hierarchy-item__*` rules
live in the editor-wide `DefaultCommon{Dark,Light}.uss` and `HierarchyView` is reused by other windows.

### D5 — Settings live outside `Assets/`

| Data | Location | Mechanism |
|---|---|---|
| Shared ruleset (headers, component rules, presets, colours) | `ProjectSettings/Packages/com.wooshii.hierarchydecorator/Settings.asset` | `ScriptableSingleton<T>` + `[FilePath(..., ProjectFolder)]` |
| Per-developer choices (active preset, master enable, density) | `UserSettings/Packages/com.wooshii.hierarchydecorator/UserSettings.asset` | `ScriptableSingleton<T>` (this path is in Unity's standard `.gitignore` → no merge conflicts) |
| Ephemeral UI state | `SessionState` | — |

This removes the `Assets/` pollution, the `FindAssets("t:Settings")` collision and the `EditorPrefs` GUID
indirection of 1.x, and answers upstream issue #25 (team-shared vs per-user settings).

### D6 — Versioned schema with a real migration chain

Every settings object starts with `[SerializeField] int m_SchemaVersion`. `OnAfterDeserialize` runs an ordered,
side-effect-free `v(n) → v(n+1)` chain. Field renames use `[FormerlySerializedAs]`; serialized type renames use
`[MovedFrom]`. Polymorphic data uses `[SerializeReference]` and is checked with
`SerializationUtility.HasManagedReferencesWithMissingTypes` so a missing type surfaces a warning instead of
silently deleting user configuration.

### D7 — Component identity is stored twice

`{ monoScriptGuid, typeName (namespace-qualified), assemblyName }`.

* Built-in Unity components have **no** `MonoScript`, so namespace-qualified name + assembly is the only key —
  and it is stable, because it is Unity's own public API surface.
* User `MonoBehaviour`s resolve by **MonoScript GUID first** (survives class rename, namespace change, asmdef
  rename and file moves), falling back to the type name; whichever path succeeds rewrites the other.
* Type enumeration uses `UnityEditor.TypeCache`, never `AppDomain.GetTypes()`.

This is the fix for upstream issues #119 (custom groups lost on restart) and #134 (duplicate type registration
spam).

### D8 — Feature isolation

Each `IRowDecorator` is invoked inside `DecoratorHost` behind a `try/catch`. A decorator that throws is
**disabled for the session** and logs exactly once (`HierarchyLog.Once`). A bug in one decoration can never
make the Hierarchy unusable, and the console can never be spammed per repaint. `Tools ▸ Hierarchy Decorator ▸
Disable All Decorations` is the manual escape hatch.

### D9 — Prefix-based headers stay, but the matcher is precompiled

The `= HEADER` / `- Subheader` / `+ Mini` convention is the product's identity and is preserved verbatim,
including the exact 1.x matching rule (`name.StartsWith(prefix)` and, unless `noSpaceAfterPrefix`, a single
following space). Regex rules keep the implicit `^` anchor but are compiled once
(`RegexOptions.Compiled | RegexOptions.CultureInvariant`) and evaluated **once per row per bind**, cached on
the row, instead of 4–6× per repaint.

1.x's two divergent prefix-stripping implementations (`DrawHierarchyStyle` used `Substring(len).Trim()`,
`GetLabelRect` used `Substring(len + 1)` with no trim) are unified into a single `NameMatcher.StripPrefix`.

Storing header metadata **outside** the GameObject name was investigated and rejected for 2.0: every
Unity-6.6-native option (a component, `HideFlags`, scene-level side tables, `Hierarchy` node properties)
either adds runtime objects to the built game, breaks prefabs, or does not round-trip through scene
serialization. The name prefix is the only representation that is free, diff-able, VCS-friendly and
migrates cleanly from 1.x. This is recorded as a known limitation, not an oversight.

### D10 — Do not reimplement native columns

Active / Static / Layer / Tag / Visibility / Picking are native, resizable, reorderable columns in 6.6.
1.x's `TagLayerInfo` (274 lines of grid and overflow math), `ToggleDrawer`'s checkbox and `StateDrawer`
are **removed**, not ported. 2.0 adds exactly one custom column — **Components** — because Unity has no
equivalent and the underlying data is already cached.

### D11 — No `GetInstanceID`, no `int` ids

`UnityEngine.Object.GetInstanceID()` is `[Obsolete(error)]` in 6.6, and `(int)EntityId` is obsolete too.
2.0 uses `EntityId` end to end as the cache key and never converts it to `int`.

---

## 5. Row rendering contract

`BindViewItem` is the only per-row entry point. Rules, derived from Unity's own documented behaviour:

1. **View items are pooled and recycled per handler.** Children added to `LeftCustomContainer` /
   `RightCustomContainer` persist across binds. 2.0 therefore uses *find-or-create by element name* and
   **never** calls `Add()` blindly.
2. **Every visual is fully re-derived on every bind.** State is expressed with
   `EnableInClassList(class, condition)` so a recycled row can never keep stale styling.
3. **`UnbindViewItem` does not undo styling.** Unity's own documentation says not to. It is used only to
   unregister callbacks and release rented buffers. Note that `item.Node` is already `HierarchyNode.Null`
   there, so the node is captured during bind.
4. **Not every node is a GameObject.** Every decorator gates on
   `item.Handler is HierarchyGameObjectHandler` first. `SubSceneAuthoring` is a separate handler type, not a
   subclass, and third parties (e.g. `com.unity.entities`) register their own. Calling the wrong handler
   returns `null` silently rather than throwing, which is exactly why the gate comes first.
5. **Suppress depth-based decoration while filtering.** `HierarchyViewItem.CalculateIndentWidth` early-returns
   `0` when `view.Filtering` is true and the list becomes flat, so tree lines are hidden during a search.
6. **`item.RowContainer` may be `null`** when the item is not yet parented into a multi-column row; full-row
   decorations degrade to the name-column container in that case.

---

## 6. Extensibility

`IRowDecorator` is the internal seam. It is deliberately **not** published as a supported API in 2.0.0 —
the surface should settle first. What *is* public and documented:

```csharp
HierarchyDecoratorAPI.RegisterComponentIcon<T>(Texture2D icon);
HierarchyDecoratorAPI.RegisterIndicator<T>(Func<Component, IndicatorInfo?> evaluate);
```

Both are thin registries consumed by the existing decorators, cost nothing when unused, and cannot break the
render path (they are evaluated inside `DecoratorHost`'s isolation).

---

## 7. Rejected alternatives

| Option | Why rejected |
|---|---|
| Port 1.x onto `hierarchyWindowItemByEntityIdOnGUI` (PR #149 style) | Drives the **legacy** window only, which is off by default in 6.6. Compiles, renders nothing. |
| Ship an IMGUI fallback for the legacy window alongside the UI Toolkit path | Doubles the renderer, doubles the bug surface, and keeps the "repaint the whole row" pathology alive for a window Unity is retiring. Detect-and-inform instead. |
| Subclass / replace `HierarchyGameObjectHandler` | `OnBindItem` etc. are `protected virtual` on a handler you can only register for **your own** node type. Unity's GameObject handler cannot be replaced. The static events are the supported path. |
| Store header metadata in a component or side asset | Adds runtime objects, breaks prefab workflows, or does not survive scene serialization. See D9. |
| Custom Layer/Tag drawing | Native columns do it better. See D10. |
| `:nth-child` USS for zebra rows | UI Toolkit USS does not support structural pseudo-classes. Row parity is toggled from C# instead. |

---

## 8. Known limitations

* Requires the **new** Hierarchy window. With `Project Settings ▸ Editor ▸ Use Legacy Hierarchy` enabled,
  2.0 renders nothing and shows a one-time console hint.
* Headers are still name-prefix based (D9).
* Drag-and-drop extension types (`HierarchyViewDragAndDropHandlingData`) exist but are `ref struct`s carrying
  a compiler-marker `[Obsolete]`; 2.0 does not use them.
* `EditorApplication.RepaintHierarchyWindow()` is a no-op for the new window; forced refresh goes through
  `view.Source.SetDirty()`.
* Unity's own `HierarchyView.k_HierarchyPingBase` constant is `"hierarchy - item__ping-base"` (with spaces)
  in 6000.6.0f1 and never matches its USS rule. 2.0 does not rely on it.
