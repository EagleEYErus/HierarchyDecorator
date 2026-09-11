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
    ///
    /// The strip has a fixed two-child layout - an icon container and the overflow label - so icon <c>i</c>
    /// is always <c>icons[i]</c>. Mixing the label in with the icons would desynchronise those indices from
    /// the cached icon array the first time a row overflowed.
    /// </summary>
    internal sealed class ComponentIconDecorator : IRowDecorator
    {
        private const string StripName = "hd-component-icons";
        private const string IconsName = "hd-icons";
        private const string OverflowName = "hd-icon-overflow";

        private const int IconsIndex = 0;
        private const int OverflowIndex = 1;

        private const float DisabledOpacity = 0.4f;

        public string Id => "component-icons";

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

            if (!active || (context.Data.IconCount == 0 && context.Data.OverflowCount == 0))
            {
                RowElements.SetVisible(strip, false);
                return;
            }

            strip ??= CreateStrip(host);
            strip.style.display = DisplayStyle.Flex;

            VisualElement icons = strip[IconsIndex];
            int required = context.Data.IconCount;

            EnsureIconElements(icons, required);

            for (int i = 0; i < icons.childCount; i++)
            {
                VisualElement element = icons[i];

                if (i >= required)
                {
                    element.style.display = DisplayStyle.None;
                    element.userData = null;
                    continue;
                }

                BindIcon(element, in context, i, options);
            }

            UpdateOverflow(strip, options.showOverflowIndicator ? context.Data.OverflowCount : 0);
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

            // Always pickable: PickingMode.Ignore would also kill the tooltip, and "no click action" is
            // not the same request as "no tooltip". OnIconClicked returns early when the action is None.
            element.pickingMode = PickingMode.Position;

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
            IconElements.SetOverflow((Label)strip[OverflowIndex], overflowCount);
        }

        private static VisualElement CreateStrip(VisualElement host)
        {
            VisualElement strip = IconElements.CreateStrip(StripName);

            // Fixed layout: icons first, then the overflow suffix. Both are created up front so their child
            // indices never move.
            strip.Add(IconElements.CreateStrip(IconsName));

            Label overflow = new Label
            {
                name = OverflowName,
                pickingMode = PickingMode.Ignore
            };

            overflow.AddToClassList("hd-icon-overflow");
            overflow.style.display = DisplayStyle.None;
            strip.Add(overflow);

            host.Add(strip);
            return strip;
        }

        private static void EnsureIconElements(VisualElement icons, int required)
        {
            for (int i = icons.childCount; i < required; i++)
            {
                VisualElement icon = IconElements.CreateIcon("hd-component-icon-" + i, interactive: true);

                // Registered once, at creation: rows are recycled and callbacks must not be re-registered on
                // every bind. The bound component travels in userData.
                icon.RegisterCallback<ClickEvent>(OnIconClicked);

                icons.Add(icon);
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

        /// <summary>
        /// Shared with the Components column so a click means the same thing in both places.
        ///
        /// <para>
        /// This is a <see cref="ClickEvent"/>, not a <see cref="PointerDownEvent"/>, and it deliberately does
        /// not stop propagation. Row selection and the drag-and-drop arming both live on ancestors in the
        /// bubble phase of the pointer-down, so swallowing that event made a row unselectable and
        /// undraggable anywhere the icon strip covered it. A click arrives after selection has already
        /// happened, which is exactly the ordering we want.
        /// </para>
        ///
        /// <para>
        /// Modified and repeated clicks are left entirely to Unity: shift and ctrl/cmd extend the selection,
        /// and a double click frames the object.
        /// </para>
        /// </summary>
        internal static void OnIconClicked(ClickEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not VisualElement element)
            {
                return;
            }

            if (evt.shiftKey || evt.ctrlKey || evt.commandKey || evt.altKey || evt.clickCount > 1)
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
        }
    }
}
