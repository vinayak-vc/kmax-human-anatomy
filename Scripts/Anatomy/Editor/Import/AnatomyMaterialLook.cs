using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// One material for a structure whose source files carry several, such as the heart's nine chambers and vessels. The
    /// body map's regions and the two activities' organs are each a single structure, so each gets one look.
    /// </summary>
    public static class AnatomyMaterialLook {
        /// <summary>
        /// The average of the pieces' tints, weighted by how much surface each has, and their texture atlas if every one uses
        /// the same. Pieces that use different atlases, such as the jaw and the teeth, cannot share one, so such a structure is
        /// drawn without a texture.
        /// </summary>
        public static MtlMaterial Average(string materialName, IReadOnlyList<AnatomyMeshData> parts, Dictionary<string, MtlMaterial> materials) {
            MtlMaterial look = new MtlMaterial(materialName);
            float red = 0f;
            float green = 0f;
            float blue = 0f;
            float total = 0f;
            string texture = null;
            bool sharesTexture = true;
            MtlMaterial heaviest = null;
            int heaviestTriangles = -1;

            for (int i = 0; i < parts.Count; i++) {
                MtlMaterial source;
                if (!materials.TryGetValue(parts[i].SourceMaterialName, out source)) {
                    continue;
                }

                float weight = parts[i].Triangles.Count;
                red += source.DiffuseColor.r * weight;
                green += source.DiffuseColor.g * weight;
                blue += source.DiffuseColor.b * weight;
                total += weight;

                string path = string.IsNullOrEmpty(source.DiffuseTexturePath) ? string.Empty : source.DiffuseTexturePath;
                if (texture == null) {
                    texture = path;
                } else if (texture != path) {
                    sharesTexture = false;
                }

                if (parts[i].Triangles.Count > heaviestTriangles) {
                    heaviestTriangles = parts[i].Triangles.Count;
                    heaviest = source;
                }
            }

            if (total > 0f) {
                look.DiffuseColor = new Color(red / total, green / total, blue / total, 1f);
            }

            if (heaviest != null) {
                look.SpecularColor = heaviest.SpecularColor;
                look.Shininess = heaviest.Shininess;
            }

            look.DiffuseTexturePath = sharesTexture && texture != null ? texture : string.Empty;
            return look;
        }
    }
}