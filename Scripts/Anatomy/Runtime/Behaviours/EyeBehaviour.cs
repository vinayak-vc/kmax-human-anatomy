using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Brings the eye to life. It looks at the pen, the muscle that pulls it that way lights up, the pupil closes as the
    /// pen comes near and the lens thickens to focus on it, and with nothing to look at it glances about by itself.
    /// Explaining a muscle turns the eye the way that muscle pulls, and explaining a part of the eye that deals with
    /// light shows the light: rays from the pen, in through the cornea and the lens, to a point on the retina.
    ///
    /// <para>The parts of the globe, which are rigid, hang from one pivot at the middle of the eyeball and are turned
    /// by turning it, so their colliders turn with them and picking stays exact. The soft tissue round it, the muscles
    /// and the sheath of the optic nerve, cannot be turned as one, so each has two blend shapes baked into its mesh at
    /// import (see <c>AnatomyEyeMotion</c>), yaw and pitch, which stretch it between the parts that turn and the parts
    /// that do not. Their colliders are re-baked a few at a time (<see cref="SoftColliderRefresher"/>).</para>
    ///
    /// <para>Yaw and pitch are an azimuth and an elevation (<see cref="EyeGazeFrame"/>). The eye is meant to look at
    /// the pen but a pen hovering near the eye is at a large angle from it, so the angle is compressed: a little
    /// offset turns the eye a little, and a large one turns it nearly as far as it goes.</para>
    /// </summary>
    public class EyeBehaviour : MonoBehaviour, IFocusListener {
        /// <summary>Names of the blend shapes the importer bakes and this behaviour drives.</summary>
        public const string YawShape = "Yaw";
        public const string PitchShape = "Pitch";
        public const string PupilShape = "Pupil";
        public const string FocusShape = "Focus";

        /// <summary>Degrees of turn the yaw and pitch shapes hold at full weight.</summary>
        public const float GazeShapeDegrees = 30f;

        public const string ScleraId = "eye_sclera";
        public const string CorneaId = "eye_cornea";
        public const string IrisId = "eye_iris";
        public const string LensId = "eye_lens";
        public const string RetinaId = "eye_retina";
        public const string MaculaId = "eye_macula";
        public const string OraSerrataId = "eye_ora_serrata";
        public const string NerveId = "eye_dura_matter";

        /// <summary>The rigid parts of the globe, which turn as one.</summary>
        public static readonly string[] GlobeIds = new string[] {
            ScleraId, CorneaId, IrisId, LensId, RetinaId, MaculaId, OraSerrataId
        };

        /// <summary>The soft tissue round the globe, which is stretched as it turns.</summary>
        public static readonly string[] SoftIds = new string[] {
            EyeMuscleActions.SuperiorRectusId, EyeMuscleActions.InferiorRectusId, EyeMuscleActions.MedialRectusId,
            EyeMuscleActions.LateralRectusId, EyeMuscleActions.SuperiorObliqueId, EyeMuscleActions.InferiorObliqueId,
            EyeMuscleActions.LevatorId, NerveId
        };

        /// <summary>The parts of the eye that focus light and catch it: explaining one of them shows the rays.</summary>
        private static readonly string[] SeeingIds = new string[] { CorneaId, LensId, RetinaId, MaculaId };

        private const float FullWeight = 100f;
        private const float MinimumPointerDistance = 0.08f;
        private const float PointerMovedSquared = 0.000004f;
        private const float IdleSeconds = 6f;
        private const float DemoSeconds = 1.8f;
        private const float DemoShare = 0.85f;
        private const float ColliderRefreshDegrees = 0.7f;
        private const float RayFadeSeconds = 0.3f;
        private const float RayLineWidth = 0.0012f;
        private const float LookReachDegrees = 70f;
        private const float LookHalfAngleDegrees = 70f;
        private const float PupilDilated = 85f;
        private const float PupilConstricted = -18f;
        private const float FocusNear = 100f;
        private const float FocusFar = -35f;
        private const float NeutralPointerDistance = 0.4f;

        /// <summary>Half the width of the ring of rays at the cornea and at the lens, in the model's metres.</summary>
        private const float CorneaRayRadius = 0.003f;
        private const float LensRayRadius = 0.0016f;
        private const float BeadRadius = 0.0035f;

        /// <summary>
        /// The light seems to come from this far in front of the cornea, as far as the pen is but between these, in
        /// eyeball widths, so the eye stays in proportion to its light however far it is zoomed.
        /// </summary>
        private const float NearestRaySourceGlobes = 0.9f;
        private const float FurthestRaySourceGlobes = 1.4f;

        /// <summary>The source is brought in, if need be, to no less than this, in world metres, and to within this share of the pop-out limit.</summary>
        private const float ClosestRaySource = 0.04f;
        private const float ComfortShare = 0.9f;

        /// <summary>The source is also kept inside this part of the window, as viewport fractions, so it is never cut by the frame.</summary>
        private const float FrameLeft = 0.05f;
        private const float FrameRight = 0.95f;
        private const float FrameBottom = 0.12f;
        private const float FrameTop = 0.88f;
        private const float FrameShrink = 0.85f;
        private const int FrameTries = 10;

        private static readonly Color RayTint = new Color(1f, 0.93f, 0.55f, 0.95f);
        private static readonly int FresnelId = Shader.PropertyToID("_Fresnel");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int DestinationBlendId = Shader.PropertyToID("_DstBlend");

        [SerializeField, Tooltip("Furthest the eye turns sideways, in degrees.")]
        private float maxYaw = 32f;
        [SerializeField, Tooltip("Furthest the eye turns up or down, in degrees.")]
        private float maxPitch = 26f;
        [SerializeField, Tooltip("Seconds the eye takes to settle on a new direction.")]
        private float gazeSmoothTime = 0.09f;
        [SerializeField, Range(0f, 1f), Tooltip("How brightly a muscle lights as it works.")]
        private float muscleGlow = 0.7f;

        private readonly List<string> _focusIds = new List<string>();
        private readonly List<Vector2> _demoPulls = new List<Vector2>();
        private readonly List<StructureHighlight> _muscleHighlights = new List<StructureHighlight>();
        private readonly List<Vector2> _musclePulls = new List<Vector2>();

        private Transform _pivot;
        private Vector3 _forward;
        private SkinnedMeshRenderer[] _softRenderers;
        private int[] _yawIndices;
        private int[] _pitchIndices;
        private SkinnedMeshRenderer _iris;
        private int _pupilIndex;
        private SkinnedMeshRenderer _lens;
        private int _focusIndex;
        private Vector3 _corneaFrontLocal;
        private Vector3 _lensBackLocal;
        private Vector3 _focusLocal;
        private float _globeDiameter;
        private bool _hasRayPoints;
        private StylusTip _tip;
        private SoftColliderRefresher _colliders;
        private EyeLightRays _rays;
        private Material _rayMaterial;

        private float _yaw;
        private float _pitch;
        private float _targetYaw;
        private float _targetPitch;
        private float _yawVelocity;
        private float _pitchVelocity;
        private float _colliderYaw;
        private float _colliderPitch;
        private Vector3 _lastTip;
        private float _lastMoveTime;
        private float _nextGlance;
        private float _glanceYaw;
        private float _glancePitch;
        private float _demoStart;
        private float _raysStrength;
        private bool _hasFocus;
        private bool _irisFocused;
        private bool _lensFocused;
        private bool _raysWanted;

        /// <summary>Finds the parts of the eye, hangs the rigid ones from a pivot and starts looking about.</summary>
        public void Begin() {
            AnatomyModel model = GetComponent<AnatomyModel>();
            AnatomyStructure sclera = model.FindStructure(ScleraId);
            AnatomyStructure cornea = model.FindStructure(CorneaId);
            if (sclera == null || cornea == null) {
                Debug.LogWarning($"[Anatomy] '{model.ModelId}' has no sclera or cornea, so its eye will not move.", model);
                enabled = false;
                return;
            }

            Vector3 globe = sclera.transform.localPosition;
            _forward = (cornea.transform.localPosition - globe).normalized;
            BuildPivot(model, globe);
            FindSoftTissue(model);
            FindMuscles(model);
            FindIrisAndLens(model);
            MeasureRayPoints(model);
            BuildRays(model);

            _tip = FindFirstObjectByType<StylusTip>();
            _lastMoveTime = Time.time;
            _nextGlance = Time.time;
            if (_tip != null) {
                _lastTip = _tip.Position;
            }
        }

        /// <summary>Notes what is being explained, so a muscle can be shown at work and the light shown where it matters.</summary>
        public void OnFocusChanged(IReadOnlyList<string> structureIds) {
            bool changed = structureIds.Count != _focusIds.Count;
            for (int i = 0; !changed && i < structureIds.Count; i++) {
                changed = structureIds[i] != _focusIds[i];
            }

            if (!changed) {
                return;
            }

            _focusIds.Clear();
            _demoPulls.Clear();
            _irisFocused = false;
            _lensFocused = false;
            _raysWanted = false;
            for (int i = 0; i < structureIds.Count; i++) {
                string id = structureIds[i];
                _focusIds.Add(id);
                Vector2 pull;
                if (EyeMuscleActions.TryGetPull(id, out pull)) {
                    _demoPulls.Add(pull);
                }

                _irisFocused |= id == IrisId;
                _lensFocused |= id == LensId;
                _raysWanted |= System.Array.IndexOf(SeeingIds, id) >= 0;
            }

            _hasFocus = structureIds.Count > 0;
            _demoStart = Time.time;
        }

        private void Update() {
            ChooseTarget();
            _yaw = Mathf.SmoothDamp(_yaw, _targetYaw, ref _yawVelocity, gazeSmoothTime);
            _pitch = Mathf.SmoothDamp(_pitch, _targetPitch, ref _pitchVelocity, gazeSmoothTime);
            _pivot.localRotation = EyeGazeFrame.Gaze(_yaw, _pitch, _forward);

            StretchSoftTissue();
            LightMuscles();
            ApplyIrisAndLens();
            DrawRays();
            _colliders.Tick();
        }

        private void OnDestroy() {
            if (_colliders != null) {
                _colliders.Release();
            }

            if (_rayMaterial != null) {
                Destroy(_rayMaterial);
            }
        }

        /// <summary>
        /// Where the eye should look. A demonstration of a muscle comes first, then a part being explained (the eye
        /// settles to look straight ahead, so it can be seen), then the pen, and with the pen still, a glance about.
        /// </summary>
        private void ChooseTarget() {
            if (_demoPulls.Count > 0) {
                int index = Mathf.FloorToInt((Time.time - _demoStart) / DemoSeconds) % _demoPulls.Count;
                _targetYaw = _demoPulls[index].x * maxYaw * DemoShare;
                _targetPitch = _demoPulls[index].y * maxPitch * DemoShare;
                return;
            }

            if (_hasFocus) {
                _targetYaw = 0f;
                _targetPitch = 0f;
                return;
            }

            float yaw;
            float pitch;
            if (TryFollowPointer(out yaw, out pitch)) {
                _targetYaw = yaw;
                _targetPitch = pitch;
                return;
            }

            GlanceAbout();
        }

        /// <summary>
        /// Turns to look at the pen. The direction to it is measured from the direction to the viewer, compressed so a pen
        /// a long way off still leaves room to move, and then limited to how far an eye turns. Returns false when there is
        /// no pen to follow or it has been still a while.
        /// </summary>
        private bool TryFollowPointer(out float yaw, out float pitch) {
            yaw = _targetYaw;
            pitch = _targetPitch;
            if (_tip == null) {
                return false;
            }

            Vector3 tip = _tip.Position;
            if ((tip - _lastTip).sqrMagnitude > PointerMovedSquared) {
                _lastTip = tip;
                _lastMoveTime = Time.time;
            }

            if (Time.time - _lastMoveTime > IdleSeconds) {
                return false;
            }

            Vector3 toTip = tip - _pivot.position;
            if (toTip.magnitude < MinimumPointerDistance) {
                return true;
            }

            Camera camera = StereoVolume.CenterCamera;
            Vector3 toViewer = camera != null
                ? (camera.transform.position - _pivot.position).normalized
                : transform.TransformDirection(Vector3.back);
            Vector3 towards = toTip.normalized;
            float angle = Vector3.Angle(toViewer, towards);
            Vector3 look = toViewer;
            if (angle > 0.01f) {
                float reach = LookReachDegrees * angle / (angle + LookHalfAngleDegrees) * (1f + LookHalfAngleDegrees / 180f);
                look = Vector3.Slerp(toViewer, towards, Mathf.Clamp01(reach / angle));
            }

            EyeGazeFrame.AnglesTo(transform.InverseTransformDirection(look), _forward, out yaw, out pitch);
            LimitGaze(ref yaw, ref pitch);
            return true;
        }

        /// <summary>With nothing to look at, the eye darts to a new place every few seconds, as an eye does.</summary>
        private void GlanceAbout() {
            if (Time.time >= _nextGlance) {
                _glanceYaw = Random.Range(-0.6f, 0.6f) * maxYaw;
                _glancePitch = Random.Range(-0.5f, 0.5f) * maxPitch;
                _nextGlance = Time.time + Random.Range(1.2f, 3.2f);
            }

            _targetYaw = _glanceYaw;
            _targetPitch = _glancePitch;
        }

        private void LimitGaze(ref float yaw, ref float pitch) {
            float across = yaw / maxYaw;
            float up = pitch / maxPitch;
            float size = Mathf.Sqrt(across * across + up * up);
            if (size > 1f) {
                yaw /= size;
                pitch /= size;
            }
        }

        private void StretchSoftTissue() {
            float yawWeight = _yaw / GazeShapeDegrees * FullWeight;
            float pitchWeight = _pitch / GazeShapeDegrees * FullWeight;
            for (int i = 0; i < _softRenderers.Length; i++) {
                _softRenderers[i].SetBlendShapeWeight(_yawIndices[i], yawWeight);
                _softRenderers[i].SetBlendShapeWeight(_pitchIndices[i], pitchWeight);
            }

            if (Mathf.Abs(_yaw - _colliderYaw) + Mathf.Abs(_pitch - _colliderPitch) > ColliderRefreshDegrees) {
                _colliderYaw = _yaw;
                _colliderPitch = _pitch;
                _colliders.RequestRefresh();
            }
        }

        /// <summary>Each muscle glows as much as it is working to hold the eye where it is.</summary>
        private void LightMuscles() {
            float yawShare = _yaw / maxYaw;
            float pitchShare = _pitch / maxPitch;
            for (int i = 0; i < _muscleHighlights.Count; i++) {
                _muscleHighlights[i].SetPulse(muscleGlow * EyeMuscleActions.Activation(_musclePulls[i], yawShare, pitchShare));
            }
        }

        /// <summary>
        /// The pupil closes as the pen comes near, like a light coming close, and the lens thickens to focus on it. While
        /// the iris or the lens is being explained they do it on their own, slowly, so it can be watched.
        /// </summary>
        private void ApplyIrisAndLens() {
            float distance = PointerDistance();
            float bright = _irisFocused
                ? 0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI / 3.5f)
                : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.14f, 0.5f, distance));
            float near = _lensFocused
                ? 0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI / 5f)
                : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.55f, distance));
            if (_iris != null && _pupilIndex >= 0) {
                _iris.SetBlendShapeWeight(_pupilIndex, Mathf.Lerp(PupilDilated, PupilConstricted, bright));
            }

            if (_lens != null && _focusIndex >= 0) {
                _lens.SetBlendShapeWeight(_focusIndex, Mathf.Lerp(FocusFar, FocusNear, near));
            }
        }

        /// <summary>
        /// The rays start from an object on the eye's own axis, as far from the eye as the pen is, and meet at the point on
        /// the retina. Fading in and out as the eye's light-handling parts are explained.
        /// </summary>
        private void DrawRays() {
            if (_rays == null) {
                return;
            }

            _raysStrength = Mathf.MoveTowards(_raysStrength, _raysWanted ? 1f : 0f, Time.deltaTime / RayFadeSeconds);
            if (_raysStrength <= 0.01f) {
                _rays.Hide();
                return;
            }

            float scale = transform.lossyScale.x;
            Vector3 axis = _pivot.TransformDirection(_forward).normalized;
            Vector3 corneaFront = _pivot.TransformPoint(_corneaFrontLocal);
            Vector3 lensBack = _pivot.TransformPoint(_lensBackLocal);
            Vector3 focus = _pivot.TransformPoint(_focusLocal);
            float globe = _globeDiameter * scale;
            float furthest = FurthestRaySourceGlobes * globe;
            float distance = Mathf.Clamp(PointerDistance(), NearestRaySourceGlobes * globe, furthest);
            distance = Mathf.Min(distance, RoomInFront(corneaFront, axis, furthest));
            Vector3 source = corneaFront + axis * distance;
            Camera camera = StereoVolume.CenterCamera;
            for (int i = 0; camera != null && i < FrameTries && distance > ClosestRaySource && !InsideFrame(camera, source); i++) {
                distance = Mathf.Max(ClosestRaySource, distance * FrameShrink);
                source = corneaFront + axis * distance;
            }

            Vector3 right = Vector3.Cross(Vector3.up, axis);
            if (right.sqrMagnitude < 0.0001f) {
                right = Vector3.Cross(Vector3.forward, axis);
            }

            right.Normalize();
            Vector3 up = Vector3.Cross(axis, right);
            _rays.Draw(source, corneaFront, lensBack, focus, right, up, CorneaRayRadius * scale, LensRayRadius * scale,
                BeadRadius * scale, _raysStrength);
        }

        /// <summary>
        /// How far in front of the cornea, along the eye's axis, the light's source can stand without coming out of the
        /// glass further than is comfortable. The eye often faces the viewer, whose side of the glass has little room.
        /// </summary>
        private static float RoomInFront(Vector3 from, Vector3 axis, float cap) {
            if (!StereoVolume.IsReady) {
                return cap;
            }

            float slope = StereoVolume.DepthOf(from + axis) - StereoVolume.DepthOf(from);
            if (slope >= -0.001f) {
                return cap;
            }

            float room = (-StereoVolume.PopOutLimit * ComfortShare - StereoVolume.DepthOf(from)) / slope;
            return Mathf.Clamp(room, ClosestRaySource, cap);
        }

        /// <summary>True when the point is in front of the camera and inside the part of the window the source may stand in.</summary>
        private static bool InsideFrame(Camera camera, Vector3 point) {
            Vector3 view = camera.WorldToViewportPoint(point);
            return view.z > 0f && view.x > FrameLeft && view.x < FrameRight && view.y > FrameBottom && view.y < FrameTop;
        }

        private float PointerDistance() {
            return _tip != null ? Vector3.Distance(_tip.Position, _pivot.position) : NeutralPointerDistance;
        }

        /// <summary>Hangs the rigid parts of the globe from a pivot at its middle, where they stay put in the world.</summary>
        private void BuildPivot(AnatomyModel model, Vector3 globe) {
            GameObject host = new GameObject("Eye gaze");
            _pivot = host.transform;
            _pivot.SetParent(transform, false);
            _pivot.localPosition = globe;
            for (int i = 0; i < GlobeIds.Length; i++) {
                AnatomyStructure structure = model.FindStructure(GlobeIds[i]);
                if (structure != null) {
                    structure.transform.SetParent(_pivot, true);
                }
            }
        }

        private void FindSoftTissue(AnatomyModel model) {
            List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
            List<int> yawIndices = new List<int>();
            List<int> pitchIndices = new List<int>();
            for (int i = 0; i < SoftIds.Length; i++) {
                AnatomyStructure structure = model.FindStructure(SoftIds[i]);
                SkinnedMeshRenderer renderer = structure != null ? structure.StructureRenderer as SkinnedMeshRenderer : null;
                if (renderer == null || renderer.sharedMesh == null) {
                    continue;
                }

                int yaw = renderer.sharedMesh.GetBlendShapeIndex(YawShape);
                int pitch = renderer.sharedMesh.GetBlendShapeIndex(PitchShape);
                if (yaw >= 0 && pitch >= 0) {
                    renderers.Add(renderer);
                    yawIndices.Add(yaw);
                    pitchIndices.Add(pitch);
                }
            }

            _softRenderers = renderers.ToArray();
            _yawIndices = yawIndices.ToArray();
            _pitchIndices = pitchIndices.ToArray();
            _colliders = new SoftColliderRefresher(_softRenderers);
            if (_softRenderers.Length == 0) {
                Debug.LogWarning($"[Anatomy] '{model.ModelId}' has no gaze shapes, so its muscles will not stretch. Re-import the topic.", model);
            }
        }

        private void FindMuscles(AnatomyModel model) {
            for (int i = 0; i < EyeMuscleActions.MuscleIds.Length; i++) {
                AnatomyStructure structure = model.FindStructure(EyeMuscleActions.MuscleIds[i]);
                Vector2 pull;
                if (structure != null && EyeMuscleActions.TryGetPull(EyeMuscleActions.MuscleIds[i], out pull)) {
                    _muscleHighlights.Add(structure.GetComponent<StructureHighlight>());
                    _musclePulls.Add(pull);
                }
            }
        }

        private void FindIrisAndLens(AnatomyModel model) {
            _pupilIndex = -1;
            _focusIndex = -1;
            AnatomyStructure iris = model.FindStructure(IrisId);
            AnatomyStructure lens = model.FindStructure(LensId);
            _iris = iris != null ? iris.StructureRenderer as SkinnedMeshRenderer : null;
            _lens = lens != null ? lens.StructureRenderer as SkinnedMeshRenderer : null;
            if (_iris != null && _iris.sharedMesh != null) {
                _pupilIndex = _iris.sharedMesh.GetBlendShapeIndex(PupilShape);
            }

            if (_lens != null && _lens.sharedMesh != null) {
                _focusIndex = _lens.sharedMesh.GetBlendShapeIndex(FocusShape);
            }
        }

        /// <summary>
        /// Where the rays pass, in the pivot's own space, measured at rest: the front of the cornea and the back of the
        /// lens on the eye's axis, and the point on the retina they meet at.
        /// </summary>
        private void MeasureRayPoints(AnatomyModel model) {
            AnatomyStructure cornea = model.FindStructure(CorneaId);
            AnatomyStructure lens = model.FindStructure(LensId);
            AnatomyStructure macula = model.FindStructure(MaculaId);
            if (cornea == null || lens == null || macula == null) {
                return;
            }

            _corneaFrontLocal = _pivot.InverseTransformPoint(cornea.transform.position) + _forward * ExtentAlong(cornea, _forward);
            _lensBackLocal = _pivot.InverseTransformPoint(lens.transform.position) - _forward * ExtentAlong(lens, _forward);
            _focusLocal = _pivot.InverseTransformPoint(macula.transform.position);
            AnatomyStructure sclera = model.FindStructure(ScleraId);
            _globeDiameter = sclera != null ? 2f * ExtentAlong(sclera, Vector3.right) : 0.025f;
            _hasRayPoints = true;
        }

        private void BuildRays(AnatomyModel model) {
            AnatomyStructure sclera = model.FindStructure(ScleraId);
            if (!_hasRayPoints || sclera == null || sclera.RimMaterial == null) {
                return;
            }

            _rayMaterial = new Material(sclera.RimMaterial);
            _rayMaterial.SetFloat(FresnelId, 0f);
            _rayMaterial.SetFloat(CullId, 0f);
            _rayMaterial.SetFloat(DestinationBlendId, (float)BlendMode.OneMinusSrcAlpha);
            _rays = new EyeLightRays(transform, _rayMaterial, RayLineWidth, RayTint);
        }

        /// <summary>How far a structure's mesh reaches from its middle along a direction, in the model's metres.</summary>
        private static float ExtentAlong(AnatomyStructure structure, Vector3 direction) {
            MeshFilter filter = structure.GetComponent<MeshFilter>();
            SkinnedMeshRenderer skinned = structure.StructureRenderer as SkinnedMeshRenderer;
            Mesh mesh = filter != null ? filter.sharedMesh : (skinned != null ? skinned.sharedMesh : null);
            if (mesh == null) {
                return 0f;
            }

            Vector3 extents = mesh.bounds.extents;
            return Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.y) * extents.y + Mathf.Abs(direction.z) * extents.z;
        }
    }
}