using System.IO;

using UnityEditor;

using UnityEngine;

using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// The two round sprites the numbered badges are drawn with, generated in code so the project carries no
    /// hand-drawn art and the edges stay sharp at any size. They are not from the DOSCH pack, so they live in
    /// <c>Content/</c> and are committed. A sprite is only rewritten when its pixels would change, so rebuilding the
    /// scene leaves version control alone.
    /// </summary>
    public static class AnatomyMarkerSprites {
        public const string Folder = KmaxRigBuilder.ModuleRoot + "/Content/Textures";

        private const string DiscPath = Folder + "/MarkerDisc.png";
        private const string RingPath = Folder + "/MarkerRing.png";
        private const int Size = 256;
        private const float Margin = 2f;
        private const float RingWidth = 14f;

        /// <summary>A filled white circle.</summary>
        public static Sprite EnsureDisc() {
            return Ensure(DiscPath, false);
        }

        /// <summary>A white circle outline.</summary>
        public static Sprite EnsureRing() {
            return Ensure(RingPath, true);
        }

        private static Sprite Ensure(string path, bool outlineOnly) {
            KmaxRigBuilder.EnsureFolder(Folder);
            byte[] png = Render(outlineOnly);
            string absolute = AnatomyPaths.ToAbsolute(path);
            if (!File.Exists(absolute) || !SameBytes(File.ReadAllBytes(absolute), png)) {
                File.WriteAllBytes(absolute, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.Uncompressed) {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }

        private static bool SameBytes(byte[] first, byte[] second) {
            if (first.Length != second.Length) {
                return false;
            }

            for (int i = 0; i < first.Length; i++) {
                if (first[i] != second[i]) {
                    return false;
                }
            }

            return true;
        }
    }
}