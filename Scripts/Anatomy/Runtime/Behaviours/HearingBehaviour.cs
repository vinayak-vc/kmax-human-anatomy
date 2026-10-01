using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Plays one sound going into the ear, over and over: rings travel down the ear canal, the eardrum and the three
    /// bones vibrate as the sound arrives, and the cochlea lights as the vibration reaches it. A soft tone is timed
    /// to the same cycle.
    ///
    /// <para>The vibration is one blend-shape weight, swung to either side of zero and shared by the drum and the bones.
    /// The shape is baked into their meshes at import (see <c>AnatomyEarMotion</c>), so the bones stay joined to one
    /// another at whatever size they are shown, and their vibration grows with them. It is slowed down and
    /// exaggerated: a real sound moves the drum by far less than a micrometre, hundreds of times a second.</para>
    ///
    /// <para>The sound repeats every <see cref="CycleSeconds"/>. When the tone is playing the animation follows its
    /// position, so the two cannot drift apart over a long day; without it the animation keeps its own time.</para>
    /// </summary>
    public class HearingBehaviour : MonoBehaviour {
        /// <summary>Name of the blend shape the importer bakes and this behaviour drives.</summary>
        public const string VibrateShape = "Vibrate";

        public const string MembraneId = "ear_tympanic_membrane";
        public const string MalleusId = "ear_malleus";
        public const string IncusId = "ear_incus";
        public const string StapesId = "ear_stapes";
        public const string CochleaId = "ear_cochlea";
        public const string CanalId = "ear_auditory_canal";

        /// <summary>Name of the route the importer measures for a sound going in.</summary>
        public const string SoundRoute = "sound";

        /// <summary>Seconds from the start of one sound to the start of the next.</summary>
        public const float CycleSeconds = 4f;

        /// <summary>The structures that vibrate, by id.</summary>
        public static readonly string[] VibratingIds = new string[] { MembraneId, MalleusId, IncusId, StapesId };

        private const int RingCount = 4;
        private const float FirstRingSeconds = 0.1f;
        private const float RingSpacingSeconds = 0.26f;
        private const float TravelSeconds = 0.9f;
        private const float FullWeight = 100f;
        private const float RingLineWidth = 0.0018f;

        private static readonly Color RingTint = new Color(1f, 0.8f, 0.3f, 0.95f);
        private static readonly int FresnelId = Shader.PropertyToID("_Fresnel");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int DestinationBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int DepthTestId = Shader.PropertyToID("_ZTest");

        [SerializeField, Range(0.5f, 2f), Tooltip("Scales the vibration, to make it gentler or bolder without re-importing.")]
        private float amplitude = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("How brightly the cochlea lights as the vibration reaches it.")]
        private float cochleaGlow = 0.55f;

        private SkinnedMeshRenderer[] _vibrators;
        private int[] _shapeIndices;
        private StructureHighlight _cochlea;
        private RouteRings _rings;
        private Material _ringMaterial;
        private AnatomyAudio _sound;
        private float _startTime;
        private float _lastWeight;

        /// <summary>Finds what vibrates and the route the sound takes, and starts the cycle and its tone.</summary>
        public void Begin(AnatomyAudio sound) {
            AnatomyModel model = GetComponent<AnatomyModel>();
            FindVibrators(model);
            AnatomyStructure cochlea = model.FindStructure(CochleaId);
            _cochlea = cochlea != null ? cochlea.GetComponent<StructureHighlight>() : null;
            BuildRings(model);

            if (_vibrators.Length == 0 && _rings == null) {
                Debug.LogWarning($"[Anatomy] '{model.ModelId}' has no vibrating structures and no sound path, so it will " +
                    "not play. Re-import the topic.", model);
                enabled = false;
                return;
            }

            _sound = sound;
            _startTime = Time.time;
            if (_sound != null) {
                _sound.StartHearing(CycleSeconds);
            }
        }

        private void Update() {
            float time = CycleTime();
            if (_rings != null) {
                for (int i = 0; i < RingCount; i++) {
                    _rings.Place(i, (time - FirstRingSeconds - i * RingSpacingSeconds) / TravelSeconds);
                }
            }

            float arrival = FirstRingSeconds + TravelSeconds;
            float burst = Burst(time, arrival);
            ApplyVibration(burst * Mathf.Cos(2f * Mathf.PI * (time - arrival) / RingSpacingSeconds));
            if (_cochlea != null) {
                float flicker = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * (time - arrival - 0.06f) / RingSpacingSeconds);
                _cochlea.SetPulse(cochleaGlow * burst * flicker);
            }
        }

        private void OnDestroy() {
            if (_sound != null) {
                _sound.StopHearing();
            }

            if (_ringMaterial != null) {
                Destroy(_ringMaterial);
            }
        }

        /// <summary>Seconds into the current cycle, taken from the tone when it is playing.</summary>
        private float CycleTime() {
            float phase = _sound != null ? _sound.HearingPhase : -1f;
            if (phase < 0f) {
                phase = Mathf.Repeat((Time.time - _startTime) / CycleSeconds, 1f);
            }

            return phase * CycleSeconds;
        }

        /// <summary>How strongly the drum and bones are shaken: nothing until the first ring arrives, then a swelling that dies away.</summary>
        private static float Burst(float time, float arrival) {
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(arrival - 0.05f, arrival + 0.3f, time));
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(arrival + 1f, arrival + 2f, time));
            return rise * (1f - fall);
        }

        private void ApplyVibration(float swing) {
            float weight = swing * amplitude * FullWeight;
            if (Mathf.Approximately(weight, _lastWeight)) {
                return;
            }

            _lastWeight = weight;
            for (int i = 0; i < _vibrators.Length; i++) {
                _vibrators[i].SetBlendShapeWeight(_shapeIndices[i], weight);
            }
        }

        private void FindVibrators(AnatomyModel model) {
            List<SkinnedMeshRenderer> renderers = new List<SkinnedMeshRenderer>();
            List<int> indices = new List<int>();
            for (int i = 0; i < VibratingIds.Length; i++) {
                AnatomyStructure structure = model.FindStructure(VibratingIds[i]);
                SkinnedMeshRenderer renderer = structure != null ? structure.StructureRenderer as SkinnedMeshRenderer : null;
                if (renderer == null || renderer.sharedMesh == null) {
                    continue;
                }

                int index = renderer.sharedMesh.GetBlendShapeIndex(VibrateShape);
                if (index >= 0) {
                    renderers.Add(renderer);
                    indices.Add(index);
                }
            }

            _vibrators = renderers.ToArray();
            _shapeIndices = indices.ToArray();
        }

        /// <summary>
        /// The rings borrow the edge-glow material the structures already carry, turned into a flat, alpha-blended line.
        /// Not additive, so a warm ring still shows against the pale pinna that an additive one would vanish into, and not
        /// depth-tested, so the wave can be followed down the canal through its wall.
        /// </summary>
        private void BuildRings(AnatomyModel model) {
            AnatomyRoute route = AnatomyRoute.Find(gameObject, SoundRoute);
            AnatomyStructure canal = model.FindStructure(CanalId);
            if (route == null || !route.IsUsable || canal == null || canal.RimMaterial == null) {
                return;
            }

            _ringMaterial = new Material(canal.RimMaterial);
            _ringMaterial.SetFloat(FresnelId, 0f);
            _ringMaterial.SetFloat(CullId, 0f);
            _ringMaterial.SetFloat(DestinationBlendId, (float)BlendMode.OneMinusSrcAlpha);
            _ringMaterial.SetFloat(DepthTestId, (float)CompareFunction.Always);
            _rings = new RouteRings(transform, _ringMaterial, route, RingCount, RingLineWidth, RingTint);
        }
    }
}