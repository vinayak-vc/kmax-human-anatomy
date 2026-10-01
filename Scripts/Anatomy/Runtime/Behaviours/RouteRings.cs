using UnityEngine;
using UnityEngine.Rendering;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// A few glowing rings that travel along a route, for showing something on its way through the body: a sound wave
    /// going into the ear, a breath going down the windpipe. A ring is a line drawn round the route at the width of what
    /// travels there, fading in as it leaves the start and out as it arrives.
    ///
    /// <para>The rings hang from the model, so they turn and zoom with it. A line's thickness is in world units, so
    /// it stays the same on screen however far the model is zoomed.</para>
    /// </summary>
    public class RouteRings {
        private const int Segments = 48;
        private const float FadeInShare = 0.14f;
        private const float FadeOutShare = 0.12f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly LineRenderer[] _lines;
        private readonly Vector3[] _points;
        private readonly float[] _radii;
        private readonly float[] _distances;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly Color _tint;

        /// <param name="parent">What the rings hang from: the model, so they share its space.</param>
        /// <param name="material">A flat, see-through line material.</param>
        /// <param name="route">The route, from where the rings start to where they arrive.</param>
        /// <param name="count">How many rings can be on the route at once.</param>
        /// <param name="lineWidth">Thickness of a ring's line, in world units.</param>
        /// <param name="tint">Colour of a ring at full strength.</param>
        public RouteRings(Transform parent, Material material, AnatomyRoute route, int count, float lineWidth, Color tint) {
            _tint = tint;
            _points = new Vector3[route.Points.Count];
            _radii = new float[route.Points.Count];
            _distances = new float[route.Points.Count];
            for (int i = 0; i < _points.Length; i++) {
                _points[i] = route.Points[i];
                _radii[i] = route.Radii[i];
                if (i > 0) {
                    _distances[i] = _distances[i - 1] + Vector3.Distance(_points[i - 1], _points[i]);
                }
            }

            GameObject root = new GameObject("Route rings");
            root.transform.SetParent(parent, false);
            _lines = new LineRenderer[count];
            for (int i = 0; i < count; i++) {
                _lines[i] = CreateRing(root.transform, i, material, lineWidth);
            }
        }

        /// <summary>
        /// Puts a ring that far along the route, from 0 at the start to 1 at the end. Outside that range the ring is
        /// hidden.
        /// </summary>
        public void Place(int index, float progress) {
            LineRenderer line = _lines[index];
            if (progress <= 0f || progress >= 1f) {
                line.enabled = false;
                return;
            }

            float distance = progress * _distances[_distances.Length - 1];
            int end = 1;
            while (end < _distances.Length - 1 && _distances[end] < distance) {
                end++;
            }

            float span = _distances[end] - _distances[end - 1];
            float along = span > 0f ? (distance - _distances[end - 1]) / span : 0f;
            Vector3 direction = _points[end] - _points[end - 1];
            Transform ring = line.transform;
            ring.localPosition = Vector3.Lerp(_points[end - 1], _points[end], along);
            if (direction.sqrMagnitude > Mathf.Epsilon) {
                ring.localRotation = Quaternion.LookRotation(direction);
            }

            ring.localScale = Vector3.one * Mathf.Lerp(_radii[end - 1], _radii[end], along);

            float fade = Mathf.SmoothStep(0f, 1f, progress / FadeInShare)
                * Mathf.SmoothStep(0f, 1f, (1f - progress) / FadeOutShare);
            Color colour = _tint;
            colour.a *= fade;
            line.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, colour);
            line.SetPropertyBlock(_block);
            line.enabled = true;
        }

        private static LineRenderer CreateRing(Transform parent, int index, Material material, float lineWidth) {
            GameObject host = new GameObject("Ring " + (index + 1));
            host.transform.SetParent(parent, false);
            LineRenderer line = host.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.alignment = LineAlignment.View;
            line.widthMultiplier = lineWidth;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = material;
            line.positionCount = Segments;
            for (int i = 0; i < Segments; i++) {
                float angle = 2f * Mathf.PI * i / Segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }

            line.enabled = false;
            return line;
        }
    }
}