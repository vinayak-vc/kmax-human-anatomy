using System;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// One stop on a guided tour: the structures to draw attention to, and what to say about them. A step may also
    /// pull the model apart or put it back together, and turn the view, so a tour can show a part that is hidden
    /// or too small to see from where the last step was.
    /// </summary>
    [Serializable]
    public class AnatomyTourStep {
        private const string ApartKey = "apart";
        private const string TogetherKey = "together";

        [SerializeField] private string[] structures = new string[0];
        [SerializeField] private string caption;
        [SerializeField, Tooltip("What this step does to the exploded view: apart, together, or empty to leave it as it is.")]
        private string exploded;
        [SerializeField, Tooltip("True when this step turns the view to the yaw and pitch below.")]
        private bool setsView;
        [SerializeField] private float yaw;
        [SerializeField] private float pitch;
        [SerializeField, Tooltip("Zoom this step frames its structures at, as a multiple of the opening size. Zero leaves the zoom alone.")]
        private float zoom;

        public string[] Structures {
            get { return structures; }
        }

        public string Caption {
            get { return caption; }
        }

        public TourExplodeChange Explode {
            get {
                if (exploded == ApartKey) {
                    return TourExplodeChange.Apart;
                }

                return exploded == TogetherKey ? TourExplodeChange.Together : TourExplodeChange.Leave;
            }
        }

        public bool SetsView {
            get { return setsView; }
        }

        public float Yaw {
            get { return yaw; }
        }

        public float Pitch {
            get { return pitch; }
        }

        /// <summary>True when this step sets the zoom, so a visitor need not zoom to see what it explains.</summary>
        public bool SetsZoom {
            get { return zoom >= 1f; }
        }

        public float Zoom {
            get { return zoom; }
        }

        /// <summary>True when the data names an exploded-view change this code does not know.</summary>
        public bool HasUnknownExplodeKey {
            get { return !string.IsNullOrEmpty(exploded) && exploded != ApartKey && exploded != TogetherKey; }
        }

        public bool Includes(string structureId) {
            for (int i = 0; i < structures.Length; i++) {
                if (structures[i] == structureId) {
                    return true;
                }
            }

            return false;
        }
    }
}