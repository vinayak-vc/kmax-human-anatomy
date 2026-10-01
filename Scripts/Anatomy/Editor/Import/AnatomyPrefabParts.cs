using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;

using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// The small pieces every generated prefab is assembled from, which the body map's and the two activities' importers share:
    /// a mesh asset that is refilled in place, and a renderer that is not shadowed and reads no probes.
    /// </summary>
    public static class AnatomyPrefabParts {
        /// <summary>
        /// The mesh asset <c>Generated/Meshes/{folderId}/{meshName}.asset</c>, created if absent and refilled with this geometry
        /// otherwise, so its GUID, and every prefab that refers to it, survives regeneration. The geometry is moved so
        /// <paramref name="pivot"/> is the mesh's origin.
        /// </summary>
        public static Mesh EnsureMesh(string folderId, string meshName, AnatomyMeshData data, Vector3 pivot) {
            string folder = AnatomyPaths.MeshFolder + "/" + folderId;
            KmaxRigBuilder.EnsureFolder(folder);
            string path = folder + "/" + meshName + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            mesh = AnatomyMeshBuilder.ToMesh(data, pivot, mesh);
            if (isNew) {
                AssetDatabase.CreateAsset(mesh, path);
            } else {
                EditorUtility.SetDirty(mesh);
            }

            return mesh;
        }

        /// <summary>A renderer that casts and receives no shadow and reads no probes: shadows shimmer between the eyes on a stereo display.</summary>
        public static MeshRenderer AddRenderer(GameObject host, Mesh mesh, Material material) {
            MeshFilter filter = host.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }
    }
}