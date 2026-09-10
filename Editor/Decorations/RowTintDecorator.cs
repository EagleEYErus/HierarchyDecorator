using UnityEngine;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// Optional override of the alternating row colours.
    ///
    /// Unity 6.6 draws alternating rows itself and does it well, so this decorator does nothing unless the
    /// user explicitly asks for different colours - which is the one thing Unity's version does not offer.
    /// It paints the shared row container, never the row content, so selection, hover and the prefab
    /// override bar keep working.
    /// </summary>
    internal sealed class RowTintDecorator : IRowDecorator
    {
        public string Id => "row-tint";

        public CacheFacet RequiredFacets => CacheFacet.None;

        public void Apply(in RowContext context)
        {
            VisualElement row = context.Item.RowContainer;

            if (row == null)
            {
                return;
            }

            RowSettings settings = context.Settings.Rows;

            if (!context.Active || !context.IsGameObject || !settings.overrideAlternatingColors)
            {
                row.style.backgroundColor = StyleKeyword.Null;
                return;
            }

            int index = HierarchyNodes.GetRowIndex(context.View.ViewModel, context.Node);

            if (index < 0)
            {
                row.style.backgroundColor = StyleKeyword.Null;
                return;
            }

            ThemeColor themeColor = (index & 1) == 0 ? settings.evenColor : settings.oddColor;
            row.style.backgroundColor = themeColor.Resolve(context.IsDarkSkin);
        }
    }
}
