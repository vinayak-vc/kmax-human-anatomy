using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Fades the whole screen to black and back, so one topic can replace another without a jump. It is a black panel over
    /// the interface, and the interface is drawn over the model and its lines, so nothing shows through it.
    ///
    /// <para>It starts clear, so a scene with no one to lift it is never left black. It uses unscaled time, so a fade still
    /// finishes if the game's clock is stopped, and it blocks clicks while any of it is showing.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ScreenFader : MonoBehaviour {
        private const float BlocksClicksAbove = 0.01f;

        private CanvasGroup _group;
        private float _target;
        private float _speed;

        /// <summary>How much of the screen is covered: 0 for none, 1 for all.</summary>
        public float Coverage { get; private set; }

        /// <summary>True while a fade is still under way.</summary>
        public bool IsFading {
            get { return !Mathf.Approximately(Coverage, _target); }
        }

        private void Awake() {
            _group = GetComponent<CanvasGroup>();
            Coverage = 0f;
            _target = 0f;
            Apply();
            enabled = false;
        }

        /// <summary>Covers the whole screen at once, for whoever is about to lift it again.</summary>
        public void Cover() {
            Coverage = 1f;
            _target = 1f;
            Apply();
        }

        /// <summary>Fades to this much coverage over this many seconds. Zero seconds is at once.</summary>
        public void FadeTo(float coverage, float seconds) {
            _target = Mathf.Clamp01(coverage);
            _speed = seconds > 0f ? Mathf.Abs(_target - Coverage) / seconds : float.MaxValue;
            enabled = true;
        }

        private void Update() {
            Coverage = Mathf.MoveTowards(Coverage, _target, _speed * Time.unscaledDeltaTime);
            Apply();
            if (!IsFading) {
                enabled = false;
            }
        }

        private void Apply() {
            _group.alpha = Coverage;
            _group.blocksRaycasts = Coverage > BlocksClicksAbove;
        }
    }
}