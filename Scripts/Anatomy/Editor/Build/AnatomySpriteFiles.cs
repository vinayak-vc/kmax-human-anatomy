using System.IO;

using UnityEditor;

using UnityEngine;

using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Writes a generated PNG and keeps it imported as a sprite. The shared sprites go into <c>Content/Textures</c>. A file is only
    /// rewritten when its pixels would change, so rebuilding the scene leaves version control alone.
    /// </summary>
    public static class AnatomySpriteFiles {
        public const string Folder = KmaxRigBuilder.ModuleRoot + "/Content/Textures";

        /// <summary>The sprite at <paramref name="path"/>, written from <paramref name="png"/> if it is missing or differs.</summary>
        /// <param name="border">Left, bottom, right and top of the nine-slice border in pixels; zero for a sprite that is not sliced.</param>
        public static Sprite Ensure(string path, byte[] png, Vector4 border) {
            KmaxRigBuilder.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            string absolute = AnatomyPaths.ToAbsolute(path);
            if (!File.Exists(absolute) || !SameBytes(File.ReadAllBytes(absolute), png)) {
                File.WriteAllBytes(absolute, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.spriteBorder != border
                || importer.textureCompression != TextureImporterCompression.Uncompressed) {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>The PNG bytes of a texture, which is then destroyed.</summary>
        public static byte[] Encode(Texture2D texture) {
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