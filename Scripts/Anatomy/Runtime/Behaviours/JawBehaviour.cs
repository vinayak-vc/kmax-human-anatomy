using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Makes the jaw open and close, slowly and over and over, and lights the muscles that do the work. Opening, the
    /// muscles that pull the jaw down and forward glow; closing, the ones that lift it do.
    ///
    /// <para>The jaw bone and the lower teeth are rigid, so they hang from a pivot at the joint in front of the ears and
    /// are turned by turning it: their colliders turn with them and picking stays exact. The muscles are stretched by a
    /// shape baked into each at import (see <c>AnatomyJawMotion</c>), and their colliders are re-baked a few at a time
    /// (<see cref="SoftColliderRefresher"/>). The same turn is used for both, from <see cref="OpenTurn"/>.</para>
    ///
    /// <para>Explaining a muscle that opens or closes the jaw makes it glow at full strength in its own part of the
    /// cycle and leaves the rest of the muscles in glass.</para>
    /// </summary>
    public class JawBehaviour : MonoBehaviour, IFocusListener {
        /// <summary>Name of the blend shape the importer bakes and this behaviour drives.</summary>
        public const string OpenShape = "Open";

        /// <summary>
        /// Name of the two-point route the importer measures: the hinge of the jaw, then the tip of the chin. It is all the
        /// runtime needs to hang the jaw from its joint and to know which way is down.
        /// </summary>
        public const string JawRoute = "jaw";

        /// <summary>Degrees of turn the open shape holds at full weight.</summary>
        public const float OpenShapeDegrees = 24f;

        public const string SkullId = "skull";
        public const string MandibleId = "mandible";
        public const string UpperTeethId = "teeth_upper";
        public const string LowerTeethId = "teeth_lower";
        public const string MasseterId = "masseter_muscle";
        public const string MedialPterygoidId = "medial_pterygoid_muscle";
        public const string LateralPterygoidId = "lateral_pterygoid_muscle";
        public const string DigastricId = "digastric_muscle";
        public const string MylohyoidId = "mylohyoid_muscle";
        public const string GeniohyoidId = "geniohyoid_muscle";

        /// <summary>The rigid parts that turn with the jaw.</summary>
        public static readonly string[] JawIds = new string[] { MandibleId, LowerTeethId };

        /// <summary>The muscles that lift the jaw, and the ones that pull it down and forward.</summary>
        public static readonly string[] ClosingIds = new string[] { MasseterId, MedialPterygoidId };
        public static readonly string[] OpeningIds = new string[] { LateralPterygoidId, DigastricId, MylohyoidId, GeniohyoidId };

        /// <summary>The muscles that are stretched as the jaw opens.</summary>
        public static readonly string[] SoftIds = new string[] {
            "buccinator_muscle", "orbicularis_muscle", "depressor_labii_inferioris_musc",
            "depressor_anguli_oris_muscle", "mentalis_muscle", MasseterId, "risorius_muscle",
            LateralPterygoidId, MedialPterygoidId, MylohyoidId, DigastricId, GeniohyoidId, "stylohyoid_muscle",
            "hyoglossus_muscle"
        };

        /// <summary>Seconds for one opening and closing of the jaw.</summary>
        public const float CycleSeconds = 4f;

        private const float OpenEnd = 0.35f;
        private const float HoldEnd = 0.45f;
        private const float CloseEnd = 0.80f;
        private const float FullWeight = 100f;
        private const float ColliderRefreshDegrees = 0.7f;

        [SerializeField, Range(4f, 24f), Tooltip("How far the jaw opens, in degrees.")]
        private float maxOpen = 20f;
        [SerializeField, Range(0f, 1f), Tooltip("How brightly a muscle lights as it works.")]
        private float muscleGlow = 0.8f;

        private readonly List<StructureHighlight> _closers = new List<StructureHighlight>();
        private readonly List<StructureHighlight> _openers = new List<StructureHighlight>();
        private Transform _pivot;
        private Vector3 _joint;
        private Vector3 _chin;
        private SkinnedMeshRenderer[] _softRenderers;
        private int[] _shapeIndices;
        private SoftColliderRefresher _colliders;
        private float _startTime;
        private float _colliderAngle;
        private bool _closerFocused;
        private bool _openerFocused;

        /// <summary>
        /// The turn that opens the jaw this many degrees about the line through its two joints: the direction that takes the
        /// chin down. The importer bakes the same turn into the muscles, so they cannot disagree.
        /// </summary>
        public static Quaternion OpenTurn(float degrees, Vector3 joint, Vector3 chin) {
            Vector3 arm = chin - joint;
            bool lowers = (Quaternion.AngleAxis(degrees, Vector3.right) * arm).y <= arm.y;
            return Quaternion.AngleAxis(lowers ? degrees : -degrees, Vector3.right);
        }

        /// <summary>Hangs the jaw from its hinge and starts the cycle.</summary>
        public void Begin() {
            AnatomyModel model = GetComponent<AnatomyModel>();
            AnatomyRoute route = AnatomyRoute.Find(gameObject, JawRoute);
            if (route == null || !route.IsUsable) {
                Debug.LogWarning($"[Anatomy] '{model.ModelId}' has no jaw hinge, so the jaw will not open. Re-import the topic.", model);
                enabled = false;
                return;
            }

            _joint = route.Points[0];
            _chin = route.Points[1];
            BuildPivot(model);
            FindSoftTissue(model);
            FindMuscles(model);
            _startTime = Time.time;
        }

        /// <summary>Notes which muscles are being explained, so they can glow at full strength in their part of the cycle.</summary>
        public void OnFocusChanged(IReadOnlyList<string> structureIds) {
            _closerFocused = false;
            _openerFocused = false;
            for (int i = 0; i < structureIds.Count; i++) {
                _closerFocused |= System.Array.IndexOf(ClosingIds, structureIds[i]) >= 0;
                _openerFocused |= System.Array.IndexOf(OpeningIds, structureIds[i]) >= 0;
            }
        }

        private void Update() {
            float phase = Mathf.Repeat((Time.time - _startTime) / CycleSeconds, 1f);
            float opened = Opened(phase);
            float angle = opened * maxOpen;
            _pivot.localRotation = OpenTurn(angle, _joint, _chin);
            StretchSoftTissue(angle);
            LightMuscles(phase, opened);
            _colliders.Tick();
        }

        private void OnDestroy() {
            if (_colliders != null) {
                _colliders.Release();
            }
        }

        /// <summary>How far open the jaw is, from 0 to 1: opening, held a moment, closing, and a rest with the teeth together.</summary>
        private static float Opened(float phase) {
            if (phase < OpenEnd) {
                return Mathf.SmoothStep(0f, 1f, phase / OpenEnd);
            }

            if (phase < HoldEnd) {
                return 1f;
            }

            if (phase < CloseEnd) {
                return 1f - Mathf.SmoothStep(0f, 1f, (phase - HoldEnd) / (CloseEnd - HoldEnd));
            }

            return 0f;
        }

        private void StretchSoftTissue(float angle) {
            float weight = angle / OpenShapeDegrees * FullWeight;
            for (int i = 0; i < _softRenderers.Length; i++) {
                _softRenderers[i].SetBlendShapeWeight(_shapeIndices[i], weight);
            }

            if (Mathf.Abs(angle - _colliderAngle) > ColliderRefreshDegrees) {
                _colliderAngle = angle;
                _colliders.RequestRefresh();
            }
        }

        /// <summary>
        /// The muscles that pull the jaw down glow as it opens and while it is held, the ones that lift it as it closes.
        /// One that is being explained glows fully in its own part of the cycle.
        /// </summary>
        private void LightMuscles(float phase, float opened) {
            bool opening = phase < OpenEnd;
            bool closing = phase >= HoldEnd && phase < CloseEnd;
            float openWork = opening ? 1f : 0.35f * opened;
            float closeWork = closing ? 1f : 0.12f * (1f - opened);
            if (_openerFocused) {
                closeWork *= 0.3f;
            }

            if (_closerFocused) {
                openWork *= 0.3f;
            }

            for (int i = 0; i < _openers.Count; i++) {
                _openers[i].SetPulse(muscleGlow * openWork);
            }

            for (int i = 0; i < _closers.Count; i++) {
                _closers[i].SetPulse(muscleGlow * closeWork);
            }
        }

        /// <summary>Hangs the rigid parts of the jaw from a pivot at the joint, where they stay put in the world.</summary>
        private void BuildPivot(AnatomyModel model) {
            GameObject host = new GameObject("Jaw hinge");
            _pivot = host.transform;
            _pivot.SetParent(transform, false);
            _pivot.localPosition = _joint;
            for (int i = 0; i < JawIds.Length; i++) {
                AnatomyStructure structure = model.FindStructure(JawIds[i]);
                if (structure != null) {
                    structure.transform.SetParent(_pivot, true);
                }
            }
        }

        private void FindSoftTissue(AnatomyModel model) {
            List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
            List<int> indices = new List<int>();
            for (int i = 0; i < SoftIds.Length; i++) {
                AnatomyStructure structure = model.FindStructure(SoftIds[i]);
                SkinnedMeshRenderer renderer = structure != null ? structure.StructureRenderer as SkinnedMeshRenderer : null;
                if (renderer == null || renderer.sharedMesh == null) {
                    continue;
                }

                int index = renderer.sharedMesh.GetBlendShapeIndex(OpenShape);
                if (index >= 0) {
                    renderers.Add(renderer);
                    indices.Add(index);
                }
            }

            _softRenderers = renderers.ToArray();
            _shapeIndices = indices.ToArray();
            _colliders = new SoftColliderRefresher(_softRenderers);
        }

        private void FindMuscles(AnatomyModel model) {
            AddHighlights(model, ClosingIds, _closers);
            AddHighlights(model, OpeningIds, _openers);
        }

        private static void AddHighlights(AnatomyModel model, string[] ids, List<StructureHighlight> into) {
            for (int i = 0; i < ids.Length; i++) {
                AnatomyStructure structure = model.FindStructure(ids[i]);
                if (structure != null) {
                    into.Add(structure.GetComponent<StructureHighlight>());
                }
            }
        }
    }
}