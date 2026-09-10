using UnityEditor.Search.Providers;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// Search filters for the Hierarchy window's search box.
    ///
    /// <para>
    /// The obvious API - <c>Unity.Hierarchy.Editor.IHierarchySearchPropositionProvider</c> - is internal, but
    /// it is not the mechanism that matters. The GameObject node handler hands every filter it does not
    /// recognise to <c>UnityEditor.Search.Providers.SceneQueryEngine</c>, whose constructor calls
    /// <c>AddFiltersFromAttribute&lt;SceneQueryEngineFilterAttribute, …&gt;()</c> - and that walks
    /// <c>TypeCache</c> across every loaded assembly. The attribute is public, and the hierarchy's query
    /// parser runs with <c>validateFilters = false</c>, so a static method carrying it in this package becomes
    /// a working token in the Hierarchy search box.
    /// </para>
    ///
    /// <para>
    /// Only decoration-specific questions are answered here. Everything Unity already provides -
    /// <c>missing:script</c>, <c>t:</c>, <c>components:</c>, <c>active:</c>, <c>is:root</c>, <c>prefab:</c> -
    /// is deliberately left alone: a second, slower answer to a solved question is worse than none.
    /// </para>
    ///
    /// <para>
    /// These run for every GameObject in the scene, not just visible rows, so they must stay cheap: name
    /// matching only, never the component cache.
    /// </para>
    /// </summary>
    internal static class HierarchyDecoratorSearchFilters
    {
        private const string None = "none";

        /// <summary>
        /// <c>hd:header</c>, <c>hd:separator</c>, <c>hd:none</c> - what kind of decoration a row carries.
        /// </summary>
        [SceneQueryEngineFilter("hd", new[] { ":", "=", "!=" })]
        public static string DecorationKind(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return None;
            }

            HierarchyDecoratorSettings settings = HierarchyDecoratorSettings.instance;
            NameMatcher.Match match = NameMatcher.FindRule(gameObject.name, settings.HeaderRules);

            if (!match.IsValid)
            {
                return None;
            }

            return settings.HeaderRules[match.RuleIndex].kind == HeaderKind.Separator
                ? "separator"
                : "header";
        }

        /// <summary>
        /// <c>hdrule:Subheader</c> - the name of the header rule a row matched.
        /// </summary>
        [SceneQueryEngineFilter("hdrule", new[] { ":", "=", "!=" })]
        public static string DecorationRule(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return string.Empty;
            }

            HierarchyDecoratorSettings settings = HierarchyDecoratorSettings.instance;
            NameMatcher.Match match = NameMatcher.FindRule(gameObject.name, settings.HeaderRules);

            return match.IsValid ? settings.HeaderRules[match.RuleIndex].name : string.Empty;
        }
    }
}
