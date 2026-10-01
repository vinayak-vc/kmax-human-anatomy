using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// The sprites of the glass look, generated in code like the badges' so the project carries no hand-drawn art: a rounded
    /// rectangle to fill, the same rounded rectangle as a one-pixel outline, and a soft dot for the launcher's drifting dust.
    /// They are white, so the colour comes from whatever draws them, and they are not from the DOSCH pack, so they live in
    /// <c>Content/</c> and are committed.
    ///
    /// <para>The rounded rectangles are nine-sliced and sized so one sprite pixel is one canvas unit: the outline is one unit
    /// thick on a button and on a card alike.</para>
    /// </summary>
    public static class AnatomyGlassSprites {
        public const float CornerRadius = 12f;

        private const string FillPath = AnatomySpriteFiles.Folder + "/GlassFill.png";
        private const string BorderPath = AnatomySpriteFiles.Folder + "/GlassBorder.png";
        private const string SoftDotPath = AnatomySpriteFiles.Folder + "/SoftDot.png";
        private const int PanelSize = 64;
        private const int DotSize = 128;
        private const float Slice = 16f;
        private const float OutlineWidth = 1.5f;

        /// <summary>A white rounded rectangle, nine-sliced.</summary>
        public static Sprite EnsureFill() {
            return AnatomySpriteFiles.Ensure(FillPath, RenderPanel(false), new Vector4(Slice, Slice, Slice, Slice));
        }

        /// <summary>The outline of the same rounded rectangle, one canvas unit thick, nine-sliced.</summary>
        public static Sprite EnsureBorder() {
            return AnatomySpriteFiles.Ensure(BorderPath, RenderPanel(true), new Vector4(Slice, Slice, Slice, Slice));
        }

        /// <summary>A white disc that fades smoothly to nothing at its edge.</summary>
        public static Sprite EnsureSoftDot() {
            return AnatomySpriteFiles.Ensure(SoftDotPath, RenderDot(), Vector4.zero);
        }

        /// <summary>
        /// Distance to the edge of a rounded rectangle, negative inside, for a pixel. The alpha is the distance run through
        /// a one-pixel ramp, so the edge is anti-aliased; the outline is the part of the shape within its width of the edge.
        /// </summary>
        private static byte[] RenderPanel(bool outlineOnly) {
            Texture2D texture = new Texture2D(PanelSize, PanelSize, TextureFormat.RGBA32, false);
            float centre = (PanelSize - 1) * 0.5f;
            float inner = PanelSize * 0.5f - 1f - CornerRadius;
            Color[] pixels = new Color[PanelSize * PanelSize];
            for (int y = 0; y < PanelSize; y++) {
                for (int x = 0; x < PanelSize; x++) {
                    float qx = Mathf.Abs(x - centre) - inner;
                    float qy = Mathf.Abs(y - centre) - inner;
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - CornerRadius;
                    float alpha = Mathf.Clamp01(0.5f - distance);
                    if (outlineOnly) {
                        alpha *= Mathf.Clamp01(distance + OutlineWidth + 0.5f);
                    }

                    pixels[y * PanelSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            return AnatomySpriteFiles.Encode(texture);
        }

        private static byte[] RenderDot() {
            Texture2D texture = new Texture2D(DotSize, DotSize, TextureFormat.RGBA32, false);
            float centre = (DotSize - 1) * 0.5f;
            Color[] pixels = new Color[DotSize * DotSize];
            for (int y = 0; y < DotSize; y++) {
                for (int x = 0; x < DotSize; x++) {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / (DotSize * 0.5f);
                    float falloff = Mathf.Clamp01(1f - distance);
                    pixels[y * DotSize + x] = new Color(1f, 1f, 1f, falloff * falloff * (3f - 2f * falloff));
                }
            }

            texture.SetPixels(pixels);
            return AnatomySpriteFiles.Encode(texture);
        }
    }
}