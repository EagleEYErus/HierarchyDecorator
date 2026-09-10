using System.Text;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// Extends the row name tooltip with decoration facts that are otherwise invisible: the real object name
    /// behind a header label, and any missing script warning.
    ///
    /// Unity hands us the StringBuilder it is going to use, so nothing is allocated unless there is something
    /// to say.
    /// </summary>
    internal static class HierarchyTooltips
    {
        internal static void OnGetTooltip(HierarchyWindow window, HierarchyView view, HierarchyViewItem item, StringBuilder tooltip, bool filtering)
        {
            if (item == null || tooltip == null || DecoratorHost.Suspended)
            {
                return;
            }

            if (!HierarchyDecoratorUserSettings.instance.Enabled)
            {
                return;
            }

            if (item.Node == HierarchyNode.Null || !HierarchyNodes.TryGetGameObject(item, out UnityEngine.GameObject gameObject))
            {
                return;
            }

            EntityId id = view.Source.GetEntityIdFromNode(item.Node);
            RowData data = DecorationCache.GetOrCreate(id);
            HierarchyDecoratorSettings settings = HierarchyDecoratorSettings.instance;

            DecorationCache.Ensure(data, gameObject, settings, CacheFacet.Name);

            if (data.HasHeader && data.HeaderRuleIndex < settings.HeaderRules.Count)
            {
                Append(tooltip, gameObject.name);
            }

            if (settings.Indicators.showMissingScripts && (data.Valid & CacheFacet.Components) != 0 && data.MissingScriptCount > 0)
            {
                Append(tooltip, data.MissingScriptCount == 1
                    ? "Missing MonoBehaviour script"
                    : data.MissingScriptCount + " missing MonoBehaviour scripts");
            }
        }

        private static void Append(StringBuilder builder, string line)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(line);
        }
    }
}
