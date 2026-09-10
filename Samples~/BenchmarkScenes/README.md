# Benchmark Scene Generator

Generates large hierarchies so the cost of Hierarchy Decorator can be measured on a reproducible scene.

## Import

Package Manager → Hierarchy Decorator 2 → Samples → **Benchmark Scene Generator** → Import.

## Use

`Tools ▸ Hierarchy Decorator ▸ Benchmark Scene Generator`

Pick 100, 1 000 or 10 000 GameObjects (or a custom count) and press the button. A new, unsaved scene is
created — the current scene is closed, so save first.

Options:

| Option | Effect |
|---|---|
| Children per node | Branching factor. Lower values make deeper trees, which exercises the tree guide lines harder. |
| Add components | Gives objects a varied mix of components so the icon strip has real work to do. |
| Add header rows | Sprinkles `= SECTION n` and `--- block n` rows through the scene. |
| Add missing scripts | Marks objects for manual missing-script testing (a script asset has to be deleted by hand — Unity cannot synthesise a broken reference from script). |

## Measuring

The window reports only **scene construction** time. That is not the decoration cost, and it is labelled as
such on purpose.

To measure decoration cost, open the Profiler in **Editor** mode and record while scrolling the Hierarchy.
Hierarchy Decorator emits these markers:

* `HierarchyDecorator.DecorateRow` — the whole per-row bind path
* `HierarchyDecorator.ScanComponents` — a component-slice cache fill
* `HierarchyDecorator.MatchHeaderRule` — a header-rule cache fill

`ScanComponents` and `MatchHeaderRule` should appear only when a row is bound for the first time or after an
invalidation. If either shows up on every frame while merely scrolling back and forth over the same rows,
the cache is not doing its job — that is the regression this sample exists to catch.

The full procedure, including the five configurations to compare, is in `PERFORMANCE.md`.
