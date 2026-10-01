using System;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// One numbered badge. It reports the pointer arriving, leaving and pressing, and shows the look it is told;
    /// which structure it stands for is decided elsewhere.
    ///
    /// <para>The badge sits inside a slot that the layout moves. The badge itself is never moved, because
    /// <see cref="ViitorCloud.KmaxDisplay.UiButtonMotion"/> captures its position once and re-applies it every frame.</para>
    /// </summary>
    public class AnatomyMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler {
        private static readonly Color IdleDisc = new Color(0.05f, 0.12f, 0.2f, 0.92f);
        private static readonly Color IdleRing = new Color(0.5f, 0.85f, 1f, 0.85f);
        private static readonly Color IdleNumber = new Color(0.96f, 0.98f, 1f, 1f);
        private static readonly Color HoverDisc = new Color(0.12f, 0.3f, 0.45f, 0.98f);
        private static readonly Color HoverRing = new Color(0.78f, 0.96f, 1f, 1f);
        private static readonly Color SelectedDisc = new Color(0.55f, 0.9f, 1f, 1f);
        private static readonly Color SelectedRing = new Color(1f, 1f, 1f, 1f);
        private static readonly Color SelectedNumber = new Color(0.03f, 0.08f, 0.13f, 1f);
        private static readonly Color RecededDisc = new Color(0.05f, 0.12f, 0.2f, 0.55f);
        private static readonly Color RecededRing = new Color(0.5f, 0.85f, 1f, 0.5f);
        private static readonly Color RecededNumber = new Color(0.96f, 0.98f, 1f, 0.6f);

        [SerializeField, Tooltip("Moved by the layout. The badge below it stays put.")]
        private RectTransform slot;
        [SerializeField] private Image disc;
        [SerializeField] private Image ring;
        [SerializeField] private TMP_Text numberText;

        /// <summary>Raised with the structure id when the pointer arrives on the badge.</summary>
        public event Action<string> Entered;

        /// <summary>Raised with the structure id when the pointer leaves the badge.</summary>
        public event Action<string> Exited;

        /// <summary>Raised with the structure id when the badge is pressed and released.</summary>
        public event Action<string> Clicked;

        public string StructureId { get; private set; }

        public RectTransform Slot {
            get { return slot; }
        }

        /// <summary>Shows the badge with this number, standing for this structure.</summary>
        public void Assign(string structureId, int number) {
            StructureId = structureId;
            numberText.text = number.ToString();
            slot.gameObject.SetActive(true);
            SetLook(HighlightState.Normal);
        }

        /// <summary>Hides the badge and forgets its structure.</summary>
        public void Clear() {
            StructureId = null;
            slot.gameObject.SetActive(false);
        }

        public void SetLook(HighlightState state) {
            switch (state) {
                case HighlightState.Focused:
                    Paint(SelectedDisc, SelectedRing, SelectedNumber);
                    break;
                case HighlightState.Hovered:
                case HighlightState.Previewed:
                    Paint(HoverDisc, HoverRing, IdleNumber);
                    break;
                case HighlightState.Dimmed:
                    Paint(RecededDisc, RecededRing, RecededNumber);
                    break;
                default:
                    Paint(IdleDisc, IdleRing, IdleNumber);
                    break;
            }
        }

        public void OnPointerEnter(PointerEventData eventData) {
            if (StructureId != null && Entered != null) {
                Entered(StructureId);
            }
        }

        public void OnPointerExit(PointerEventData eventData) {
            if (StructureId != null && Exited != null) {
                Exited(StructureId);
            }
        }

        public void OnPointerClick(PointerEventData eventData) {
            if (StructureId != null && Clicked != null) {
                Clicked(StructureId);
            }
        }

        private void Paint(Color discColor, Color ringColor, Color numberColor) {
            disc.color = discColor;
            ring.color = ringColor;
            numberText.color = numberColor;
        }
    }
}