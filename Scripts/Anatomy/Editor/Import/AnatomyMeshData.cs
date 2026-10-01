using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// The geometry of one structure in Unity's axes, before it is centred and turned into a mesh.
    /// Normals, UVs and colours are either empty or exactly as long as the vertex list.
    /// </summary>
    public class AnatomyMeshData {
        /// <summary>How much further than the closest vertex still counts as close, as a squared distance ratio.</summary>
        private const float AnchorTolerance = 1.8f;

        public AnatomyMeshData(string sourceMaterialName) {
            SourceMaterialName = sourceMaterialName;
            StructureId = sourceMaterialName;
            Vertices = new List<Vector3>();
            Normals = new List<Vector3>();
            Uvs = new List<Vector2>();
            Colors = new List<Color>();
            Triangles = new List<int>();
        }

        /// <summary>The material name in the source file, which the material library is keyed by.</summary>
        public string SourceMaterialName { get; private set; }

        /// <summary>Unique within a model. Equals the source material name unless two files collided.</summary>
        public string StructureId { get; set; }

        public List<Vector3> Vertices { get; private set; }
        public List<Vector3> Normals { get; private set; }
        public List<Vector2> Uvs { get; private set; }

        /// <summary>Vertex colours. Only the body map's layers use them: the colour is the tint and the alpha is the crop's fade.</summary>
        public List<Color> Colors { get; private set; }

        public List<int> Triangles { get; private set; }
        public Bounds Bounds { get; set; }

        public bool HasNormals {
            get { return Normals.Count > 0 && Normals.Count == Vertices.Count; }
        }

        public bool HasUvs {
            get { return Uvs.Count > 0 && Uvs.Count == Vertices.Count; }
        }

        public bool HasColors {
            get { return Colors.Count > 0 && Colors.Count == Vertices.Count; }
        }

        /// <summary>
        /// Where a marker's leader line should end, in the same space as <see cref="Vertices"/>: a vertex near
        /// the middle of the structure, so the line points at its body and not at the air between the branches
        /// of a vessel tree. Of the vertices nearly as close as the closest, the one facing the viewer is used,
        /// because the patient faces -Z and the opening view looks down +Z.
        /// </summary>
        public Vector3 FindLabelAnchor() {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < Vertices.Count; i++) {
                sum += Vertices[i];
            }

            Vector3 centre = sum / Vertices.Count;
            float nearest = float.MaxValue;
            for (int i = 0; i < Vertices.Count; i++) {
                nearest = Mathf.Min(nearest, (Vertices[i] - centre).sqrMagnitude);
            }

            float limit = nearest * AnchorTolerance;
            Vector3 anchor = Vertices[0];
            float frontmost = float.MaxValue;
            for (int i = 0; i < Vertices.Count; i++) {
                bool close = (Vertices[i] - centre).sqrMagnitude <= limit;
                if (close && Vertices[i].z < frontmost) {
                    frontmost = Vertices[i].z;
                    anchor = Vertices[i];
                }
            }

            return anchor;
        }
    }
}