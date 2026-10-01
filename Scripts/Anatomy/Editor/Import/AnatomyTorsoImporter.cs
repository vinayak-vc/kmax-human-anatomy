using System.Collections.Generic;
using System.IO;

using UnityEditor;

using UnityEngine;

using ViitorCloud.KmaxDisplay;
using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Builds the torso the two activities are played on.
    ///
    /// <para><b>The organ puzzle</b> (<see cref="ImportOrgans"/>): ten organs as separate solid structures in their true places,
    /// each with a faint outline of itself left behind as the place it belongs (a slot), inside a frame of the torso's bones drawn as
    /// glow. Each organ is a pen target: a convex collider, a kinematic body and a <see cref="Grabbable"/> that carries it by
    /// moving its transform.</para>
    ///
    /// <para><b>The scan</b> (<see cref="ImportScan"/>): the same torso drawn solid, in layers, so a lens can cut through it. The
    /// muscles that cover it (each file on its own atlas), the bones, the vessels and sixteen organs, all with the Section
    /// shader, which is lit like Universal Lit and can be cut away. The pen reads the organ it is inside from the organs'
    /// surfaces, so the organs have no colliders. A box the figure is trimmed to travels with the model.</para>
    ///
    /// <para>The organs are real DOSCH meshes at the positions the pack gives them, so putting one back is putting it where it
    /// really lies. The geometry of an organ is built once and shared by both activities' prefabs. Like every importer it
    /// updates in place, so regenerating never changes an asset's GUID.</para>
    /// </summary>
    public static class AnatomyTorsoImporter {
        /// <summary>The id of the organ puzzle: its prefab, its topic data and its behaviour's key.</summary>
        public const string OrgansModelId = "organs";

        /// <summary>The id of the scan: its prefab, its topic data and its behaviour's key.</summary>
        public const string ScanModelId = "scan";

        private const string LogPrefix = "[Anatomy] ";
        private const string MeshFolderId = "torso";
        private const float GrabMargin = 0.008f;

        /// <summary>Imports the organ puzzle and returns its prefab, or null after logging why it could not.</summary>
        public static GameObject ImportOrgans() {
            Dictionary<string, MtlMaterial> materials = new Dictionary<string, MtlMaterial>();
            List<TorsoOrgan> organs = ReadOrgans(true, materials);
            if (organs == null) {
                return null;
            }

            AnatomyBodyLayerSpec frameSpec = AnatomyTorsoSources.Frame();
            AnatomyMeshData frame = ReadFrame(frameSpec, materials);
            if (frame == null) {
                return null;
            }

            Bounds bounds = AnatomyCropRegion.SolidBoundsOf(frame);
            for (int i = 0; i < organs.Count; i++) {
                bounds.Encapsulate(organs[i].Data.Bounds);
            }

            KmaxRigBuilder.EnsureFolder(AnatomyPaths.MaterialFolder + "/" + OrgansModelId);
            KmaxRigBuilder.EnsureFolder(AnatomyPaths.PrefabFolder);

            GameObject root = new GameObject(OrgansModelId);
            CreateFrame(root.transform, frameSpec, frame, bounds.center);

            Material rim = AnatomyGlowMaterials.EnsureRim();
            Material slot = AnatomyGlowMaterials.EnsureSlot();
            AnatomyStructure[] structures = new AnatomyStructure[organs.Count];
            for (int i = 0; i < organs.Count; i++) {
                Material solid;
                Material ghost;
                EnsureLitMaterials(OrgansModelId, organs[i], out solid, out ghost);
                structures[i] = CreateOrgan(root.transform, organs[i], bounds.center, solid, ghost, rim, true, true);
                CreateSlot(root.transform, organs[i], bounds.center, slot);
            }

            AnatomyModelImporter.ConfigureModel(root.AddComponent<AnatomyModel>(), OrgansModelId, bounds.size, structures);
            return Save(root, OrgansModelId, structures.Length, bounds);
        }

        /// <summary>Imports the scan figure and returns its prefab, or null after logging why it could not.</summary>
        public static GameObject ImportScan() {
            Dictionary<string, MtlMaterial> materials = new Dictionary<string, MtlMaterial>();
            List<TorsoOrgan> organs = ReadOrgans(false, materials);
            if (organs == null) {
                return null;
            }

            List<SolidLayer> layers = new List<SolidLayer>();
            string[] muscleFiles = AnatomyTorsoSources.MuscleFiles();
            for (int i = 0; i < muscleFiles.Length; i++) {
                SolidLayer muscle = ReadSolidLayer("muscle_" + Path.GetFileNameWithoutExtension(muscleFiles[i]),
                    new string[] { muscleFiles[i] }, LayerPaint.SourceTint, materials);
                if (muscle == null) {
                    return null;
                }

                layers.Add(muscle);
            }

            int muscles = layers.Count;
            SolidLayer bones = ReadSolidLayer("bones", AnatomyTorsoSources.BoneFiles(), LayerPaint.Ivory, materials);
            SolidLayer vessels = ReadSolidLayer("vessels", AnatomyTorsoSources.VesselFiles(), LayerPaint.Brightened, materials);
            if (bones == null || vessels == null) {
                return null;
            }

            layers.Add(bones);
            layers.Add(vessels);

            // Centred on the muscles and bones: the figure the viewer sees, not the organs, which reach beyond it.
            Bounds bounds = AnatomyCropRegion.SolidBoundsOf(layers[0].Data);
            for (int i = 1; i < muscles; i++) {
                bounds.Encapsulate(AnatomyCropRegion.SolidBoundsOf(layers[i].Data));
            }

            bounds.Encapsulate(AnatomyCropRegion.SolidBoundsOf(bones.Data));

            KmaxRigBuilder.EnsureFolder(AnatomyPaths.MaterialFolder + "/" + ScanModelId);
            KmaxRigBuilder.EnsureFolder(AnatomyPaths.PrefabFolder);

            GameObject root = new GameObject(ScanModelId);
            for (int i = 0; i < layers.Count; i++) {
                CreateSolidLayer(root.transform, layers[i], bounds.center);
            }

            Material rim = AnatomyGlowMaterials.EnsureRim();
            AnatomyStructure[] structures = new AnatomyStructure[organs.Count];
            for (int i = 0; i < organs.Count; i++) {
                Material solid;
                Material ghost;
                EnsureSectionMaterials(organs[i], out solid, out ghost);
                structures[i] = CreateOrgan(root.transform, organs[i], bounds.center, solid, ghost, rim, false, false);
            }

            Bounds shown = AnatomyCropRegion.Torso.ShownBox(1f);
            AnatomyCutBox cut = root.AddComponent<AnatomyCutBox>();
            SerializedObject cutObject = new SerializedObject(cut);
            KmaxRigBuilder.SetBounds(cutObject, "localBounds", new Bounds(shown.center - bounds.center, shown.size));
            cutObject.ApplyModifiedPropertiesWithoutUndo();

            AnatomyModelImporter.ConfigureModel(root.AddComponent<AnatomyModel>(), ScanModelId, bounds.size, structures);
            return Save(root, ScanModelId, structures.Length, bounds);
        }

        private static GameObject Save(GameObject root, string modelId, int structureCount, Bounds bounds) {
            string prefabPath = AnatomyPaths.PrefabFolder + "/" + modelId + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogPrefix}'{modelId}': {structureCount} organ(s), " +
                $"{bounds.size.x * 1000f:F0} x {bounds.size.y * 1000f:F0} x {bounds.size.z * 1000f:F0} mm " +
                $"(width x height x depth) -> {prefabPath}");
            return prefab;
        }

        /// <summary>The organs the activity needs, each one structure with one look, or null after logging why one has no geometry.</summary>
        private static List<TorsoOrgan> ReadOrgans(bool puzzlePiecesOnly, Dictionary<string, MtlMaterial> materials) {
            AnatomyTorsoOrganSpec[] specs = AnatomyTorsoSources.Organs();
            List<TorsoOrgan> organs = new List<TorsoOrgan>();
            for (int i = 0; i < specs.Length; i++) {
                if (puzzlePiecesOnly && !specs[i].IsPuzzlePiece) {
                    continue;
                }

                List<AnatomyMeshData> parts = new List<AnatomyMeshData>();
                if (!AnatomySourceReader.Read(specs[i].SourceFiles, parts, materials) || parts.Count == 0) {
                    Debug.LogError($"{LogPrefix}Organ '{specs[i].Id}' produced no geometry; check its source files.");
                    return null;
                }

                MtlMaterial look = AnatomyMaterialLook.Average("torso_" + specs[i].Id, parts, materials);
                organs.Add(new TorsoOrgan(specs[i], AnatomyMeshMerger.Merge(parts, specs[i].Id), look));
            }

            return organs;
        }

        /// <summary>The bones of the frame, merged into one mesh and cropped to the torso, or null after logging why there is none.</summary>
        private static AnatomyMeshData ReadFrame(AnatomyBodyLayerSpec spec, Dictionary<string, MtlMaterial> materials) {
            List<AnatomyMeshData> parts = new List<AnatomyMeshData>();
            if (!AnatomySourceReader.Read(spec.SourceFiles, parts, materials)) {
                return null;
            }

            for (int i = 0; i < parts.Count; i++) {
                AnatomyMeshMerger.Paint(parts[i], Color.white);
            }

            AnatomyMeshData cropped = AnatomyCropRegion.Torso.Apply(AnatomyMeshMerger.Merge(parts, spec.Id));
            if (cropped.Triangles.Count == 0) {
                Debug.LogError($"{LogPrefix}The torso frame has nothing left after the crop; check its source files.");
                return null;
            }

            return cropped;
        }

        /// <summary>
        /// One solid layer of the scan: the files merged into one mesh, painted as asked and cropped to the torso, with the one look
        /// their groups share. Null after logging why when a file has nothing to give.
        /// </summary>
        private static SolidLayer ReadSolidLayer(string id, string[] files, LayerPaint paint, Dictionary<string, MtlMaterial> materials) {
            List<AnatomyMeshData> parts = new List<AnatomyMeshData>();
            if (!AnatomySourceReader.Read(files, parts, materials) || parts.Count == 0) {
                Debug.LogError($"{LogPrefix}Scan layer '{id}' produced no geometry; check its source files.");
                return null;
            }

            MtlMaterial look = AnatomyMaterialLook.Average("scan_" + id, parts, materials);
            for (int i = 0; i < parts.Count; i++) {
                AnatomyMeshMerger.Paint(parts[i], PaintFor(parts[i], paint, materials));
            }

            AnatomyMeshData cropped = AnatomyCropRegion.Torso.Apply(AnatomyMeshMerger.Merge(parts, id));
            if (cropped.Triangles.Count == 0) {
                Debug.LogError($"{LogPrefix}Scan layer '{id}' has nothing left after the torso crop; check its source files.");
                return null;
            }

            return new SolidLayer(id, cropped, look, paint);
        }

        /// <summary>The vertex colour of a piece: its own tint for muscle, white for bone, whose tint is the material's, and the pack's colour brightened for vessels.</summary>
        private static Color PaintFor(AnatomyMeshData part, LayerPaint paint, Dictionary<string, MtlMaterial> materials) {
            MtlMaterial source;
            if (paint == LayerPaint.Ivory || !materials.TryGetValue(part.SourceMaterialName, out source)) {
                return Color.white;
            }

            Color colour = source.DiffuseColor;
            if (paint == LayerPaint.Brightened) {
                float strongest = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
                colour = strongest > 0f ? new Color(colour.r / strongest, colour.g / strongest, colour.b / strongest, 1f) : Color.white;
            }

            colour.a = 1f;
            return PlayerSettings.colorSpace == ColorSpace.Linear ? colour.linear : colour;
        }

        private static void CreateSolidLayer(Transform parent, SolidLayer layer, Vector3 modelCentre) {
            GameObject host = new GameObject(layer.Id);
            host.transform.SetParent(parent, false);
            Vector3 pivot = layer.Data.Bounds.center;
            host.transform.localPosition = pivot - modelCentre;

            Texture2D texture = string.IsNullOrEmpty(layer.Look.DiffuseTexturePath) ? null : AnatomyTextureFactory.EnsureTexture(layer.Look.DiffuseTexturePath);
            Color tint = layer.Paint == LayerPaint.Ivory ? new Color(0.88f, 0.85f, 0.78f, 1f) : Color.white;
            Color surface = layer.Paint == LayerPaint.SourceTint ? layer.Look.DiffuseColor : tint;
            Material material = AnatomySectionMaterials.Ensure(AnatomyPaths.MaterialFolder + "/" + ScanModelId + "/" + layer.Id + ".mat", tint,
                AnatomySectionMaterials.CutFaceOf(surface), layer.Paint == LayerPaint.Ivory ? 0.3f : 0.25f, texture);

            Mesh mesh = AnatomyPrefabParts.EnsureMesh(MeshFolderId, "scan_" + layer.Id, layer.Data, pivot);
            AnatomyPrefabParts.AddRenderer(host, mesh, material);
        }

        /// <summary>The Section material of an organ, with its texture, and the transparent twin the structure still carries.</summary>
        private static void EnsureSectionMaterials(TorsoOrgan organ, out Material solid, out Material ghost) {
            Texture2D texture = string.IsNullOrEmpty(organ.Look.DiffuseTexturePath) ? null : AnatomyTextureFactory.EnsureTexture(organ.Look.DiffuseTexturePath);
            string folder = AnatomyPaths.MaterialFolder + "/" + ScanModelId;
            solid = AnatomySectionMaterials.Ensure(folder + "/" + organ.Spec.Id + ".mat", organ.Look.DiffuseColor,
                AnatomySectionMaterials.CutFaceOf(organ.Look.DiffuseColor), AnatomyMaterialFactory.SmoothnessOf(organ.Look), texture);
            ghost = AnatomyMaterialFactory.EnsureGhostMaterial(folder + "/" + organ.Spec.Id + "_ghost.mat", organ.Look, texture);
        }

        private static void CreateFrame(Transform parent, AnatomyBodyLayerSpec spec, AnatomyMeshData data, Vector3 modelCentre) {
            GameObject host = new GameObject(spec.Id);
            host.transform.SetParent(parent, false);
            Vector3 pivot = data.Bounds.center;
            host.transform.localPosition = pivot - modelCentre;
            Mesh mesh = AnatomyPrefabParts.EnsureMesh(MeshFolderId, "frame", data, pivot);
            AnatomyPrefabParts.AddRenderer(host, mesh, AnatomyGlowMaterials.EnsureLayer(spec));
        }

        /// <summary>The solid material of an organ with its texture, and the transparent twin the highlight turns it to glass with.</summary>
        private static void EnsureLitMaterials(string modelId, TorsoOrgan organ, out Material solid, out Material ghost) {
            Texture2D texture = string.IsNullOrEmpty(organ.Look.DiffuseTexturePath) ? null : AnatomyTextureFactory.EnsureTexture(organ.Look.DiffuseTexturePath);
            string folder = AnatomyPaths.MaterialFolder + "/" + modelId;
            solid = AnatomyMaterialFactory.EnsureMaterial(folder + "/" + organ.Spec.Id + ".mat", organ.Look, texture);
            ghost = AnatomyMaterialFactory.EnsureGhostMaterial(folder + "/" + organ.Spec.Id + "_ghost.mat", organ.Look, texture);
        }

        /// <summary>
        /// One organ as a structure at the place the pack gives it. Its collider, if it is given one, is the convex hull of its own
        /// surface, which is what a grab and a touch need, and a grabbable organ gets a kinematic body and the component that
        /// lets the pen carry it. The scan gives its organs no collider: it finds them from their surfaces (see OrganProbe).
        /// </summary>
        private static AnatomyStructure CreateOrgan(Transform parent, TorsoOrgan organ, Vector3 modelCentre, Material solid,
            Material ghost, Material rim, bool withCollider, bool grabbable) {
            GameObject host = new GameObject(organ.Spec.Id);
            host.transform.SetParent(parent, false);
            Vector3 pivot = organ.Data.Bounds.center;
            host.transform.localPosition = pivot - modelCentre;

            Mesh mesh = AnatomyPrefabParts.EnsureMesh(MeshFolderId, "organ_" + organ.Spec.Id, organ.Data, pivot);
            MeshFilter filter = host.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = solid;

            if (withCollider) {
                MeshCollider collider = host.AddComponent<MeshCollider>();
                collider.convex = true;
                collider.sharedMesh = mesh;
            }

            if (grabbable) {
                Rigidbody body = host.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                Grabbable pickup = host.AddComponent<Grabbable>();
                SerializedObject serialized = new SerializedObject(pickup);
                KmaxRigBuilder.SetBool(serialized, "kinematicHold", true);
                KmaxRigBuilder.SetBool(serialized, "holdAtContactPoint", true);
                KmaxRigBuilder.SetBool(serialized, "clampToComfortVolume", true);
                KmaxRigBuilder.SetFloat(serialized, "grabMargin", GrabMargin);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return AnatomyModelImporter.AddStructureComponent(host, organ.Data, pivot, renderer, ghost, rim);
        }

        /// <summary>
        /// The faint outline of an organ, left where it belongs when the organ is carried away. It shares the organ's mesh and is
        /// hidden until the puzzle shows it.
        /// </summary>
        private static void CreateSlot(Transform parent, TorsoOrgan organ, Vector3 modelCentre, Material slot) {
            GameObject host = new GameObject("slot_" + organ.Spec.Id);
            host.transform.SetParent(parent, false);
            Vector3 pivot = organ.Data.Bounds.center;
            host.transform.localPosition = pivot - modelCentre;
            Mesh mesh = AnatomyPrefabParts.EnsureMesh(MeshFolderId, "organ_" + organ.Spec.Id, organ.Data, pivot);
            MeshRenderer renderer = AnatomyPrefabParts.AddRenderer(host, mesh, slot);
            renderer.enabled = false;
        }

        /// <summary>How a solid layer's vertices are coloured.</summary>
        private enum LayerPaint {
            /// <summary>Each group in the colour the pack gives it, which the atlas is meant to be multiplied by: the muscles.</summary>
            SourceTint,

            /// <summary>White, the material carrying one colour: the bones.</summary>
            Ivory,

            /// <summary>The pack's colour for each group, brightened until its strongest channel is full: the vessels.</summary>
            Brightened
        }

        /// <summary>One solid layer of the scan: merged, cropped and painted.</summary>
        private class SolidLayer {
            public SolidLayer(string id, AnatomyMeshData data, MtlMaterial look, LayerPaint paint) {
                Id = id;
                Data = data;
                Look = look;
                Paint = paint;
            }

            public string Id { get; private set; }
            public AnatomyMeshData Data { get; private set; }
            public MtlMaterial Look { get; private set; }
            public LayerPaint Paint { get; private set; }
        }

        /// <summary>An organ read from the pack: what it is, its geometry, and the one look its groups share.</summary>
        private class TorsoOrgan {
            public TorsoOrgan(AnatomyTorsoOrganSpec spec, AnatomyMeshData data, MtlMaterial look) {
                Spec = spec;
                Data = data;
                Look = look;
            }

            public AnatomyTorsoOrganSpec Spec { get; private set; }
            public AnatomyMeshData Data { get; private set; }
            public MtlMaterial Look { get; private set; }
        }
    }
}