using System;

using UnityEngine;

using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// One loose organ of the puzzle: where it belongs, the way it travels when it is thrown out or settles home, how it
    /// glows when the pen is on it, and the faint outline it leaves behind. It reports being picked up and let go; what
    /// that means is the puzzle's to decide.
    ///
    /// <para>The pen carries the organ by moving its transform (see <see cref="Grabbable.HoldsKinematically"/>). The organ
    /// itself only ever moves along a glide, which a grab cuts short, so the two never fight over where it is.</para>
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class OrganPiece : MonoBehaviour {
        private const float HiddenBelow = 0.004f;
        private const float SlotFadePerSecond = 3f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Grabbable _grabbable;
        private StructureHighlight _highlight;
        private Renderer _slot;
        private MaterialPropertyBlock _slotBlock;
        private Color _slotColour = Color.white;
        private Vector3 _homeLocal;
        private Vector3 _glideFrom;
        private Vector3 _glideTo;
        private float _glideSeconds;
        private float _glideClock;
        private float _slotShown;
        private float _slotTarget;
        private float _touchGlow;
        private float _hintGlow;
        private float _waveGlow;

        /// <summary>Raised when the pen takes hold of the organ.</summary>
        public event Action<OrganPiece> Grabbed;

        /// <summary>Raised when the pen lets go of the organ.</summary>
        public event Action<OrganPiece> Released;

        public string OrganId { get; private set; }

        /// <summary>How far the organ reaches from its middle across the screen, in metres. The scatter keeps this much room for it.</summary>
        public float Radius { get; private set; }

        /// <summary>How close to its home it must be let go to settle there, in metres.</summary>
        public float SnapRadius { get; private set; }

        /// <summary>True once it has settled home and is locked there.</summary>
        public bool IsPlaced { get; private set; }

        public bool IsHeld {
            get { return _grabbable != null && _grabbable.IsHeld; }
        }

        public bool IsGliding {
            get { return _glideSeconds > 0f; }
        }

        /// <summary>Where the organ belongs, in the world.</summary>
        public Vector3 HomePosition {
            get { return transform.parent.TransformPoint(_homeLocal); }
        }

        /// <summary>Takes charge of the organ where it stands, which must be its home: the puzzle starts with every organ in place.</summary>
        /// <param name="slot">The renderer of the faint outline that stays at home. May be null.</param>
        public void Begin(string organId, Renderer slot, float radius, float snapRadius) {
            OrganId = organId;
            Radius = radius;
            SnapRadius = snapRadius;
            _slot = slot;
            _grabbable = GetComponent<Grabbable>();
            _highlight = GetComponent<StructureHighlight>();
            _homeLocal = transform.localPosition;
            _slotBlock = new MaterialPropertyBlock();
            _slotShown = -1f;
            if (_slot != null && _slot.sharedMaterial != null && _slot.sharedMaterial.HasProperty(BaseColorId)) {
                _slotColour = _slot.sharedMaterial.GetColor(BaseColorId);
            }

            _grabbable.Grabbed.AddListener(OnGrabbed);
            _grabbable.Released.AddListener(OnReleased);
            SetSlotTarget(0f);
        }

        /// <summary>Carries the organ to a place in the world over this many seconds, after a delay. The pen cannot hold it on the way.</summary>
        public void Throw(Vector3 worldPosition, float seconds, float delay) {
            IsPlaced = false;
            Lock(true);
            Glide(worldPosition, seconds, delay);
        }

        /// <summary>Settles the organ into its home and locks it there, so it cannot be carried off again.</summary>
        public void SettleHome(float seconds) {
            IsPlaced = true;
            Lock(true);
            Glide(HomePosition, seconds, 0f);
        }

        /// <summary>Lets the pen take hold of the organ, or not.</summary>
        public void Lock(bool locked) {
            _grabbable.enabled = !locked;
        }

        /// <summary>How bright the outline at home is meant to be. It fades there, so it never flickers.</summary>
        public void SetSlotTarget(float brightness) {
            _slotTarget = brightness;
        }

        /// <summary>The glow of the pen being on the organ.</summary>
        public void SetTouchGlow(float glow) {
            _touchGlow = glow;
            ApplyGlow();
        }

        /// <summary>The glow of the organ being the one to try next.</summary>
        public void SetHintGlow(float glow) {
            _hintGlow = glow;
            ApplyGlow();
        }

        /// <summary>The glow of the finished puzzle passing through.</summary>
        public void SetWaveGlow(float glow) {
            _waveGlow = glow;
            ApplyGlow();
        }

        private void Update() {
            if (_glideSeconds > 0f) {
                StepGlide();
            }

            if (!Mathf.Approximately(_slotShown, _slotTarget)) {
                _slotShown = _slotShown < 0f ? _slotTarget : Mathf.MoveTowards(_slotShown, _slotTarget, SlotFadePerSecond * Time.deltaTime);
                DrawSlot();
            }
        }

        private void Glide(Vector3 worldPosition, float seconds, float delay) {
            _glideFrom = transform.position;
            _glideTo = worldPosition;
            _glideSeconds = Mathf.Max(seconds, 0.01f);
            _glideClock = -delay;
        }

        private void StepGlide() {
            _glideClock += Time.deltaTime;
            if (_glideClock < 0f) {
                return;
            }

            float progress = Mathf.Clamp01(_glideClock / _glideSeconds);
            transform.position = Vector3.Lerp(_glideFrom, _glideTo, Mathf.SmoothStep(0f, 1f, progress));
            if (progress >= 1f) {
                _glideSeconds = 0f;
            }
        }

        private void DrawSlot() {
            if (_slot == null) {
                return;
            }

            _slot.enabled = _slotShown > HiddenBelow;
            Color colour = _slotColour;
            colour.a = _slotShown;
            _slot.GetPropertyBlock(_slotBlock);
            _slotBlock.SetColor(BaseColorId, colour);
            _slot.SetPropertyBlock(_slotBlock);
        }

        private void ApplyGlow() {
            if (_highlight != null) {
                _highlight.SetPulse(Mathf.Max(_touchGlow, Mathf.Max(_hintGlow, _waveGlow)));
            }
        }

        private void OnGrabbed() {
            _glideSeconds = 0f;
            if (Grabbed != null) {
                Grabbed(this);
            }
        }

        private void OnReleased() {
            if (Released != null) {
                Released(this);
            }
        }
    }
}