using System;
using System.Collections;

using KmaxXR;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// The launcher, the screen the exhibit opens on and returns to: a row of cards, one for each exhibit, with the chosen exhibit's
    /// own model turning in front of the glass above them and a Load button below. Pressing a card chooses it; pressing the chosen
    /// card again, or Load, asks for it to be opened.
    ///
    /// <para>It asks and does not open: <see cref="LoadRequested"/> is answered by the kiosk shell, which fades the screen, so a
    /// change of topic looks the same whoever asks for it. While the launcher is up the view is held at its opening angle
    /// and the camera backgrounds are a lighter slate than the topics', with a few motes of dust drifting behind the model.</para>
    ///
    /// <para>The picture on each card is generated from the DOSCH models, so on a fresh clone the cards show their names alone.</para>
    /// </summary>
    public class AnatomyLauncher : MonoBehaviour {
        private const string SubtitleSeparator = "  •  ";

        [SerializeField, Tooltip("The pool of cards, in reading order. Cards beyond the exhibits on offer are hidden.")]
        private LauncherCard[] cards = new LauncherCard[0];
        [SerializeField] private TMP_Text titleText;
        [SerializeField, Tooltip("Names the chosen exhibit and says what it is.")]
        private TMP_Text subtitleText;
        [SerializeField] private Button loadButton;
        [SerializeField] private TMP_Text loadLabel;
        [SerializeField, Tooltip("The turntable the chosen exhibit's model turns on.")]
        private LauncherPreview preview;
        [SerializeField, Tooltip("The drifting dust behind the model. Optional.")]
        private ParticleSystem dust;
        [SerializeField, Tooltip("Held at its opening angle while the launcher is up.")]
        private ViewerFlyController viewer;
        [SerializeField, Tooltip("Optional. A soft bell when a card is chosen.")]
        private AnatomyAudio sound;
        [SerializeField, Tooltip("How far in front of the glass the middle of the model sits, in metres.")]
        private float popOut = 0.03f;
        [SerializeField, Tooltip("Where the dust is centred, in metres from the middle of the glass: across, up and into the screen.")]
        private Vector3 dustCentre = new Vector3(0f, 0f, 0.06f);
        [SerializeField, Tooltip("The colour the cameras clear to while the launcher is up.")]
        private Color backdropColor = new Color(0.145f, 0.165f, 0.21f, 1f);

        private AnatomyLauncherData _data;
        private Camera[] _cameras = new Camera[0];
        private Color[] _savedBackgrounds = new Color[0];
        private int _selected = -1;
        private bool _preloadDone;

        /// <summary>Raised with a topic's id when the visitor asks for the exhibit to be opened.</summary>
        public event Action<string> LoadRequested;

        public bool IsShown {
            get { return gameObject.activeSelf; }
        }

        /// <summary>The id of the chosen exhibit's topic, or null when none is chosen.</summary>
        public string SelectedTopicId {
            get { return _selected >= 0 && _data != null ? _data.Entries[_selected].Topic : null; }
        }

        /// <summary>The model on the turntable, for measuring where it is drawn. Null when the launcher is not up.</summary>
        public LauncherPreview Preview {
            get { return preview; }
        }

        private void OnEnable() {
            loadButton.onClick.AddListener(OnLoadClicked);
            for (int i = 0; i < cards.Length; i++) {
                cards[i].Clicked += OnCardClicked;
            }
        }

        private void OnDisable() {
            loadButton.onClick.RemoveListener(OnLoadClicked);
            for (int i = 0; i < cards.Length; i++) {
                cards[i].Clicked -= OnCardClicked;
            }
        }

        /// <summary>
        /// Brings the launcher up: holds the view at its opening angle, lights the backdrop, deals the cards and puts the first
        /// exhibit on the turntable. Returns false, after logging why, when the launcher has nothing to offer.
        /// </summary>
        public bool Show() {
            if (_data == null) {
                _data = AnatomyTopicLibrary.LoadLauncher();
            }

            if (_data == null || _data.Entries.Count == 0) {
                return false;
            }

            gameObject.SetActive(true);
            FaceTheGlass();
            TintBackdrop();

            titleText.text = _data.Title;
            for (int i = 0; i < cards.Length; i++) {
                if (i < _data.Entries.Count) {
                    cards[i].Bind(i, _data.Entries[i].Label, AnatomyTopicLibrary.LoadThumbnail(_data.Entries[i].Topic));
                } else {
                    cards[i].Hide();
                }
            }

            _selected = -1;
            Select(0, false);
            PlaceDust();
            if (!_preloadDone) {
                StartCoroutine(PreloadModels());
            }

            return true;
        }

        /// <summary>
        /// Reads every exhibit's model from disk in the background, one after another, while the launcher is up, so that choosing a
        /// card the first time does not stall the turntable. A model that has not arrived when its card is chosen is simply loaded
        /// then. Taking the launcher down stops it, and bringing it up again carries on until all have been read once.
        /// </summary>
        private IEnumerator PreloadModels() {
            for (int i = 0; i < _data.Entries.Count; i++) {
                ResourceRequest request = AnatomyTopicLibrary.LoadModelPrefabAsync(_data.Entries[i].Topic);
                while (!request.isDone) {
                    yield return null;
                }
            }

            _preloadDone = true;
        }

        /// <summary>Takes the launcher down: the models, the dust, the backdrop and the cards.</summary>
        public void Hide() {
            preview.Clear();
            if (dust != null) {
                dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                dust.gameObject.SetActive(false);
            }

            RestoreBackdrop();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// The topic after this one in the order of the cards, for the Next button. Null after the last, or for a topic the
        /// launcher does not list, which means the way on is back to the launcher.
        /// </summary>
        public string NextTopicId(string currentTopicId) {
            AnatomyLauncherData data = _data != null ? _data : AnatomyTopicLibrary.LoadLauncher();
            if (data == null) {
                return null;
            }

            for (int i = 0; i < data.Entries.Count - 1; i++) {
                if (data.Entries[i].Topic == currentTopicId) {
                    return data.Entries[i + 1].Topic;
                }
            }

            return null;
        }

        /// <summary>Chooses the next card, and the first after the last. For an exhibit showing itself; it makes no sound.</summary>
        public void AdvanceShowcase() {
            if (_data != null && _data.Entries.Count > 0) {
                Select((_selected + 1) % _data.Entries.Count, false);
            }
        }

        /// <summary>Chooses a card: the cards show which, the title says what it is and the model on the turntable changes.</summary>
        public void Select(int index, bool announce) {
            if (_data == null || index < 0 || index >= _data.Entries.Count || index == _selected) {
                return;
            }

            _selected = index;
            AnatomyLauncherEntry entry = _data.Entries[index];
            for (int i = 0; i < cards.Length; i++) {
                cards[i].SetSelected(i == index);
            }

            AnatomyTopicData topic = AnatomyTopicLibrary.LoadData(entry.Topic);
            subtitleText.text = topic != null ? entry.Label + SubtitleSeparator + topic.Subtitle : entry.Label;
            loadLabel.text = string.Format(_data.LoadFormat, entry.Label);
            preview.Show(entry.Topic);

            if (announce && sound != null) {
                sound.PlaySelect();
            }
        }

        private void OnCardClicked(int index) {
            if (index == _selected) {
                RequestLoad();
            } else {
                Select(index, true);
            }
        }

        private void OnLoadClicked() {
            RequestLoad();
        }

        private void RequestLoad() {
            string topic = SelectedTopicId;
            if (topic != null && LoadRequested != null) {
                LoadRequested(topic);
            }
        }

        /// <summary>
        /// Puts the view at its opening angle and the middle of the model <see cref="popOut"/> in front of the glass, and holds it
        /// there: nothing the pen or the mouse does turns the view while the launcher is up.
        /// </summary>
        private void FaceTheGlass() {
            if (viewer == null) {
                return;
            }

            viewer.EnableFly = true;
            viewer.FlyTo(0f, 0f, StereoCamera.DefaultDistance - popOut, false);
            viewer.EnableFly = false;
        }

        private void TintBackdrop() {
            XRRig rig = FindFirstObjectByType<XRRig>();
            _cameras = rig != null ? rig.GetComponentsInChildren<Camera>(true) : new Camera[0];
            _savedBackgrounds = new Color[_cameras.Length];
            for (int i = 0; i < _cameras.Length; i++) {
                _savedBackgrounds[i] = _cameras[i].backgroundColor;
                _cameras[i].backgroundColor = backdropColor;
            }
        }

        private void RestoreBackdrop() {
            for (int i = 0; i < _cameras.Length; i++) {
                if (_cameras[i] != null) {
                    _cameras[i].backgroundColor = _savedBackgrounds[i];
                }
            }

            _cameras = new Camera[0];
            _savedBackgrounds = new Color[0];
        }

        /// <summary>
        /// Centres the dust on the glass and starts it afresh. Its motes are in world space, so they would stay where the last
        /// run left them if the view had moved; stopping and playing again puts the whole cloud where the glass is now.
        /// </summary>
        private void PlaceDust() {
            if (dust == null) {
                return;
            }

            dust.gameObject.SetActive(true);
            dust.transform.position = StereoVolume.ToWorld(dustCentre);
            dust.transform.rotation = StereoVolume.ScreenTransform != null ? StereoVolume.ScreenTransform.rotation : Quaternion.identity;
            dust.Clear(true);
            dust.Play(true);
        }
    }
}