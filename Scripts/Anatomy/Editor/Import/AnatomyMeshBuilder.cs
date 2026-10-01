using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Converts a parsed material group into Unity geometry.
    ///
    /// <para><b>Axes.</b> The DOSCH files are right-handed and Z-up, with the patient facing -Y and their
    /// left on +X, measured from the vertex data. Unity is left-handed and Y-up, so
    /// (x, y, z) becomes (x, z, y): up stays up, the patient's left stays on the viewer's right, and the
    /// patient faces -Z, towards a viewer looking down +Z. Swapping two axes mirrors the coordinates, which
    /// is exactly what a change of handedness needs, and it reverses the triangle winding.</para>
    ///
    /// <para>The winding is checked against the file's own vertex normals rather than trusted, and turned
    /// back if they disagree.</para>
    /// </summary>
    public static class AnatomyMeshBuilder {
        private const int MaxSampledTriangles = 4000;
        private const int SixteenBitVertexLimit = 65535;
        private const float MinimumAgreement = 0.5f;

        /// <summary>Builds the structure's geometry in Unity axes at the source's own scale (metres).</summary>
        public static AnatomyMeshData Build(ObjModel model, ObjMaterialGroup group) {
            AnatomyMeshData data = new AnatomyMeshData(group.MaterialName);
            Dictionary<ObjCorner, int> remap = new Dictionary<ObjCorner, int>(group.Corners.Count / 2);
            bool hasNormals = true;
            bool hasUvs = true;

            for (int i = 0; i < group.Corners.Count; i++) {
                ObjCorner corner = group.Corners[i];
                int index;
                if (!remap.TryGetValue(corner, out index)) {
                    index = data.Vertices.Count;
                    remap.Add(corner, index);
                    data.Vertices.Add(ToUnityAxes(model.Positions[corner.Position]));

                    if (corner.Normal >= 0) {
                        data.Normals.Add(ToUnityAxes(model.Normals[corner.Normal]));
                    } else {
                        data.Normals.Add(Vector3.zero);
                        hasNormals = false;
                    }

                    if (corner.Uv >= 0) {
                        data.Uvs.Add(model.Uvs[corner.Uv]);
                    } else {
                        data.Uvs.Add(Vector2.zero);
                        hasUvs = false;
                    }
                }

                data.Triangles.Add(index);
            }

            if (data.Vertices.Count == 0) {
                return data;
            }

            if (!hasNormals) {
                data.Normals.Clear();
            }

            if (!hasUvs) {
                data.Uvs.Clear();
            }

            ReverseWinding(data.Triangles);
            if (data.HasNormals && AgreementWithNormals(data) < MinimumAgreement) {
                ReverseWinding(data.Triangles);
                Debug.LogWarning($"[Anatomy] '{data.StructureId}': triangle winding disagreed with the file's " +
                    "normals after the axis change, so it was kept as declared.");
            }

            data.Bounds = ComputeBounds(data.Vertices);
            return data;
        }

        /// <summary>
        /// Creates or refills a mesh with this geometry, moved so <paramref name="pivot"/> becomes the origin.
        /// Passing an existing mesh refills it in place, which keeps its asset GUID.
        /// </summary>
        public static Mesh ToMesh(AnatomyMeshData data, Vector3 pivot, Mesh target) {
            Mesh mesh = target != null ? target : new Mesh();
            mesh.Clear();
            mesh.name = data.StructureId;
            mesh.indexFormat = data.Vertices.Count > SixteenBitVertexLimit ? IndexFormat.UInt32 : IndexFormat.UInt16;

            Vector3[] vertices = new Vector3[data.Vertices.Count];
            for (int i = 0; i < vertices.Length; i++) {
                vertices[i] = data.Vertices[i] - pivot;
            }

            mesh.vertices = vertices;
            if (data.HasNormals) {
                mesh.SetNormals(data.Normals);
            }

            if (data.HasUvs) {
                mesh.SetUVs(0, data.Uvs);
            }

            if (data.HasColors) {
                mesh.SetColors(data.Colors);
            }

            mesh.SetTriangles(data.Triangles, 0);
            if (!data.HasNormals) {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 ToUnityAxes(Vector3 source) {
            return new Vector3(source.x, source.z, source.y);
        }

        private static void ReverseWinding(List<int> triangles) {
            for (int i = 0; i + 2 < triangles.Count; i += 3) {
                int second = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = second;
            }
        }

        /// <summary>
        /// The share of sampled triangles whose geometric normal points the same way as the file's vertex
        /// normals. Unity treats clockwise triangles as front-facing, and for those Cross(b - a, c - a)
        /// is the outward normal.
        /// </summary>
        private static float AgreementWithNormals(AnatomyMeshData data) {
            int triangleCount = data.Triangles.Count / 3;
            int step = Mathf.Max(1, triangleCount / MaxSampledTriangles);
            int agreeing = 0;
            int sampled = 0;

            for (int t = 0; t < triangleCount; t += step) {
                int a = data.Triangles[t * 3];
                int b = data.Triangles[t * 3 + 1];
                int c = data.Triangles[t * 3 + 2];
                Vector3 geometric = Vector3.Cross(data.Vertices[b] - data.Vertices[a], data.Vertices[c] - data.Vertices[a]);
                Vector3 declared = data.Normals[a] + data.Normals[b] + data.Normals[c];
                if (Vector3.Dot(geometric, declared) > 0f) {
                    agreeing++;
                }

                sampled++;
            }

            return sampled == 0 ? 1f : agreeing / (float)sampled;
        }

        private static Bounds ComputeBounds(List<Vector3> vertices) {
            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Count; i++) {
                bounds.Encapsulate(vertices[i]);
            }

            return bounds;
        }
    }
}