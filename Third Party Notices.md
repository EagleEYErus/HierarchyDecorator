# Third Party Notices

This package contains no third-party code beyond the original HierarchyDecorator sources it derives from,
which are covered by [LICENSE.md](LICENSE.md) and [NOTICE.md](NOTICE.md).

## How this was determined

* `package.json` declares `"dependencies": {}` — the package pulls in no other UPM packages.
* `Editor/` contains no vendored or copied third-party code. Every source file is written for this package
  and carries no external copyright header.
* The only namespaces referenced from `Editor/` are the .NET base class library (`System.*`) and Unity's own
  modules (`UnityEngine.*`, `UnityEditor.*`, `Unity.Hierarchy`, `Unity.Collections`, `Unity.Profiling`),
  all supplied by the Unity Editor.
* `Tests/Editor/` additionally references `NUnit.Framework`, provided by Unity's built-in
  `com.unity.test-framework` package. It is not redistributed here.
* `Samples~/BenchmarkScenes/` is likewise original code and references only the .NET base class library and
  Unity's own modules.
* `Editor/Migration/MiniYaml.cs` is a purpose-written reader for the subset of YAML that Unity writes into
  `.asset` files, not a copy of an existing YAML library.

If third-party code is ever added, list it here with its source, version and license text before merging.
