using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// The box a figure is trimmed to when it is scanned: a torso is cut from the base of the neck to the pelvis and across the
    /// shoulders. It is kept in the model's own space, so it travels with the model to wherever the model is shown.
    /// </summary>
    public class AnatomyCutBox : MonoBehaviour {
        [SerializeField, Tooltip("The box, in this object's local space, outside which the figure is cut away.")]
        private Bounds localBounds;

        public Bounds LocalBounds {
            get { return localBounds; }
        }
    }
}