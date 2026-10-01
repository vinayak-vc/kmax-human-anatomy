using System;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// One card of the launcher: a picture of the exhibit over its name. It reports a press and shows whether it is the chosen
    /// card, with a cyan bar along its top, warmer glass and a brighter outline; what choosing it does is the launcher's.
    ///
    /// <para>The card is a button in a slot the layout positions, with <see cref="UiButtonMotion"/> on it for its swell and sink.
    /// Its resting colour changes with its state, so the state is handed to the motion rather than set on the image, which
    /// the motion would put back.</para>
    /// </summary>
    public class LauncherCard : MonoBehaviour {
        [SerializeField, Tooltip("The slot the layout positions; shown or hidden as a whole.")]
        private GameObject slot;
        [SerializeField] private Button button;
        [SerializeField] private UiButtonMotion motion;
        [SerializeField] private Image thumbnail;
        [SerializeField] private TMP_Text label;
        [SerializeField, Tooltip("The cyan bar along the top of the chosen card.")]
        private Image selectBar;
        [SerializeField, Tooltip("The glass outline, which brightens on the chosen card.")]
        private Image outline;
        [SerializeField] private Color restFill = new Color(0.059f, 0.09f, 0.165f, 0.82f);
        [SerializeField] private Color chosenFill = new Color(0.03f, 0.34f, 0.41f, 0.94f);
        [SerializeField] private Color restOutline = new Color(0.2f, 0.255f, 0.333f, 0.6f);
        [SerializeField] private Color chosenOutline = new Color(0.133f, 0.827f, 0.933f, 1f);
        [SerializeField] private Color restLabel = new Color(0.886f, 0.91f, 0.941f, 1f);
        [SerializeField] private Color chosenLabel = Color.white;

        private int _index = -1;

        /// <summary>Raised with the card's place in the launcher when it is pressed.</summary>
        public event Action<int> Clicked;

        private void OnEnable() {
            button.onClick.AddListener(OnClicked);
        }

        private void OnDisable() {
            button.onClick.RemoveListener(OnClicked);
        }

        /// <summary>Shows the card with this name and picture. A card with no picture shows its name alone.</summary>
        public void Bind(int index, string text, Sprite picture) {
            _index = index;
            label.text = text;
            thumbnail.sprite = picture;
            thumbnail.enabled = picture != null;
            slot.SetActive(true);
            SetSelected(false);
        }

        /// <summary>Hides a card the launcher has no exhibit for.</summary>
        public void Hide() {
            _index = -1;
            slot.SetActive(false);
        }

        public void SetSelected(bool selected) {
            selectBar.enabled = selected;
            outline.color = selected ? chosenOutline : restOutline;
            label.color = selected ? chosenLabel : restLabel;
            motion.SetRestTint(selected ? chosenFill : restFill);
        }

        private void OnClicked() {
            if (_index >= 0 && Clicked != null) {
                Clicked(_index);
            }
        }
    }
}