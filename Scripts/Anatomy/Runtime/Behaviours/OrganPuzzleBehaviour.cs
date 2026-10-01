using System;
using System.Collections.Generic;

using KmaxXR;

using UnityEngine;

using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Put the organs back. The torso opens with every organ in its place; a moment later the organs are thrown out
    /// to float in front of the glass, each leaving a faint outline of itself where it belongs. The visitor touches one with
    /// the pen, holds the button, carries it to its outline and lets go: close enough, and it settles home with a chime and
    /// an info card. When the last is back the whole thing celebrates and offers another round.
    ///
    /// <para>The pen does the carrying (<see cref="StylusGrab"/> and <see cref="Grabbable"/>), so the puzzle only reacts:
    /// to an organ being taken up, let go, or touched. It owns everything the visitor reads, through
    /// <see cref="ITopicNarrator"/>: the organ in the hand, the organ just placed, the hint, how many are back.</para>
    ///
    /// <para>An organ must be let go within a distance of its home that grows with its size, and a depth error counts for
    /// less than one across the screen, because depth is the hardest thing to judge in the air. Pairs, the lungs and the
    /// kidneys, each settle only in their own place. A hint is offered by a button, and given once by itself if nothing
    /// has happened for a while.</para>
    /// </summary>
    public class OrganPuzzleBehaviour : MonoBehaviour, ITopicNarrator, ITopicAction, IResetListener {
        private const float ScatterStagger = 0.07f;
        private const float WaveSeconds = 1.2f;
        private const float WaveStagger = 0.1f;
        private const float TouchGlow = 0.3f;
        private const float HintGlowBase = 0.25f;
        private const float HintGlowSwing = 0.2f;
        private const float HintGlowRate = 6f;
        private const float HintOutline = 0.95f;
        private const float RadiusShare = 0.85f;

        [SerializeField, Tooltip("How far in front of the glass the loose organs float, in metres. The mouse stands in for the pen at about this depth.")]
        private float scatterDepth = -0.05f;
        [SerializeField, Tooltip("Room left between one loose organ and the next, in metres.")]
        private float scatterGap = 0.008f;
        [SerializeField, Tooltip("Seconds the torso is shown whole before the organs are thrown out.")]
        private float introSeconds = 1.1f;
        [SerializeField, Tooltip("Seconds an organ takes to fly to its place among the loose ones.")]
        private float scatterSeconds = 1.1f;
        [SerializeField, Tooltip("Seconds an organ takes to settle into its home.")]
        private float settleSeconds = 0.3f;
        [SerializeField, Range(0.1f, 1f), Tooltip("How close to its home an organ must be let go, as a share of its own size.")]
        private float snapFraction = 0.5f;
        [SerializeField, Tooltip("The least distance an organ is given to settle from, in metres, however small it is.")]
        private float minimumSnap = 0.02f;
        [SerializeField, Range(0f, 1f), Tooltip("How much a depth error counts against an error across the screen. Depth is hard to judge in the air.")]
        private float depthWeight = 0.3f;
        [SerializeField, Tooltip("Seconds without anything happening before a hint is given by itself.")]
        private float hintAfterSeconds = 25f;
        [SerializeField, Tooltip("Seconds a hint is shown for.")]
        private float hintSeconds = 4f;
        [SerializeField, Tooltip("Seconds the card of an organ just put back stays up.")]
        private float placedCardSeconds = 6f;
        [SerializeField, Range(0f, 1f), Tooltip("How bright the outline of a loose organ's home is.")]
        private float outlineBrightness = 0.16f;
        [SerializeField, Range(0f, 1f), Tooltip("How bright the outline of the home of the organ in the hand is.")]
        private float heldOutlineBrightness = 0.8f;
        [SerializeField, Tooltip("Lowest and highest a loose organ may appear on the glass, in metres from the middle of the screen: " +
            "clear of the caption below and of the title above.")]
        private Vector2 verticalRange = new Vector2(-0.09f, 0.098f);
        [SerializeField, Tooltip("Room kept between a loose organ and the sides of the window, in metres.")]
        private float edgeMargin = 0.012f;
        [SerializeField, Tooltip("Room kept round the organs' own places, in metres. The bones of the frame fade out at the shoulders, " +
            "and a loose organ may float over their faint ends.")]
        private float torsoMargin = 0.016f;
        [SerializeField, Tooltip("Rectangles on the glass, in metres from the middle of the screen, where no organ may appear: the buttons.")]
        private Rect[] keepClear = new Rect[] { new Rect(0.15f, 0.02f, 0.14f, 0.13f) };
        [SerializeField] private string hintLabel = "Hint";
        [SerializeField] private string playAgainLabel = "Play again";
        [SerializeField] private string completeHeading = "Every organ is back";
        [SerializeField] private string completeBody = "The body is whole again. Touch an organ to read about it, or press Play again for another round.";
        [SerializeField, Tooltip("How many are back. {0} is the number in place and {1} the number of organs.")]
        private string progressFormat = "{0} of {1} in place";

        private readonly List<OrganPiece> _pieces = new List<OrganPiece>();
        private readonly List<Vector2> _halfSizes = new List<Vector2>();
        private AnatomyTopicData _data;
        private AnatomyAudio _sound;
        private StylusHaptics _haptics;
        private StylusTip _tip;
        private Rect _torso;
        private TopicMessage _message;
        private OrganPiece _held;
        private OrganPiece _touched;
        private OrganPiece _lastPlaced;
        private OrganPiece _hint;
        private OrganPiece _spokenPiece;
        private Phase _phase;
        private Say _spoken;
        private int _spokenPlaced = -1;
        private int _placed;
        private float _phaseClock;
        private float _idleClock;
        private float _hintClock;
        private float _placedClock;

        private enum Phase {
            Intro,
            Scattering,
            Playing,
            Complete
        }

        private enum Say {
            Idle,
            Held,
            Placed,
            Hint,
            Touched,
            Complete
        }

        public event Action MessageChanged;
        public event Action ActionChanged;

        public TopicMessage Message {
            get { return _message; }
        }

        public string ActionLabel {
            get { return _phase == Phase.Complete ? playAgainLabel : hintLabel; }
        }

        /// <summary>How many organs are home.</summary>
        public int PlacedCount {
            get { return _placed; }
        }

        public int PieceCount {
            get { return _pieces.Count; }
        }

        public bool IsComplete {
            get { return _phase == Phase.Complete; }
        }

        /// <summary>True once the organs have been thrown out and the pen may take them.</summary>
        public bool IsPlayable {
            get { return _phase == Phase.Playing; }
        }

        /// <summary>Takes charge of the torso's organs, which the topic has just loaded in their places.</summary>
        public void Begin(AnatomyBehaviourContext context) {
            _data = context.Data;
            _sound = context.Sound;
            _haptics = context.Haptics;
            _tip = context.Tip;

            AnatomyModel model = GetComponent<AnatomyModel>();
            Bounds torso = new Bounds(transform.position, Vector3.zero);
            for (int i = 0; i < model.Structures.Count; i++) {
                AnatomyStructure structure = model.Structures[i];
                if (structure.GetComponent<Grabbable>() == null) {
                    continue;
                }

                Bounds bounds = structure.StructureRenderer.bounds;
                if (_pieces.Count == 0) {
                    torso = bounds;
                } else {
                    torso.Encapsulate(bounds);
                }

                _halfSizes.Add(new Vector2(bounds.extents.x, bounds.extents.y));
                _pieces.Add(Adopt(structure));
            }

            _torso = GlassRectOf(torso);
            if (_tip != null) {
                _tip.TouchBegan += OnTipTouchBegan;
                _tip.TouchEnded += OnTipTouchEnded;
            }

            _phase = Phase.Intro;
            RefreshMessage();
        }

        /// <summary>The hint button: a hint, or, when the puzzle is done, another round.</summary>
        public void PerformAction() {
            if (_phase == Phase.Complete) {
                Restart();
            } else if (_phase == Phase.Playing) {
                ShowHint();
            }
        }

        /// <summary>Reset starts the round again.</summary>
        public void OnResetRequested() {
            if (_phase != Phase.Intro) {
                Restart();
            }
        }

        private void Update() {
            UpdatePhase();
            UpdateOutlines();
            UpdateHint();
            RefreshMessage();
        }

        private void OnDestroy() {
            if (_tip != null) {
                _tip.TouchBegan -= OnTipTouchBegan;
                _tip.TouchEnded -= OnTipTouchEnded;
            }
        }

        private OrganPiece Adopt(AnatomyStructure structure) {
            Transform outline = transform.Find("slot_" + structure.StructureId);
            Renderer slot = outline != null ? outline.GetComponent<Renderer>() : null;
            float radius = ScreenRadius(structure.StructureRenderer.bounds);
            OrganPiece piece = structure.gameObject.AddComponent<OrganPiece>();
            piece.Begin(structure.StructureId, slot, radius, Mathf.Max(minimumSnap, radius * snapFraction));
            piece.Grabbed += OnPieceGrabbed;
            piece.Released += OnPieceReleased;
            piece.Lock(true);
            return piece;
        }

        private void UpdatePhase() {
            _phaseClock += Time.deltaTime;
            switch (_phase) {
                case Phase.Intro:
                    if (_phaseClock >= introSeconds) {
                        ScatterAll();
                        EnterPhase(Phase.Scattering);
                    }

                    break;
                case Phase.Scattering:
                    if (_phaseClock >= scatterSeconds + ScatterStagger * _pieces.Count) {
                        UnlockLoose();
                        _idleClock = 0f;
                        EnterPhase(Phase.Playing);
                    }

                    break;
                case Phase.Playing:
                    _idleClock += Time.deltaTime;
                    if (_idleClock >= hintAfterSeconds && _hint == null && _held == null) {
                        ShowHint();
                    }

                    break;
                case Phase.Complete:
                    PassWave();
                    break;
            }
        }

        private void EnterPhase(Phase phase) {
            _phase = phase;
            _phaseClock = 0f;
        }

        /// <summary>
        /// Throws every organ that is not in the hand out to a place of its own, one after another. The places are worked out
        /// as they will appear on the glass, where the window, the title, the caption and the buttons are, and then moved back
        /// to the depth the organs float at: something in front of the glass looks larger and further from the middle of
        /// the screen than it is.
        /// </summary>
        private void ScatterAll() {
            float magnification = Magnification(scatterDepth);
            Vector2[] halfSizes = new Vector2[_pieces.Count];
            for (int i = 0; i < halfSizes.Length; i++) {
                halfSizes[i] = _halfSizes[i] * magnification;
            }

            Vector2 window = StereoVolume.IsReady ? StereoVolume.Window : new Vector2(0.5977f, 0.3362f);
            float halfWidth = window.x * 0.5f - edgeMargin;
            Rect area = new Rect(-halfWidth, verticalRange.x, 2f * halfWidth, verticalRange.y - verticalRange.x);
            List<Rect> clear = new List<Rect>(keepClear);
            clear.Add(new Rect(_torso.xMin - torsoMargin, _torso.yMin - torsoMargin, _torso.width + 2f * torsoMargin, _torso.height + 2f * torsoMargin));

            Vector2[] spots = OrganScatter.Place(halfSizes, area, clear, scatterGap, UnityEngine.Random.Range(0, int.MaxValue));
            for (int i = 0; i < _pieces.Count; i++) {
                if (_pieces[i].IsHeld) {
                    continue;
                }

                Vector3 world = StereoVolume.ToWorld(new Vector3(spots[i].x / magnification, spots[i].y / magnification, scatterDepth));
                _pieces[i].Throw(world, scatterSeconds, i * ScatterStagger);
            }
        }

        private void UnlockLoose() {
            for (int i = 0; i < _pieces.Count; i++) {
                if (!_pieces[i].IsPlaced) {
                    _pieces[i].Lock(false);
                }
            }
        }

        /// <summary>Starts another round: every organ, wherever it is, is thrown out again.</summary>
        private void Restart() {
            _placed = 0;
            _held = null;
            _lastPlaced = null;
            ClearHint();
            for (int i = 0; i < _pieces.Count; i++) {
                _pieces[i].SetWaveGlow(0f);
            }

            ScatterAll();
            EnterPhase(Phase.Scattering);
            RaiseActionChanged();
        }

        private void OnPieceGrabbed(OrganPiece piece) {
            _held = piece;
            _idleClock = 0f;
            if (_hint == piece) {
                ClearHint();
            }

            if (_sound != null) {
                _sound.PlayHover();
            }
        }

        private void OnPieceReleased(OrganPiece piece) {
            if (_held == piece) {
                _held = null;
            }

            _idleClock = 0f;
            if (_phase == Phase.Playing && !piece.IsPlaced && SnapDistance(piece) <= piece.SnapRadius) {
                Place(piece);
            }
        }

        private void Place(OrganPiece piece) {
            _placed++;
            _lastPlaced = piece;
            _placedClock = 0f;
            if (_hint == piece) {
                ClearHint();
            }

            piece.SettleHome(settleSeconds);
            if (_sound != null) {
                _sound.PlayPlaced(_placed);
            }

            if (_haptics != null) {
                _haptics.Success();
            }

            if (_placed >= _pieces.Count) {
                EnterPhase(Phase.Complete);
                if (_sound != null) {
                    _sound.PlayComplete();
                }

                RaiseActionChanged();
            }
        }

        /// <summary>
        /// How far the organ is from its home, with an error in depth counting for less than one across the screen: the part of
        /// the distance along the screen's axis is scaled down.
        /// </summary>
        private float SnapDistance(OrganPiece piece) {
            Vector3 away = piece.transform.position - piece.HomePosition;
            Vector3 axis = StereoVolume.IsReady ? StereoVolume.ScreenTransform.forward : Vector3.forward;
            float depth = Vector3.Dot(away, axis);
            Vector3 across = away - depth * axis;
            return Mathf.Sqrt(across.sqrMagnitude + depthWeight * depthWeight * depth * depth);
        }

        private void ShowHint() {
            OrganPiece target = null;
            float nearest = float.MaxValue;
            for (int i = 0; i < _pieces.Count; i++) {
                OrganPiece piece = _pieces[i];
                if (piece.IsPlaced || piece.IsHeld || piece.IsGliding) {
                    continue;
                }

                float distance = (piece.transform.position - piece.HomePosition).sqrMagnitude;
                if (distance < nearest) {
                    nearest = distance;
                    target = piece;
                }
            }

            if (target == null) {
                return;
            }

            ClearHint();
            _hint = target;
            _hintClock = hintSeconds;
            _idleClock = 0f;
        }

        private void ClearHint() {
            if (_hint != null) {
                _hint.SetHintGlow(0f);
                _hint = null;
            }

            _hintClock = 0f;
        }

        private void UpdateHint() {
            if (_hint == null) {
                return;
            }

            _hintClock -= Time.deltaTime;
            if (_hintClock <= 0f) {
                ClearHint();
                return;
            }

            _hint.SetHintGlow(HintGlowBase + HintGlowSwing * Mathf.Sin(Time.time * HintGlowRate));
        }

        /// <summary>The outline of each organ's home: faint while it is loose, bright while it is in the hand or hinted at, gone once it is back.</summary>
        private void UpdateOutlines() {
            bool outlinesShown = _phase == Phase.Scattering || _phase == Phase.Playing;
            for (int i = 0; i < _pieces.Count; i++) {
                OrganPiece piece = _pieces[i];
                float brightness = 0f;
                if (outlinesShown && !piece.IsPlaced) {
                    brightness = outlineBrightness;
                    if (piece == _held) {
                        brightness = heldOutlineBrightness;
                    } else if (piece == _hint) {
                        brightness = HintOutline;
                    }
                }

                piece.SetSlotTarget(brightness);
            }
        }

        /// <summary>The finished puzzle's celebration: a glow that passes from organ to organ, once.</summary>
        private void PassWave() {
            for (int i = 0; i < _pieces.Count; i++) {
                float t = _phaseClock - i * WaveStagger;
                float glow = t > 0f && t < WaveSeconds ? 0.6f * Mathf.Sin(Mathf.PI * t / WaveSeconds) : 0f;
                _pieces[i].SetWaveGlow(glow);
            }
        }

        private void OnTipTouchBegan(Collider collider) {
            OrganPiece piece = collider.GetComponentInParent<OrganPiece>();
            if (piece == null) {
                return;
            }

            _touched = piece;
            _idleClock = 0f;
            piece.SetTouchGlow(TouchGlow);
            if (_haptics != null && !piece.IsPlaced) {
                _haptics.Tick();
            }
        }

        private void OnTipTouchEnded(Collider collider) {
            OrganPiece piece = collider != null ? collider.GetComponentInParent<OrganPiece>() : null;
            if (piece == null) {
                return;
            }

            piece.SetTouchGlow(0f);
            if (_touched == piece) {
                _touched = null;
            }
        }

        /// <summary>Works out what the caption should say, and says it only when that has changed.</summary>
        private void RefreshMessage() {
            _placedClock += Time.deltaTime;
            Say say = Say.Idle;
            OrganPiece about = null;
            if (_phase == Phase.Complete) {
                say = Say.Complete;
            } else if (_held != null) {
                say = Say.Held;
                about = _held;
            } else if (_lastPlaced != null && _placedClock < placedCardSeconds) {
                say = Say.Placed;
                about = _lastPlaced;
            } else if (_hint != null) {
                say = Say.Hint;
                about = _hint;
            } else if (_touched != null) {
                say = Say.Touched;
                about = _touched;
            }

            if (_message != null && say == _spoken && about == _spokenPiece && _placed == _spokenPlaced) {
                return;
            }

            _spoken = say;
            _spokenPiece = about;
            _spokenPlaced = _placed;
            _message = BuildMessage(say, about);
            if (MessageChanged != null) {
                MessageChanged();
            }
        }

        private TopicMessage BuildMessage(Say say, OrganPiece about) {
            string progress = string.Format(progressFormat, _placed, _pieces.Count);
            string key = "organs:" + say + ":" + (about != null ? about.OrganId : string.Empty) + ":" + _placed;
            if (say == Say.Complete) {
                return new TopicMessage(key, completeHeading, completeBody, string.Empty, progress);
            }

            if (about == null) {
                return new TopicMessage(key, _data.Title, _data.Intro, string.Empty, progress);
            }

            AnatomyStructureInfo info = _data.FindStructure(about.OrganId);
            string name = info != null ? info.Name : about.OrganId;
            string summary = info != null ? info.Summary : string.Empty;
            string fact = info != null && (say == Say.Placed || about.IsPlaced) ? info.Fact : string.Empty;
            string heading = say == Say.Hint ? "Hint: " + name : name;
            return new TopicMessage(key, heading, summary, fact, progress);
        }

        private void RaiseActionChanged() {
            if (ActionChanged != null) {
                ActionChanged();
            }
        }

        /// <summary>How far an organ reaches from its middle across the screen: a little less than the corner of its bounding box.</summary>
        private static float ScreenRadius(Bounds bounds) {
            return 0.5f * Mathf.Sqrt(bounds.size.x * bounds.size.x + bounds.size.y * bounds.size.y) * RadiusShare;
        }

        /// <summary>
        /// How many times larger, and how many times further from the middle of the screen, something floating at this depth
        /// looks to the viewer than it is. Depth is negative in front of the glass, and the eye is half a metre from it.
        /// </summary>
        private static float Magnification(float depth) {
            float eye = StereoCamera.DefaultDistance;
            return eye / Mathf.Max(0.05f, eye + depth);
        }

        /// <summary>The rectangle a world-space box covers on the glass, in metres from the middle of the screen.</summary>
        private static Rect GlassRectOf(Bounds bounds) {
            Vector2 low = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 high = new Vector2(float.MinValue, float.MinValue);
            for (int corner = 0; corner < 8; corner++) {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                Vector3 volume = StereoVolume.IsReady ? StereoVolume.ToVolumeSpace(point) : point;
                float scale = Magnification(volume.z);
                low = Vector2.Min(low, new Vector2(volume.x, volume.y) * scale);
                high = Vector2.Max(high, new Vector2(volume.x, volume.y) * scale);
            }

            return new Rect(low.x, low.y, high.x - low.x, high.y - low.y);
        }
    }
}