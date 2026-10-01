using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Where a vertex at this point in space is carried to by a motion, minus where it was. A field depends on the point
    /// and on nothing else, so surfaces that touch keep touching however far they are moved.
    /// </summary>
    public delegate Vector3 DisplacementField(Vector3 point);

    /// <summary>
    /// Bakes a displacement field into a mesh as one blend shape, and stores how the surface normals turn, worked out
    /// from the field's own derivative, so the lighting stays right while the shape plays. Shared by every motion the
    /// importer can bake.
    /// </summary>
    public static class AnatomyFieldShapes {
        public const float FullWeight = 100f;

        /// <summary>Distance, in metres, over which a field is sampled to find its derivative.</summary>
        private const float SampleStep = 0.0004f;

        /// <summary>A shape that moves nothing further than this is not worth storing.</summary>
        private const float MinimumMovement = 0.00005f;

        /// <summary>The structure's vertices, in the model's space: the space a field is written in.</summary>
        public static Vector3[] PositionsOf(AnatomyMeshData part, Vector3 modelCentre) {
            Vector3[] positions = new Vector3[part.Vertices.Count];
            for (int i = 0; i < positions.Length; i++) {
                positions[i] = part.Vertices[i] - modelCentre;
            }

            return positions;
        }

        public static Vector3[] NormalsOf(AnatomyMeshData part) {
            Vector3[] normals = new Vector3[part.Vertices.Count];
            for (int i = 0; i < normals.Length; i++) {
                normals[i] = part.HasNormals ? part.Normals[i] : Vector3.up;
            }

            return normals;
        }

        /// <summary>Adds the field to the mesh as a shape and returns 1, or returns 0 when the field hardly moves this structure.</summary>
        public static int AddFieldShape(Mesh mesh, string shapeName, DisplacementField field, Vector3[] positions, Vector3[] normals) {
            Vector3[] deltaVertices = new Vector3[positions.Length];
            Vector3[] deltaNormals = new Vector3[positions.Length];
            float largest = 0f;
            for (int i = 0; i < positions.Length; i++) {
                deltaVertices[i] = field(positions[i]);
                largest = Mathf.Max(largest, deltaVertices[i].magnitude);
                deltaNormals[i] = TurnedNormal(field, positions[i], normals[i]) - normals[i];
            }

            if (largest < MinimumMovement) {
                return 0;
            }

            mesh.AddBlendShapeFrame(shapeName, FullWeight, deltaVertices, deltaNormals, null);
            Debug.Log($"[Anatomy]   {mesh.name}: '{shapeName}' moves up to {largest * 1000f:F1} mm.");
            return 1;
        }

        /// <summary>
        /// A normal carried through the field. Normals turn by the inverse transpose of the field's derivative, which
        /// is found by sampling the field a little either side of the point.
        /// </summary>
        public static Vector3 TurnedNormal(DisplacementField field, Vector3 point, Vector3 normal) {
            Matrix4x4 jacobian = Matrix4x4.identity;
            for (int column = 0; column < 3; column++) {
                Vector3 step = Vector3.zero;
                step[column] = SampleStep;
                Vector3 slope = (field(point + step) - field(point - step)) / (2f * SampleStep);
                for (int row = 0; row < 3; row++) {
                    jacobian[row, column] += slope[row];
                }
            }

            return jacobian.inverse.transpose.MultiplyVector(normal).normalized;
        }
    }
}