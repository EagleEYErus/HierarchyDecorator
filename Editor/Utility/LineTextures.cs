using UnityEngine;

namespace HierarchyDecorator
{
    /// <summary>
    /// Tiny repeating textures used for dashed and dotted guide lines.
    ///
    /// Generated once per domain and tiled by UI Toolkit's <c>background-repeat</c>, which is what makes the
    /// "avoid expensive texture generation every repaint" requirement trivially satisfiable: nothing is
    /// created on the render path at all. Solid lines use a background colour and need no texture.
    /// </summary>
    internal static class LineTextures
    {
        private static Texture2D s_VerticalDash;
        private static Texture2D s_VerticalDot;
        private static Texture2D s_HorizontalDash;
        private static Texture2D s_HorizontalDot;
        private static Texture2D s_Solid;

        /// <summary>A single opaque pixel, used where a line has to be a background image rather than a colour.</summary>
        public static Texture2D GetSolid()
        {
            return s_Solid != null ? s_Solid : s_Solid = Create(1, 1, 1);
        }

        /// <summary>The tileable strip for a style, in the orientation requested. Never null.</summary>
        public static Texture2D GetStrip(LineStyle style, bool vertical)
        {
            Texture2D texture = vertical ? GetVertical(style) : GetHorizontal(style);
            return texture != null ? texture : GetSolid();
        }

        public static Texture2D GetVertical(LineStyle style)
        {
            switch (style)
            {
                case LineStyle.Dashed:
                    return s_VerticalDash != null ? s_VerticalDash : s_VerticalDash = Create(1, 6, 3);

                case LineStyle.Dotted:
                    return s_VerticalDot != null ? s_VerticalDot : s_VerticalDot = Create(1, 2, 1);

                default:
                    return null;
            }
        }

        public static Texture2D GetHorizontal(LineStyle style)
        {
            switch (style)
            {
                case LineStyle.Dashed:
                    return s_HorizontalDash != null ? s_HorizontalDash : s_HorizontalDash = Create(6, 1, 3);

                case LineStyle.Dotted:
                    return s_HorizontalDot != null ? s_HorizontalDot : s_HorizontalDot = Create(2, 1, 1);

                default:
                    return null;
            }
        }

        /// <summary>
        /// Builds a 1-pixel-wide (or tall) strip where the first <paramref name="onLength"/> pixels along the
        /// long axis are opaque white and the rest are transparent. The element tints it, so the texture
        /// itself is colour-agnostic.
        /// </summary>
        private static Texture2D Create(int width, int height, int onLength)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "HD_Line_" + width + "x" + height,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            bool vertical = height > width;
            int length = vertical ? height : width;
            Color32[] pixels = new Color32[width * height];

            for (int i = 0; i < length; i++)
            {
                Color32 value = i < onLength
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);

                pixels[i] = value;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        internal static void Release()
        {
            DestroyIfPresent(ref s_VerticalDash);
            DestroyIfPresent(ref s_VerticalDot);
            DestroyIfPresent(ref s_HorizontalDash);
            DestroyIfPresent(ref s_HorizontalDot);
            DestroyIfPresent(ref s_Solid);
        }

        private static void DestroyIfPresent(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            Object.DestroyImmediate(texture);
            texture = null;
        }
    }
}
