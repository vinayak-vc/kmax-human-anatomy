using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// One see-through layer of the body map, such as the skeleton or the muscles: a single merged mesh drawn as a glow.
    /// The importer builds it and <see cref="BodyBehaviour"/> shows and hides it.
    /// </summary>
    public class AnatomyLayer : MonoBehaviour {
        [SerializeField, Tooltip("Stable id, for example skeleton.")]
        private string layerId;
        [SerializeField, Tooltip("What the layer's button says.")]
        private string label;
        [SerializeField, Tooltip("Renderer that draws the layer.")]
        private Renderer layerRenderer;
        [SerializeField, Tooltip("Colour the glow is multiplied by.")]
        private Color tint = Color.white;
        [SerializeField, Tooltip("Colour of the swatch on the layer's button, which says what colour the layer is drawn in.")]
        private Color swatch = Color.white;
        [SerializeField, Range(0f, 1f), Tooltip("How bright the glow is while the layer is shown.")]
        private float intensity = 0.6f;
        [SerializeField, Tooltip("Whether the layer is shown when the body map opens.")]
        private bool startsShown = true;

        public string LayerId {
            get { return layerId; }
        }

        public string Label {
            get { return label; }
        }

        public Renderer LayerRenderer {
            get { return layerRenderer; }
        }

        public Color Tint {
            get { return tint; }
        }

        public Color Swatch {
            get { return swatch; }
        }

        public float Intensity {
            get { return intensity; }
        }

        public bool StartsShown {
            get { return startsShown; }
        }
    }
}