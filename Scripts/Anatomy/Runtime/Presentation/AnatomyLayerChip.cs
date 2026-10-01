using System;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// One layer button: a colour swatch and the layer's name, bright while the layer is shown and dim while it is hidden.
    /// It reports presses and shows only what it is told.
    ///
    /// <para>The button sits in a slot that the layout group positions, and the slot is what is shown or hidden, because
    /// <see cref="ViitorCloud.KmaxDisplay.UiButtonMotion"/> keeps the button itself where it captured it.</para>
    /// </summary>
    public class AnatomyLayerChip : MonoBehaviour {
        private const float OffAlpha = 0.35f;

        [SerializeField, Tooltip("Slot the layout positions; shown or hidden as a whole.")]
        private GameObject slot;
        [SerializeField] private Button button;
        [SerializeField] private Image swatch;
        [SerializeField] private TMP_Text label;

        private Color _swatchColour = Color.white;
        private Color _labelColour = Color.white;

        /// <summary>Raised with this chip when it is pressed.</summary>
        public event Action<AnatomyLayerChip> Clicked;

        private void Awake() {
            if (label != null) {
                _labelColour = label.color;
            }
        }

        private void OnEnable() {
            button.onClick.AddListener(OnClicked);
        }

        private void OnDisable() {
            button.onClick.RemoveListener(OnClicked);
        }

        /// <summary>Shows the chip for a layer with this name and colour.</summary>
        public void Show(string text, Color colour, bool on) {
            label.text = text;
            _swatchColour = colour;
            slot.SetActive(true);
            SetOn(on);
        }

        public void Hide() {
            slot.SetActive(false);
        }

        /// <summary>Brightens the chip for a layer that is shown and dims it for one that is hidden.</summary>
        public void SetOn(bool on) {
            float alpha = on ? 1f : OffAlpha;
            Color swatchColour = _swatchColour;
            swatchColour.a = alpha;
            swatch.color = swatchColour;
            Color labelColour = _labelColour;
            labelColour.a = alpha;
            label.color = labelColour;
        }

        private void OnClicked() {
            if (Clicked != null) {
                Clicked(this);
            }
        }
    }
}