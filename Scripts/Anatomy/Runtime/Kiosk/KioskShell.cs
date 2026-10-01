using System.Collections;

using KmaxXR;

using UnityEngine;

using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Runs the exhibit unattended. It opens on the launcher, fades between the launcher and each topic, sends a topic that
    /// nobody is touching back to the launcher, and lets the launcher show itself, card by card, when no visitor has come for a
    /// while. The clock and the three states are <see cref="KioskFlow"/>'s; this is what watches for visitors and what
    /// acts when the flow says something is due.
    ///
    /// <para>The launcher is not a topic. While it is up the topic's whole interface is switched off and no topic is loaded; a
    /// topic that is loaded has the launcher put away. Both changes happen behind the fade, whoever asks for them: a card, the
    /// Load button, a region of the body map, the Menu and Next buttons, or the idle clock.</para>
    ///
    /// <para>A visitor is anything that moves the pen or the mouse, presses a key or a button, turns the wheel, or,
    /// when a head tracker is given, is seen looking at the screen. The idle times are unscaled, so they hold if the game
    /// clock is stopped.</para>
    ///
    /// <para>In the Editor, F1 and onward switch between topics, in the order they were built: F7 is the body map, F8 the organ
    /// puzzle and F9 the scan. F10 is the launcher.</para>
    /// </summary>
    public class KioskShell : MonoBehaviour {
        /// <summary>The id the shell takes for the launcher when it is asked to go there, which no topic uses.</summary>
        public const string LauncherId = "launcher";

        private const float MouseMovePixels = 2f;
        private const float TipMoveMetres = 0.003f;

        [SerializeField] private AnatomyTopicController controller;
        [SerializeField] private AnatomyControls controls;
        [SerializeField] private AnatomyLauncher launcher;
        [SerializeField, Tooltip("Everything that belongs to a topic: its title, caption, badges and buttons. Off while the launcher is up.")]
        private GameObject topicInterface;
        [SerializeField] private ScreenFader fader;
        [SerializeField, Tooltip("Optional. Moving the pen counts as a visitor being there.")]
        private StylusTip stylusTip;
        [SerializeField, Tooltip("Optional. Someone whose eyes are tracked counts as a visitor being there.")]
        private HeadTracker headTracker;
        [SerializeField, Tooltip("Seconds the screen takes to go black when the screen is about to change.")]
        private float fadeOutSeconds = 0.25f;
        [SerializeField, Tooltip("Seconds the screen takes to come back once the new screen is in place.")]
        private float fadeInSeconds = 0.45f;
        [SerializeField, Tooltip("Seconds without a visitor before the launcher starts showing itself.")]
        private float attractAfterSeconds = 45f;
        [SerializeField, Tooltip("Seconds without a visitor before a topic gives way to the launcher.")]
        private float returnAfterSeconds = 120f;
        [SerializeField, Tooltip("Seconds the launcher dwells on each exhibit while it shows itself.")]
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
            if (controller == null || controls == null || launcher == null || topicInterface == null || fader == null) {
                Debug.LogError($"{nameof(KioskShell)} on '{name}' needs a controller, the controls, a launcher, the topic interface and a screen fader; the kiosk shell is disabled.", this);
                enabled = false;
                return;
            }

            _flow = new KioskFlow(attractAfterSeconds, returnAfterSeconds);
        }

        private void OnEnable() {
            _flow.AttractStarted += OnAttractStarted;
            _flow.ReturnDue += OnReturnDue;
            controller.TopicRequested += RequestTopic;
            launcher.LoadRequested += RequestTopic;
            controls.HomeRequested += OnHomeRequested;
            controls.NextExhibitRequested += OnNextExhibitRequested;
        }

        private void OnDisable() {
            if (_flow == null) {
                return;
            }

            _flow.AttractStarted -= OnAttractStarted;
            _flow.ReturnDue -= OnReturnDue;
            controller.TopicRequested -= RequestTopic;
            launcher.LoadRequested -= RequestTopic;
            controls.HomeRequested -= OnHomeRequested;
            controls.NextExhibitRequested -= OnNextExhibitRequested;
        }

        private void Start() {
            _lastMouse = Input.mousePosition;
            if (stylusTip != null) {
                _lastTip = stylusTip.Position;
            }

            // Black until the first screen is in place; Open lifts it.
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

        /// <summary>
        /// The controller loads its start topic, if it has one, in its own Start, so wait a frame; then put up the launcher, or
        /// the topic the exhibit was told to start on, and lift the screen.
        /// </summary>
        private IEnumerator Open() {
            yield return null;
            if (controller.CurrentTopicId != null) {
                topicInterface.SetActive(true);
            } else {
                ShowLauncher();
            }

            NoteScreenShown();
            fader.FadeTo(0f, fadeInSeconds);
        }

        private void RequestTopic(string topicId) {
            if (_switching != null) {
                return;
            }

            if (topicId == LauncherId) {
                if (!launcher.IsShown) {
                    _switching = StartCoroutine(SwitchToLauncher());
                }

                return;
            }

            if (topicId != controller.CurrentTopicId) {
                _switching = StartCoroutine(SwitchToTopic(topicId));
            }
        }

        /// <summary>
        /// Goes black, swaps the screen for the topic, and comes back. A topic that cannot be loaded leaves what was on the screen
        /// in place, and the screen comes back to it.
        /// </summary>
        private IEnumerator SwitchToTopic(string topicId) {
            fader.FadeTo(1f, fadeOutSeconds);
            while (fader.IsFading) {
                yield return null;
            }

            bool fromLauncher = launcher.IsShown;
            if (fromLauncher) {
                launcher.Hide();
                topicInterface.SetActive(true);
            }

            if (!controller.LoadTopic(topicId) && fromLauncher) {
                ShowLauncher();
            }

            NoteScreenShown();
            yield return LiftScreen();
        }

        /// <summary>Goes black, takes the topic away, and comes back to the launcher.</summary>
        private IEnumerator SwitchToLauncher() {
            fader.FadeTo(1f, fadeOutSeconds);
            while (fader.IsFading) {
                yield return null;
            }

            controller.ClearTopic();
            ShowLauncher();
            NoteScreenShown();
            yield return LiftScreen();
        }

        /// <summary>A frame for the new screen to take its place, then the screen lifts.</summary>
        private IEnumerator LiftScreen() {
            yield return null;
            fader.FadeTo(0f, fadeInSeconds);
            while (fader.IsFading) {
                yield return null;
            }

            _switching = null;
        }

        /// <summary>Puts the launcher up with the topic's interface switched off beneath it.</summary>
        private void ShowLauncher() {
            topicInterface.SetActive(false);
            if (!launcher.Show()) {
                Debug.LogError($"{nameof(KioskShell)} could not show the launcher, so the screen has nothing on it.", this);
            }
        }

        private void NoteScreenShown() {
            if (launcher.IsShown) {
                _flow.EnterHub();
            } else {
                _flow.EnterTopic();
            }
        }

        private void OnHomeRequested() {
            RequestTopic(LauncherId);
        }

        /// <summary>The next exhibit in the order of the cards, and the launcher after the last.</summary>
        private void OnNextExhibitRequested() {
            string next = launcher.NextTopicId(controller.CurrentTopicId);
            RequestTopic(next != null ? next : LauncherId);
        }

        private void OnReturnDue() {
            RequestTopic(LauncherId);
        }

        private void OnAttractStarted() {
            _attractClock = 0f;
        }

        /// <summary>The launcher chooses its next card.</summary>
        private void AdvanceAttract() {
            _attractClock += Time.unscaledDeltaTime;
            if (_attractClock >= attractStepSeconds) {
                _attractClock = 0f;
                launcher.AdvanceShowcase();
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

            if (Input.GetKeyDown(KeyCode.F10)) {
                RequestTopic(LauncherId);
            }
        }
#endif
    }
}