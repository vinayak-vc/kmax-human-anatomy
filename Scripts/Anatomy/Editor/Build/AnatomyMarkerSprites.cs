using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// The two round sprites the numbered badges are drawn with, generated in code so the project carries no
    /// hand-drawn art and the edges stay sharp at any size. They are not from the DOSCH pack, so they live in
    /// <c>Content/</c> and are committed. A sprite is only rewritten when its pixels would change, so rebuilding the
    /// scene leaves version control alone.
    /// </summary>
    public static class AnatomyMarkerSprites {
        public const string Folder = AnatomySpriteFiles.Folder;

        private const string DiscPath = Folder + "/MarkerDisc.png";
        private const string RingPath = Folder + "/MarkerRing.png";
        private const int Size = 256;
        private const float Margin = 2f;
        private const float RingWidth = 14f;

        /// <summary>A filled white circle.</summary>
        public static Sprite EnsureDisc() {
            return AnatomySpriteFiles.Ensure(DiscPath, Render(false), Vector4.zero);
        }

        /// <summary>A white circle outline.</summary>
        public static Sprite EnsureRing() {
            return AnatomySpriteFiles.Ensure(RingPath, Render(true), Vector4.zero);
        }

        /// <summary>White with an anti-aliased circular alpha, one pixel soft at the edge.</summary>
        private static byte[] Render(bool outlineOnly) {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            float centre = (Size - 1) * 0.5f;
            float radius = Size * 0.5f - Margin;
            Color[] pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++) {
                for (int x = 0; x < Size; x++) {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    if (outlineOnly) {
                        alpha *= Mathf.Clamp01(distance - (radius - RingWidth) + 0.5f);
                    }

                    pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            return AnatomySpriteFiles.Encode(texture);
        }
    }
}