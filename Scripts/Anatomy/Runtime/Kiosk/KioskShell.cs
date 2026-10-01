using System.Collections;

using KmaxXR;

using UnityEngine;

using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Runs the exhibit unattended. It opens on the body map, fades between the body map and each topic, sends a topic
    /// that nobody is touching back to the body map, and lets the body map show itself when no visitor has come for a
    /// while. The clock and the three states are <see cref="KioskFlow"/>'s; this is what watches for visitors and what
    /// acts when the flow says something is due.
    ///
    /// <para>A visitor is anything that moves the pen or the mouse, presses a key or a button, turns the wheel, or,
    /// when a head tracker is given, is seen looking at the screen. The idle times are unscaled, so they hold if the game
    /// clock is stopped.</para>
    ///
    /// <para>In the Editor, F1 and onward switch between topics, in the order they were built: F7 is the body map, F8 the organ
    /// puzzle and F9 the scan.</para>
    /// </summary>
    public class KioskShell : MonoBehaviour {
        private const float MouseMovePixels = 2f;
        private const float TipMoveMetres = 0.003f;

        [SerializeField] private AnatomyTopicController controller;
        [SerializeField] private AnatomyControls controls;
        [SerializeField] private ScreenFader fader;
        [SerializeField, Tooltip("Optional. Moving the pen counts as a visitor being there.")]
        private StylusTip stylusTip;
        [SerializeField, Tooltip("Optional. Someone whose eyes are tracked counts as a visitor being there.")]
        private HeadTracker headTracker;
        [SerializeField, Tooltip("Seconds the screen takes to go black when a topic is about to change.")]
        private float fadeOutSeconds = 0.25f;
        [SerializeField, Tooltip("Seconds the screen takes to come back once the new topic is in place.")]
        private float fadeInSeconds = 0.45f;
        [SerializeField, Tooltip("Seconds without a visitor before the body map starts showing itself.")]
        private float attractAfterSeconds = 45f;
        [SerializeField, Tooltip("Seconds without a visitor before a topic gives way to the body map.")]
        private float returnAfterSeconds = 120f;
        [SerializeField, Tooltip("Seconds the body map dwells on each place while it shows itself.")]
        private float attractStepSeconds = 7f;

#if UNITY_EDITOR
        private static readonly string[] ShortcutTopics = new string[] { "heart", "brain", "ear", "eye", "breathing", "skull", "body", "organs", "scan" };
#endif

        private KioskFlow _flow;
        private Coroutine _switching;
        private Vector3 _lastMouse;
        private Vector3 _lastTip;
        private float _attractClock;

        private void Awake() {
            if (controller == null || controls == null || fader == null) {
                Debug.LogError($"{nameof(KioskShell)} on '{name}' needs a controller, the controls and a screen fader; the kiosk shell is disabled.", this);
                enabled = false;
                return;
            }

            _flow = new KioskFlow(attractAfterSeconds, returnAfterSeconds);
        }

        private void OnEnable() {
            _flow.AttractStarted += OnAttractStarted;
            _flow.AttractStopped += OnAttractStopped;
            _flow.ReturnDue += OnReturnDue;
            controller.TopicRequested += RequestTopic;
            controls.HomeRequested += OnHomeRequested;
        }

        private void OnDisable() {
            if (_flow == null) {
                return;
            }

            _flow.AttractStarted -= OnAttractStarted;
            _flow.AttractStopped -= OnAttractStopped;
            _flow.ReturnDue -= OnReturnDue;
            controller.TopicRequested -= RequestTopic;
            controls.HomeRequested -= OnHomeRequested;
        }

        private void Start() {
            _lastMouse = Input.mousePosition;
            if (stylusTip != null) {
                _lastTip = stylusTip.Position;
            }

            // Black until the first topic is in place; Open lifts it.
            fader.Cover();
            StartCoroutine(Open());
        }

        private void Update() {
#if UNITY_EDITOR
            HandleShortcuts();
#endif
            if (_switching != null) {
                return;
            }

            _flow.Tick(Time.unscaledDeltaTime, SampleActivity());
            if (_flow.State == KioskState.Attract) {
                AdvanceAttract();
            }
        }

        /// <summary>The first topic is loaded by the controller in its own Start, so wait a frame, then lift the screen.</summary>
        private IEnumerator Open() {
            yield return null;
            NoteTopicShown();
            fader.FadeTo(0f, fadeInSeconds);
        }

        private void RequestTopic(string topicId) {
            if (_switching != null || topicId == controller.CurrentTopicId) {
                return;
            }

            _switching = StartCoroutine(SwitchTo(topicId));
        }

        /// <summary>
        /// Goes black, swaps the topic, and comes back. A topic that cannot be loaded leaves the current one in place, and
        /// the screen comes back to it.
        /// </summary>
        private IEnumerator SwitchTo(string topicId) {
            fader.FadeTo(1f, fadeOutSeconds);
            while (fader.IsFading) {
                yield return null;
            }

            controller.LoadTopic(topicId);
            NoteTopicShown();

            // A frame for the new model to take its place before the screen lifts.
            yield return null;
            fader.FadeTo(0f, fadeInSeconds);
            while (fader.IsFading) {
                yield return null;
            }

            _switching = null;
        }

        private void NoteTopicShown() {
            if (controller.IsOnHub) {
                _flow.EnterHub();
            } else {
                _flow.EnterTopic();
            }
        }

        private void OnHomeRequested() {
            RequestTopic(controller.HubTopicId);
        }

        private void OnReturnDue() {
            RequestTopic(controller.HubTopicId);
        }

        private void OnAttractStarted() {
            _attractClock = 0f;
            controller.BeginShowcase();
        }

        private void OnAttractStopped() {
            controller.ResetToHome();
        }

        /// <summary>The body map takes its next step round the places it leads to.</summary>
        private void AdvanceAttract() {
            _attractClock += Time.unscaledDeltaTime;
            if (_attractClock >= attractStepSeconds) {
                _attractClock = 0f;
                controller.AdvanceShowcase();
            }
        }

        /// <summary>True when a visitor did anything since the last call.</summary>
        private bool SampleActivity() {
            bool active = Input.anyKey || Input.mouseScrollDelta.sqrMagnitude > 0f;

            Vector3 mouse = Input.mousePosition;
            if ((mouse - _lastMouse).sqrMagnitude > MouseMovePixels * MouseMovePixels) {
                active = true;
            }

            _lastMouse = mouse;

            if (stylusTip != null) {
                Vector3 tip = stylusTip.Position;
                if ((tip - _lastTip).sqrMagnitude > TipMoveMetres * TipMoveMetres) {
                    active = true;
                }

                _lastTip = tip;
            }

            if (headTracker != null && headTracker.EyeVisible) {
                active = true;
            }

            return active;
        }

#if UNITY_EDITOR
        private void HandleShortcuts() {
            for (int i = 0; i < ShortcutTopics.Length; i++) {
                if (Input.GetKeyDown(KeyCode.F1 + i)) {
                    RequestTopic(ShortcutTopics[i]);
                }
            }
        }
#endif
    }
}