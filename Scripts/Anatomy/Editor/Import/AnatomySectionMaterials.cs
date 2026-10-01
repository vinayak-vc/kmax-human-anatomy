using UnityEditor;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Creates or updates the materials the scan draws with. They use the Section shader, which is lit as a Universal Lit
    /// material is and can be cut away by the lens, and they take their tint, texture and gloss from the pack in the same way
    /// <see cref="AnatomyMaterialFactory"/> does, so a structure looks as it does in its own topic.
    /// </summary>
    public static class AnatomySectionMaterials {
        private const string ShaderName = "Kmax Anatomy/Section";

        /// <summary>
        /// The material at <paramref name="assetPath"/>, created if absent and always re-applied.
        /// </summary>
        /// <param name="tint">Multiplies the texture. White for a mesh whose vertex colours carry the tint.</param>
        /// <param name="cutFace">The colour of the inside of the structure, which shows where the lens cuts it open.</param>
        public static Material Ensure(string assetPath, Color tint, Color cutFace, float smoothness, Texture2D texture) {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) {
                Debug.LogError($"[Anatomy] Shader '{ShaderName}' not found; is Content/Shaders/AnatomySection.shader imported?");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null) {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            if (material.shader != shader) {
                material.shader = shader;
            }

            tint.a = 1f;
            cutFace.a = 1f;
            material.SetColor("_BaseColor", tint);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", smoothness);
            material.SetColor("_EmissionColor", Color.black);
            material.SetColor("_CapColor", cutFace);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The colour of the inside of a tissue whose surface is this colour: the same hue, darker and richer, as the inside of a
        /// muscle or an organ is.
        /// </summary>
        public static Color CutFaceOf(Color surface) {
            float hue;
            float saturation;
            float value;
            Color.RGBToHSV(surface, out hue, out saturation, out value);
            return Color.HSVToRGB(hue, Mathf.Min(1f, saturation * 1.25f + 0.05f), value * 0.72f);
        }
    }
}