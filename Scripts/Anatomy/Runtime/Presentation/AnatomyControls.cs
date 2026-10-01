using System;
using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// The on-screen buttons: start or stop the tour, step to the previous or next structure, zoom, reset, and, on the body
    /// map, explore the picked organ. A Body map button leads back to the body map from every topic. It reports presses
    /// and shows only what it is told; it decides nothing itself.
    ///
    /// <para>Each button sits in a slot that the layout group positions, and only the tour button's slot is ever
    /// hidden. The button itself is never moved by layout, because
    /// <see cref="ViitorCloud.KmaxDisplay.UiButtonMotion"/> captures its position once and re-applies it every
    /// frame.</para>
    /// </summary>
    public class AnatomyControls : MonoBehaviour {
        private const float UnavailableAlpha = 0.35f;

        [SerializeField, Tooltip("Slot shown only while the picked structure opens a topic.")]
        private GameObject exploreSlot;
        [SerializeField] private Button exploreButton;
        [SerializeField, Tooltip("Slot shown in every topic but the body map, which it leads back to.")]
        private GameObject homeSlot;
        [SerializeField] private Button homeButton;
        [SerializeField, Tooltip("Slot the layout positions; shown or hidden as a whole when the topic has no tour.")]
        private GameObject tourSlot;
        [SerializeField] private Button tourButton;
        [SerializeField] private TMP_Text tourLabel;
        [SerializeField, Tooltip("Slot shown only when the topic can be pulled apart.")]
        private GameObject explodeSlot;
        [SerializeField] private Button explodeButton;
        [SerializeField] private TMP_Text explodeLabel;
        [SerializeField, Tooltip("The row holding Previous and Next, hidden for a topic that has nothing to step through.")]
        private GameObject stepRow;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField, Tooltip("The row holding the zoom buttons, hidden for a topic that cannot be zoomed.")]
        private GameObject zoomRow;
        [SerializeField] private Button zoomOutButton;
        [SerializeField] private TMP_Text zoomOutLabel;
        [SerializeField] private Button zoomInButton;
        [SerializeField] private TMP_Text zoomInLabel;
        [SerializeField] private TMP_Text zoomText;
        [SerializeField] private Button resetButton;
        [SerializeField] private TMP_Text resetLabel;
        [SerializeField, Tooltip("The pool of buttons that lead from a topic to others, in order.")]
        private GameObject[] linkSlots = new GameObject[0];
        [SerializeField] private Button[] linkButtons = new Button[0];
        [SerializeField] private TMP_Text[] linkLabels = new TMP_Text[0];
        [SerializeField] private string resetText = "Reset view";
        [SerializeField] private string startTourText = "Start tour";
        [SerializeField] private string stopTourText = "Stop tour";
        [SerializeField] private string explodeText = "Explode";
        [SerializeField] private string assembleText = "Put together";

        public event Action<int> LinkRequested;
        public event Action ExploreRequested;
        public event Action HomeRequested;
        public event Action TourToggled;
        public event Action ExplodeToggled;
        public event Action PreviousRequested;
        public event Action NextRequested;
        public event Action ZoomInRequested;
        public event Action ZoomOutRequested;
        public event Action ResetRequested;

        private UnityAction[] _linkHandlers = new UnityAction[0];

        private void OnEnable() {
            if (!IsFullyWired()) {
                Debug.LogError($"{nameof(AnatomyControls)} on '{name}' is missing a button, slot or label; the controls are disabled.", this);
                enabled = false;
                return;
            }

            exploreButton.onClick.AddListener(OnExploreClicked);
            homeButton.onClick.AddListener(OnHomeClicked);
            tourButton.onClick.AddListener(OnTourClicked);
            explodeButton.onClick.AddListener(OnExplodeClicked);
            previousButton.onClick.AddListener(OnPreviousClicked);
            nextButton.onClick.AddListener(OnNextClicked);
            zoomInButton.onClick.AddListener(OnZoomInClicked);
            zoomOutButton.onClick.AddListener(OnZoomOutClicked);
            resetButton.onClick.AddListener(OnResetClicked);

            _linkHandlers = new UnityAction[linkButtons.Length];
            for (int i = 0; i < linkButtons.Length; i++) {
                int index = i;
                _linkHandlers[i] = delegate () {
                    OnLinkClicked(index);
                };
                linkButtons[i].onClick.AddListener(_linkHandlers[i]);
            }
        }

        private void OnDisable() {
            if (!IsFullyWired()) {
                return;
            }

            exploreButton.onClick.RemoveListener(OnExploreClicked);
            homeButton.onClick.RemoveListener(OnHomeClicked);
            tourButton.onClick.RemoveListener(OnTourClicked);
            explodeButton.onClick.RemoveListener(OnExplodeClicked);
            previousButton.onClick.RemoveListener(OnPreviousClicked);
            nextButton.onClick.RemoveListener(OnNextClicked);
            zoomInButton.onClick.RemoveListener(OnZoomInClicked);
            zoomOutButton.onClick.RemoveListener(OnZoomOutClicked);
            resetButton.onClick.RemoveListener(OnResetClicked);

            for (int i = 0; i < linkButtons.Length && i < _linkHandlers.Length; i++) {
                linkButtons[i].onClick.RemoveListener(_linkHandlers[i]);
            }
        }

        /// <summary>
        /// Previous and Next, and the zoom, are only there for a topic that has something to step through and can be zoomed. A
        /// topic that works in the room's space has neither.
        /// </summary>
        public void ShowNavigation(bool showSteps, bool showZoom) {
            stepRow.SetActive(showSteps);
            zoomRow.SetActive(showZoom);
        }

        /// <summary>Shows a button for each link a topic offers, with its wording, and hides the rest of the pool.</summary>
        public void ShowLinks(IReadOnlyList<AnatomyTopicLink> links) {
            for (int i = 0; i < linkSlots.Length; i++) {
                bool shown = links != null && i < links.Count;
                linkSlots[i].SetActive(shown);
                if (shown) {
                    linkLabels[i].text = links[i].Label;
                }
            }
        }

        /// <summary>What the Reset button says. Empty wording uses the button's own.</summary>
        public void ShowResetLabel(string wording) {
            resetLabel.text = string.IsNullOrEmpty(wording) ? resetText : wording;
        }

        /// <summary>The Explore button is only there while what is picked opens a topic.</summary>
        public void ShowExplore(bool canExplore) {
            exploreSlot.SetActive(canExplore);
        }

        /// <summary>The Body map button is there in every topic but the body map itself.</summary>
        public void ShowHome(bool visible) {
            homeSlot.SetActive(visible);
        }

        /// <summary>The tour button is only there when the topic has a tour, and says what pressing it will do.</summary>
        public void ShowTour(bool hasTour, bool touring) {
            tourSlot.SetActive(hasTour);
            tourLabel.text = touring ? stopTourText : startTourText;
        }

        /// <summary>
        /// The explode button is only there when the topic can be pulled apart, and says what pressing it will do. A topic
        /// may word the button itself, for example Magnify; empty wording uses the button's own.
        /// </summary>
        public void ShowExplode(bool canExplode, bool exploded, string topicExplodeText, string topicAssembleText) {
            explodeSlot.SetActive(canExplode);
            string fallback = exploded ? assembleText : explodeText;
            string wording = exploded ? topicAssembleText : topicExplodeText;
            explodeLabel.text = string.IsNullOrEmpty(wording) ? fallback : wording;
        }

        /// <summary>Shows the zoom as a multiple, and dims a zoom button that has nowhere further to go.</summary>
        public void ShowZoom(float zoom, bool canZoomIn, bool canZoomOut) {
            zoomText.text = zoom.ToString("0.0") + "x";
            SetAvailable(zoomInButton, zoomInLabel, canZoomIn);
            SetAvailable(zoomOutButton, zoomOutLabel, canZoomOut);
        }

        private static void SetAvailable(Button button, TMP_Text label, bool available) {
            button.interactable = available;
            Color color = label.color;
            color.a = available ? 1f : UnavailableAlpha;
            label.color = color;
        }

        private bool IsFullyWired() {
            if (stepRow == null || zoomRow == null || resetLabel == null || linkSlots.Length != linkButtons.Length
                || linkSlots.Length != linkLabels.Length) {
                return false;
            }

            return exploreSlot != null && exploreButton != null && homeSlot != null && homeButton != null
                && tourSlot != null && tourButton != null && tourLabel != null && explodeSlot != null
                && explodeButton != null && explodeLabel != null && previousButton != null
                && nextButton != null && zoomOutButton != null && zoomOutLabel != null && zoomInButton != null
                && zoomInLabel != null && zoomText != null && resetButton != null;
        }

        private void OnLinkClicked(int index) {
            if (LinkRequested != null) {
                LinkRequested(index);
            }
        }

        private void OnExploreClicked() {
            if (ExploreRequested != null) {
                ExploreRequested();
            }
        }

        private void OnHomeClicked() {
            if (HomeRequested != null) {
                HomeRequested();
            }
        }

        private void OnTourClicked() {
            if (TourToggled != null) {
                TourToggled();
            }
        }

        private void OnExplodeClicked() {
            if (ExplodeToggled != null) {
                ExplodeToggled();
            }
        }

        private void OnPreviousClicked() {
            if (PreviousRequested != null) {
                PreviousRequested();
            }
        }

        private void OnNextClicked() {
            if (NextRequested != null) {
                NextRequested();
            }
        }

        private void OnZoomInClicked() {
            if (ZoomInRequested != null) {
                ZoomInRequested();
            }
        }

        private void OnZoomOutClicked() {
            if (ZoomOutRequested != null) {
                ZoomOutRequested();
            }
        }

        private void OnResetClicked() {
            if (ResetRequested != null) {
                ResetRequested();
            }
        }
    }
}