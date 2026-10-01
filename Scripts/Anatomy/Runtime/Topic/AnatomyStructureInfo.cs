using System;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// What the exhibit says about one structure. The id matches <see cref="AnatomyStructure.StructureId"/>.
    /// </summary>
    [Serializable]
    public class AnatomyStructureInfo {
        [SerializeField] private string id;
        [SerializeField] private string name;
        [SerializeField] private string summary;
        [SerializeField] private string fact;
        [SerializeField, Tooltip("How far the structure moves in the exploded view, in millimetres, along the model's own " +
            "axes: x towards the patient's left, y up, z towards the back. Zero leaves it where it is.")]
        private Vector3 explodeMm;
        [SerializeField, Tooltip("How many times larger the structure is shown in the exploded view, for parts too small to see. " +
            "Every structure given a scale grows together, about their common middle, so they stay joined. Zero or one keeps the size.")]
        private float explodeScale;
        [SerializeField, Tooltip("True for a structure that is only context while the view is exploded: it turns to glass.")]
        private bool recedesWhenExploded;
        [SerializeField, Range(0f, 1f), Tooltip("How solid the structure is when nothing is being explained. Zero, the default, " +
            "means fully solid. A clear structure such as the cornea rests as faint glass so what lies behind it shows.")]
        private float opacity;
        [SerializeField, Range(0f, 1f), Tooltip("How solid the structure is while it has receded, as a share of the usual. Zero, the default, " +
            "means the usual. A structure with a great deal of edge, such as the ribs, reads better receding more quietly.")]
        private float recededSolidity;
        [SerializeField, Range(0f, 1f), Tooltip("How bright its edge glow is while it has receded, as a share of the usual. Zero means the usual.")]
        private float recededGlow;
        [SerializeField, Tooltip("True for a structure the topic describes but does not number: it has no badge, and Previous " +
            "and Next pass it by. For the small ones that would otherwise crowd the badges out.")]
        private bool unnumbered;
        [SerializeField, Tooltip("Id of the topic this structure opens when the visitor chooses to explore it, for example heart. " +
            "Only the body map uses it. Empty for none.")]
        private string topic;

        public string Id {
            get { return id; }
        }

        public string Name {
            get { return name; }
        }

        public string Summary {
            get { return summary; }
        }

        /// <summary>An optional extra line worth remembering. Empty when there is none.</summary>
        public string Fact {
            get { return fact; }
        }

        /// <summary>Where the structure moves to in the exploded view, from where it sits, in metres.</summary>
        public Vector3 ExplodeOffset {
            get { return explodeMm * 0.001f; }
        }

        /// <summary>How solid the structure is at rest, from nearly clear to fully solid.</summary>
        public float RestOpacity {
            get { return opacity > 0f ? Mathf.Min(1f, opacity) : 1f; }
        }

        /// <summary>How solid the structure is while receded, as a share of the usual.</summary>
        public float RecededSolidity {
            get { return recededSolidity > 0f ? recededSolidity : 1f; }
        }

        /// <summary>How bright its edge glow is while receded, as a share of the usual.</summary>
        public float RecededGlow {
            get { return recededGlow > 0f ? recededGlow : 1f; }
        }

        /// <summary>The id of the topic this structure opens. Empty when it opens none.</summary>
        public string Topic {
            get { return topic; }
        }

        public bool OpensTopic {
            get { return !string.IsNullOrEmpty(topic); }
        }

        /// <summary>False for a structure the topic describes without giving it a number.</summary>
        public bool IsNumbered {
            get { return !unnumbered; }
        }

        /// <summary>How many times larger the structure is shown when the view is exploded. One when it is not enlarged.</summary>
        public float ExplodeScale {
            get { return explodeScale > 1f ? explodeScale : 1f; }
        }

        /// <summary>True when the structure is enlarged in the exploded view, so picking it should explode the view.</summary>
        public bool Magnifies {
            get { return explodeScale > 1f; }
        }

        /// <summary>True when the structure is only context in the exploded view, so it turns to glass.</summary>
        public bool RecedesWhenExploded {
            get { return recedesWhenExploded; }
        }

        public bool MovesWhenExploded {
            get { return explodeMm != Vector3.zero || Magnifies; }
        }
    }
}