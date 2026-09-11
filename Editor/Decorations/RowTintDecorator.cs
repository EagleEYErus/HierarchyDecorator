using UnityEngine;
using UnityEngine.UIElements;

namespace HierarchyDecorator
{
    /// <summary>
    /// Optional override of the alternating row colours.
    ///
    /// Unity 6.6 draws alternating rows itself and does it well, so this decorator does nothing unless the
    /// user explicitly asks for different colours - which is the one thing Unity's version does not offer.
    /// It paints the shared row container, never the row content, so the prefab override bar and everything
    /// Unity draws inside the row keep working.
    ///
    /// <para>
    /// The background is an inline style, and an inline style outranks every stylesheet - including the rule
    /// that draws Unity's selection highlight. A selected row is therefore skipped and left to Unity. Hover
    /// is a pseudo-state that cannot be read from C#, so a tinted row still loses its hover highlight; that
    /// is a known limitation, recorded in ARCHITECTURE.md.
    /// </para>
    /// </summary>
    internal sealed class RowTintDecorator : IRowDecorator
    {
        public string Id => "row-tint";

        public void Apply(in RowContext context)
        {
            VisualElement row = context.Item.RowContainer;

            if (row == null)
            {
                return;
            }

            RowSettings settings = context.Settings.Rows;

            // A selected row is Unity's to colour. Our colour is an inline style and would outrank the
            // selection rule rather than sit beneath it, so the row would lose its highlight entirely.
            if (!context.Active || !context.IsGameObject || context.IsSelected || !settings.overrideAlternatingColors)
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
