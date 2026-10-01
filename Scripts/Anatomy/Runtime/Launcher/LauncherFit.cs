using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Works out how large a model may be shown on the launcher's turntable. It turns about its vertical axis and leans a little
    /// towards the viewer, so as it goes round its horizontal reach sweeps into depth: the room it needs in front of and behind
    /// its middle is its horizontal radius, plus what the lean adds. The arithmetic is plain, so it can be checked on its own;
    /// measuring a model's outline is the one part that reads Unity meshes.
    /// </summary>
    public static class LauncherFit {
        /// <summary>
        /// Adds the points that outline a drawn part to a list, in the space <paramref name="toSpace"/> leads to: every vertex of
        /// its mesh at rest, or the corners of its box when the mesh cannot be read. The vertices are what the eye sees, where a
        /// box round a tilted or branching part has corners far beyond its surface.
        /// </summary>
        public static void AppendOutline(Renderer renderer, Matrix4x4 toSpace, List<Vector3> points) {
            Mesh mesh = null;
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null) {
                mesh = skinned.sharedMesh;
            } else {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                mesh = filter != null ? filter.sharedMesh : null;
            }

            if (mesh != null && mesh.isReadable && mesh.vertexCount > 0) {
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) {
                    points.Add(toSpace.MultiplyPoint3x4(vertices[i]));
                }

                return;
            }

            Bounds local = renderer.localBounds;
            for (int corner = 0; corner < 8; corner++) {
                points.Add(toSpace.MultiplyPoint3x4(local.center + new Vector3(
                    (corner & 1) == 0 ? -local.extents.x : local.extents.x,
                    (corner & 2) == 0 ? -local.extents.y : local.extents.y,
                    (corner & 4) == 0 ? -local.extents.z : local.extents.z)));
            }
        }

        /// <summary>
        /// The largest scale, as a multiple of the model's real size, at which it stays inside every limit however far round it
        /// has turned.
        /// </summary>
        /// <param name="radius">The model's horizontal reach from its middle, in metres at real size.</param>
        /// <param name="halfHeight">Half the model's height, in metres at real size.</param>
        /// <param name="tiltDegrees">How far the turntable leans towards the viewer.</param>
        /// <param name="maxDepthRadius">The most the model may reach in front of or behind its middle, in metres.</param>
        /// <param name="maxHalfHeight">The most the model may reach above or below its middle on the screen, in metres.</param>
        /// <param name="maxHalfWidth">The most the model may reach to either side of its middle, in metres.</param>
        public static float ScaleFor(float radius, float halfHeight, float tiltDegrees, float maxDepthRadius, float maxHalfHeight, float maxHalfWidth) {
            float tilt = tiltDegrees * Mathf.Deg2Rad;
            float depth = radius * Mathf.Cos(tilt) + halfHeight * Mathf.Sin(tilt);
            float height = halfHeight * Mathf.Cos(tilt) + radius * Mathf.Sin(tilt);

            float scale = float.MaxValue;
            scale = Mathf.Min(scale, Ratio(maxDepthRadius, depth));
            scale = Mathf.Min(scale, Ratio(maxHalfHeight, height));
            scale = Mathf.Min(scale, Ratio(maxHalfWidth, radius));
            return scale == float.MaxValue ? 1f : scale;
        }

        private static float Ratio(float limit, float extent) {
            return extent > Mathf.Epsilon ? limit / extent : float.MaxValue;
        }
    }
}