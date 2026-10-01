using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Makes the heart beat by turning four blend-shape weights up and down. The shapes themselves are baked into
    /// the meshes at import (see <c>AnatomyHeartMotion</c>), so a structure that a shape does not touch is simply left
    /// alone.
    ///
    /// <para>In each beat the atria contract first, then the ventricles shorten, squeeze and wring as they eject,
    /// and the great arteries swell as the blood arrives. The ventricles let go quickly after the second heart
    /// sound, and the wring unwinds faster than the squeeze relaxes, as it does in a real heart.</para>
    ///
    /// <para>One beat is expressed as a fraction, shared with the synthesised heartbeat sound, so the "lub" lands as
    /// the ventricles begin to squeeze and the "dub" as they let go.</para>
    /// </summary>
    public class HeartbeatBehaviour : MonoBehaviour {
        public const float BeatsPerMinute = 72f;

        /// <summary>Names of the blend shapes the importer bakes and this behaviour drives.</summary>
        public const string ContractShape = "Contract";
        public const string TwistShape = "Twist";
        public const string SqueezeShape = "Squeeze";
        public const string PulseShape = "Pulse";

        /// <summary>Structures the beat treats as atria and as ventricles, by id.</summary>
        public static readonly string[] AtriumIds = new string[] { "left_atrium", "right_atrium" };
        public static readonly string[] VentricleIds = new string[] { "left_ventricle", "right_ventricle" };

        private const float LubFraction = 0.10f;
        private const float DubFraction = 0.42f;
        private const float FullWeight = 100f;
        private const int Contract = 0;
        private const int Twist = 1;
        private const int Squeeze = 2;
        private const int Pulse = 3;

        private static readonly string[] ShapeNames = new string[] { ContractShape, TwistShape, SqueezeShape, PulseShape };

        [SerializeField, Range(0.5f, 2f), Tooltip("Scales every shape, to make the beat gentler or bolder without re-importing.")]
        private float amplitude = 1.25f;

        private readonly float[] _weights = new float[ShapeNames.Length];
        private SkinnedMeshRenderer[] _renderers;
        private int[][] _shapeIndices;
        private AnatomyAudio _sound;
        private float _startTime;

        /// <summary>Finds the animated structures and starts the beat and its sound.</summary>
        public void Begin(AnatomyAudio sound) {
            AnatomyModel model = GetComponent<AnatomyModel>();
            _renderers = new SkinnedMeshRenderer[model.Structures.Count];
            _shapeIndices = new int[model.Structures.Count][];
            int animated = 0;
            for (int i = 0; i < model.Structures.Count; i++) {
                SkinnedMeshRenderer renderer = model.Structures[i].StructureRenderer as SkinnedMeshRenderer;
                if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount == 0) {
                    continue;
                }

                _renderers[i] = renderer;
                _shapeIndices[i] = new int[ShapeNames.Length];
                for (int shape = 0; shape < ShapeNames.Length; shape++) {
                    _shapeIndices[i][shape] = renderer.sharedMesh.GetBlendShapeIndex(ShapeNames[shape]);
                }

                animated++;
            }

            if (animated == 0) {
                Debug.LogWarning($"[Anatomy] '{model.ModelId}' has no blend shapes, so it will not beat. Re-import the topic.", model);
                enabled = false;
                return;
            }

            _sound = sound;
            _startTime = Time.time;
            if (_sound != null) {
                _sound.StartHeartbeat(BeatsPerMinute, LubFraction, DubFraction);
            }
        }

        private void Update() {
            float phase = Mathf.Repeat((Time.time - _startTime) * BeatsPerMinute / 60f, 1f);
            _weights[Contract] = Rise(0.10f, 0.21f, phase) * (1f - Rise(0.36f, 0.52f, phase));
            _weights[Twist] = Rise(0.12f, 0.28f, phase) * (1f - Rise(0.40f, 0.47f, phase));
            _weights[Squeeze] = Hump(Mathf.Repeat(phase + 0.08f, 1f), 0f, 0.18f);
            _weights[Pulse] = Hump(phase, 0.14f, 0.50f);

            for (int i = 0; i < _renderers.Length; i++) {
                if (_renderers[i] == null) {
                    continue;
                }

                for (int shape = 0; shape < ShapeNames.Length; shape++) {
                    int index = _shapeIndices[i][shape];
                    if (index >= 0) {
                        _renderers[i].SetBlendShapeWeight(index, _weights[shape] * amplitude * FullWeight);
                    }
                }
            }
        }

        private void OnDestroy() {
            if (_sound != null) {
                _sound.StopHeartbeat();
            }
        }

        /// <summary>Zero before <paramref name="start"/>, one after <paramref name="end"/>, and a smooth rise between.</summary>
        private static float Rise(float start, float end, float phase) {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, phase));
        }

        /// <summary>A smooth rise and fall across the window from start to end, zero outside it.</summary>
        private static float Hump(float phase, float start, float end) {
            if (phase <= start || phase >= end) {
                return 0f;
            }

            return Mathf.Sin((phase - start) / (end - start) * Mathf.PI);
        }
    }
}