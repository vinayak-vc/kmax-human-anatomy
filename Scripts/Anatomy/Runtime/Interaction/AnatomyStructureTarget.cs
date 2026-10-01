using System;

using UnityEngine;
using UnityEngine.EventSystems;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Turns the event system's pointer events on one structure's collider into plain C# events.
    ///
    /// <para>The same three events arrive from the mouse and from the stylus ray, because the Kmax input
    /// module feeds both through the event system. Nothing here knows which one it was.</para>
    /// </summary>
    [RequireComponent(typeof(AnatomyStructure))]
    public class AnatomyStructureTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler {
        /// <summary>
        /// Furthest the pointer may travel between press and release for it to count as a click. Beyond it the
        /// viewer was turning the model, and letting go over a structure must not pick it.
        /// </summary>
        private const float ClickTolerancePixels = 12f;

        private AnatomyStructure _structure;

        /// <summary>Raised when the pointer arrives on the structure.</summary>
        public event Action<AnatomyStructure> Entered;

        /// <summary>Raised when the pointer leaves the structure.</summary>
        public event Action<AnatomyStructure> Exited;

        /// <summary>Raised when the structure is pressed and released.</summary>
        public event Action<AnatomyStructure> Clicked;

        private void Awake() {
            _structure = GetComponent<AnatomyStructure>();
        }

        public void OnPointerEnter(PointerEventData eventData) {
            if (Entered != null) {
                Entered(_structure);
            }
        }

        public void OnPointerExit(PointerEventData eventData) {
            if (Exited != null) {
                Exited(_structure);
            }
        }

        public void OnPointerClick(PointerEventData eventData) {
            float travelled = (eventData.position - eventData.pressPosition).magnitude;
            if (travelled > ClickTolerancePixels) {
                return;
            }

            if (Clicked != null) {
                Clicked(_structure);
            }
        }
    }
}