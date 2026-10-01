using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Root of one imported anatomical model, at real size in metres.
    ///
    /// The importer centres the model on its own bounds, so this object's origin is the middle of the
    /// anatomy and the whole model is placed at a chosen depth by moving this object alone.
    /// </summary>
    public class AnatomyModel : MonoBehaviour {
        [SerializeField, Tooltip("Id the model was imported under, for example heart.")]
        private string modelId;
        [SerializeField, Tooltip("Bounds of every structure at real size, in this object's local space.")]
        private Bounds localBounds;
        [SerializeField, Tooltip("Every structure of the model, in source order.")]
        private AnatomyStructure[] structures = new AnatomyStructure[0];

        public string ModelId {
            get { return modelId; }
        }

        public Bounds LocalBounds {
            get { return localBounds; }
        }

        public IReadOnlyList<AnatomyStructure> Structures {
            get { return structures; }
        }

        /// <summary>The structure with this id, or null when the model has none.</summary>
        public AnatomyStructure FindStructure(string structureId) {
            for (int i = 0; i < structures.Length; i++) {
                AnatomyStructure candidate = structures[i];
                if (candidate != null && candidate.StructureId == structureId) {
                    return candidate;
                }
            }

            return null;
        }
    }
}