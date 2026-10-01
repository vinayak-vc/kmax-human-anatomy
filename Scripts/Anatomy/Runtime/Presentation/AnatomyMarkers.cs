using System;

using KmaxXR;

using UnityEngine;

using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Numbered badges around the model, one for each structure the topic describes. A badge stands in a column at
    /// the side, level with its structure, and a thin line in depth joins it to the structure. Pointing at a badge or
    /// pressing it does what pointing at or pressing the structure does, so a structure hidden behind others can
    /// still be reached.
    ///
    /// <para>Badges live on the screen plane with the rest of the interface, where stereo is sharpest and the
    /// pointer has a flat target. Only the lines are in depth. A badge is placed at the height its structure
    /// appears at, allowing for the structure's pop-out, and keeps to its column however the model is turned, so
    /// numbers do not jump from side to side.</para>
    ///
    /// <para>Badges and lines are a fixed pool built with the scene. A topic with more structures than the pool
    /// holds shows the first ones and logs a warning.</para>
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class AnatomyMarkers : MonoBehaviour {
        private const float LineWidth = 0.0007f;
        private const float IdleLineAlpha = 0.4f;
        private const float HoverLineAlpha = 0.9f;
        private const float RecededLineAlpha = 0.14f;
        private const int BezierSegments = 24;

        private static readonly Color LineColor = new Color(0.55f, 0.9f, 1f, 1f);
        private readonly Vector3[] _curveBuffer = new Vector3[BezierSegments + 1];

        [SerializeField, Tooltip("Area of the interface the badges are placed in. Its middle is the middle of the screen.")]
        private RectTransform layer;
        [SerializeField, Tooltip("The pool of badges, in number order.")]
        private AnatomyMarker[] badges = new AnatomyMarker[0];
        [SerializeField, Tooltip("The pool of lines, one for each badge.")]
        private LineRenderer[] lines = new LineRenderer[0];
        [SerializeField, Tooltip("Canvas units from the middle of the screen to each column of badges.")]
        private float columnOffset = 420f;
        [SerializeField, Tooltip("Lowest a badge may stand, in canvas units from the middle of the screen.")]
        private float lowest = -240f;
        [SerializeField, Tooltip("Highest a badge may stand, in canvas units from the middle of the screen.")]
        private float highest = 300f;
        [SerializeField, Tooltip("Least distance between two badges in a column, in canvas units.")]
        private float spacing = 78f;
        [SerializeField, Tooltip("Seconds a badge takes to follow its structure.")]
        private float smoothTime = 0.12f;

        private AnatomyStructure[] _structures;
        private Vector3[] _anchors;
        private float[] _anchorHeights;
        private float[] _slotHeights;
        private float[] _slotVelocities;
        private float[] _positions;
        private bool[] _onRight;
        private int[] _order;
        private float[] _spread;
        private int _count;
        private bool _snap;

        /// <summary>Raised with the structure id when the pointer arrives on a badge.</summary>
        public event Action<string> Entered;

        /// <summary>Raised with the structure id when the pointer leaves a badge.</summary>
        public event Action<string> Exited;

        /// <summary>Raised with the structure id when a badge is pressed and released.</summary>
        public event Action<string> Clicked;

        private void Awake() {
            if (layer == null || badges.Length == 0 || lines.Length != badges.Length) {
                Debug.LogError($"{nameof(AnatomyMarkers)} on '{name}' needs a layer and the same number of badges and lines; " +
                    "the markers are disabled.", this);
                enabled = false;
                return;
            }

            int capacity = badges.Length;
            _structures = new AnatomyStructure[capacity];
            _anchors = new Vector3[capacity];
            _anchorHeights = new float[capacity];
            _slotHeights = new float[capacity];
            _slotVelocities = new float[capacity];
            _positions = new float[capacity];
            _onRight = new bool[capacity];
            _order = new int[capacity];
            _spread = new float[capacity];

            for (int i = 0; i < capacity; i++) {
                badges[i].Entered += OnBadgeEntered;
                badges[i].Exited += OnBadgeExited;
                badges[i].Clicked += OnBadgeClicked;
                badges[i].Clear();
                lines[i].positionCount = BezierSegments + 1;
                lines[i].enabled = false;
            }

            enabled = false;
        }

        private void OnDestroy() {
            if (_structures == null) {
                return;
            }

            for (int i = 0; i < badges.Length; i++) {
                if (badges[i] != null) {
                    badges[i].Entered -= OnBadgeEntered;
                    badges[i].Exited -= OnBadgeExited;
                    badges[i].Clicked -= OnBadgeClicked;
                }
            }
        }

        /// <summary>
        /// Numbers the structures in the order given, from 1, and starts placing badges for them. An entry that is
        /// null still uses up its number but shows nothing.
        /// </summary>
        public void Bind(AnatomyStructure[] ordered) {
            if (_structures == null) {
                return;
            }

            Unbind();
            if (ordered.Length > badges.Length) {
                Debug.LogWarning($"[Anatomy] The topic has {ordered.Length} structures but there are only {badges.Length} " +
                    "markers; the rest are not numbered.", this);
            }

            _count = Mathf.Min(ordered.Length, badges.Length);
            for (int i = 0; i < _count; i++) {
                _structures[i] = ordered[i];
                _positions[i] = float.MaxValue;
                if (ordered[i] == null) {
                    continue;
                }

                badges[i].Assign(ordered[i].StructureId, i + 1);
                lines[i].enabled = true;
                _positions[i] = AcrossScreen(ordered[i].transform.TransformPoint(ordered[i].LabelAnchor));
            }

            MarkerLayout.SplitByPosition(_positions, _count, _onRight);
            _snap = true;
            enabled = true;
        }

        /// <summary>
        /// How far across the screen a point is, from the viewer's left. The opening view decides it, so a topic
        /// that opens turned to one side puts the badges of its near end on one side and its far end on the other.
        /// </summary>
        private static float AcrossScreen(Vector3 worldPoint) {
            return StereoVolume.IsReady ? StereoVolume.ToVolumeSpace(worldPoint).x : worldPoint.x;
        }

        /// <summary>Hides every badge and line.</summary>
        public void Unbind() {
            if (_structures == null) {
                return;
            }

            for (int i = 0; i < badges.Length; i++) {
                _structures[i] = null;
                badges[i].Clear();
                lines[i].enabled = false;
            }

            _count = 0;
            enabled = false;
        }

        /// <summary>Restyles each badge and its line for how its structure is being shown.</summary>
        public void Refresh(AnatomyExplorer explorer) {
            for (int i = 0; i < _count; i++) {
                if (_structures[i] == null) {
                    continue;
                }

                HighlightState state = explorer.StateOf(_structures[i].StructureId);
                badges[i].SetLook(state);
                StyleLine(lines[i], state);
            }
        }

        private void LateUpdate() {
            ComputeAnchors();
            PlaceColumn(false);
            PlaceColumn(true);
            DrawLines();
            _snap = false;
        }

        /// <summary>
        /// Where each structure's anchor is in the world, and how high it appears on the screen plane. A point that
        /// pops out is magnified about the middle of the screen as seen from the viewer, so its height is scaled by
        /// the same factor.
        /// </summary>
        private void ComputeAnchors() {
            float unit = Mathf.Abs(layer.lossyScale.x);
            if (unit <= 0f) {
                return;
            }

            float eye = StereoCamera.DefaultDistance / unit;
            for (int i = 0; i < _count; i++) {
                if (_structures[i] == null) {
                    continue;
                }

                Vector3 world = _structures[i].transform.TransformPoint(_structures[i].LabelAnchor);
                _anchors[i] = world;
                Vector3 local = layer.InverseTransformPoint(world);
                _anchorHeights[i] = local.y * eye / Mathf.Max(1f, eye + local.z);
            }
        }

        private void PlaceColumn(bool right) {
            int inColumn = 0;
            for (int i = 0; i < _count; i++) {
                if (_structures[i] == null || _onRight[i] != right) {
                    continue;
                }

                int at = inColumn;
                while (at > 0 && _anchorHeights[_order[at - 1]] < _anchorHeights[i]) {
                    _order[at] = _order[at - 1];
                    at--;
                }

                _order[at] = i;
                inColumn++;
            }

            for (int k = 0; k < inColumn; k++) {
                _spread[k] = _anchorHeights[_order[k]];
            }

            MarkerLayout.Spread(_spread, inColumn, spacing, lowest, highest);

            float x = right ? columnOffset : -columnOffset;
            for (int k = 0; k < inColumn; k++) {
                int index = _order[k];
                if (_snap) {
                    _slotHeights[index] = _spread[k];
                    _slotVelocities[index] = 0f;
                } else {
                    _slotHeights[index] = Mathf.SmoothDamp(_slotHeights[index], _spread[k], ref _slotVelocities[index], smoothTime);
                }

                badges[index].Slot.anchoredPosition = new Vector2(x, _slotHeights[index]);
            }
        }

        private void DrawLines() {
            for (int i = 0; i < _count; i++) {
                if (_structures[i] == null) {
                    continue;
                }

                float x = _onRight[i] ? columnOffset : -columnOffset;
                Vector3 p0 = layer.TransformPoint(new Vector3(x, _slotHeights[i], 0f));
                Vector3 p3 = _anchors[i];
                Vector3 inward = _onRight[i] ? -layer.right : layer.right;
                float dist = Vector3.Distance(p0, p3);
                float handleLength = Mathf.Clamp(dist * 0.45f, 0.015f, 0.18f);
                Vector3 p1 = p0 + inward * handleLength;
                Vector3 p2 = Vector3.Lerp(p1, p3, 0.65f);

                for (int s = 0; s <= BezierSegments; s++) {
                    float t = (float)s / BezierSegments;
                    _curveBuffer[s] = EvaluateCubicBezier(p0, p1, p2, p3, t);
                }

                lines[i].SetPositions(_curveBuffer);
            }
        }

        private static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
        }

        private static void StyleLine(LineRenderer line, HighlightState state) {
            float alpha;
            float width;
            switch (state) {
                case HighlightState.Focused:
                    alpha = 1f;
                    width = 1.6f;
                    break;
                case HighlightState.Hovered:
                case HighlightState.Previewed:
                    alpha = HoverLineAlpha;
                    width = 1.3f;
                    break;
                case HighlightState.Dimmed:
                    alpha = RecededLineAlpha;
                    width = 0.8f;
                    break;
                default:
                    alpha = IdleLineAlpha;
                    width = 1f;
                    break;
            }

            Color color = LineColor;
            color.a = alpha;
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = LineWidth * width;
        }

        private void OnBadgeEntered(string structureId) {
            if (Entered != null) {
                Entered(structureId);
            }
        }

        private void OnBadgeExited(string structureId) {
            if (Exited != null) {
                Exited(structureId);
            }
        }

        private void OnBadgeClicked(string structureId) {
            if (Clicked != null) {
                Clicked(structureId);
            }
        }
    }
}