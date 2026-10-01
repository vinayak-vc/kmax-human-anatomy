using System.IO;

using UnityEditor;

using UnityEngine;

using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Copies a source texture into the generated folder and imports it with settings suited to a stereo
    /// display: mipmaps and anisotropic filtering, because a surface seen at a slant by two eyes shimmers
    /// otherwise, and high-quality compression.
    /// </summary>
    public static class AnatomyTextureFactory {
        private const int AnisotropicLevel = 8;
        private const int MaximumSize = 2048;

        /// <summary>The imported texture, or null when the source file is missing.</summary>
        public static Texture2D EnsureTexture(string sourceFile) {
            if (string.IsNullOrEmpty(sourceFile) || !File.Exists(sourceFile)) {
                Debug.LogWarning($"[Anatomy] Texture not found: {sourceFile}");
                return null;
            }

            KmaxRigBuilder.EnsureFolder(AnatomyPaths.TextureFolder);
            string assetPath = AnatomyPaths.TextureFolder + "/" + Path.GetFileName(sourceFile);
            string destination = AnatomyPaths.ToAbsolute(assetPath);

            if (!File.Exists(destination) || new FileInfo(destination).Length != new FileInfo(sourceFile).Length) {
                File.Copy(sourceFile, destination, true);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            ApplyImportSettings(assetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static void ApplyImportSettings(string assetPath) {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) {
                return;
            }

            bool matches = importer.sRGBTexture
                && importer.mipmapEnabled
                && importer.anisoLevel == AnisotropicLevel
                && importer.maxTextureSize == MaximumSize
                && importer.textureCompression == TextureImporterCompression.CompressedHQ;
            if (matches) {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.anisoLevel = AnisotropicLevel;
            importer.maxTextureSize = MaximumSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }
}