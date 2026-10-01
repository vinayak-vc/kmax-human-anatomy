using System.Collections.Generic;

using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;

using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Builds the body map's prefab: a bust of the whole body drawn as a few see-through glowing layers, with the organs
    /// that lead into topics set inside it as solid regions.
    ///
    /// <para>Each layer, such as the skeleton or the muscles, merges many structures into a single mesh, because the body
    /// map only shows them as a backdrop and thousands of separate objects would be wasted. The bust crop fades each one
    /// out below the ribs and towards the arms. A region is a structure like those of a topic, so it is pointed at,
    /// highlighted and numbered by the same code; its target is a sphere larger than the organ, and its text names the
    /// topic it opens.</para>
    ///
    /// <para>Like the topic importer it updates in place, so regenerating never changes an asset's GUID.</para>
    /// </summary>
    public static class AnatomyBodyImporter {
        /// <summary>The id the prefab is saved under and the topic data is written against.</summary>
        public const string ModelId = "body";

        private const string LogPrefix = "[Anatomy] ";

        /// <summary>Imports the body map and returns its prefab, or null after logging why it could not.</summary>
        public static GameObject Import() {
            Dictionary<string, MtlMaterial> materials = new Dictionary<string, MtlMaterial>();
            AnatomyBodyLayerSpec[] layerSpecs = AnatomyBodySources.Layers();
            AnatomyBodyRegionSpec[] regionSpecs = AnatomyBodySources.Regions();

            AnatomyMeshData[] layers = new AnatomyMeshData[layerSpecs.Length];
            for (int i = 0; i < layerSpecs.Length; i++) {
                layers[i] = BuildLayer(layerSpecs[i], materials);
                if (layers[i] == null) {
                    return null;
                }
            }

            AnatomyMeshData[] regions = new AnatomyMeshData[regionSpecs.Length];
            MtlMaterial[] looks = new MtlMaterial[regionSpecs.Length];
            for (int i = 0; i < regionSpecs.Length; i++) {
                List<AnatomyMeshData> parts = new List<AnatomyMeshData>();
                if (!AnatomySourceReader.Read(regionSpecs[i].SourceFiles, parts, materials) || parts.Count == 0) {
                    Debug.LogError($"{LogPrefix}Body map region '{regionSpecs[i].Id}' produced no geometry; check its source files.");
                    return null;
                }

                looks[i] = AnatomyMaterialLook.Average("body_" + regionSpecs[i].Id, parts, materials);
                regions[i] = AnatomyMeshMerger.Merge(parts, regionSpecs[i].Id);
            }

            Bounds bounds = AnatomyCropRegion.SolidBoundsOf(layers[0]);
            for (int i = 1; i < layers.Length; i++) {
                bounds.Encapsulate(AnatomyCropRegion.SolidBoundsOf(layers[i]));
            }

            for (int i = 0; i < regions.Length; i++) {
                bounds.Encapsulate(regions[i].Bounds);
            }

            KmaxRigBuilder.EnsureFolder(AnatomyPaths.MeshFolder + "/" + ModelId);
            KmaxRigBuilder.EnsureFolder(AnatomyPaths.MaterialFolder + "/" + ModelId);
            KmaxRigBuilder.EnsureFolder(AnatomyPaths.PrefabFolder);

            GameObject root = new GameObject(ModelId);
            for (int i = 0; i < layers.Length; i++) {
                CreateLayer(root.transform, layerSpecs[i], layers[i], bounds.center);
            }

            Material rim = AnatomyGlowMaterials.EnsureRim();
            AnatomyStructure[] structures = new AnatomyStructure[regions.Length];
            for (int i = 0; i < regions.Length; i++) {
                structures[i] = CreateRegion(root.transform, regionSpecs[i], regions[i], looks[i], rim, bounds.center);
            }

            AnatomyModelImporter.ConfigureModel(root.AddComponent<AnatomyModel>(), ModelId, bounds.size, structures);

            string prefabPath = AnatomyPaths.PrefabFolder + "/" + ModelId + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogPrefix}'{ModelId}': {layers.Length} layer(s), {structures.Length} region(s), " +
                $"{bounds.size.x * 1000f:F0} x {bounds.size.y * 1000f:F0} x {bounds.size.z * 1000f:F0} mm " +
                $"(width x height x depth) -> {prefabPath}");
            return prefab;
        }

        /// <summary>One cropped mesh for the layer, or null after logging why there is none.</summary>
        private static AnatomyMeshData BuildLayer(AnatomyBodyLayerSpec spec, Dictionary<string, MtlMaterial> materials) {
            List<AnatomyMeshData> parts = new List<AnatomyMeshData>();
            if (!AnatomySourceReader.Read(spec.SourceFiles, parts, materials)) {
                return null;
            }

            for (int i = 0; i < parts.Count; i++) {
                Color colour = spec.UsesSourceColours ? SourceColourOf(parts[i], materials) : Color.white;
                AnatomyMeshMerger.Paint(parts[i], colour);
            }

            AnatomyMeshData cropped = AnatomyCropRegion.Bust.Apply(AnatomyMeshMerger.Merge(parts, spec.Id));
            if (cropped.Triangles.Count == 0) {
                Debug.LogError($"{LogPrefix}Body map layer '{spec.Id}' has nothing left after the bust crop; check its source files.");
                return null;
            }

            return cropped;
        }

        /// <summary>
        /// The colour the pack gives a structure, brightened until its strongest channel is full, because a layer is drawn
        /// as light and a dark blue vein would hardly show. A structure with no colour is white.
        /// </summary>
        private static Color SourceColourOf(AnatomyMeshData part, Dictionary<string, MtlMaterial> materials) {
            MtlMaterial source;
            if (!materials.TryGetValue(part.SourceMaterialName, out source)) {
                return Color.white;
            }

            Color colour = source.DiffuseColor;
            float strongest = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
            if (strongest <= 0f) {
                return Color.white;
            }

            colour = new Color(colour.r / strongest, colour.g / strongest, colour.b / strongest, 1f);
            return PlayerSettings.colorSpace == ColorSpace.Linear ? colour.linear : colour;
        }

        private static void CreateLayer(Transform parent, AnatomyBodyLayerSpec spec, AnatomyMeshData data, Vector3 modelCentre) {
            GameObject host = new GameObject(spec.Id);
            host.transform.SetParent(parent, false);
            Vector3 pivot = data.Bounds.center;
            host.transform.localPosition = pivot - modelCentre;

            MeshFilter filter = host.AddComponent<MeshFilter>();
            filter.sharedMesh = EnsureMesh("layer_" + spec.Id, data, pivot);
            MeshRenderer renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AnatomyGlowMaterials.EnsureLayer(spec);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            AnatomyLayer layer = host.AddComponent<AnatomyLayer>();
            SerializedObject serialized = new SerializedObject(layer);
            KmaxRigBuilder.SetString(serialized, "layerId", spec.Id);
            KmaxRigBuilder.SetString(serialized, "label", spec.Label);
            KmaxRigBuilder.SetReference(serialized, "layerRenderer", renderer);
            KmaxRigBuilder.SetFloat(serialized, "intensity", spec.Intensity);
            KmaxRigBuilder.SetBool(serialized, "startsShown", spec.StartsShown);
            SetColor(serialized, "tint", spec.Tint);
            SetColor(serialized, "swatch", spec.Swatch);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetColor(SerializedObject target, string field, Color value) {
            SerializedProperty property = target.FindProperty(field);
            if (property == null) {
                Debug.LogWarning($"{LogPrefix}no colour field '{field}' on '{target.targetObject.GetType().Name}'.");
                return;
            }

            property.colorValue = value;
        }

        private static AnatomyStructure CreateRegion(Transform parent, AnatomyBodyRegionSpec spec, AnatomyMeshData data,
            MtlMaterial look, Material rim, Vector3 modelCentre) {
            GameObject host = new GameObject(spec.Id);
            host.transform.SetParent(parent, false);
            Vector3 pivot = data.Bounds.center;
            host.transform.localPosition = pivot - modelCentre;

            Mesh mesh = EnsureMesh("region_" + spec.Id, data, pivot);
            Texture2D texture = string.IsNullOrEmpty(look.DiffuseTexturePath) ? null : AnatomyTextureFactory.EnsureTexture(look.DiffuseTexturePath);
            string materialPath = AnatomyPaths.MaterialFolder + "/" + ModelId + "/" + spec.Id + ".mat";
            string ghostPath = AnatomyPaths.MaterialFolder + "/" + ModelId + "/" + spec.Id + "_ghost.mat";
            Material material = AnatomyMaterialFactory.EnsureMaterial(materialPath, look, texture);
            Material ghost = AnatomyMaterialFactory.EnsureGhostMaterial(ghostPath, look, texture);

            MeshFilter filter = host.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            if (spec.HasSphereHotspot) {
                SphereCollider hotspot = host.AddComponent<SphereCollider>();
                hotspot.center = spec.HotspotCentre - data.Bounds.center;
                hotspot.radius = spec.HotspotRadius;
            } else {
                MeshCollider surface = host.AddComponent<MeshCollider>();
                surface.sharedMesh = mesh;
            }

            return AnatomyModelImporter.AddStructureComponent(host, data, pivot, renderer, ghost, rim);
        }

        private static Mesh EnsureMesh(string meshName, AnatomyMeshData data, Vector3 pivot) {
            return AnatomyPrefabParts.EnsureMesh(ModelId, meshName, data, pivot);
        }
    }
}