using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Cuts the whole body down to the part an activity shows. A whole standing body would have to be shown so small on a
    /// 27" screen that an eye was a speck and an organ a pebble, so the exhibit shows a bust (head, neck and chest) on the body
    /// map and a torso (neck to pelvis) in the two activities, each several times larger than the whole body could be.
    ///
    /// <para>Nothing ends in a hard edge. The layers are see-through glow, so they fade out at the cut, which reads as a
    /// figure dissolving into the dark and not as a sliced model. The fade is written into the alpha of each vertex colour,
    /// and triangles that have faded out completely are dropped, which also leaves each layer a fraction of the
    /// triangles of the whole body.</para>
    ///
    /// <para>Heights are metres in Unity's axes, measured up from the soles of the feet as the DOSCH pack has them. Each
    /// edge has a distance at which it is fully shown and one beyond which nothing is.</para>
    /// </summary>
    public class AnatomyCropRegion {
        private const float VisibleAlpha = 0.04f;
        private const float SolidAlpha = 0.5f;

        private static readonly AnatomyCropRegion BustRegion = new AnatomyCropRegion(1.18f, 1.3f, float.PositiveInfinity,
            float.PositiveInfinity, 0.19f, 0.3f);
        private static readonly AnatomyCropRegion TorsoRegion = new AnatomyCropRegion(0.9f, 0.96f, 1.54f, 1.6f, 0.21f, 0.3f);

        private readonly float _bottomClear;
        private readonly float _bottomFull;
        private readonly float _topFull;
        private readonly float _topClear;
        private readonly float _sideFull;
        private readonly float _sideClear;

        public AnatomyCropRegion(float bottomClear, float bottomFull, float topFull, float topClear, float sideFull, float sideClear) {
            _bottomClear = bottomClear;
            _bottomFull = bottomFull;
            _topFull = topFull;
            _topClear = topClear;
            _sideFull = sideFull;
            _sideClear = sideClear;
        }

        /// <summary>The head, neck and chest down to the lowest ribs, fading towards the arms: the body map's bust.</summary>
        public static AnatomyCropRegion Bust {
            get { return BustRegion; }
        }

        /// <summary>The torso from the base of the neck to the pelvis, fading at the shoulders: the two activities' figure.</summary>
        public static AnatomyCropRegion Torso {
            get { return TorsoRegion; }
        }

        /// <summary>How much of a point of the body is shown, from 0 for gone to 1 for fully shown.</summary>
        public float AlphaAt(Vector3 point) {
            float aboveBottom = Rise(point.y, _bottomClear, _bottomFull);
            float belowTop = float.IsInfinity(_topFull) ? 1f : 1f - Rise(point.y, _topFull, _topClear);
            float besideBody = 1f - Rise(Mathf.Abs(point.x), _sideFull, _sideClear);
            return aboveBottom * belowTop * besideBody;
        }

        /// <summary>
        /// The box the region shows in full, in the pack's own space and as deep as <paramref name="depth"/> metres either side of the
        /// middle of the body. A figure drawn solid, which cannot fade, is cut to this box instead.
        /// </summary>
        public Bounds ShownBox(float depth) {
            float top = float.IsInfinity(_topFull) ? _bottomFull + 1f : _topFull;
            return new Bounds(new Vector3(0f, (_bottomFull + top) * 0.5f, 0f), new Vector3(2f * _sideFull, top - _bottomFull, 2f * depth));
        }

        /// <summary>
        /// A copy of the geometry with the fade multiplied into its vertex colours (white where it had none) and every
        /// triangle that has faded out removed, along with the vertices only they used.
        /// </summary>
        public AnatomyMeshData Apply(AnatomyMeshData source) {
            int vertexCount = source.Vertices.Count;
            float[] fade = new float[vertexCount];
            int[] remap = new int[vertexCount];
            for (int i = 0; i < vertexCount; i++) {
                fade[i] = AlphaAt(source.Vertices[i]);
                remap[i] = -1;
            }

            AnatomyMeshData cropped = new AnatomyMeshData(source.SourceMaterialName);
            cropped.StructureId = source.StructureId;
            for (int t = 0; t + 2 < source.Triangles.Count; t += 3) {
                int a = source.Triangles[t];
                int b = source.Triangles[t + 1];
                int c = source.Triangles[t + 2];
                if (Mathf.Max(fade[a], Mathf.Max(fade[b], fade[c])) < VisibleAlpha) {
                    continue;
                }

                cropped.Triangles.Add(Keep(a, source, fade, remap, cropped));
                cropped.Triangles.Add(Keep(b, source, fade, remap, cropped));
                cropped.Triangles.Add(Keep(c, source, fade, remap, cropped));
            }

            if (cropped.Vertices.Count > 0) {
                cropped.Bounds = AnatomyMeshMerger.BoundsOf(cropped.Vertices);
            }

            return cropped;
        }

        /// <summary>
        /// The box round the part of cropped geometry that is mostly there. A long triangle that reaches into the fade keeps
        /// vertices far beyond the edge, and centring the model on those would hang the figure off-centre on the screen.
        /// </summary>
        public static Bounds SolidBoundsOf(AnatomyMeshData cropped) {
            Bounds bounds = new Bounds(cropped.Bounds.center, Vector3.zero);
            bool started = false;
            for (int i = 0; i < cropped.Vertices.Count; i++) {
                if (cropped.Colors[i].a < SolidAlpha) {
                    continue;
                }

                if (started) {
                    bounds.Encapsulate(cropped.Vertices[i]);
                } else {
                    bounds = new Bounds(cropped.Vertices[i], Vector3.zero);
                    started = true;
                }
            }

            return started ? bounds : cropped.Bounds;
        }

        /// <summary>Eases from 0 at <paramref name="from"/> to 1 at <paramref name="to"/>, the other way round if to is the smaller.</summary>
        private static float Rise(float value, float from, float to) {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
        }

        /// <summary>The index the vertex has in the cropped geometry, which it is added to on first use.</summary>
        private static int Keep(int index, AnatomyMeshData source, float[] fade, int[] remap, AnatomyMeshData cropped) {
            if (remap[index] >= 0) {
                return remap[index];
            }

            remap[index] = cropped.Vertices.Count;
            cropped.Vertices.Add(source.Vertices[index]);
            if (source.HasNormals) {
                cropped.Normals.Add(source.Normals[index]);
            }

            if (source.HasUvs) {
                cropped.Uvs.Add(source.Uvs[index]);
            }

            Color colour = source.HasColors ? source.Colors[index] : Color.white;
            colour.a *= fade[index];
            cropped.Colors.Add(colour);
            return remap[index];
        }
    }
}