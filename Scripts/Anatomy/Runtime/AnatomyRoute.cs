using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// A route through a model, measured from the model's own shape when it is imported: a line of points, and how wide
    /// something travelling along it is at each. The ear has one for a sound going in and the lungs have one for a breath
    /// going down the windpipe. Positions are in the model's space, in metres.
    /// </summary>
    public class AnatomyRoute : MonoBehaviour {
        [SerializeField, Tooltip("What the route is for, for example sound or air. A model may carry several.")]
        private string routeName;
        [SerializeField, Tooltip("Points along the route, in the order things travel.")]
        private Vector3[] points = new Vector3[0];
        [SerializeField, Tooltip("Half-width of what travels, at each point, in metres.")]
        private float[] radii = new float[0];

        public string RouteName {
            get { return routeName; }
        }

        public IReadOnlyList<Vector3> Points {
            get { return points; }
        }

        public IReadOnlyList<float> Radii {
            get { return radii; }
        }

        /// <summary>True when there is a route to draw: at least two points, each with a width.</summary>
        public bool IsUsable {
            get { return points.Length >= 2 && points.Length == radii.Length; }
        }

        /// <summary>The route of this name that the model carries, or null when it has none.</summary>
        public static AnatomyRoute Find(GameObject model, string name) {
            AnatomyRoute[] routes = model.GetComponents<AnatomyRoute>();
            for (int i = 0; i < routes.Length; i++) {
                if (routes[i].routeName == name) {
                    return routes[i];
                }
            }

            return null;
        }
    }
}