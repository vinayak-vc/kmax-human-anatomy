using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// The pen as an instrument. The torso is drawn solid, in layers, and the tip of the pen cuts into it: everything between
    /// the viewer and the tip, within a lens round it, is cut away, so pushing the pen deeper peels back the muscle, then the
    /// ribs and vessels, then reaches the organs. A second instrument cuts the whole body at the depth of the tip, like a
    /// scan slice.
    ///
    /// <para>The cutting is done by the Section shader (see <c>AnatomySection.shader</c>), which every surface of the figure uses,
    /// so it is clipping and not transparency: what is cut is gone and what is behind it is solid. This only tells the shader
    /// where the cut is, once a frame, through global shader values, and does the rest: finds the organ the tip is inside or
    /// looking at, lights it, and says what it is and how deep the tip has gone.</para>
    ///
    /// <para>The lens is at the tip, in the room's space, and the cut runs along the screen's axis, so it looks the same from both
    /// eyes. The pen's orientation does not matter; with a mouse the lens follows the mouse, at the depth the wheel sets.</para>
    /// </summary>
    public class ScanBehaviour : MonoBehaviour, ITopicNarrator, ITopicAction, IResetListener {
        private const int RingSegments = 64;
        private const float LineWidth = 0.0006f;
        private const float OrganGlow = 0.3f;
        private const float ReachBehind = 0.3f;
        private const int SurfacePoints = 1500;
        private const float CentimetresPerMetre = 100f;

        private static readonly int LensId = Shader.PropertyToID("_KmaxLens");
        private static readonly int LensAxisId = Shader.PropertyToID("_KmaxLensAxis");
        private static readonly int LensColourId = Shader.PropertyToID("_KmaxLensColor");
        private static readonly int BoxMinId = Shader.PropertyToID("_KmaxBoxMin");
        private static readonly int BoxMaxId = Shader.PropertyToID("_KmaxBoxMax");
        private static readonly int FresnelId = Shader.PropertyToID("_Fresnel");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int DestinationBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int DepthTestId = Shader.PropertyToID("_ZTest");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("Radius of the lens, in metres, at the depth of the pen's tip.")]
        private float lensRadius = 0.022f;
        [SerializeField, Tooltip("Radius of the slice, in metres: large enough to cut the whole body.")]
        private float sliceRadius = 10f;
        [SerializeField, Tooltip("Width of the glowing ring where the lens meets a surface, in metres.")]
        private float ringWidth = 0.004f;
        [SerializeField, Tooltip("Thickness of the bright disc at the scan plane, in metres.")]
        private float planeThickness = 0.0015f;
        [SerializeField, Tooltip("How near the line of sight an organ's surface must be for the organ to count as in view, in metres.")]
        private float lookRadius = 0.004f;
        [SerializeField, Tooltip("Organs too thin for the tip to be inside of, ducts and tubes and sheets, which the tip is at instead.")]
        private string[] thinOrgans = new string[] { "diaphragm", "gall_bladder", "windpipe", "esophagus", "pancreas" };
        [SerializeField, Tooltip("How near a thin organ's surface the tip must be to be at it, in metres.")]
        private float touchRadius = 0.003f;
        [SerializeField, Tooltip("Colour of the scanner's glow.")]
        private Color glow = new Color(0.35f, 0.85f, 1f, 1f);
        [SerializeField, Tooltip("How far the lens's outline reaches towards the viewer from the tip, in metres.")]
        private float outlineLength = 0.03f;
        [SerializeField] private string sliceLabel = "Slice the body";
        [SerializeField] private string lensLabel = "Use the lens";
        [SerializeField] private string tissueHeading = "Scanning";
        [SerializeField] private string tissueBody = "Only muscle, bone and blood vessels here. Move the pen towards an organ, or push it deeper.";
        [SerializeField, Tooltip("How deep the pen has gone. {0} is the depth into the body, in centimetres, at the body's real size.")]
        private string depthFormat = "Depth {0:0.0} cm";

        private readonly List<AnatomyStructure> _organs = new List<AnatomyStructure>();
        private OrganProbe _probe;
        private readonly Vector3[] _ring = new Vector3[RingSegments];
        private AnatomyTopicData _data;
        private AnatomyAudio _sound;
        private StylusHaptics _haptics;
        private StylusTip _tip;
        private AnatomyStructure _organ;
        private Material _lineMaterial;
        private LineRenderer _tipRing;
        private LineRenderer _nearRing;
        private LineRenderer[] _edges;
        private TopicMessage _message;
        private Bounds _figure;
        private float _frontDepth;
        private float _backDepth;
        private float _scale = 1f;
        private float _mouseScanFraction = 0.35f;
        private bool _slicing;
        private Say _spoken = Say.Outside;
        private string _spokenOrgan;
        private int _spokenTenths = -2;

        private enum Say {
            Outside,
            Tissue,
            Organ
        }

        public event Action MessageChanged;
        public event Action ActionChanged;

        public TopicMessage Message {
            get { return _message; }
        }

        /// <summary>What the instrument button offers: the other instrument.</summary>
        public string ActionLabel {
            get { return _slicing ? lensLabel : sliceLabel; }
        }

        /// <summary>True while the whole body is being cut at the tip's depth and not just a lens of it.</summary>
        public bool IsSlicing {
            get { return _slicing; }
        }

        /// <summary>The organ the tip is inside, or looking at, or null.</summary>
        public AnatomyStructure CurrentOrgan {
            get { return _organ; }
        }

        /// <summary>Takes charge of the scan figure, which the topic has just loaded.</summary>
        public void Begin(AnatomyBehaviourContext context) {
            _data = context.Data;
            _sound = context.Sound;
            _haptics = context.Haptics;
            _tip = context.Tip;
            _scale = Mathf.Max(0.0001f, transform.lossyScale.x);

            AnatomyModel model = GetComponent<AnatomyModel>();
            Material rim = null;
            for (int i = 0; i < model.Structures.Count; i++) {
                _organs.Add(model.Structures[i]);
                rim = rim != null ? rim : model.Structures[i].RimMaterial;
            }

            _probe = new OrganProbe(_organs, SurfacePoints, thinOrgans, touchRadius);
            AnatomyCutBox cutBox = GetComponent<AnatomyCutBox>();
            if (cutBox != null) {
                PushBox(cutBox);
            }

            MeasureFigure();
            CreateOutline(rim);
            _message = BuildMessage(Say.Outside, null, -1f);
        }

        /// <summary>The instrument button: the other instrument.</summary>
        public void PerformAction() {
            _slicing = !_slicing;
            if (_tipRing != null) {
                SetOutlineShown(!_slicing);
            }

            if (ActionChanged != null) {
                ActionChanged();
            }
        }

        /// <summary>Reset puts the lens back in the pen's hand.</summary>
        public void OnResetRequested() {
            if (_slicing) {
                PerformAction();
            }
        }

        private void Update() {
            if (!StereoVolume.IsReady) {
                SwitchOff();
                return;
            }

            Vector3 point;
            Transform screen = StereoVolume.ScreenTransform;
            if (_tip != null && _tip.IsTracked) {
                point = _tip.Position;
            } else {
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > Mathf.Epsilon) {
                    _mouseScanFraction = Mathf.Clamp01(_mouseScanFraction + scroll * 0.05f);
                }

                float targetDepth = Mathf.Lerp(_frontDepth + 0.02f, _backDepth, _mouseScanFraction);
                Camera cam = StereoVolume.CenterCamera != null ? StereoVolume.CenterCamera : Camera.main;
                if (cam == null) {
                    SwitchOff();
                    return;
                }

                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                Vector3 planePoint = screen.TransformPoint(new Vector3(0f, 0f, targetDepth));
                Vector3 planeNormal = screen.forward;
                float denom = Vector3.Dot(ray.direction, planeNormal);
                if (Mathf.Abs(denom) < 1e-6f) {
                    SwitchOff();
                    return;
                }

                float dist = Vector3.Dot(planePoint - ray.origin, planeNormal) / denom;
                if (dist <= 0f) {
                    SwitchOff();
                    return;
                }

                point = ray.GetPoint(dist);
            }

            float radius = _slicing ? sliceRadius : lensRadius;
            PushLens(point, screen.forward, radius);
            if (!_slicing) {
                DrawOutline(point, screen, radius);
            }

            AnatomyStructure organ = Identify(point, screen.forward);
            Track(organ);
            Narrate(point, organ);
        }

        private void OnDestroy() {
            SwitchOff();
            if (_organ != null) {
                StructureHighlight highlight = _organ.GetComponent<StructureHighlight>();
                if (highlight != null) {
                    highlight.SetPulse(0f);
                }
            }

            Shader.SetGlobalVector(BoxMinId, Vector4.zero);
            Shader.SetGlobalVector(BoxMaxId, Vector4.zero);
            if (_lineMaterial != null) {
                Destroy(_lineMaterial);
            }
        }

        /// <summary>Tells every Section material where the cut is.</summary>
        private void PushLens(Vector3 point, Vector3 axis, float radius) {
            Shader.SetGlobalVector(LensId, new Vector4(point.x, point.y, point.z, radius));
            Shader.SetGlobalVector(LensAxisId, new Vector4(axis.x, axis.y, axis.z, ringWidth));
            Shader.SetGlobalVector(LensColourId, new Vector4(glow.r, glow.g, glow.b, planeThickness));
        }

        /// <summary>Takes the lens away, so nothing is cut but the box.</summary>
        private void SwitchOff() {
            Shader.SetGlobalVector(LensId, Vector4.zero);
            if (_tipRing != null) {
                SetOutlineShown(false);
            }
        }

        private void PushBox(AnatomyCutBox cutBox) {
            Bounds local = cutBox.LocalBounds;
            Vector3 first = transform.TransformPoint(local.min);
            Vector3 second = transform.TransformPoint(local.max);
            Vector3 low = Vector3.Min(first, second);
            Vector3 high = Vector3.Max(first, second);
            Shader.SetGlobalVector(BoxMinId, new Vector4(low.x, low.y, low.z, 1f));
            Shader.SetGlobalVector(BoxMaxId, new Vector4(high.x, high.y, high.z, 0f));
            _figure = new Bounds((low + high) * 0.5f, high - low);
        }

        /// <summary>How far in front of the glass the figure's nearest surface is, and how far behind its furthest.</summary>
        private void MeasureFigure() {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0 || !StereoVolume.IsReady) {
                return;
            }

            Bounds all = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) {
                all.Encapsulate(renderers[i].bounds);
            }

            _frontDepth = float.MaxValue;
            _backDepth = float.MinValue;
            for (int corner = 0; corner < 8; corner++) {
                Vector3 point = all.center + Vector3.Scale(all.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                float depth = StereoVolume.ToVolumeSpace(point).z;
                _frontDepth = Mathf.Min(_frontDepth, depth);
                _backDepth = Mathf.Max(_backDepth, depth);
            }
        }

        /// <summary>
        /// The organ the pen is working on: the one its tip is inside, else the first one in view down the line the lens cuts
        /// along, which is what shows through the lens.
        /// </summary>
        private AnatomyStructure Identify(Vector3 point, Vector3 axis) {
            int inside = _probe.InsideOf(point);
            if (inside >= 0) {
                return _organs[inside];
            }

            int seen = _probe.FirstAlong(point, axis, lookRadius, ReachBehind);
            return seen >= 0 ? _organs[seen] : null;
        }

        /// <summary>Lights the organ being scanned, and gives the hand and the ear a small cue when the scan reaches a new one.</summary>
        private void Track(AnatomyStructure organ) {
            if (organ == _organ) {
                return;
            }

            SetOrganGlow(_organ, 0f);
            _organ = organ;
            SetOrganGlow(_organ, OrganGlow);
            if (_organ == null) {
                return;
            }

            if (_sound != null) {
                _sound.PlayHover();
            }

            if (_haptics != null) {
                _haptics.Tick();
            }
        }

        private static void SetOrganGlow(AnatomyStructure organ, float glow) {
            if (organ == null) {
                return;
            }

            StructureHighlight highlight = organ.GetComponent<StructureHighlight>();
            if (highlight != null) {
                highlight.SetPulse(glow);
            }
        }

        /// <summary>Works out what the caption should say, and says it only when something it shows has changed.</summary>
        private void Narrate(Vector3 point, AnatomyStructure organ) {
            float centimetres;
            bool inside = IsInsideFigure(point, out centimetres);
            Say say = !inside ? Say.Outside : (organ != null ? Say.Organ : Say.Tissue);
            string organId = organ != null ? organ.StructureId : null;
            int tenths = inside ? Mathf.RoundToInt(centimetres * 10f) : -1;
            if (say == _spoken && organId == _spokenOrgan && tenths == _spokenTenths && _message != null) {
                return;
            }

            _spoken = say;
            _spokenOrgan = organId;
            _spokenTenths = tenths;
            _message = BuildMessage(say, organ, inside ? centimetres : -1f);
            if (MessageChanged != null) {
                MessageChanged();
            }
        }

        private TopicMessage BuildMessage(Say say, AnatomyStructure organ, float centimetres) {
            string progress = centimetres >= 0f ? string.Format(depthFormat, centimetres) : string.Empty;
            string key = "scan:" + say + ":" + (organ != null ? organ.StructureId : string.Empty);
            if (say == Say.Organ) {
                AnatomyStructureInfo info = _data.FindStructure(organ.StructureId);
                string name = info != null ? info.Name : organ.FallbackName;
                return new TopicMessage(key, name, info != null ? info.Summary : string.Empty, info != null ? info.Fact : string.Empty, progress);
            }

            if (say == Say.Tissue) {
                return new TopicMessage(key, tissueHeading, tissueBody, string.Empty, progress);
            }

            return new TopicMessage(key, _data.Title, _data.Intro, string.Empty, string.Empty);
        }

        /// <summary>
        /// Whether the tip is within the figure, across the screen and in depth, and how deep it is into the body, in
        /// centimetres at the body's real size: the model is shown at a fraction of its size, so a depth in the room is scaled back.
        /// </summary>
        private bool IsInsideFigure(Vector3 point, out float centimetres) {
            centimetres = 0f;
            float depth = StereoVolume.ToVolumeSpace(point).z;
            bool acrossScreen = _figure.size.x > 0f
                && Mathf.Abs(point.x - _figure.center.x) <= _figure.extents.x
                && Mathf.Abs(point.y - _figure.center.y) <= _figure.extents.y;
            if (!acrossScreen || depth < _frontDepth || depth > _backDepth) {
                return false;
            }

            centimetres = (depth - _frontDepth) / _scale * CentimetresPerMetre;
            return true;
        }

        /// <summary>The lens's outline: a ring at the tip, another nearer the viewer, and four lines joining them, so the tunnel can be seen in the air.</summary>
        private void CreateOutline(Material rim) {
            if (rim == null) {
                return;
            }

            _lineMaterial = new Material(rim);
            _lineMaterial.SetFloat(FresnelId, 0f);
            _lineMaterial.SetFloat(CullId, 0f);
            _lineMaterial.SetFloat(DestinationBlendId, (float)BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetFloat(DepthTestId, (float)CompareFunction.Always);

            GameObject root = new GameObject("Lens outline");
            root.transform.SetParent(transform, false);
            _tipRing = CreateLine(root.transform, "Ring at the tip", RingSegments, true, 1f);
            _nearRing = CreateLine(root.transform, "Ring nearer the viewer", RingSegments, true, 0.45f);
            _edges = new LineRenderer[4];
            for (int i = 0; i < _edges.Length; i++) {
                _edges[i] = CreateLine(root.transform, "Edge " + (i + 1), 2, false, 0.45f);
            }

            SetOutlineShown(false);
        }

        private LineRenderer CreateLine(Transform parent, string lineName, int points, bool loop, float strength) {
            GameObject host = new GameObject(lineName);
            host.transform.SetParent(parent, false);
            LineRenderer line = host.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.alignment = LineAlignment.View;
            line.widthMultiplier = LineWidth;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = _lineMaterial;
            line.positionCount = points;
            Color colour = glow;
            colour.a = strength;
            line.startColor = colour;
            line.endColor = colour;
            return line;
        }

        private void SetOutlineShown(bool shown) {
            _tipRing.enabled = shown;
            _nearRing.enabled = shown;
            for (int i = 0; i < _edges.Length; i++) {
                _edges[i].enabled = shown;
            }
        }

        private void DrawOutline(Vector3 point, Transform screen, float radius) {
            if (_tipRing == null) {
                return;
            }

            if (!_tipRing.enabled) {
                SetOutlineShown(true);
            }

            Vector3 nearer = -screen.forward * outlineLength;
            for (int i = 0; i < RingSegments; i++) {
                float angle = 2f * Mathf.PI * i / RingSegments;
                _ring[i] = point + (screen.right * Mathf.Cos(angle) + screen.up * Mathf.Sin(angle)) * radius;
            }

            _tipRing.SetPositions(_ring);
            for (int i = 0; i < RingSegments; i++) {
                _ring[i] += nearer;
            }

            _nearRing.SetPositions(_ring);
            for (int i = 0; i < _edges.Length; i++) {
                int at = i * RingSegments / _edges.Length;
                _edges[i].SetPosition(0, _ring[at] - nearer);
                _edges[i].SetPosition(1, _ring[at]);
            }
        }
    }
}