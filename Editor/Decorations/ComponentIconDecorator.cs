using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// The component icon strip, drawn right-aligned at the end of the Name column - the placement users
    /// asked for in the upstream Unity 6 discussion.
    ///
    /// Everything expensive (enumerating components, resolving icons, detecting missing scripts) already
    /// happened once in <see cref="DecorationCache"/>. This method only writes styles onto pooled elements.
    /// The one live read is the component's enabled state, which is a single native call per icon and must be
    /// current: caching it would make the fade lag behind the Inspector checkbox.
    /// </summary>
    internal sealed class ComponentIconDecorator : IRowDecorator
    {
        private const string StripName = "hd-component-icons";
        private const string OverflowName = "hd-icon-overflow";

        private const float DisabledOpacity = 0.4f;

        public string Id => "component-icons";

        public CacheFacet RequiredFacets => CacheFacet.Components;

        public void Apply(in RowContext context)
        {
            VisualElement host = context.Item.RightCustomContainer;

            if (host == null)
            {
                return;
            }

            ComponentIconSettings options = context.Settings.ComponentIcons;
            HeaderRule header = context.Header;

            bool active = context.Active
                          && context.IsGameObject
                          && options.enabled
                          && context.Data != null
                          && (header == null || header.showComponentIcons);

            VisualElement strip = RowElements.Find<VisualElement>(host, StripName);

            if (!active || context.Data.IconCount == 0 && context.Data.OverflowCount == 0)
            {
                RowElements.SetVisible(strip, false);
                return;
            }

            strip ??= CreateStrip(host);
            strip.style.display = DisplayStyle.Flex;

            int required = context.Data.IconCount;
            EnsureIconElements(strip, required);

            for (int i = 0; i < strip.childCount; i++)
            {
                VisualElement element = strip[i];

                if (element.name == OverflowName)
                {
                    continue;
                }

                if (i >= required)
                {
                    element.style.display = DisplayStyle.None;
                    element.userData = null;
                    continue;
                }

                BindIcon(element, in context, i, options);
            }

            UpdateOverflow(strip, context.Data.OverflowCount);
        }

        private static void BindIcon(VisualElement element, in RowContext context, int index, ComponentIconSettings options)
        {
            ComponentIconEntry entry = context.Data.Icons[index];
            Component component = entry.Component;

            if (component == null)
            {
                element.style.display = DisplayStyle.None;
                element.userData = null;
                return;
            }

            element.style.display = DisplayStyle.Flex;
            element.style.backgroundImage = entry.Icon != null ? new StyleBackground(entry.Icon) : StyleKeyword.Null;
            element.userData = component;

            // -1 means the component has no enabled checkbox at all (e.g. Transform).
            int enabled = options.fadeDisabledComponents ? EditorUtility.GetObjectEnabled(component) : 1;
            element.style.opacity = enabled == 0 ? DisabledOpacity : 1f;

            element.pickingMode = options.clickAction == ComponentClickAction.None
                ? PickingMode.Ignore
                : PickingMode.Position;

            if (options.showTooltips)
            {
                element.tooltip = entry.StackCount > 1
                    ? $"{entry.DisplayName} ({entry.StackCount})"
                    : entry.DisplayName;
            }
            else
            {
                element.tooltip = null;
            }

            Label badge = element.childCount > 0 ? element[0] as Label : null;

            if (entry.StackCount > 1)
            {
                badge ??= CreateStackBadge(element);
                badge.style.display = DisplayStyle.Flex;
                badge.text = entry.StackCount.ToString();
            }
            else if (badge != null)
            {
                badge.style.display = DisplayStyle.None;
            }
        }

        private static void UpdateOverflow(VisualElement strip, int overflowCount)
        {
            Label overflow = RowElements.Find<Label>(strip, OverflowName);

            if (overflowCount <= 0)
            {
                RowElements.SetVisible(overflow, false);
                return;
            }

            if (overflow == null)
            {
                overflow = new Label
                {
                    name = OverflowName,
                    pickingMode = PickingMode.Ignore
                };

                overflow.AddToClassList("hd-icon-overflow");
                strip.Add(overflow);
            }
            else
            {
                // Keep it last so it always reads as a suffix.
                overflow.BringToFront();
            }

            overflow.style.display = DisplayStyle.Flex;
            overflow.text = "+" + overflowCount;
            overflow.tooltip = $"{overflowCount} more component(s) hidden by the icon limit";
        }

        private static VisualElement CreateStrip(VisualElement host)
        {
            VisualElement strip = IconElements.CreateStrip(StripName);
            host.Add(strip);
            return strip;
        }

        private static void EnsureIconElements(VisualElement strip, int required)
        {
            int existing = 0;

            for (int i = 0; i < strip.childCount; i++)
            {
                if (strip[i].name != OverflowName)
                {
                    existing++;
                }
            }

            for (int i = existing; i < required; i++)
            {
                VisualElement icon = IconElements.CreateIcon("hd-component-icon-" + i, interactive: true);

                // Registered once, at creation: rows are recycled and callbacks must not be re-registered on
                // every bind. The bound component travels in userData.
                icon.RegisterCallback<PointerDownEvent>(OnIconPointerDown);

                strip.Insert(strip.childCount, icon);
            }
        }

        private static Label CreateStackBadge(VisualElement icon)
        {
            Label badge = new Label
            {
                name = "hd-icon-count",
                pickingMode = PickingMode.Ignore
            };

            badge.AddToClassList("hd-icon-count");
            icon.Insert(0, badge);
            return badge;
        }

        private static void OnIconPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not VisualElement element)
            {
                return;
            }

            if (element.userData is not Component component || component == null)
            {
                return;
            }

            ComponentClickAction action = HierarchyDecoratorSettings.instance.ComponentIcons.clickAction;

            switch (action)
            {
                case ComponentClickAction.Select:
                    Selection.activeGameObject = component.gameObject;
                    EditorGUIUtility.PingObject(component);
                    break;

                case ComponentClickAction.ToggleEnabled:
                {
                    int enabled = EditorUtility.GetObjectEnabled(component);

                    if (enabled < 0)
                    {
                        return;
                    }

                    Undo.RecordObject(component, enabled == 1 ? "Disable Component" : "Enable Component");
                    EditorUtility.SetObjectEnabled(component, enabled == 0);
                    Undo.FlushUndoRecordObjects();
                    element.style.opacity = enabled == 1 ? DisabledOpacity : 1f;
                    break;
                }

                default:
                    return;
            }

            evt.StopPropagation();
        }
    }
}
