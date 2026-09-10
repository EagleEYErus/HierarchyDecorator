using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// Find-or-create helpers for elements we attach to recycled hierarchy rows.
    ///
    /// Rows are pooled: anything added to <c>LeftCustomContainer</c> / <c>RightCustomContainer</c> survives
    /// rebinding. Blind <c>Add()</c> calls therefore accumulate elements forever, which is why every
    /// decorator goes through here. Lookup is a linear scan over a handful of children - cheaper and
    /// allocation-free compared with a UQuery.
    /// </summary>
    internal static class RowElements
    {
        public static T Find<T>(VisualElement parent, string name) where T : VisualElement
        {
            if (parent == null)
            {
                return null;
            }

            int count = parent.childCount;

            for (int i = 0; i < count; i++)
            {
                VisualElement child = parent[i];

                if (child.name == name && child is T typed)
                {
                    return typed;
                }
            }

            return null;
        }

        public static T GetOrCreate<T>(VisualElement parent, string name, string ussClass = null) where T : VisualElement, new()
        {
            T existing = Find<T>(parent, name);

            if (existing != null)
            {
                return existing;
            }

            T created = new T
            {
                name = name,
                pickingMode = PickingMode.Ignore
            };

            if (!string.IsNullOrEmpty(ussClass))
            {
                created.AddToClassList(ussClass);
            }

            parent.Add(created);
            return created;
        }

        /// <summary>Hides an element we may have added earlier without creating one if it is absent.</summary>
        public static void HideIfPresent(VisualElement parent, string name)
        {
            VisualElement element = Find<VisualElement>(parent, name);

            if (element != null)
            {
                element.style.display = DisplayStyle.None;
            }
        }

        public static void SetVisible(VisualElement element, bool visible)
        {
            if (element == null)
            {
                return;
            }

            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
