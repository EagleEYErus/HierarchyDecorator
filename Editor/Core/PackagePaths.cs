using System.IO;
using UnityEditor;

namespace HierarchyDecorator
{
    /// <summary>
    /// Resolves where this package actually lives.
    ///
    /// Hard-coding "Packages/com.wooshii.hierarchydecorator" only works for the registry/Git install. The
    /// same source is routinely embedded under <c>Packages/</c> with a different folder name, referenced from
    /// a local path, or dropped straight into <c>Assets/</c>, and in all of those cases a hard-coded path
    /// silently fails to load the stylesheets - which is invisible until the icons come out zero-sized.
    /// </summary>
    internal static class PackagePaths
    {
        private static string s_Root;

        /// <summary>Project-relative path of the package root, without a trailing slash.</summary>
        public static string Root
        {
            get
            {
                if (!string.IsNullOrEmpty(s_Root))
                {
                    return s_Root;
                }

                s_Root = Resolve();
                return s_Root;
            }
        }

        public static string Ui(string fileName)
        {
            return Root + "/Editor/UI/" + fileName;
        }

        internal static void Invalidate()
        {
            s_Root = null;
        }

        private static string Resolve()
        {
            // Works for registry, Git, local file: and embedded packages.
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PackagePaths).Assembly);

            if (package != null && !string.IsNullOrEmpty(package.assetPath))
            {
                return package.assetPath.TrimEnd('/');
            }

            // Source dropped into Assets/: find our own assembly definition and walk up from Editor/.
            string[] guids = AssetDatabase.FindAssets("Wooshii.HierarchyDecorator.Editor t:AssemblyDefinitionAsset");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (!path.EndsWith("/Wooshii.HierarchyDecorator.Editor.asmdef", System.StringComparison.Ordinal))
                {
                    continue;
                }

                string editorFolder = Path.GetDirectoryName(path)?.Replace('\\', '/');
                string root = Path.GetDirectoryName(editorFolder)?.Replace('\\', '/');

                if (!string.IsNullOrEmpty(root))
                {
                    return root;
                }
            }

            HierarchyLog.Once(
                "package-root",
                "Could not determine where the package is installed; falling back to " +
                PackageInfo.DefaultPackageRoot + ". Stylesheets may not load.");

            return PackageInfo.DefaultPackageRoot;
        }
    }
}
