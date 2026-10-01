using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Joins pieces of geometry into one. The body map draws each of its layers as a single mesh, and makes one
    /// structure of each organ however many material groups its source files have.
    /// </summary>
    public static class AnatomyMeshMerger {
        /// <summary>
        /// One piece holding all of these. Normals and UVs are kept only if every piece has them. Colours are kept if any
        /// piece has them, and a piece without any is white.
        /// </summary>
        public static AnatomyMeshData Merge(IReadOnlyList<AnatomyMeshData> parts, string structureId) {
            AnatomyMeshData merged = new AnatomyMeshData(structureId);
            bool keepNormals = parts.Count > 0;
            bool keepUvs = parts.Count > 0;
            bool keepColours = false;
            for (int i = 0; i < parts.Count; i++) {
                keepNormals &= parts[i].HasNormals;
                keepUvs &= parts[i].HasUvs;
                keepColours |= parts[i].HasColors;
            }

            for (int i = 0; i < parts.Count; i++) {
                AnatomyMeshData part = parts[i];
                int offset = merged.Vertices.Count;
                merged.Vertices.AddRange(part.Vertices);
                if (keepNormals) {
                    merged.Normals.AddRange(part.Normals);
                }

                if (keepUvs) {
                    merged.Uvs.AddRange(part.Uvs);
                }

                if (keepColours) {
                    AddColours(merged, part);
                }

                for (int t = 0; t < part.Triangles.Count; t++) {
                    merged.Triangles.Add(part.Triangles[t] + offset);
                }
            }

            if (merged.Vertices.Count > 0) {
                merged.Bounds = BoundsOf(merged.Vertices);
            }

            return merged;
        }

        /// <summary>Gives every vertex of the piece this colour.</summary>
        public static void Paint(AnatomyMeshData part, Color colour) {
            part.Colors.Clear();
            for (int i = 0; i < part.Vertices.Count; i++) {
                part.Colors.Add(colour);
            }
        }

        /// <summary>The smallest box that holds all of these points. There must be at least one.</summary>
        public static Bounds BoundsOf(IReadOnlyList<Vector3> points) {
            Bounds bounds = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Count; i++) {
                bounds.Encapsulate(points[i]);
            }

            return bounds;
        }

        private static void AddColours(AnatomyMeshData into, AnatomyMeshData part) {
            if (part.HasColors) {
                into.Colors.AddRange(part.Colors);
                return;
            }

            for (int i = 0; i < part.Vertices.Count; i++) {
                into.Colors.Add(Color.white);
            }
        }
    }
}