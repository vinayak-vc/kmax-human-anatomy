using System;
using System.Collections.Generic;

using UnityEngine;

using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Magnifies the model. Moving the camera closer cannot do it: it only pushes the model further out of the glass,
    /// and the depth keeper would push it straight back. So the model itself is scaled, about whatever is in focus.
    ///
    /// <para>The zoom buttons, the wheel and the stylus's push and pull all end here. The wheel and the pen arrive as
    /// dolly input from <see cref="ViewerFlyController"/>, which is set not to move the camera for them.</para>
    ///
    /// <para>With nothing in focus the model is scaled about its own middle. With structures in focus, the more the
    /// view is zoomed the more their middle is drawn to the middle of the screen, so zooming in on them never
    /// sends them off the edge. At the opening size nothing moves. The focus follows the structures' live
    /// positions, so it keeps up when they are pulled apart.</para>
    /// </summary>
    public class AnatomyZoom : MonoBehaviour {
        private const float Settled = 0.0005f;

        [SerializeField, Tooltip("Where wheel and stylus dolly input comes from. Zoom buttons work without it.")]
        private ViewerFlyController viewer;
        [SerializeField, Tooltip("Multiple applied to the zoom by one press of a zoom button.")]
        private float buttonStep = 1.4f;
        [SerializeField, Tooltip("Natural-log units of zoom for each metre of dolly input. A wheel notch is about 0.05 m.")]
        private float dollySensitivity = 3f;
        [SerializeField, Tooltip("Seconds the zoom takes to settle.")]
        private float zoomSmoothTime = 0.16f;
        [SerializeField, Tooltip("Seconds the view takes to move to a new focus.")]
        private float focusSmoothTime = 0.3f;
        [SerializeField, Tooltip("Zoom at which the focused structure is fully in the middle of the screen.")]
        private float fullFocusZoom = 1.6f;

        private const float BudgetFit = 0.97f;

        private readonly List<Transform> _focusTargets = new List<Transform>();
        private ComfortDepthKeeper _keeper;
        private Transform _content;
        private float _baseScale = 1f;
        private float _maxZoom = 1f;
        private float _zoom = 1f;
        private float _zoomTarget = 1f;
        private float _zoomVelocity;
        private Vector3 _focus;
        private Vector3 _focusTarget;
        private Vector3 _focusVelocity;

        /// <summary>Raised when the zoom the viewer has asked for changes, before the model has finished moving to it.</summary>
        public event Action Changed;

        /// <summary>The zoom the viewer has asked for, where 1 is the opening size.</summary>
        public float TargetZoom {
            get { return _zoomTarget; }
        }

        public bool CanZoomIn {
            get { return _content != null && _zoomTarget < _maxZoom - Settled; }
        }

        public bool CanZoomOut {
            get { return _content != null && _zoomTarget > 1f + Settled; }
        }

        private void OnEnable() {
            if (viewer != null) {
                viewer.DollyInput += OnDollyInput;
            }
        }

        private void OnDisable() {
            if (viewer != null) {
                viewer.DollyInput -= OnDollyInput;
            }
        }

        /// <summary>Takes charge of a model's scale and position, starting at the opening size.</summary>
        /// <param name="content">The model. Its parent is the orbit centre.</param>
        /// <param name="baseScale">The scale the topic opens at.</param>
        /// <param name="maxZoom">Furthest in, as a multiple of the opening size.</param>
        /// <param name="keeper">
        /// Optional. When given, the zoom backs off wherever the model would be deeper than the depth budget, which a long
        /// model seen end-on can be at a zoom that fits from the side.
        /// </param>
        public void Bind(Transform content, float baseScale, float maxZoom, ComfortDepthKeeper keeper = null) {
            _keeper = keeper;
            _content = content;
            _baseScale = baseScale;
            _maxZoom = Mathf.Max(1f, maxZoom);
            _zoom = 1f;
            _zoomTarget = 1f;
            _zoomVelocity = 0f;
            _focus = Vector3.zero;
            _focusTarget = Vector3.zero;
            _focusVelocity = Vector3.zero;
            _focusTargets.Clear();
            Apply();
            RaiseChanged();
        }

        /// <summary>Lets go of the model, for when a topic is unloaded.</summary>
        public void Unbind() {
            _content = null;
            RaiseChanged();
        }

        public void ZoomIn() {
            SetTarget(_zoomTarget * buttonStep);
        }

        public void ZoomOut() {
            SetTarget(_zoomTarget / buttonStep);
        }

        /// <summary>Zooms to this multiple of the opening size, within the topic's limits.</summary>
        public void ZoomTo(float zoom) {
            SetTarget(zoom);
        }

        /// <summary>
        /// Zooms about the middle of these structures, wherever they hang in the model: their positions are read in the
        /// model's own space. It takes effect as the view is zoomed in.
        /// </summary>
        public void FocusOn(IReadOnlyList<Transform> structures) {
            _focusTargets.Clear();
            for (int i = 0; i < structures.Count; i++) {
                _focusTargets.Add(structures[i]);
            }
        }

        /// <summary>Zooms about the middle of the model again.</summary>
        public void ClearFocus() {
            _focusTargets.Clear();
        }

        /// <summary>Back to the opening size, centred.</summary>
        public void ResetZoom() {
            SetTarget(1f);
            ClearFocus();
        }

        private void Update() {
            if (_content == null) {
                return;
            }

            FindFocusTarget();
            LimitToBudget();
            _zoom = Mathf.SmoothDamp(_zoom, _zoomTarget, ref _zoomVelocity, zoomSmoothTime);
            _focus = Vector3.SmoothDamp(_focus, _focusTarget, ref _focusVelocity, focusSmoothTime);
            Apply();
        }

        /// <summary>
        /// A model deeper than the depth budget cannot be made comfortable by moving it, so the zoom backs off as far as it
        /// must. Depth grows in step with the zoom, so the zoom that fits is the current one scaled by how far over budget
        /// the model is.
        /// </summary>
        private void LimitToBudget() {
            if (_keeper == null || _zoomTarget <= 1f) {
                return;
            }

            float extent = _keeper.Extent;
            float budget = _keeper.Budget;
            if (extent <= budget) {
                return;
            }

            float fitting = Mathf.Max(1f, _zoom * budget * BudgetFit / extent);
            if (_zoomTarget > fitting + Settled) {
                _zoomTarget = fitting;
                RaiseChanged();
            }
        }

        private void FindFocusTarget() {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < _focusTargets.Count; i++) {
                if (_focusTargets[i] != null) {
                    sum += _content.InverseTransformPoint(_focusTargets[i].position);
                    count++;
                }
            }

            _focusTarget = count > 0 ? sum / count : Vector3.zero;
        }

        private void OnDollyInput(float metres) {
            SetTarget(_zoomTarget * Mathf.Exp(metres * dollySensitivity));
        }

        private void SetTarget(float zoom) {
            float clamped = Mathf.Clamp(zoom, 1f, _maxZoom);
            if (Mathf.Abs(clamped - _zoomTarget) < Settled) {
                return;
            }

            _zoomTarget = clamped;
            RaiseChanged();
        }

        private void Apply() {
            if (_content == null) {
                return;
            }

            float scale = _baseScale * _zoom;
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, Mathf.Max(1.01f, fullFocusZoom), _zoom));
            _content.localScale = Vector3.one * scale;
            _content.localPosition = -scale * weight * _focus;
        }

        private void RaiseChanged() {
            if (Changed != null) {
                Changed();
            }
        }
    }
}