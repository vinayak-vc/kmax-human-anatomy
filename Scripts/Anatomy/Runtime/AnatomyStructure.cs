using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// One named anatomical structure: a single material group of a source mesh, split out so it can be
    /// pointed at, highlighted and moved on its own.
    ///
    /// The id is the source material name and never changes, so topic data can refer to a structure
    /// without holding a reference to it. The fallback name is derived from that id and is only what
    /// shows when topic data supplies nothing better.
    /// </summary>
    public class AnatomyStructure : MonoBehaviour {
        [SerializeField, Tooltip("Stable id: the source material name, for example left_ventricle.")]
        private string structureId;
        [SerializeField, Tooltip("Readable name derived from the id. Topic data overrides it on screen.")]
        private string fallbackName;
        [SerializeField, Tooltip("Renderer that draws this structure.")]
        private Renderer structureRenderer;
        [SerializeField, Tooltip("Transparent twin of the structure's material, swapped in while it recedes.")]
        private Material ghostMaterial;
        [SerializeField, Tooltip("Edge glow drawn over the transparent twin. Shared by every structure.")]
        private Material rimMaterial;
        [SerializeField, Tooltip("Where a numbered marker's line ends, in this object's local space: a surface " +
            "point near the middle of the structure.")]
        private Vector3 labelAnchor;

        public string StructureId {
            get { return structureId; }
        }

        public string FallbackName {
            get { return fallbackName; }
        }

        public Renderer StructureRenderer {
            get { return structureRenderer; }
        }

        public Material GhostMaterial {
            get { return ghostMaterial; }
        }

        public Material RimMaterial {
            get { return rimMaterial; }
        }

        public Vector3 LabelAnchor {
            get { return labelAnchor; }
        }
    }
}