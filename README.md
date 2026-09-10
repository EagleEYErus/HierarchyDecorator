# Hierarchy Decorator 2

Hierarchy Decorator is a Unity Editor extension that turns the Hierarchy window into a readable map of your
scene. Name a GameObject `= PLAYER` and it becomes a section header; empty objects named `---` become
separator rules; nested objects get tree guide lines that show real depth; each row can carry the icons of the
components on it, and a warning badge when a MonoBehaviour script is missing. Version 2.0 is a full rewrite on
Unity 6.6's public `Unity.Hierarchy` extension API, so decorations are UI Toolkit elements *added* to the row
Unity already drew — the plugin never repaints the row itself, which is why selection, hover, prefab override
bars, SubScene carets and prefab text colors all keep working.

---

## Requirements

* **Unity 6000.6.0f1 or newer.**
* **The new Hierarchy window is required.** Unity 6.6 uses it by default. The Unity 6.6 extension API that
  2.0 is built on is never invoked by the legacy IMGUI Hierarchy, so if
  `Edit > Project Settings > Editor > Hierarchy > Use Legacy Hierarchy` is enabled, Hierarchy Decorator draws
  nothing and writes a single hint to the Console. Turn that setting off; open Hierarchy windows update
  immediately.
* Editor-only. Nothing is compiled into a player build, and nothing is added to your scenes at runtime.

---

## Installation

**Package Manager > Install package from Git URL**

```
https://github.com/WooshiiDev/HierarchyDecorator.git
```

Open `Window > Package Manager`, press `+`, choose **Install package from Git URL...**, paste the URL and
press **Install**.

To pin a release instead of tracking the default branch, append the tag:

```
https://github.com/WooshiiDev/HierarchyDecorator.git#v2.0.0
```

**manifest.json**

Add the dependency directly to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.wooshii.hierarchydecorator": "https://github.com/WooshiiDev/HierarchyDecorator.git#v2.0.0"
  }
}
```

Pinning a tag is recommended for team projects — an unpinned Git dependency resolves to whatever the default
branch holds at the moment each machine last resolved packages.

---

## Screenshots

> Captures for 2.0 have not been taken yet. The images referenced below do not exist in the repository; the
> paths are placeholders reserved for them.

![Headers](Documentation~/images/headers.png)
![Tree guide lines](Documentation~/images/tree-lines.png)
![Component icons](Documentation~/images/component-icons.png)
![Settings](Documentation~/images/settings.png)

---

## Features

### Headers & Separators

A row becomes a header when its GameObject name matches a **header rule**. Rules are name-prefix based:
rename an empty GameObject to `= PLAYER` and the row is drawn as a centered, uppercase, bold header with the
`=` stripped from the label. The four rules that ship by default are:

| Prefix | Rule | Result |
|---|---|---|
| `= ` | Header (Centered) | Centered, uppercase, bold, size 11 |
| `- ` | Subheader | Left-aligned, uppercase, bold, size 10 |
| `+ ` | Mini Header (Centered) | Centered, uppercase, bold, blue fill |
| `---` | Separator | A horizontal line across the row, no background fill (a label may still follow, e.g. `--- GAMEPLAY`) |

Matching is ordinal and the rule list is evaluated top to bottom — **first match wins**. Unless a rule turns
`Require Space After Prefix` off, the prefix must be followed by exactly one space, which is what keeps `=`
from matching `===`. The `Separator` rule is deliberately listed first, and has the space requirement off, so
`---` never falls through to the `-` subheader rule.

Per rule you can set: alignment, text case, bold, font size, letter spacing, text and background colors (each
as a light/dark pair), whether a line is drawn and in which style and thickness, whether the prefix stays in
the drawn label, and whether component icons and tree lines are still drawn on matched rows. A rule can also
be switched to **Use Regex**, in which case the pattern is a regular expression implicitly anchored with `^`.

You do not have to type the prefixes by hand: right-click a row (or a selection) in the Hierarchy and use
`Hierarchy Decorator > Convert To > <rule name>`, or `Hierarchy Decorator > Clear Decoration` to strip it
again. Both are single, undoable rename operations. Regex rules are not offered in that menu, because they
have no single literal prefix to apply.

Because a header is just a renamed GameObject, the row keeps its real name in the tooltip.

### Tree guide lines

Vertical guide lines are drawn for the row's ancestors, plus a short horizontal connector into the row. The
branch line stops halfway on the last visible child, so a subtree ends in an `L` rather than a `T`.

Options live under **Tree Guide Lines**: `Enabled`, `Full Depth` (every ancestor level, or only the immediate
parent), `Show Connector`, `Style` (Solid / Dashed / Dotted), `Opacity` and a light/dark `Color` pair.

Guide lines are hidden automatically while the Hierarchy search field is in use — Unity flattens the list and
removes indentation there, so the lines would point at nothing. They are also hidden on header rows unless
that rule's `Show Tree Lines` is enabled, since a section marker with lines running through it usually reads
as noise.

### Component icons

Each row can show the icons of the components on its GameObject, right-aligned at the end of the Name column.
Click behavior, count and filtering are all configurable under **Component Icons**:

* `Mode` — `All` shows every component except the ones a rule hides; `Selected` shows *only* the components a
  rule explicitly shows.
* `Include Custom Scripts` — include your own `MonoBehaviour`s when the mode is `All`.
* `Hide Transform` — never draw `Transform` or `RectTransform`, which are on everything.
* `Max Icons Per Row` — `0` means unlimited. Anything past the limit is summarized as a `+N` suffix.
* `Stack Duplicates` — collapse repeated components of the same type into one icon with a count badge.
* `Fade Disabled Components` — dim the icon when the component's enable checkbox is off.
* `Order` — component order on the GameObject, or alphabetical.
* `Show Tooltips` — hover an icon for the component name.
* `Click Action` — `Select` (select the GameObject and ping the component), `Toggle Enabled` (flip the
  component's enable checkbox, recorded for undo), or `None`.

Which components are eligible is decided by the **Displayed Components** list on the same page: search the
project's component types and set each to `Default`, `Show` or `Hide`. Only types you actually change are
written to the settings file, so a project that never touches this list stores nothing.

### Missing-script indicator

A GameObject holding a `MonoBehaviour` whose script can no longer be resolved gets a warning badge at the
start of the Name column, with a tooltip giving the count. The same text is appended to the row's tooltip.
Toggle it under **Diagnostics > Show Missing Scripts**.

The count is a by-product of the component scan that already feeds the icon strip, which is why it is cheap.
This is the only indicator that ships — Hierarchy Decorator is not a static analyser.

### Row colors

Unity 6.6 draws alternating row backgrounds itself, and 2.0 leaves that alone by default. Enable
**Rows > Override Alternating Colors** only when you want different colors than the editor's, then set the
`Even Color` and `Odd Color` light/dark pairs. The override paints the shared row container, so selection,
hover and the prefab override bar continue to work.

### Presets

A preset is a named snapshot of the feature toggles — it never touches your header rules or component rules,
because those are content a team authors rather than a visual style. Pick one from the dropdown at the top of
the settings page and press **Apply**. **Save Current As...** captures the current toggles as a new project
preset; **Delete** removes a custom one. The five built-in presets cannot be deleted.

Which preset is active is a per-developer choice and is stored in your user settings, not in the shared file.

### Columns

Unity 6.6's Hierarchy has resizable, reorderable columns, and 2.0 registers two of its own. Both are
**hidden by default** — right-click the Hierarchy's column header and tick them to show them.

**Components** shows the same icons as the inline strip, in a fixed-width slot. It shares the cache with the
strip, so turning it on costs no extra component scanning, and it keeps working when the inline strip is
switched off.

**Children** shows how many descendants a row has, collapsed ones included. It is a single native call per
visible row — no component scan, no traversal — and rows with no children stay blank, so the column only
speaks where it has something to say.

Active, Static, Layer, Tag, Visibility and Picking are native Unity columns in 6.6 and are enabled from the
same menu. 2.0 does not reimplement them.

---

## Presets

| Preset | Turns on |
|---|---|
| **Minimal** | Headers. Tree lines, dotted, immediate parent only, no connector, very low opacity. Missing-script indicator. No component icons, no row color override. |
| **Clean** (default) | Headers. Solid tree lines at full depth with connector. Component icons in `Selected` mode — only components you explicitly show, custom scripts excluded, at most 4 per row, tooltips on, click selects. Missing-script indicator. |
| **Developer** | Headers. Solid tree lines at full depth with connector, stronger opacity. Component icons in `All` mode including custom scripts, up to 8 per row, tooltips on, disabled components faded, **click toggles the component's enable state**. Missing-script indicator. |
| **Designer** | Headers. Solid tree lines at full depth with connector, low opacity. No component icons, no tooltips, no missing-script indicator, no row color override. Structure only. |
| **Debug** | Headers. Dashed tree lines at full depth, high opacity. **Row color override on.** Component icons in `All` mode including custom scripts, up to 12 per row, tooltips on, disabled components faded, click toggles enable state. Missing-script indicator. |

---

## Settings

Nothing is ever written into `Assets/`. Configuration is split across two files by who owns it:

| File | Contents |
|---|---|
| `ProjectSettings/Packages/com.wooshii.hierarchydecorator/Settings.asset` | The shared ruleset: header rules, component display rules, component icon options, tree line options, row colors, diagnostics toggles and custom presets. **Commit this** — it is what makes a team's Hierarchy look the same. |
| `UserSettings/Packages/com.wooshii.hierarchydecorator/UserSettings.asset` | Per-developer choices: the master enable toggle, the active preset, and whether the legacy-Hierarchy warning is shown. `UserSettings/` is in Unity's standard `.gitignore`, so this never causes a merge conflict. |

Three ways to reach the pages:

* `Edit > Project Settings > Hierarchy Decorator` — the shared ruleset (Preset, Headers & Separators, Tree
  Guide Lines, Rows, Component Icons + Displayed Components, Diagnostics, Maintenance).
* `Preferences > Hierarchy Decorator` (the Preferences window; `Unity > Settings` on macOS) — your personal
  preferences, plus the resolved paths of both files.
* `Tools > Hierarchy Decorator > Settings...` — a shortcut to the project page.

The rest of the `Tools > Hierarchy Decorator` menu: `Disable All Decorations`, `Re-enable Failed Decorations`,
`Import Settings From 1.x`, `Reset Settings To Defaults`.

Edits apply to the live Hierarchy as you make them, so the real window is the preview.

---

## Performance philosophy

The design goal is that decoration cost scales with *change*, not with repaints or scene size.

1.x hooked an IMGUI callback that ran for every visible row on every repaint, and recomputed everything each
time — component enumeration, prefix matching, style sizing, line drawing. 2.0 hooks `BindViewItem`, which
Unity raises when a row scrolls into view or its data changes, not when the window repaints. Everything
expensive is derived once there and stored in a cache keyed by `EntityId`: the header rule match and its
label, the component icon slice, and the missing-script count.

That cache is invalidated per *facet* from `UnityEditor.ObjectChangeEvents`, not wholesale. Adding a component
invalidates only that object's component data; reparenting invalidates tree data for the object, its subtree
and both parents; a plain property change — dragging a Transform, for example — does an O(1) revalidation and
never triggers a component rescan. `EditorApplication.hierarchyChanged` is kept only as a coarse safety net.
Scrolling therefore writes styles onto pooled `VisualElement`s and does no allocation, no type scanning and no
texture rasterization; dashed and dotted lines use one-pixel repeating textures generated once per domain.
The single value read live per icon is the component's enabled state, because caching it would make the fade
lag behind the Inspector.

Concrete numbers have not been measured yet. See [PERFORMANCE.md](PERFORMANCE.md) for the measurement
methodology and the results once they exist. The package ships a **Benchmark Scene Generator** sample that
builds the 100 / 1 000 / 10 000 GameObject hierarchies those measurements run against, and three
`ProfilerMarker`s — `HierarchyDecorator.DecorateRow`, `.ScanComponents` and `.MatchHeaderRule` — so the claims
above can be checked rather than taken on trust.

---

## Search

Hierarchy Decorator contributes two filters to the Hierarchy's own search box:

| Query | Finds |
|---|---|
| `hd:header` | every row rendered as a header |
| `hd:separator` | every row rendered as a separator |
| `hd:none` | every undecorated row |
| `hdrule:Subheader` | every row matched by the rule named "Subheader" |

They compose with Unity's own filters, so `hd:header t:Camera` or `hdrule:Section is:root` work as expected,
and they appear in the query-builder dropdown alongside the built-in ones.

Unity 6.6 already answers the other obvious questions itself - `missing:script`, `t:`, `components:`,
`active:`, `is:root|leaf|child|static|prefab`, `prefab:`, `layer:`, `tag:` - so those are deliberately not
duplicated.

---

## Migration from 1.x

Settings from HierarchyDecorator 1.x are imported, not lost. On first load in a project that has no 2.0
settings file, the package scans `Assets/` for a 1.x settings asset and, if it finds exactly one, imports the
header styles and component rules automatically and reports what it did in the Console. If the project already
has 2.0 settings, or several 1.x assets exist, nothing is touched silently — run
`Tools > Hierarchy Decorator > Import Settings From 1.x` when you are ready. The 1.x asset is read as text and
is never modified or deleted, so an import can be repeated.

Not everything carries over: features 1.x implemented itself that Unity 6.6 now provides natively (the active
toggle, tag and layer labels) are not reimplemented, and their 1.x settings have no 2.0 equivalent.
See [MIGRATION.md](MIGRATION.md) for the full field-by-field mapping and what to do about the gaps.

---

## Troubleshooting

**Nothing is drawn at all.**
Almost always the legacy Hierarchy window. Check
`Edit > Project Settings > Editor > Hierarchy > Use Legacy Hierarchy` and turn it **off**; the Console will
also carry a one-time hint, and the project settings page shows a warning box while it is on. If the new
window is already in use, check that `Tools > Hierarchy Decorator > Disable All Decorations` is not ticked,
and that `Enable decorations` is on in `Preferences > Hierarchy Decorator`.

**A decoration stopped working and the Console has one warning about it.**
Each decoration runs isolated. One that throws is disabled for the rest of the session and reported exactly
once, so a bug in a single decoration can neither break the Hierarchy window nor spam the Console. Fix or
report the cause, then run `Tools > Hierarchy Decorator > Re-enable Failed Decorations` — that menu item is
only enabled while something is actually disabled. Failures also clear on the next domain reload.

**Icons are not appearing.**
Work down this list: `Component Icons > Enabled` is off; the mode is `Selected`, which shows *only* components
with an explicit `Show` rule in the Displayed Components list; `Max Icons Per Row` is small and the rest
collapsed into the `+N` suffix; `Hide Transform` is hiding the only component present; `Include Custom
Scripts` is off and the object only carries your own `MonoBehaviour`s; or the row matched a header rule whose
`Show Component Icons` is off. The separate **Components column** is a different feature and is hidden until
you enable it from the Hierarchy's column-header right-click menu.

**Settings are not shared with the team.**
Commit `ProjectSettings/Packages/com.wooshii.hierarchydecorator/Settings.asset`. If it is not in source
control, everyone gets the defaults. `UserSettings/Packages/com.wooshii.hierarchydecorator/UserSettings.asset`
is deliberately *not* shared — the master enable and the active preset are personal choices, so a teammate
appearing to have "no decorations" usually means their own enable toggle is off, not that the ruleset is
missing.

**How do I turn everything off?**
`Tools > Hierarchy Decorator > Disable All Decorations` is the immediate kill switch; it is a checked toggle
and resets on the next domain reload. For a persistent, per-developer off switch, uncheck `Enable decorations`
in `Preferences > Hierarchy Decorator` — that changes nothing for anyone else. To remove the package
entirely, uninstall it from the Package Manager; the two settings files under `ProjectSettings/` and
`UserSettings/` can then be deleted by hand.

---

## Attribution and License

Hierarchy Decorator 2 is based on HierarchyDecorator, originally created by
[Damian Slocombe / WooshiiDev](https://github.com/WooshiiDev/HierarchyDecorator), modernized for Unity 6.6+.
The header-prefix convention, the component icon strip and the tree guide lines are his design, and the 1.x
default header rules are reproduced verbatim so upgraded projects look unchanged.

Released under the MIT License. Copyright (c) 2020 - 2024 Damian Slocombe. See [LICENSE.md](LICENSE.md) for
the full text.

Architecture, design decisions and known limitations: [ARCHITECTURE.md](ARCHITECTURE.md).

---

## Support

Bugs, questions and feature requests belong on the
[Issues](https://github.com/WooshiiDev/HierarchyDecorator/issues) page.

Hierarchy Decorator is developed in the author's free time. If you would like to support that work:

[![PayPal](https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif)](https://paypal.me/Wooshii?locale.x=en_GB)
[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/L3L026UOE)

Reach the author through [wooshii.dev](https://wooshii.dev/) or wooshiidev@gmail.com.
