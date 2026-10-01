using UnityEngine;

namespace ViitorCloud.KmaxDisplay {
    /// <summary>
    /// Keeps content inside the comfortable depth budget however it is turned, by easing it along the view
    /// axis only as far as it needs to go.
    ///
    /// <para>An object that pops out 90 mm end-on can pop out 150 mm side-on, because turning it swings its
    /// width into depth. Placing it once cannot fix that, and clamping every renderer would tear it apart.
    /// This measures the content's extent in the glass's own space each frame and moves the whole thing
    /// forward or back, if it must, so its near edge stays clear of the pop-out limit.</para>
    ///
    /// <para>Near edge first. Too much pop-out is what hurts; if the content is deeper than the whole budget
    /// it is the near limit that is kept and the far edge is allowed to run over.</para>
    ///
    /// <para>Put this on the object that carries the content, and hand it the content to measure. It moves
    /// its own transform along the rig's forward axis, so it must not also be positioned by anything else.</para>
    ///
    /// <para>What is measured is the mesh itself, not its bounding box. A box around something tilted in depth
    /// has corners far beyond the surface, and the mistake grows with scale: at 2.4 times zoom it pushed a heart
    /// 130 mm deeper than it needed to go. Each mesh is reduced once, when tracking starts, to its outermost
    /// vertex in a few dozen directions, and those are what are carried through the glass's space each frame.
    /// A renderer whose mesh cannot be read falls back to the corners of its box.</para>
    /// </summary>
    public class ComfortDepthKeeper : MonoBehaviour {
        [SerializeField, Tooltip("Metres kept clear of each limit, so content is not sitting exactly on it.")]
        private float margin = 0.015f;
        [SerializeField, Tooltip("Seconds the correction takes to settle. Short enough to keep up with a turn, " +
            "long enough that the model does not visibly pump.")]
        private float smoothTime = 0.15f;

        private static readonly Vector3[] Directions = BuildDirections();

        private Renderer[] _renderers = new Renderer[0];
        private Vector3[][] _extremes = new Vector3[0][];
        private float _shift;
        private float _velocity;
        private float _extent;

        /// <summary>The distance currently applied along the view axis, in metres. Positive is deeper.</summary>
        public float Shift {
            get { return _shift; }
        }

        /// <summary>How deep the content is as it stands, from its nearest edge to its furthest, in metres.</summary>
        public float Extent {
            get { return _extent; }
        }

        /// <summary>
        /// The depth the budget allows with the margins kept clear, in metres. Content deeper than this cannot be made
        /// comfortable by moving it, however far.
        /// </summary>
        public float Budget {
            get { return StereoVolume.IsReady ? StereoVolume.PopOutLimit + StereoVolume.DepthLimit - 2f * margin : float.MaxValue; }
        }

        /// <summary>
        /// Starts keeping this content inside the budget. Pass null to stop and put the content back where it
        /// was placed.
        /// </summary>
        public void Track(Transform content) {
            _renderers = content != null ? MeshRenderersOf(content.GetComponentsInChildren<Renderer>(false)) : new Renderer[0];
            _extremes = new Vector3[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++) {
                _extremes[i] = FindExtremes(_renderers[i]);
            }

            if (content == null) {
                _shift = 0f;
                _velocity = 0f;
                _extent = 0f;
                transform.localPosition = Vector3.zero;
            }
        }

        /// <summary>
        /// Only what is drawn from a mesh is measured. A line or a particle system has no surface to reduce to its outermost
        /// vertices, and its box at the moment tracking starts says nothing about where it will be drawn: a ring built as a
        /// unit circle and sized each frame looked a metre wide.
        /// </summary>
        private static Renderer[] MeshRenderersOf(Renderer[] all) {
            int count = 0;
            for (int i = 0; i < all.Length; i++) {
                if (all[i] is MeshRenderer || all[i] is SkinnedMeshRenderer) {
                    count++;
                }
            }

            Renderer[] meshes = new Renderer[count];
            int next = 0;
            for (int i = 0; i < all.Length; i++) {
                if (all[i] is MeshRenderer || all[i] is SkinnedMeshRenderer) {
                    meshes[next] = all[i];
                    next++;
                }
            }

            return meshes;
        }

        private void LateUpdate() {
            if (_renderers.Length == 0 || !StereoVolume.IsReady) {
                return;
            }

            // Measure the content where it would sit if left alone. The rig turns every frame, so last
            // frame's shift no longer points along the view axis and cannot simply be subtracted.
            transform.localPosition = Vector3.zero;
            Vector3 rest = transform.position;

            float nearest;
            float furthest;
            if (!MeasureDepth(out nearest, out furthest)) {
                return;
            }

            _extent = furthest - nearest;
            float lowest = -(StereoVolume.PopOutLimit - margin) - nearest;
            float highest = (StereoVolume.DepthLimit - margin) - furthest;
            float target = lowest <= highest ? Mathf.Clamp(0f, lowest, highest) : lowest;

            _shift = Mathf.SmoothDamp(_shift, target, ref _velocity, smoothTime);
            transform.position = rest + StereoVolume.ScreenTransform.forward * _shift;
        }

        /// <summary>
        /// Nearest and furthest depth of the content in the glass's space. Each renderer contributes the
        /// corners of its own oriented box, which is tighter than a world-aligned box once the view is turned.
        /// </summary>
        private bool MeasureDepth(out float nearest, out float furthest) {
            nearest = float.MaxValue;
            furthest = float.MinValue;
            for (int i = 0; i < _renderers.Length; i++) {
                Renderer renderer = _renderers[i];
                if (renderer == null) {
                    continue;
                }

                Vector3[] points = _extremes[i];
                for (int p = 0; p < points.Length; p++) {
                    float depth = StereoVolume.DepthOf(renderer.transform.TransformPoint(points[p]));
                    if (depth < nearest) {
                        nearest = depth;
                    }

                    if (depth > furthest) {
                        furthest = depth;
                    }
                }
            }

            return nearest <= furthest;
        }

        /// <summary>
        /// The points, in the renderer's own space, that stand for its shape: its outermost vertex along each of
        /// <see cref="Directions"/>. Falls back to the corners of its box when its mesh cannot be read.
        /// </summary>
        private static Vector3[] FindExtremes(Renderer renderer) {
            Mesh mesh = MeshOf(renderer);
            if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0) {
                return BoxCorners(renderer.localBounds);
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] found = new Vector3[Directions.Length];
            float[] best = new float[Directions.Length];
            for (int d = 0; d < Directions.Length; d++) {
                best[d] = float.MinValue;
            }

            for (int v = 0; v < vertices.Length; v++) {
                for (int d = 0; d < Directions.Length; d++) {
                    float along = Vector3.Dot(vertices[v], Directions[d]);
                    if (along > best[d]) {
                        best[d] = along;
                        found[d] = vertices[v];
                    }
                }
            }

            return found;
        }

        private static Mesh MeshOf(Renderer renderer) {
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null) {
                return skinned.sharedMesh;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private static Vector3[] BoxCorners(Bounds local) {
            Vector3[] corners = new Vector3[8];
            for (int corner = 0; corner < 8; corner++) {
                corners[corner] = local.center + new Vector3(
                    (corner & 1) == 0 ? -local.extents.x : local.extents.x,
                    (corner & 2) == 0 ? -local.extents.y : local.extents.y,
                    (corner & 4) == 0 ? -local.extents.z : local.extents.z);
            }

            return corners;
        }

        /// <summary>The 26 directions from the middle of a cube to its faces, edges and corners.</summary>
        private static Vector3[] BuildDirections() {
            Vector3[] directions = new Vector3[26];
            int count = 0;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    for (int z = -1; z <= 1; z++) {
                        if (x != 0 || y != 0 || z != 0) {
                            directions[count] = new Vector3(x, y, z).normalized;
                            count++;
                        }
                    }
                }
            }

            return directions;
        }
    }
}