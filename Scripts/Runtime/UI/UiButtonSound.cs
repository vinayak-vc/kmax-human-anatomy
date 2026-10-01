using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ViitorCloud.KmaxDisplay {
    /// <summary>
    /// Sounds a button's hover and press through the <see cref="PersistentAudioDirector"/>: a tick when the pointer arrives and a
    /// short drop when the button is pressed. It sits on the button itself, beside <see cref="UiButtonMotion"/>, because the
    /// event system only delivers a click to the object that took the press.
    ///
    /// <para>A button that cannot be pressed at the moment, one dimmed because it has nowhere to go, makes no sound. With no
    /// director in the scene the button is silent and nothing else changes.</para>
    /// </summary>
    public class UiButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler {
        private Selectable _selectable;

        private void Awake() {
            _selectable = GetComponent<Selectable>();
        }

        public void OnPointerEnter(PointerEventData eventData) {
            PersistentAudioDirector director = PersistentAudioDirector.Instance;
            if (director != null && IsAvailable()) {
                director.PlayUiHover();
            }
        }

        public void OnPointerDown(PointerEventData eventData) {
            PersistentAudioDirector director = PersistentAudioDirector.Instance;
            if (director != null && IsAvailable()) {
                director.PlayUiClick();
            }
        }

        private bool IsAvailable() {
            return _selectable == null || _selectable.IsInteractable();
        }
    }
}