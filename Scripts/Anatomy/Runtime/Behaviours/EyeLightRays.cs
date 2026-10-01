using UnityEngine;
using UnityEngine.Rendering;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// A few rays of light, drawn from an object in front of the eye, in through the cornea and the lens and on to a
    /// single point on the retina. They show what the eye does with light: bend it, and bring it to a focus.
    ///
    /// <para>One ray runs down the middle and the others are spread round a ring, the same way in front of the cornea
    /// and behind the lens, so the picture is the same from any side. The lines are in world space, redrawn every
    /// frame, because the eye turns.</para>
    /// </summary>
    public class EyeLightRays {
        private const int RingRays = 6;
        private const int RayCount = RingRays + 1;
        private const int PointsPerRay = 4;
        private const int BeadPoints = 24;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly LineRenderer[] _lines;
        private readonly LineRenderer _bead;
        private readonly Vector3[] _points = new Vector3[PointsPerRay];
        private readonly Vector3[] _beadPoints = new Vector3[BeadPoints];
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly Color _tint;

        /// <param name="parent">What the lines hang from. Their points are in world space, so it only tidies the hierarchy.</param>
        /// <param name="material">A flat, see-through line material.</param>
        /// <param name="lineWidth">Thickness of a ray, in world units.</param>
        /// <param name="tint">Colour of a ray at full strength.</param>
        public EyeLightRays(Transform parent, Material material, float lineWidth, Color tint) {
            _tint = tint;
            GameObject root = new GameObject("Light rays");
            root.transform.SetParent(parent, false);
            _lines = new LineRenderer[RayCount];
            for (int i = 0; i < RayCount; i++) {
                _lines[i] = CreateLine(root.transform, i, material, lineWidth);
            }

            _bead = CreateLine(root.transform, RayCount, material, lineWidth * 1.5f);
            _bead.loop = true;
            _bead.positionCount = BeadPoints;
        }

        /// <summary>Hides every ray.</summary>
        public void Hide() {
            for (int i = 0; i < _lines.Length; i++) {
                _lines[i].enabled = false;
            }

            _bead.enabled = false;
        }

        /// <summary>
        /// Draws the rays, all points in world space. <paramref name="right"/> and <paramref name="up"/> span the plane
        /// across the eye's axis that the ring of rays is spread in.
        /// </summary>
        /// <param name="source">The object the light comes from, on the eye's axis.</param>
        /// <param name="corneaFront">Where the axis enters the cornea.</param>
        /// <param name="lensBack">Where the axis leaves the lens.</param>
        /// <param name="focus">The point on the retina the rays meet at.</param>
        /// <param name="corneaRadius">Half the width of the ring of rays at the cornea.</param>
        /// <param name="lensRadius">Half the width of the ring of rays at the lens.</param>
        /// <param name="beadRadius">Radius of the small ring drawn round the source, so the light seems to come from something.</param>
        /// <param name="strength">From 0, hidden, to 1, full.</param>
        public void Draw(Vector3 source, Vector3 corneaFront, Vector3 lensBack, Vector3 focus, Vector3 right, Vector3 up,
            float corneaRadius, float lensRadius, float beadRadius, float strength) {
            if (strength <= 0.01f) {
                Hide();
                return;
            }

            Color colour = _tint;
            colour.a *= strength;
            for (int i = 0; i < RayCount; i++) {
                Vector3 across = Vector3.zero;
                if (i > 0) {
                    float angle = 2f * Mathf.PI * (i - 1) / RingRays;
                    across = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                }

                _points[0] = source;
                _points[1] = corneaFront + across * corneaRadius;
                _points[2] = lensBack + across * lensRadius;
                _points[3] = focus;
                _lines[i].SetPositions(_points);
                _lines[i].GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, colour);
                _lines[i].SetPropertyBlock(_block);
                _lines[i].enabled = true;
            }

            for (int i = 0; i < BeadPoints; i++) {
                float angle = 2f * Mathf.PI * i / BeadPoints;
                _beadPoints[i] = source + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * beadRadius;
            }

            _bead.SetPositions(_beadPoints);
            _bead.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, colour);
            _bead.SetPropertyBlock(_block);
            _bead.enabled = true;
        }

        private static LineRenderer CreateLine(Transform parent, int index, Material material, float lineWidth) {
            GameObject host = new GameObject("Ray " + (index + 1));
            host.transform.SetParent(parent, false);
            LineRenderer line = host.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.widthMultiplier = lineWidth;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = material;
            line.positionCount = PointsPerRay;
            line.enabled = false;
            return line;
        }
    }
}