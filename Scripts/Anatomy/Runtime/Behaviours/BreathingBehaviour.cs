using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Makes the chest breathe, steadily, about twelve times a minute. The ribs swing up and out, the diaphragm flattens
    /// and drops, the lungs fill, and the airway lengthens, all by one blend-shape weight turned up and down. The shape is
    /// baked into the meshes at import (see <c>AnatomyBreathMotion</c>). Rings of air travel down the windpipe as the
    /// breath goes in and back up as it goes out, the lungs glow faintly while air moves in or out of them, and a soft
    /// rush of breath is timed to the same cycle.
    ///
    /// <para>The breath is drawn in over two fifths of the cycle, held a moment, let out over a little under half and
    /// followed by a short rest, which is what a relaxed breath does. When the breath's own sound is playing the
    /// animation follows its position, so the two cannot drift apart over a long day; without it the animation keeps
    /// its own time.</para>
    /// </summary>
    public class BreathingBehaviour : MonoBehaviour {
        /// <summary>Name of the blend shape the importer bakes and this behaviour drives.</summary>
        public const string InhaleShape = "Inhale";

        /// <summary>Name of the route the importer measures for air going down the windpipe.</summary>
        public const string AirRoute = "air";

        public const string RibsId = "ribs";
        public const string RibCartilageId = "rib_cartilage";
        public const string SternumId = "sternum";
        public const string DiaphragmId = "diaphragm";
        public const string AirwayId = "bronchial_tubes";

        /// <summary>The pack names both lungs "lungs"; the second of them is "lungs_2". Which is on which side is measured.</summary>
        public const string LeftLungId = "lungs";
        public const string RightLungId = "lungs_2";

        /// <summary>Seconds from the start of one breath to the start of the next: twelve breaths a minute.</summary>
        public const float CycleSeconds = 5f;

        /// <summary>The structures the breath moves, by id.</summary>
        public static readonly string[] MovingIds = new string[] {
            RibsId, RibCartilageId, SternumId, DiaphragmId, AirwayId, LeftLungId, RightLungId
        };

        private const float InhaleEnd = 0.40f;
        private const float ExhaleStart = 0.46f;
        private const float ExhaleEnd = 0.94f;
        private const int RingCount = 4;
        private const float RingSpacing = 0.05f;
        private const float RingTravel = 0.26f;
        private const float FullWeight = 100f;
        private const float RingLineWidth = 0.0012f;

        private static readonly Color RingTint = new Color(0.6f, 0.9f, 1f, 0.9f);
        private static readonly int FresnelId = Shader.PropertyToID("_Fresnel");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int DestinationBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int DepthTestId = Shader.PropertyToID("_ZTest");

        [SerializeField, Range(0.5f, 1.6f), Tooltip("How deep the breath is. Scales the movement without re-importing.")]
        private float depth = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("How brightly the lungs glow while air moves in or out of them.")]
        private float lungGlow = 0.3f;

        private SkinnedMeshRenderer[] _movers;
        private int[] _shapeIndices;
        private readonly List<StructureHighlight> _lungs = new List<StructureHighlight>();
        private RouteRings _rings;
        private Material _ringMaterial;
        private AnatomyAudio _sound;
        private float _startTime;
        private float _lastWeight;

        /// <summary>Finds what moves and the route the air takes, and starts the breath and its sound.</summary>
        public void Begin(AnatomyAudio sound) {
            AnatomyModel model = GetComponent<AnatomyModel>();
            FindMovers(model);
            FindLungs(model);
            BuildRings(model);

            if (_movers.Length == 0) {
                Debug.LogWarning($"[Anatomy] '{model.ModelId}' has no breathing shapes, so it will not breathe. Re-import the topic.", model);
                enabled = false;
                return;
            }

            _sound = sound;
            _startTime = Time.time;
            if (_sound != null) {
                _sound.StartBreathing(CycleSeconds);
            }
        }

        private void Update() {
            float phase = CyclePhase();
            float breath = Breath(phase);
            ApplyBreath(breath);
            LightLungs(breath);
            PlaceRings(phase);
        }

        private void OnDestroy() {
            if (_sound != null) {
                _sound.StopBreathing();
            }

            if (_ringMaterial != null) {
                Destroy(_ringMaterial);
            }
        }

        /// <summary>How far through the cycle the breath is, from 0 to 1, taken from the sound when it is playing.</summary>
        private float CyclePhase() {
            float phase = _sound != null ? _sound.BreathPhase : -1f;
            return phase >= 0f ? phase : Mathf.Repeat((Time.time - _startTime) / CycleSeconds, 1f);
        }

        /// <summary>How full the lungs are, from 0 to 1: drawn in, held a moment, let out, and a short rest.</summary>
        private static float Breath(float phase) {
            if (phase < InhaleEnd) {
                return Mathf.SmoothStep(0f, 1f, phase / InhaleEnd);
            }

            if (phase < ExhaleStart) {
                return 1f;
            }

            if (phase < ExhaleEnd) {
                return 1f - Mathf.SmoothStep(0f, 1f, (phase - ExhaleStart) / (ExhaleEnd - ExhaleStart));
            }

            return 0f;
        }

        private void ApplyBreath(float breath) {
            float weight = breath * depth * FullWeight;
            if (Mathf.Approximately(weight, _lastWeight)) {
                return;
            }

            _lastWeight = weight;
            for (int i = 0; i < _movers.Length; i++) {
                _movers[i].SetBlendShapeWeight(_shapeIndices[i], weight);
            }
        }

        /// <summary>The lungs glow while they are filling or emptying, most halfway, and not at all when full or empty.</summary>
        private void LightLungs(float breath) {
            float flowing = lungGlow * 4f * breath * (1f - breath);
            for (int i = 0; i < _lungs.Count; i++) {
                _lungs[i].SetPulse(flowing);
            }
        }

        /// <summary>Rings of air go down the windpipe as the breath goes in and back up as it goes out.</summary>
        private void PlaceRings(float phase) {
            if (_rings == null) {
                return;
            }

            for (int i = 0; i < RingCount; i++) {
                float drawn = (phase - i * RingSpacing) / RingTravel;
                float released = (phase - ExhaleStart - i * RingSpacing) / RingTravel;
                if (drawn > 0f && drawn < 1f) {
                    _rings.Place(i, drawn);
                } else if (released > 0f && released < 1f) {
                    _rings.Place(i, 1f - released);
                } else {
                    _rings.Place(i, -1f);
                }
            }
        }

        private void FindMovers(AnatomyModel model) {
            List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
            List<int> indices = new List<int>();
            for (int i = 0; i < MovingIds.Length; i++) {
                AnatomyStructure structure = model.FindStructure(MovingIds[i]);
                SkinnedMeshRenderer renderer = structure != null ? structure.StructureRenderer as SkinnedMeshRenderer : null;
                if (renderer == null || renderer.sharedMesh == null) {
                    continue;
                }

                int index = renderer.sharedMesh.GetBlendShapeIndex(InhaleShape);
                if (index >= 0) {
                    renderers.Add(renderer);
                    indices.Add(index);
                }
            }

            _movers = renderers.ToArray();
            _shapeIndices = indices.ToArray();
        }

        private void FindLungs(AnatomyModel model) {
            string[] ids = new string[] { LeftLungId, RightLungId };
            for (int i = 0; i < ids.Length; i++) {
                AnatomyStructure lung = model.FindStructure(ids[i]);
                if (lung != null) {
                    StructureHighlight highlight = lung.GetComponent<StructureHighlight>();
                    if (highlight != null) {
                        _lungs.Add(highlight);
                    }
                }
            }
        }

        /// <summary>
        /// The rings borrow the edge-glow material the structures already carry, turned into a flat, alpha-blended line
        /// that is not depth-tested, so the breath can be followed down the windpipe through its wall.
        /// </summary>
        private void BuildRings(AnatomyModel model) {
            AnatomyRoute route = AnatomyRoute.Find(gameObject, AirRoute);
            AnatomyStructure airway = model.FindStructure(AirwayId);
            if (route == null || !route.IsUsable || airway == null || airway.RimMaterial == null) {
                return;
            }

            _ringMaterial = new Material(airway.RimMaterial);
            _ringMaterial.SetFloat(FresnelId, 0f);
            _ringMaterial.SetFloat(CullId, 0f);
            _ringMaterial.SetFloat(DestinationBlendId, (float)BlendMode.OneMinusSrcAlpha);
            _ringMaterial.SetFloat(DepthTestId, (float)CompareFunction.Always);
            _rings = new RouteRings(transform, _ringMaterial, route, RingCount, RingLineWidth, RingTint);
        }
    }
}