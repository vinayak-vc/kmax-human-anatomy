using System;
using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// The layer buttons down the left of the body map: one for each layer of the body, to show or hide it. It is bound to
    /// the body's behaviour when the body map opens and hidden in every other topic.
    ///
    /// <para>The buttons are a fixed pool built with the scene. A body with more layers than the pool holds shows the first
    /// ones and logs a warning.</para>
    /// </summary>
    public class AnatomyLayerPanel : MonoBehaviour {
        [SerializeField, Tooltip("The panel's contents, shown while a body is bound and hidden otherwise.")]
        private GameObject content;
        [SerializeField, Tooltip("The pool of layer buttons, in layer order.")]
        private AnatomyLayerChip[] chips = new AnatomyLayerChip[0];

        private BodyBehaviour _body;

        private void OnEnable() {
            for (int i = 0; i < chips.Length; i++) {
                chips[i].Clicked += OnChipClicked;
            }

            content.SetActive(_body != null);
        }

        private void OnDisable() {
            for (int i = 0; i < chips.Length; i++) {
                chips[i].Clicked -= OnChipClicked;
            }
        }

        /// <summary>Shows a button for each of the body's layers and keeps them in step with it.</summary>
        public void Bind(BodyBehaviour body) {
            Unbind();
            _body = body;
            IReadOnlyList<AnatomyLayer> layers = body.Layers;
            if (layers.Count > chips.Length) {
                Debug.LogWarning($"[Anatomy] The body has {layers.Count} layers but there are only {chips.Length} layer buttons; " +
                    "the rest cannot be switched.", this);
            }

            for (int i = 0; i < chips.Length; i++) {
                if (i < layers.Count) {
                    chips[i].Show(layers[i].Label, layers[i].Swatch, body.IsShown(i));
                } else {
                    chips[i].Hide();
                }
            }

            body.LayersChanged += OnLayersChanged;
            content.SetActive(layers.Count > 0);
        }

        /// <summary>Hides the panel, for a topic that has no layers.</summary>
        public void Unbind() {
            if (_body != null) {
                _body.LayersChanged -= OnLayersChanged;
                _body = null;
            }

            content.SetActive(false);
        }

        private void OnLayersChanged() {
            for (int i = 0; i < chips.Length && i < _body.Layers.Count; i++) {
                chips[i].SetOn(_body.IsShown(i));
            }
        }

        private void OnChipClicked(AnatomyLayerChip chip) {
            int index = Array.IndexOf(chips, chip);
            if (_body != null && index >= 0) {
                _body.ToggleLayer(index);
            }
        }
    }
}