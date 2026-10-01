using System;
using System.Collections.Generic;

using KmaxXR;

using UnityEngine;

using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Runs one topic in the persistent scene: brings its model and text in, wires pointer events to an
    /// <see cref="AnatomyExplorer"/>, and turns the explorer's state into highlights, captions, markers and sound.
    ///
    /// <para>It implements <see cref="IViewResetHandler"/>, so the stylus's reset button, the R key and the
    /// Reset button all clear the selection, the tour and the zoom as well as the camera.</para>
    ///
    /// <para>The model is placed at the world origin, which is the orbit centre of
    /// <see cref="ViewerFlyController"/>. The viewer's distance then decides how far the model's centre sits
    /// in front of the glass, which is why the topic data specifies a pop-out rather than a position.</para>
    ///
    /// <para>A structure whose text names a topic, as on the body map, can be explored: a second click on it, or the
    /// Explore button, raises <see cref="TopicRequested"/>. Getting there is left to whoever runs the exhibit, so it can
    /// fade, time and log the change; with nobody listening the topic simply opens.</para>
    /// </summary>
    public class AnatomyTopicController : MonoBehaviour, IViewResetHandler {
        [SerializeField, Tooltip("Topic shown at start, for example heart.")]
        private string startTopicId = "heart";
        [SerializeField, Tooltip("Parent of the model. Must sit at the orbit centre, the world origin.")]
        private Transform modelParent;
        [SerializeField] private ViewerFlyController viewer;
        [SerializeField] private AnatomyInfoPanel infoPanel;
        [SerializeField] private AnatomyControls controls;
        [SerializeField, Tooltip("Optional. Numbered badges that name each structure and can be pointed at.")]
        private AnatomyMarkers markers;
        [SerializeField, Tooltip("Optional. Magnifies the model. Without it the model stays at its opening size.")]
        private AnatomyZoom zoom;
        [SerializeField, Tooltip("Optional. The exhibit is silent without it.")]
        private AnatomyAudio sound;
        [SerializeField, Tooltip("Optional. Pulses the pen on hover and pick; does nothing without a tracked pen.")]
        private StylusHaptics haptics;
        [SerializeField, Tooltip("Optional. Keeps the model inside the comfortable depth budget as it turns.")]
        private ComfortDepthKeeper depthKeeper;
        [SerializeField, Tooltip("Optional. The layer buttons of the body map.")]
        private AnatomyLayerPanel layerPanel;
        [SerializeField, Tooltip("Id of the body map, the topic every other topic leads back to.")]
        private string hubTopicId = "body";
        [SerializeField, Tooltip("Optional. The pen's tip, for a topic whose behaviour works with the pen in the room's space.")]
        private StylusTip stylusTip;

        private readonly Dictionary<string, StructureHighlight> _highlights = new Dictionary<string, StructureHighlight>();
        private readonly Dictionary<string, AnatomyStructure> _structures = new Dictionary<string, AnatomyStructure>();
        private readonly List<Transform> _focusScratch = new List<Transform>();
        private readonly List<string> _focusIds = new List<string>();
        private AnatomyTopicData _data;
        private AnatomyExplorer _explorer;
        private GameObject _modelObject;
        private ExplodedViewBehaviour _exploder;
        private IFocusListener[] _focusListeners = new IFocusListener[0];
        private IResetListener[] _resetListeners = new IResetListener[0];
        private ITopicAction _action;
        private ITopicNarrator _narrator;
        private int _shownTourIndex = -1;

        /// <summary>Raised with a topic's id when the visitor chooses to explore what is picked.</summary>
        public event Action<string> TopicRequested;

        /// <summary>The id of the body map, which the Body map button leads back to.</summary>
        public string HubTopicId {
            get { return hubTopicId; }
        }

        /// <summary>The id of the topic on show, or null before one has loaded.</summary>
        public string CurrentTopicId {
            get { return _data != null ? _data.Id : null; }
        }

        public bool IsOnHub {
            get { return _data != null && _data.Id == hubTopicId; }
        }

        private void OnEnable() {
            if (controls != null) {
                controls.LinkRequested += OnLinkRequested;
                controls.ExploreRequested += OnExploreRequested;
                controls.TourToggled += OnTourToggled;
                controls.ExplodeToggled += OnExplodeToggled;
                controls.PreviousRequested += OnPreviousRequested;
                controls.NextRequested += OnNextRequested;
                controls.ZoomInRequested += OnZoomInRequested;
                controls.ZoomOutRequested += OnZoomOutRequested;
                controls.ResetRequested += OnResetRequested;
            }

            if (markers != null) {
                markers.Entered += OnMarkerEntered;
                markers.Exited += OnMarkerExited;
                markers.Clicked += OnMarkerClicked;
            }

            if (zoom != null) {
                zoom.Changed += OnZoomChanged;
            }
        }

        private void OnDisable() {
            if (controls != null) {
                controls.LinkRequested -= OnLinkRequested;
                controls.ExploreRequested -= OnExploreRequested;
                controls.TourToggled -= OnTourToggled;
                controls.ExplodeToggled -= OnExplodeToggled;
                controls.PreviousRequested -= OnPreviousRequested;
                controls.NextRequested -= OnNextRequested;
                controls.ZoomInRequested -= OnZoomInRequested;
                controls.ZoomOutRequested -= OnZoomOutRequested;
                controls.ResetRequested -= OnResetRequested;
            }

            if (markers != null) {
                markers.Entered -= OnMarkerEntered;
                markers.Exited -= OnMarkerExited;
                markers.Clicked -= OnMarkerClicked;
            }

            if (zoom != null) {
                zoom.Changed -= OnZoomChanged;
            }
        }

        private void Start() {
            LoadTopic(startTopicId);
        }

        /// <summary>
        /// Replaces the current topic. Returns false, after logging why, when the new one cannot be shown, and
        /// then leaves the current one in place: a topic that fails to load must not blank the screen.
        /// </summary>
        public bool LoadTopic(string topicId) {
            AnatomyTopicData data = AnatomyTopicLibrary.LoadData(topicId);
            GameObject prefab = AnatomyTopicLibrary.LoadModelPrefab(topicId);
            if (data == null || prefab == null) {
                return false;
            }

            UnloadTopic();

            _data = data;
            _modelObject = Instantiate(prefab, modelParent);
            _modelObject.name = topicId;
            _modelObject.transform.localPosition = Vector3.zero;
            _modelObject.transform.localScale = Vector3.one * data.DisplayScale;

            AnatomyModel model = _modelObject.GetComponent<AnatomyModel>();
            for (int i = 0; i < model.Structures.Count; i++) {
                AttachToStructure(model.Structures[i]);
            }

            _explorer = new AnatomyExplorer(data);
            _explorer.Changed += Refresh;
            AnatomyBehaviours.Attach(_modelObject, data.Behaviour, new AnatomyBehaviourContext(data, sound, haptics, stylusTip));
            _focusListeners = _modelObject.GetComponents<IFocusListener>();
            _resetListeners = _modelObject.GetComponents<IResetListener>();
            BindActivity();
            BindLayers();
            if (data.CanExplode) {
                _exploder = _modelObject.AddComponent<ExplodedViewBehaviour>();
                _exploder.Begin(model, data);
            }

            // A topic that works in the room's space is held still: the depth keeper does not shift it and the view does not move.
            if (depthKeeper != null) {
                depthKeeper.Track(data.IsStationary ? null : _modelObject.transform);
            }

            if (zoom != null) {
                zoom.Bind(_modelObject.transform, data.DisplayScale, data.MaxZoom, depthKeeper);
            }

            // The view first, so the markers choose their sides from how the topic opens.
            if (viewer != null) {
                viewer.EnableFly = true;
            }

            ApplyView(false);
            if (viewer != null && data.IsStationary) {
                viewer.EnableFly = false;
            }

            BindMarkers();
            Refresh();
            return true;
        }

        /// <summary>Clears the selection, the tour and the zoom, and returns the camera to the topic's opening view.</summary>
        public void ResetToHome() {
            if (_explorer == null) {
                return;
            }

            _explorer.Reset();
            if (!_data.IsStationary) {
                ApplyView(true);
            }

            if (zoom != null) {
                zoom.ResetZoom();
            }

            for (int i = 0; i < _resetListeners.Length; i++) {
                _resetListeners[i].OnResetRequested();
            }

            if (sound != null) {
                sound.PlayReset();
            }
        }

        /// <summary>
        /// Starts the tour from the opening state, for an exhibit showing itself with nobody there. Does nothing for a topic
        /// that has no tour.
        /// </summary>
        public void BeginShowcase() {
            if (_explorer == null) {
                return;
            }

            _explorer.Reset();
            _explorer.StartTour();
        }

        /// <summary>Moves the showcase on a step, and round to the first step again after the last.</summary>
        public void AdvanceShowcase() {
            if (_explorer == null) {
                return;
            }

            _explorer.NextStep();
            if (!_explorer.IsTouring) {
                _explorer.StartTour();
            }
        }

        private void AttachToStructure(AnatomyStructure structure) {
            StructureHighlight highlight = structure.gameObject.AddComponent<StructureHighlight>();
            AnatomyStructureInfo info = _data.FindStructure(structure.StructureId);
            if (info != null) {
                highlight.SetRestOpacity(info.RestOpacity);
                highlight.SetRecededLook(info.RecededSolidity, info.RecededGlow);
            }

            // A topic that gives the pen another job has no pointing at its structures at all: the pen's tip works with
            // them, and a pointer that also highlighted and sounded for each one would only echo it.
            if (_data.IsSelectable) {
                AnatomyStructureTarget target = structure.gameObject.AddComponent<AnatomyStructureTarget>();
                target.Entered += OnStructureEntered;
                target.Exited += OnStructureExited;
                target.Clicked += OnStructureClicked;
            }

            _highlights[structure.StructureId] = highlight;
            _structures[structure.StructureId] = structure;
        }

        /// <summary>Hooks up what the topic's behaviour offers: a button of its own, and things to say in the caption.</summary>
        private void BindActivity() {
            _action = _modelObject.GetComponent<ITopicAction>();
            if (_action != null) {
                _action.ActionChanged += Refresh;
            }

            _narrator = _modelObject.GetComponent<ITopicNarrator>();
            if (_narrator != null) {
                _narrator.MessageChanged += ShowCaption;
            }
        }

        /// <summary>Gives the layer buttons the body's layers when the topic has them, and hides the buttons when it has none.</summary>
        private void BindLayers() {
            if (layerPanel == null) {
                return;
            }

            BodyBehaviour body = _modelObject.GetComponent<BodyBehaviour>();
            if (body != null) {
                layerPanel.Bind(body);
            } else {
                layerPanel.Unbind();
            }
        }

        /// <summary>Gives the markers the topic's numbered structures in the order the topic lists them, which is their numbering.</summary>
        private void BindMarkers() {
            if (markers == null) {
                return;
            }

            List<AnatomyStructure> ordered = new List<AnatomyStructure>();
            for (int i = 0; i < _data.Structures.Count; i++) {
                if (!_data.Structures[i].IsNumbered) {
                    continue;
                }

                string id = _data.Structures[i].Id;
                AnatomyStructure structure;
                if (!_structures.TryGetValue(id, out structure)) {
                    Debug.LogWarning($"[Anatomy] Topic '{_data.Id}' describes '{id}', which the model does not have.", this);
                }

                ordered.Add(structure);
            }

            markers.Bind(ordered.ToArray());
        }

        private void UnloadTopic() {
            if (_explorer != null) {
                _explorer.Changed -= Refresh;
            }

            if (_action != null) {
                _action.ActionChanged -= Refresh;
            }

            if (_narrator != null) {
                _narrator.MessageChanged -= ShowCaption;
            }

            if (markers != null) {
                markers.Unbind();
            }

            if (zoom != null) {
                zoom.Unbind();
            }

            if (layerPanel != null) {
                layerPanel.Unbind();
            }

            if (_modelObject != null) {
                Destroy(_modelObject);
            }

            _highlights.Clear();
            _structures.Clear();
            _explorer = null;
            _modelObject = null;
            _exploder = null;
            _focusListeners = new IFocusListener[0];
            _resetListeners = new IResetListener[0];
            _action = null;
            _narrator = null;
            if (depthKeeper != null) {
                depthKeeper.Track(null);
            }
            _shownTourIndex = -1;
        }

        private void ApplyView(bool animated) {
            if (viewer == null) {
                return;
            }

            viewer.FlyTo(_data.HomeYaw, _data.HomePitch, StereoCamera.DefaultDistance - _data.PopOut, animated);
        }

        private void OnStructureEntered(AnatomyStructure structure) {
            HoverStructure(structure.StructureId);
        }

        private void OnStructureExited(AnatomyStructure structure) {
            UnhoverStructure(structure.StructureId);
        }

        private void OnStructureClicked(AnatomyStructure structure) {
            ToggleStructure(structure.StructureId);
        }

        private void OnMarkerEntered(string structureId) {
            HoverStructure(structureId);
        }

        private void OnMarkerExited(string structureId) {
            UnhoverStructure(structureId);
        }

        private void OnMarkerClicked(string structureId) {
            ToggleStructure(structureId);
        }

        private void HoverStructure(string structureId) {
            if (_explorer == null) {
                return;
            }

            _explorer.Hover(structureId);
            if (sound != null) {
                sound.PlayHover();
            }

            if (haptics != null) {
                haptics.Tick();
            }
        }

        private void UnhoverStructure(string structureId) {
            if (_explorer != null) {
                _explorer.Unhover(structureId);
            }
        }

        /// <summary>
        /// Picks the structure, or puts it down again. A structure that opens a topic is not put down: pressing it again is
        /// how it is explored.
        /// </summary>
        private void ToggleStructure(string structureId) {
            if (_explorer == null) {
                return;
            }

            AnatomyStructureInfo info = _data.FindStructure(structureId);
            if (info != null && info.OpensTopic && _explorer.SelectedId == structureId) {
                RequestTopic(info.Topic);
                return;
            }

            _explorer.ToggleSelect(structureId);
            if (sound != null) {
                sound.PlaySelect();
            }

            if (haptics != null) {
                haptics.Grab();
            }
        }

        private void OnLinkRequested(int index) {
            if (_data != null && index >= 0 && index < _data.Links.Count) {
                RequestTopic(_data.Links[index].Topic);
            }
        }

        private void OnExploreRequested() {
            string target = ExploreTarget();
            if (target != null) {
                RequestTopic(target);
            }
        }

        /// <summary>
        /// The topic Explore would open: the picked structure's, or during a tour the first structure of the step that opens
        /// one. Null when there is none.
        /// </summary>
        private string ExploreTarget() {
            if (_explorer == null) {
                return null;
            }

            if (_explorer.IsTouring) {
                string[] ids = _explorer.CurrentStep.Structures;
                for (int i = 0; i < ids.Length; i++) {
                    AnatomyStructureInfo stepInfo = _data.FindStructure(ids[i]);
                    if (stepInfo != null && stepInfo.OpensTopic) {
                        return stepInfo.Topic;
                    }
                }

                return null;
            }

            AnatomyStructureInfo info = _explorer.SelectedId != null ? _data.FindStructure(_explorer.SelectedId) : null;
            return info != null && info.OpensTopic ? info.Topic : null;
        }

        /// <summary>Asks for a topic to be opened, or opens it at once when nobody is listening.</summary>
        private void RequestTopic(string topicId) {
            PlaySelectCue();
            if (TopicRequested != null) {
                TopicRequested(topicId);
            } else {
                LoadTopic(topicId);
            }
        }

        private void OnTourToggled() {
            if (_explorer.IsTouring) {
                _explorer.StopTour();
            } else {
                _explorer.StartTour();
            }
        }

        private void OnExplodeToggled() {
            if (_explorer == null) {
                return;
            }

            if (_action != null) {
                _action.PerformAction();
                PlaySelectCue();
                return;
            }

            _explorer.ToggleExplode();
            PlaySelectCue();
        }

        /// <summary>During a tour, back a step (or out of the tour from the first); otherwise the previous numbered structure.</summary>
        private void OnPreviousRequested() {
            if (_explorer == null) {
                return;
            }

            if (!_explorer.IsTouring) {
                _explorer.SelectPrevious();
                PlaySelectCue();
            } else if (_explorer.TourIndex == 0) {
                _explorer.StopTour();
            } else {
                _explorer.PreviousStep();
            }
        }

        /// <summary>During a tour, on a step; otherwise the next numbered structure.</summary>
        private void OnNextRequested() {
            if (_explorer == null) {
                return;
            }

            if (_explorer.IsTouring) {
                _explorer.NextStep();
            } else {
                _explorer.SelectNext();
                PlaySelectCue();
            }
        }

        private void OnZoomInRequested() {
            if (zoom != null) {
                zoom.ZoomIn();
            }
        }

        private void OnZoomOutRequested() {
            if (zoom != null) {
                zoom.ZoomOut();
            }
        }

        private void OnZoomChanged() {
            if (controls != null && zoom != null) {
                controls.ShowZoom(zoom.TargetZoom, zoom.CanZoomIn, zoom.CanZoomOut);
            }
        }

        private void OnResetRequested() {
            if (viewer != null) {
                viewer.RequestReset();
            }
        }

        private void PlaySelectCue() {
            if (sound != null) {
                sound.PlaySelect();
            }
        }

        /// <summary>Redraws everything that depends on the explorer's state.</summary>
        private void Refresh() {
            foreach (KeyValuePair<string, StructureHighlight> pair in _highlights) {
                pair.Value.SetState(_explorer.StateOf(pair.Key));
            }

            if (markers != null) {
                markers.Refresh(_explorer);
            }

            ShowCaption();
            UpdateFocus();
            if (_exploder != null) {
                _exploder.SetExploded(_explorer.IsExploded);
            }

            if (controls != null) {
                controls.ShowExplore(ExploreTarget() != null);
                controls.ShowHome(!IsOnHub);
                controls.ShowTour(_explorer.HasTour, _explorer.IsTouring);
                if (_action != null) {
                    controls.ShowExplode(true, false, _action.ActionLabel, _action.ActionLabel);
                } else {
                    controls.ShowExplode(_explorer.CanExplode, _explorer.IsExploded, _data.ExplodeLabel, _data.AssembleLabel);
                }

                controls.ShowNavigation(!_data.IsStationary, !_data.IsStationary);
                controls.ShowLinks(_data.Links);
                controls.ShowResetLabel(_data.ResetLabel);
            }

            if (_explorer.IsTouring && _explorer.TourIndex != _shownTourIndex) {
                EnterTourStep();
            }

            _shownTourIndex = _explorer.TourIndex;
        }

        /// <summary>A new tour step was reached: a cue, and the view and zoom move where the step asks them to.</summary>
        private void EnterTourStep() {
            if (sound != null) {
                sound.PlaySelect();
            }

            AnatomyTourStep step = _explorer.CurrentStep;
            if (step.SetsView && viewer != null) {
                viewer.FlyTo(step.Yaw, step.Pitch, StereoCamera.DefaultDistance - _data.PopOut, true);
            }

            if (step.SetsZoom && zoom != null) {
                zoom.ZoomTo(step.Zoom);
            }
        }

        /// <summary>
        /// Points the zoom at what is being explained, so zooming in closes on it and not on the middle of the model, and
        /// tells the topic's behaviours what it is.
        /// </summary>
        private void UpdateFocus() {
            _focusScratch.Clear();
            _focusIds.Clear();
            if (_explorer.IsTouring) {
                string[] ids = _explorer.CurrentStep.Structures;
                for (int i = 0; i < ids.Length; i++) {
                    _focusIds.Add(ids[i]);
                    AnatomyStructure structure;
                    if (_structures.TryGetValue(ids[i], out structure)) {
                        _focusScratch.Add(structure.transform);
                    }
                }
            } else if (_explorer.SelectedId != null) {
                _focusIds.Add(_explorer.SelectedId);
                AnatomyStructure selected;
                if (_structures.TryGetValue(_explorer.SelectedId, out selected)) {
                    _focusScratch.Add(selected.transform);
                }
            }

            if (zoom != null) {
                if (_focusScratch.Count > 0) {
                    zoom.FocusOn(_focusScratch);
                } else {
                    zoom.ClearFocus();
                }
            }

            for (int i = 0; i < _focusListeners.Length; i++) {
                _focusListeners[i].OnFocusChanged(_focusIds);
            }
        }

        private void ShowCaption() {
            if (infoPanel == null) {
                return;
            }

            if (_narrator != null && _narrator.Message != null) {
                infoPanel.ShowHeader(_data);
                infoPanel.ShowMessage(_narrator.Message);
                return;
            }

            if (_explorer.IsTouring) {
                infoPanel.ShowTourStep(_explorer.TourIndex, _explorer.TourLength, _explorer.CurrentStep.Caption);
                return;
            }

            string infoId = _explorer.InfoId;
            if (infoId == null) {
                infoPanel.ShowTopic(_data);
                return;
            }

            AnatomyStructureInfo info = _data.FindStructure(infoId);
            if (info != null) {
                int number = _explorer.NumberOf(infoId);
                infoPanel.ShowStructure(infoId, number > 0 ? number + ". " + info.Name : info.Name, info.Summary, info.Fact);
                return;
            }

            infoPanel.ShowStructure(infoId, _structures[infoId].FallbackName, string.Empty, string.Empty);
        }
    }
}