using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Turns source OBJ files into one prefab: a root carrying <see cref="AnatomyModel"/> and one child
    /// per named structure, each with its own mesh, material and collider.
    ///
    /// <para>Every step updates in place - meshes are cleared and refilled, materials are re-applied and
    /// the prefab is saved over the old one - so regenerating never changes an asset's GUID and anything
    /// that refers to it stays valid.</para>
    ///
    /// <para>Geometry keeps its real-world size in metres. Each structure's mesh is centred on its own
    /// bounds and the whole model is centred on the origin, so a structure can be scaled or moved about
    /// its own middle and the model is placed by moving one object.</para>
    /// </summary>
    public static class AnatomyModelImporter {
        private const string LogPrefix = "[Anatomy] ";

        /// <summary>Metres added to each side of an animated mesh's bounds, so it is not culled while it moves.</summary>
        private const float AnimatedBoundsMargin = 0.012f;

        /// <summary>Imports one topic and returns its prefab, or null after logging why it could not.</summary>
        public static GameObject Import(AnatomyImportSpec spec) {
            List<AnatomyMeshData> parts = new List<AnatomyMeshData>();
            Dictionary<string, MtlMaterial> materials = new Dictionary<string, MtlMaterial>();
            if (!ReadSources(spec, parts, materials)) {
                return null;
            }

            Bounds modelBounds = parts[0].Bounds;
            for (int i = 1; i < parts.Count; i++) {
                modelBounds.Encapsulate(parts[i].Bounds);
            }

            KmaxRigBuilder.EnsureFolder(AnatomyPaths.MeshFolder + "/" + spec.ModelId);
            KmaxRigBuilder.EnsureFolder(AnatomyPaths.MaterialFolder + "/" + spec.ModelId);
            KmaxRigBuilder.EnsureFolder(AnatomyPaths.PrefabFolder);

            IAnatomyMotion motion = ChooseMotion(spec, parts, modelBounds.center);
            ImportContext context = new ImportContext(spec.ModelId, modelBounds.center, materials, AnatomyGlowMaterials.EnsureRim(), motion);
            GameObject root = new GameObject(spec.ModelId);
            AnatomyStructure[] structures = new AnatomyStructure[parts.Count];
            for (int i = 0; i < parts.Count; i++) {
                structures[i] = CreateStructure(root.transform, parts[i], context);
            }

            ConfigureModel(root.AddComponent<AnatomyModel>(), spec.ModelId, modelBounds.size, structures);
            if (motion != null) {
                motion.Finish(root);
            }

            string prefabPath = AnatomyPaths.PrefabFolder + "/" + spec.ModelId + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogPrefix}'{spec.ModelId}': {structures.Length} structure(s), " +
                $"{modelBounds.size.x * 1000f:F0} x {modelBounds.size.y * 1000f:F0} x {modelBounds.size.z * 1000f:F0} mm " +
                $"(width x height x depth) -> {prefabPath}");
            return prefab;
        }

        /// <summary>The motion to bake into this topic's meshes, or null when it has none or cannot be measured.</summary>
        private static IAnatomyMotion ChooseMotion(AnatomyImportSpec spec, List<AnatomyMeshData> parts, Vector3 modelCentre) {
            if (spec.BakesHeartbeat) {
                return AnatomyHeartMotion.Analyse(parts, modelCentre);
            }

            if (spec.BakesHearing) {
                return AnatomyEarMotion.Analyse(parts, modelCentre);
            }

            if (spec.BakesGaze) {
                return AnatomyEyeMotion.Analyse(parts, modelCentre);
            }

            if (spec.BakesBreathing) {
                return AnatomyBreathMotion.Analyse(parts, modelCentre);
            }

            return spec.BakesJaw ? AnatomyJawMotion.Analyse(parts, modelCentre) : null;
        }

        private static bool ReadSources(AnatomyImportSpec spec, List<AnatomyMeshData> parts,
            Dictionary<string, MtlMaterial> materials) {
            if (!AnatomySourceReader.Read(spec.SourceFiles, parts, materials)) {
                return false;
            }

            HashSet<string> usedIds = new HashSet<string>();
            for (int i = 0; i < parts.Count; i++) {
                parts[i].StructureId = MakeUnique(parts[i].StructureId, usedIds);
            }

            if (parts.Count == 0) {
                Debug.LogError($"{LogPrefix}'{spec.ModelId}' produced no geometry; check the source files.");
                return false;
            }

            return true;
        }

        private static string MakeUnique(string id, HashSet<string> usedIds) {
            if (usedIds.Add(id)) {
                return id;
            }

            int suffix = 2;
            string candidate = id + "_" + suffix;
            while (!usedIds.Add(candidate)) {
                suffix++;
                candidate = id + "_" + suffix;
            }

            Debug.LogWarning($"{LogPrefix}structure id '{id}' appears in more than one source file; the later one is '{candidate}'.");
            return candidate;
        }

        private static AnatomyStructure CreateStructure(Transform parent, AnatomyMeshData data, ImportContext context) {
            GameObject host = new GameObject(data.StructureId);
            host.transform.SetParent(parent, false);

            Vector3 pivot = data.Bounds.center;
            host.transform.localPosition = pivot - context.ModelCentre;

            string meshPath = AnatomyPaths.MeshFolder + "/" + context.ModelId + "/" + data.StructureId + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            bool isNewMesh = mesh == null;
            mesh = AnatomyMeshBuilder.ToMesh(data, pivot, mesh);
            int shapes = context.Motion != null ? context.Motion.AddShapes(data, context.ModelCentre, mesh) : 0;
            if (isNewMesh) {
                AssetDatabase.CreateAsset(mesh, meshPath);
            } else {
                EditorUtility.SetDirty(mesh);
            }

            MtlMaterial source;
            context.Materials.TryGetValue(data.SourceMaterialName, out source);
            Texture2D texture = source != null ? AnatomyTextureFactory.EnsureTexture(source.DiffuseTexturePath) : null;
            string materialPath = AnatomyPaths.MaterialFolder + "/" + context.ModelId + "/" + data.StructureId + ".mat";
            string ghostPath = AnatomyPaths.MaterialFolder + "/" + context.ModelId + "/" + data.StructureId + "_ghost.mat";
            Material material = AnatomyMaterialFactory.EnsureMaterial(materialPath, source, texture);
            Material ghost = AnatomyMaterialFactory.EnsureGhostMaterial(ghostPath, source, texture);

            Renderer renderer = AddRenderer(host, mesh, material, shapes > 0);
            MeshCollider collider = host.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            return AddStructureComponent(host, data, pivot, renderer, ghost, context.Rim);
        }

        /// <summary>
        /// Makes the object a structure: its id and readable name, its renderer, the transparent twin and edge glow it turns
        /// to glass with, and where a numbered marker's line ends on it. The body importer builds its regions the same way.
        /// </summary>
        internal static AnatomyStructure AddStructureComponent(GameObject host, AnatomyMeshData data, Vector3 pivot,
            Renderer renderer, Material ghost, Material rim) {
            AnatomyStructure structure = host.AddComponent<AnatomyStructure>();
            SerializedObject serialized = new SerializedObject(structure);
            KmaxRigBuilder.SetString(serialized, "structureId", data.StructureId);
            KmaxRigBuilder.SetString(serialized, "fallbackName", AnatomyNames.Prettify(data.StructureId));
            KmaxRigBuilder.SetReference(serialized, "structureRenderer", renderer);
            KmaxRigBuilder.SetReference(serialized, "ghostMaterial", ghost);
            KmaxRigBuilder.SetReference(serialized, "rimMaterial", rim);
            KmaxRigBuilder.SetVector3(serialized, "labelAnchor", data.FindLabelAnchor() - pivot);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return structure;
        }

        /// <summary>
        /// A structure that has blend shapes needs a skinned renderer to play them, even though it has no bones:
        /// the renderer then follows its own transform, so the highlight can still scale it. Everything else keeps
        /// the cheaper mesh renderer.
        /// </summary>
        private static Renderer AddRenderer(GameObject host, Mesh mesh, Material material, bool animated) {
            if (!animated) {
                MeshFilter filter = host.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer plain = host.AddComponent<MeshRenderer>();
                plain.sharedMaterial = material;
                return plain;
            }

            SkinnedMeshRenderer skinned = host.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
            skinned.sharedMaterial = material;
            skinned.skinnedMotionVectors = false;
            skinned.localBounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * (2f * AnimatedBoundsMargin));
            return skinned;
        }

        internal static void ConfigureModel(AnatomyModel model, string modelId, Vector3 size, AnatomyStructure[] structures) {
            SerializedObject serialized = new SerializedObject(model);
            KmaxRigBuilder.SetString(serialized, "modelId", modelId);
            KmaxRigBuilder.SetBounds(serialized, "localBounds", new Bounds(Vector3.zero, size));
            KmaxRigBuilder.SetReferences(serialized, "structures", structures);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>What every structure of one import shares.</summary>
        private class ImportContext {
            public ImportContext(string modelId, Vector3 modelCentre, Dictionary<string, MtlMaterial> materials,
                Material rim, IAnatomyMotion motion) {
                ModelId = modelId;
                ModelCentre = modelCentre;
                Materials = materials;
                Rim = rim;
                Motion = motion;
            }

            public string ModelId { get; private set; }
            public Vector3 ModelCentre { get; private set; }
            public Dictionary<string, MtlMaterial> Materials { get; private set; }
            public Material Rim { get; private set; }
            public IAnatomyMotion Motion { get; private set; }
        }
    }
}