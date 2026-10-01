using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;

using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// The shared materials that use the Glow shader: the edge glow drawn over a structure that has
    /// receded, the leader line from a numbered marker to its structure, and one glow for each layer of the body map.
    /// None comes from the DOSCH pack, so they live in <c>Content/</c> and are committed.
    /// </summary>
    public static class AnatomyGlowMaterials {
        public const string Folder = KmaxRigBuilder.ModuleRoot + "/Content/Materials";
        public const string RimPath = Folder + "/AnatomyRim.mat";
        public const string LinePath = Folder + "/AnatomyLine.mat";

        private const string ShaderName = "Kmax Anatomy/Glow";
        private const float RimPower = 2.6f;

        /// <summary>Additive silhouette glow, depth-tested so a structure in front still hides it.</summary>
        public static Material EnsureRim() {
            return Ensure(RimPath, BlendMode.One, CompareFunction.LessEqual, CullMode.Back, true);
        }

        /// <summary>Alpha-blended flat line that is never hidden, so a marker's line always reaches its structure.</summary>
        public static Material EnsureLine() {
            return Ensure(LinePath, BlendMode.OneMinusSrcAlpha, CompareFunction.Always, CullMode.Off, false);
        }

        /// <summary>
        /// The glow one body-map layer is drawn with: additive and depth-tested, so layers stack without sorting, brightest
        /// at the edges and, by the layer's floor, a little in the middle.
        /// </summary>
        public static Material EnsureLayer(AnatomyBodyLayerSpec spec) {
            string path = Folder + "/BodyLayer" + AnatomyNames.Prettify(spec.Id) + ".mat";
            Material material = Ensure(path, BlendMode.One, CompareFunction.LessEqual, CullMode.Back, true);
            if (material == null) {
                return null;
            }

            material.SetFloat("_RimPower", spec.RimPower);
            material.SetFloat("_Floor", spec.Floor);
            material.SetColor("_BaseColor", new Color(spec.Tint.r, spec.Tint.g, spec.Tint.b, spec.Intensity));
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The faint outline of where an organ belongs in the organ puzzle: additive and depth-tested like a layer, brightest at the
        /// edge and with enough of a floor that the shape reads. The puzzle sets its brightness through a property block.
        /// </summary>
        public static Material EnsureSlot() {
            Material material = Ensure(Folder + "/OrganSlot.mat", BlendMode.One, CompareFunction.LessEqual, CullMode.Back, true);
            if (material == null) {
                return null;
            }

            material.SetFloat("_RimPower", 1.8f);
            material.SetFloat("_Floor", 0.3f);
            material.SetColor("_BaseColor", new Color(0.45f, 0.9f, 1f, 0.5f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Ensure(string path, BlendMode destination, CompareFunction depthTest, CullMode cull, bool edgeOnly) {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null) {
                Debug.LogError($"[Anatomy] Shader '{ShaderName}' not found; is Content/Shaders/AnatomyGlow.shader imported?");
                return null;
            }

            KmaxRigBuilder.EnsureFolder(Folder);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.shader != shader) {
                material.shader = shader;
            }

            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)destination);
            material.SetFloat("_ZTest", (float)depthTest);
            material.SetFloat("_Cull", (float)cull);
            material.SetFloat("_Fresnel", edgeOnly ? 1f : 0f);
            material.SetFloat("_RimPower", RimPower);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}