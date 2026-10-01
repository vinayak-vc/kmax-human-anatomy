using System;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// The state of one visit to a topic: what the pointer is on, what has been picked, and where the
    /// guided tour is. Plain C# with no Unity types, so it can be reasoned about and tested on its own;
    /// everything that draws or speaks reacts to <see cref="Changed"/>.
    ///
    /// <para>Picking a structure and running the tour are mutually exclusive, because both decide what the
    /// viewer is being asked to look at.</para>
    /// </summary>
    public class AnatomyExplorer {
        private const int NoTour = -1;

        private readonly AnatomyTopicData _data;

        public AnatomyExplorer(AnatomyTopicData data) {
            _data = data;
            TourIndex = NoTour;
        }

        /// <summary>Raised after any change to hover, selection or tour.</summary>
        public event Action Changed;

        public string HoveredId { get; private set; }
        public string SelectedId { get; private set; }
        public int TourIndex { get; private set; }

        /// <summary>True while the structures are pulled apart. Independent of picking and touring.</summary>
        public bool IsExploded { get; private set; }

        public bool CanExplode {
            get { return _data.CanExplode; }
        }

        public bool IsTouring {
            get { return TourIndex != NoTour; }
        }

        public bool HasTour {
            get { return _data.Tour.Count > 0; }
        }

        /// <summary>How many structures carry a number: those the topic describes, less any it leaves unnumbered.</summary>
        public int StructureCount {
            get {
                int count = 0;
                for (int i = 0; i < _data.Structures.Count; i++) {
                    if (_data.Structures[i].IsNumbered) {
                        count++;
                    }
                }

                return count;
            }
        }

        public int TourLength {
            get { return _data.Tour.Count; }
        }

        public AnatomyTourStep CurrentStep {
            get { return IsTouring ? _data.Tour[TourIndex] : null; }
        }

        /// <summary>True while something is being explained, so everything else should recede.</summary>
        public bool HasFocus {
            get { return SelectedId != null || IsTouring; }
        }

        /// <summary>The structure whose text belongs on screen: the picked one, else the hovered one.</summary>
        public string InfoId {
            get { return SelectedId != null ? SelectedId : HoveredId; }
        }

        public void Hover(string structureId) {
            if (HoveredId == structureId) {
                return;
            }

            HoveredId = structureId;
            RaiseChanged();
        }

        /// <summary>Clears the hover, but only if it is still on that structure: enter and exit can cross.</summary>
        public void Unhover(string structureId) {
            if (HoveredId != structureId) {
                return;
            }

            HoveredId = null;
            RaiseChanged();
        }

        /// <summary>Picks the structure, or puts it down again if it was already picked.</summary>
        public void ToggleSelect(string structureId) {
            TourIndex = NoTour;
            SelectedId = SelectedId == structureId ? null : structureId;
            MagnifySelection();
            RaiseChanged();
        }

        /// <summary>
        /// The number the structure carries on screen, counting from 1 in the order the topic lists its structures, or 0
        /// when the topic has no text for it or describes it without a number.
        /// </summary>
        public int NumberOf(string structureId) {
            int number = 0;
            for (int i = 0; i < _data.Structures.Count; i++) {
                AnatomyStructureInfo info = _data.Structures[i];
                if (info.IsNumbered) {
                    number++;
                }

                if (info.Id == structureId) {
                    return info.IsNumbered ? number : 0;
                }
            }

            return 0;
        }

        /// <summary>Picks the next structure in the topic's order, starting from the first and wrapping after the last.</summary>
        public void SelectNext() {
            StepSelection(1);
        }

        /// <summary>Picks the previous structure in the topic's order, starting from the last and wrapping before the first.</summary>
        public void SelectPrevious() {
            StepSelection(-1);
        }

        /// <summary>Pulls the structures apart, or puts them back together. Does nothing for a topic that cannot explode.</summary>
        public void ToggleExplode() {
            if (!CanExplode) {
                return;
            }

            IsExploded = !IsExploded;
            RaiseChanged();
        }

        public void StartTour() {
            if (!HasTour) {
                return;
            }

            SelectedId = null;
            TourIndex = 0;
            ApplyStepArrangement();
            RaiseChanged();
        }

        /// <summary>Moves on, starting the tour if it is not running, and ending it after the last step.</summary>
        public void NextStep() {
            if (!HasTour) {
                return;
            }

            if (!IsTouring) {
                StartTour();
                return;
            }

            TourIndex = TourIndex + 1 < TourLength ? TourIndex + 1 : NoTour;
            ApplyStepArrangement();
            RaiseChanged();
        }

        public void PreviousStep() {
            if (!IsTouring || TourIndex == 0) {
                return;
            }

            TourIndex--;
            ApplyStepArrangement();
            RaiseChanged();
        }

        public void StopTour() {
            if (!IsTouring) {
                return;
            }

            TourIndex = NoTour;
            RaiseChanged();
        }

        /// <summary>Back to the opening state: nothing hovered, picked or touring, and put back together.</summary>
        public void Reset() {
            HoveredId = null;
            SelectedId = null;
            TourIndex = NoTour;
            IsExploded = false;
            RaiseChanged();
        }

        /// <summary>How the structure should be drawn right now.</summary>
        public HighlightState StateOf(string structureId) {
            bool hovered = HoveredId == structureId;
            if (!HasFocus) {
                if (IsExploded && RecedesWhenExploded(structureId)) {
                    return hovered ? HighlightState.Previewed : HighlightState.Dimmed;
                }

                return hovered ? HighlightState.Hovered : HighlightState.Normal;
            }

            if (IsFocused(structureId)) {
                return HighlightState.Focused;
            }

            return hovered ? HighlightState.Previewed : HighlightState.Dimmed;
        }

        private void StepSelection(int direction) {
            int count = StructureCount;
            if (count == 0) {
                return;
            }

            int current = SelectedId != null ? NumberOf(SelectedId) - 1 : -1;
            int next;
            if (current < 0) {
                next = direction > 0 ? 0 : count - 1;
            } else {
                next = (current + direction + count) % count;
            }

            TourIndex = NoTour;
            SelectedId = IdOfNumber(next + 1);
            MagnifySelection();
            RaiseChanged();
        }

        /// <summary>The id of the structure that carries this number, counting from 1.</summary>
        private string IdOfNumber(int number) {
            int counted = 0;
            for (int i = 0; i < _data.Structures.Count; i++) {
                if (_data.Structures[i].IsNumbered) {
                    counted++;
                    if (counted == number) {
                        return _data.Structures[i].Id;
                    }
                }
            }

            return null;
        }

        /// <summary>Picking a structure that is too small to see at its true size explodes the view, which enlarges it.</summary>
        private void MagnifySelection() {
            if (SelectedId == null || IsExploded || !CanExplode) {
                return;
            }

            AnatomyStructureInfo info = _data.FindStructure(SelectedId);
            if (info != null && info.Magnifies) {
                IsExploded = true;
            }
        }

        /// <summary>A tour step may pull the model apart or put it back together as it is reached.</summary>
        private void ApplyStepArrangement() {
            AnatomyTourStep step = CurrentStep;
            if (step == null) {
                return;
            }

            if (step.Explode == TourExplodeChange.Apart) {
                IsExploded = CanExplode;
            } else if (step.Explode == TourExplodeChange.Together) {
                IsExploded = false;
            }
        }

        private bool RecedesWhenExploded(string structureId) {
            AnatomyStructureInfo info = _data.FindStructure(structureId);
            return info != null && info.RecedesWhenExploded;
        }

        private bool IsFocused(string structureId) {
            if (SelectedId == structureId) {
                return true;
            }

            AnatomyTourStep step = CurrentStep;
            return step != null && step.Includes(structureId);
        }

        private void RaiseChanged() {
            if (Changed != null) {
                Changed();
            }
        }
    }
}