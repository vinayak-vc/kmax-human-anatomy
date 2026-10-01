using UnityEditor;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Creates or updates the URP Lit material for one structure from its source material.
    ///
    /// The source tint multiplies the painted texture, which is how the DOSCH atlases are meant to be
    /// used: the texture carries tissue detail and the tint carries the anatomical colour coding. Only
    /// two finishes are needed. Tissue with no specular colour is matte; anything with one is glossy in
    /// proportion to the source's shininess, which suits vessel walls and muscle.
    /// </summary>
    public static class AnatomyMaterialFactory {
        private const string LitShaderName = "Universal Render Pipeline/Lit";
        private const float MatteSmoothness = 0.22f;
        private const float SatinSmoothness = 0.35f;
        private const float GlossySmoothness = 0.78f;
        private const float SpecularThreshold = 0.05f;
        private const float MaximumShininess = 256f;

        /// <summary>The material at <paramref name="assetPath"/>, created if absent and always re-applied.</summary>
        public static Material EnsureMaterial(string assetPath, MtlMaterial source, Texture2D texture) {
            return Ensure(assetPath, source, texture, false);
        }

        /// <summary>
        /// The transparent twin of a structure's material. It looks the same at full opacity, so the highlight
        /// can swap to it while a structure fades and the swap is not visible.
        /// </summary>
        public static Material EnsureGhostMaterial(string assetPath, MtlMaterial source, Texture2D texture) {
            return Ensure(assetPath, source, texture, true);
        }

        private static Material Ensure(string assetPath, MtlMaterial source, Texture2D texture, bool transparent) {
            Shader shader = Shader.Find(LitShaderName);
            if (shader == null) {
                Debug.LogError("[Anatomy] URP Lit shader not found.");
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

            Color tint = source != null ? source.DiffuseColor : Color.white;
            tint.a = 1f;
            material.SetColor("_BaseColor", tint);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", SmoothnessOf(source));

            // Emission is off at rest and switched on per renderer by StructureHighlight, which can only
            // drive the colour: the keyword has to be enabled on the shared material.
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            // Both faces are drawn: several DOSCH structures are open shells, such as the left atrium, and
            // seen into from outside their far wall would otherwise vanish.
            material.SetFloat("_Cull", 0f);

            // A ghost fades its highlights along with the rest of it. Left at the default, the specular is kept at
            // full strength however transparent the surface is, and the glass turns milky.
            material.SetFloat("_BlendModePreserveSpecular", 0f);
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_Blend", 0f);
            BaseShaderGUI.SetupMaterialBlendMode(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>How glossy a structure is: matte with no specular colour, and otherwise glossy in proportion to the source's shininess.</summary>
        internal static float SmoothnessOf(MtlMaterial source) {
            if (source == null) {
                return MatteSmoothness;
            }

            Color specular = source.SpecularColor;
            float strength = Mathf.Max(specular.r, Mathf.Max(specular.g, specular.b));
            if (strength < SpecularThreshold) {
                return MatteSmoothness;
            }

            return Mathf.Lerp(SatinSmoothness, GlossySmoothness, Mathf.Clamp01(source.Shininess / MaximumShininess));
        }
    }
}