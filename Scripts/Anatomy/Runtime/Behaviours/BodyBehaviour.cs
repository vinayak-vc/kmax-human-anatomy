using System;
using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// The body map's behaviour. It shows and hides the see-through layers with a fade, and lets the organs that lead into
    /// topics glow slowly, the head first and the chest a moment later, so they invite a pointer.
    ///
    /// <para>While an organ is being explained the layers fall back, so the glow drawn over it does not compete with it.
    /// The layers are drawn by the shared Glow material, so their brightness is set through a property block and the
    /// material assets are never touched.</para>
    /// </summary>
    public class BodyBehaviour : MonoBehaviour, IFocusListener {
        private const float FadeSeconds = 0.35f;
        private const float HiddenBelow = 0.004f;
        private const float PulseCyclesPerMetre = 1.4f;
        private const float TwoPi = 6.2831855f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Range(0f, 0.5f), Tooltip("How much the resting organs glow at the top of their slow pulse.")]
        private float invitation = 0.14f;
        [SerializeField, Tooltip("Seconds for one slow pulse.")]
        private float invitationSeconds = 2.6f;
        [SerializeField, Range(0f, 1f), Tooltip("How much of its brightness a layer keeps while an organ is being explained.")]
        private float focusedShare = 0.4f;

        private MaterialPropertyBlock _block;
        private AnatomyLayer[] _layers = new AnatomyLayer[0];
        private float[] _shown = new float[0];
        private float[] _wanted = new float[0];
        private float[] _applied = new float[0];
        private StructureHighlight[] _regions = new StructureHighlight[0];
        private float[] _phases = new float[0];
        private float _focusEase;
        private bool _focused;

        /// <summary>Raised when a layer is asked to show or hide.</summary>
        public event Action LayersChanged;

        public IReadOnlyList<AnatomyLayer> Layers {
            get { return _layers; }
        }

        /// <summary>Finds the layers and the regions, and puts each layer at the brightness the body map opens with.</summary>
        public void Begin() {
            _block = new MaterialPropertyBlock();
            _layers = GetComponentsInChildren<AnatomyLayer>(true);
            _shown = new float[_layers.Length];
            _wanted = new float[_layers.Length];
            _applied = new float[_layers.Length];
            for (int i = 0; i < _layers.Length; i++) {
                _wanted[i] = _layers[i].StartsShown ? 1f : 0f;
                _shown[i] = _wanted[i];
                _applied[i] = -1f;
                ApplyLayer(i);
            }

            FindRegions(GetComponent<AnatomyModel>());
        }

        public bool IsShown(int index) {
            return index >= 0 && index < _wanted.Length && _wanted[index] > 0.5f;
        }

        /// <summary>Shows the layer if it is hidden and hides it if it is shown.</summary>
        public void ToggleLayer(int index) {
            if (index < 0 || index >= _wanted.Length) {
                return;
            }

            _wanted[index] = _wanted[index] > 0.5f ? 0f : 1f;
            if (LayersChanged != null) {
                LayersChanged();
            }
        }

        /// <summary>Notes whether an organ is being explained, so the layers can fall back while it is.</summary>
        public void OnFocusChanged(IReadOnlyList<string> structureIds) {
            _focused = structureIds.Count > 0;
        }

        private void Update() {
            float step = Time.deltaTime / FadeSeconds;
            _focusEase = Mathf.MoveTowards(_focusEase, _focused ? 1f : 0f, step);
            for (int i = 0; i < _layers.Length; i++) {
                _shown[i] = Mathf.MoveTowards(_shown[i], _wanted[i], step);
                ApplyLayer(i);
            }

            PulseRegions();
        }

        private void ApplyLayer(int index) {
            float brightness = _layers[index].Intensity * _shown[index] * Mathf.Lerp(1f, focusedShare, _focusEase);
            if (Mathf.Approximately(brightness, _applied[index])) {
                return;
            }

            _applied[index] = brightness;
            Renderer layerRenderer = _layers[index].LayerRenderer;
            layerRenderer.enabled = brightness > HiddenBelow;
            Color colour = _layers[index].Tint;
            colour.a = brightness;
            layerRenderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, colour);
            layerRenderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// Glows each organ that has nothing said about it, in a wave that runs from the head to the chest. An organ being
        /// pointed at or explained, or one that has receded, is left to its own highlight.
        /// </summary>
        private void PulseRegions() {
            float time = Time.time / invitationSeconds;
            for (int i = 0; i < _regions.Length; i++) {
                float glow = 0f;
                if (_regions[i].State == HighlightState.Normal) {
                    float wave = 0.5f + 0.5f * Mathf.Sin((time + _phases[i]) * TwoPi);
                    glow = invitation * wave;
                }

                _regions[i].SetPulse(glow);
            }
        }

        private void FindRegions(AnatomyModel model) {
            List<StructureHighlight> regions = new List<StructureHighlight>();
            List<float> phases = new List<float>();
            for (int i = 0; i < model.Structures.Count; i++) {
                StructureHighlight highlight = model.Structures[i].GetComponent<StructureHighlight>();
                if (highlight != null) {
                    regions.Add(highlight);
                    phases.Add(model.Structures[i].transform.localPosition.y * PulseCyclesPerMetre);
                }
            }

            _regions = regions.ToArray();
            _phases = phases.ToArray();
        }
    }
}