using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ViitorCloud.KmaxDisplay {
    /// <summary>
    /// Gives a UGUI button the same interaction feel the 3D badges have: it swells on hover, compresses and sinks on press
    /// and springs back on release.
    ///
    /// On a stereo display the buttons sit on a world-space canvas at a fixed depth, so there is no
    /// cursor shadow or hover highlight to tell the viewer the pointer has arrived. Scale is the
    /// cue that survives being looked at with two eyes from an angle, and the small sink on a press moves the
    /// button's two images apart a hair, which reads as a button going down.
    ///
    /// <para>Scale, lift and sink are each a damped spring (<see cref="UiSpring"/>), not an ease, so a button settles with a
    /// slight overshoot instead of stopping dead.</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UiButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler {
        private const float MetresPerMillimetre = 0.001f;

        [Header("Scale")]
        [SerializeField, Range(1f, 1.4f), Tooltip("Scale the spring pulls towards while the pointer rests on the button.")]
        private float hoverScale = 1.04f;
        [SerializeField, Range(0.6f, 1f), Tooltip("Scale the spring pulls towards while the button is pressed.")]
        private float pressScale = 0.94f;

        [Header("Spring")]
        [SerializeField, Range(4f, 60f), Tooltip("Undamped angular frequency of the spring, in radians a second. " +
            "Larger is stiffer and snappier.")]
        private float springFrequency = 18f;
        [SerializeField, Range(0.2f, 1.5f), Tooltip("Damping ratio. Below one the button overshoots a little and settles, which " +
            "is the bounce; one or more settles without overshoot.")]
        private float springDamping = 0.7f;

        [Header("Depth")]
        [SerializeField, Tooltip("How far the button sinks into the screen while pressed, in millimetres of the world. " +
            "It is converted to canvas units through the scale of the canvas, so it is the same on any window size.")]
        private float pressDepthMillimetres = 2f;

        [Header("Lift")]
        [SerializeField, Tooltip("Canvas units the button rises while hovered. Zero keeps the swell the only hover cue.")]
        private float hoverLift;

        [Header("Tint")]
        [SerializeField, Tooltip("Graphic tinted on hover. Left empty, no tinting happens.")]
        private Graphic tintTarget;
        [SerializeField, Tooltip("Colour the graphic takes while hovered.")]
        private Color hoverTint = new Color(0.62f, 0.88f, 1f, 1f);
        [SerializeField, Tooltip("Seconds the tint takes to cross-fade.")]
        private float tintBlendTime = 0.1f;

        [Header("Press Flash")]
        [SerializeField, Tooltip("Flare the button on press. The compression alone is easy to miss " +
            "with a wand, where there is no cursor resting on the control to watch.")]
        private bool pressFlash = true;
        [SerializeField, Tooltip("Colour flashed at the instant of the press.")]
        private Color pressFlashTint = new Color(0.90f, 0.97f, 1f, 1f);
        [SerializeField, Tooltip("Seconds the flash takes to fall away.")]
        private float pressFlashDecay = 0.26f;

        [Header("Attention")]
        [SerializeField, Tooltip("Breathe gently when idle. Worth it for the one button that starts " +
            "the experience, distracting on every button at once.")]
        private bool idlePulse;
        [SerializeField, Range(0f, 0.08f), Tooltip("Depth of the idle breathing.")]
        private float idlePulseAmplitude = 0.02f;
        [SerializeField, Range(0.2f, 3f), Tooltip("Speed of the idle breathing.")]
        private float idlePulseSpeed = 1.4f;

        private RectTransform _rectTransform;
        private Selectable _selectable;
        private Vector3 _restScale = Vector3.one;
        private Vector3 _restAnchoredPosition;
        private Color _restTint = Color.white;

        private float _hoverProgress;
        private float _pressProgress;
        private float _flashProgress;
        private float _currentScale = 1f;
        private float _scaleVelocity;
        private float _currentLift;
        private float _liftVelocity;
        private float _currentDepth;
        private float _depthVelocity;
        private bool _isHovered;
        private bool _isPressed;

        /// <summary>
        /// Raised when the pointer moves onto or off the button. True means it moved on.
        /// Lets the controller sound a hover cue without this class knowing about audio.
        /// </summary>
        public event Action<bool> HoverChanged;

        /// <summary>
        /// Changes the colour the tinted graphic rests at, for a button whose resting look changes with its state, such as a
        /// card that has been chosen. The graphic eases to it, and to the hover tint from it, as usual.
        /// </summary>
        public void SetRestTint(Color tint) {
            _restTint = tint;
        }

        private void Awake() {
            _rectTransform = GetComponent<RectTransform>();
            _selectable = GetComponent<Selectable>();
            _restScale = _rectTransform.localScale;
            _restAnchoredPosition = _rectTransform.anchoredPosition3D;

            if (tintTarget != null) {
                _restTint = tintTarget.color;
            }
        }

        /// <summary>
        /// The Back and Next buttons are switched off and on as the flow changes, so the state is
        /// cleared here - otherwise a button hidden mid-hover comes back still swollen.
        /// </summary>
        private void OnEnable() {
            _isHovered = false;
            _isPressed = false;
            _hoverProgress = 0f;
            _pressProgress = 0f;
            _flashProgress = 0f;
            _currentScale = 1f;
            _scaleVelocity = 0f;
            _currentLift = 0f;
            _liftVelocity = 0f;
            _currentDepth = 0f;
            _depthVelocity = 0f;

            if (_rectTransform != null) {
                _rectTransform.localScale = _restScale;
                _rectTransform.anchoredPosition3D = _restAnchoredPosition;
            }

            if (tintTarget != null) {
                tintTarget.color = _restTint;
            }
        }

        private void Update() {
            float dt = Time.unscaledDeltaTime;
            bool interactable = _selectable == null || _selectable.IsInteractable();
            bool hovered = _isHovered && interactable;
            bool pressed = _isPressed && interactable;

            // The progress values only drive the tint and the lift; the spring is what shapes the scale and the sink.
            _hoverProgress = Mathf.MoveTowards(_hoverProgress, hovered ? 1f : 0f, dt * 9f);
            _pressProgress = Mathf.MoveTowards(_pressProgress, pressed ? 1f : 0f, dt * 16f);

            float targetScale = pressed ? pressScale : (hovered ? hoverScale : 1f);
            if (idlePulse && interactable) {
                // Only breathe while resting, so the pulse never fights the hover.
                float rest = 1f - _hoverProgress;
                targetScale *= 1f + idlePulseAmplitude * rest * Mathf.Sin(Time.unscaledTime * idlePulseSpeed);
            }

            UiSpring.Step(ref _currentScale, ref _scaleVelocity, targetScale, springFrequency, springDamping, dt);
            _rectTransform.localScale = _restScale * _currentScale;

            // The lift eases out under the press, so pressing pushes the button back down again.
            float targetLift = hoverLift * _hoverProgress * (1f - _pressProgress);
            UiSpring.Step(ref _currentLift, ref _liftVelocity, targetLift, springFrequency, springDamping, dt);

            float targetDepth = pressed ? PressDepthInCanvasUnits() : 0f;
            UiSpring.Step(ref _currentDepth, ref _depthVelocity, targetDepth, springFrequency, springDamping, dt);
            _rectTransform.anchoredPosition3D = _restAnchoredPosition + new Vector3(0f, _currentLift, _currentDepth);

            if (_flashProgress > 0f) {
                _flashProgress = Mathf.MoveTowards(_flashProgress, 0f,
                    pressFlashDecay > 0f ? dt / pressFlashDecay : 1f);
            }

            if (tintTarget != null) {
                Color settled = Color.Lerp(_restTint, hoverTint, _hoverProgress);
                float step = tintBlendTime > 0f ? dt / tintBlendTime : 1f;
                tintTarget.color = Color.Lerp(tintTarget.color, settled, Mathf.Clamp01(step));

                if (pressFlash && _flashProgress > 0f) {
                    // Applied after the blend rather than through it, so the flash is instant on the
                    // way in and only the decay is eased.
                    tintTarget.color = Color.Lerp(tintTarget.color, pressFlashTint,
                        Mathf.SmoothStep(0f, 1f, _flashProgress));
                }
            }
        }

        /// <summary>
        /// The press depth in the canvas's own units. A world-space canvas faces the viewer with its +Z running into the
        /// screen, so a positive offset is a button going down.
        /// </summary>
        private float PressDepthInCanvasUnits() {
            Transform parent = _rectTransform.parent;
            float scale = parent != null ? parent.lossyScale.z : 1f;
            if (scale <= Mathf.Epsilon) {
                return 0f;
            }

            return pressDepthMillimetres * MetresPerMillimetre / scale;
        }

        public void OnPointerEnter(PointerEventData eventData) {
            if (_isHovered) {
                return;
            }

            _isHovered = true;

            if (HoverChanged != null) {
                HoverChanged(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData) {
            _isPressed = false;

            if (!_isHovered) {
                return;
            }

            _isHovered = false;

            if (HoverChanged != null) {
                HoverChanged(false);
            }
        }

        public void OnPointerDown(PointerEventData eventData) {
            _isPressed = true;
            _flashProgress = 1f;
        }

        public void OnPointerUp(PointerEventData eventData) {
            _isPressed = false;
        }
    }
}
