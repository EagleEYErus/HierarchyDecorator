using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// Resolves <see cref="ComponentRule"/>s to runtime <see cref="Type"/>s and back.
    ///
    /// Identity is stored twice (see ARCHITECTURE.md D7): built-in components have no MonoScript, and user
    /// scripts only survive a rename through their MonoScript GUID. Whichever lookup succeeds rewrites the
    /// other, so a rule self-heals the first time it is used after a refactor.
    /// </summary>
    internal static class ComponentCatalog
    {
        private static Dictionary<string, Type> s_TypesByName;
        private static Dictionary<Type, string> s_GuidsByType;
        private static List<Type> s_AllComponentTypes;

        // Assembly.GetName() builds a fresh AssemblyName every call; the settings list asks for one per
        // visible row per keystroke.
        private static readonly Dictionary<System.Reflection.Assembly, string> s_AssemblyNames =
            new Dictionary<System.Reflection.Assembly, string>();

        public static string AssemblyNameOf(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            System.Reflection.Assembly assembly = type.Assembly;

            if (!s_AssemblyNames.TryGetValue(assembly, out string name))
            {
                name = assembly.GetName().Name;
                s_AssemblyNames.Add(assembly, name);
            }

            return name;
        }

        public static IReadOnlyList<Type> AllComponentTypes
        {
            get
            {
                EnsureTypeIndex();
                return s_AllComponentTypes;
            }
        }

        /// <summary>Discards the type/script indexes. Called after a domain reload or script recompilation.</summary>
        public static void Invalidate()
        {
            s_TypesByName = null;
            s_GuidsByType = null;
            s_AllComponentTypes = null;
            s_AssemblyNames.Clear();
        }

        private static void EnsureTypeIndex()
        {
            if (s_TypesByName != null)
            {
                return;
            }

            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<Component>();

            s_TypesByName = new Dictionary<string, Type>(types.Count, StringComparer.Ordinal);
            s_AllComponentTypes = new List<Type>(types.Count);

            foreach (Type type in types)
            {
                if (type == null || type.IsAbstract || type.IsGenericType)
                {
                    continue;
                }

                s_AllComponentTypes.Add(type);

                string key = type.FullName;

                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                // Duplicate names across assemblies are possible; the first registration wins and the rule's
                // assembly name disambiguates on lookup.
                if (!s_TypesByName.ContainsKey(key))
                {
                    s_TypesByName.Add(key, type);
                }
            }

            s_AllComponentTypes.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
        }

        /// <summary>
        /// Builds the MonoScript GUID index by walking the AssetDatabase.
        ///
        /// It is bounded rather than cheap: built at most once per domain, and only reached when a rule has
        /// to be resolved - which happens once per settings revision when the rule table is rebuilt, and from
        /// the settings UI. It is never on the per-row path.
        /// </summary>
        private static void EnsureScriptIndex()
        {
            if (s_GuidsByType != null)
            {
                return;
            }

            s_GuidsByType = new Dictionary<Type, string>();

            string[] guids = AssetDatabase.FindAssets("t:MonoScript");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                Type type = script != null ? script.GetClass() : null;

                if (type == null || !typeof(Component).IsAssignableFrom(type))
                {
                    continue;
                }

                s_GuidsByType[type] = guids[i];
            }
        }

        public static string FindMonoScriptGuid(Type type)
        {
            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                return string.Empty;
            }

            EnsureScriptIndex();
            return s_GuidsByType.TryGetValue(type, out string guid) ? guid : string.Empty;
        }

        public static ComponentRule CreateRule(Type type, ComponentDisplay display)
        {
            if (type == null)
            {
                return null;
            }

            return new ComponentRule(
                type.FullName,
                AssemblyNameOf(type),
                FindMonoScriptGuid(type),
                display);
        }

        /// <summary>
        /// Resolves a rule to a live type. Returns null when the type no longer exists, which is reported in
        /// the settings UI rather than silently dropping the user's configuration.
        /// </summary>
        public static Type Resolve(ComponentRule rule)
        {
            if (rule == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(rule.monoScriptGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(rule.monoScriptGuid);

                if (!string.IsNullOrEmpty(path))
                {
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    Type scriptType = script != null ? script.GetClass() : null;

                    if (scriptType != null)
                    {
                        // Self-heal: the script was renamed or moved, so refresh the name-based identity.
                        rule.typeName = scriptType.FullName;
                        rule.assemblyName = AssemblyNameOf(scriptType);
                        return scriptType;
                    }
                }
            }

            if (string.IsNullOrEmpty(rule.typeName))
            {
                return null;
            }

            EnsureTypeIndex();

            if (!s_TypesByName.TryGetValue(rule.typeName, out Type type))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(rule.assemblyName) &&
                !string.Equals(AssemblyNameOf(type), rule.assemblyName, StringComparison.Ordinal))
            {
                // Same name in a different assembly: still the best candidate we have, but record the move.
                rule.assemblyName = AssemblyNameOf(type);
            }

            if (string.IsNullOrEmpty(rule.monoScriptGuid))
            {
                rule.monoScriptGuid = FindMonoScriptGuid(type);
            }

            return type;
        }

        public static bool IsUserScript(Type type)
        {
            return type != null && typeof(MonoBehaviour).IsAssignableFrom(type);
        }
    }
}
