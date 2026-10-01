using System;
using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Everything the exhibit says and decides about one topic, read from a JSON file so the wording can
    /// be reviewed and changed without touching code.
    ///
    /// <para>Strings are keyed by structure id, so a second language is a second file with the same ids.</para>
    /// </summary>
    [Serializable]
    public class AnatomyTopicData {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField] private string subtitle;
        [SerializeField] private string intro;
        [SerializeField, Tooltip("Names a topic-specific animation, for example heartbeat. Empty for none.")]
        private string behaviour;
        [SerializeField, Tooltip("Multiple of real size the model is shown at.")]
        private float displayScale = 1f;
        [SerializeField, Tooltip("How far in front of the glass the model's centre sits, in metres.")]
        private float popOut = 0.03f;
        [SerializeField, Tooltip("Furthest the viewer can zoom in, as a multiple of the size the topic opens at.")]
        private float maxZoom = 2.5f;
        [SerializeField] private float homeYaw;
        [SerializeField] private float homePitch;
        [SerializeField, Tooltip("What the button that pulls the topic apart says, for example Magnify. Empty uses the button's own wording.")]
        private string explodeLabel;
        [SerializeField, Tooltip("What the same button says while the topic is pulled apart. Empty uses the button's own wording.")]
        private string assembleLabel;
        [SerializeField, Tooltip("What the Reset button says for this topic, for example Start again. Empty uses the button's own wording.")]
        private string resetLabel;
        [SerializeField, Tooltip("True for a topic whose interaction happens in the room's space, where the pen touches things where they " +
            "appear: the view is held still, and there is no zoom and no depth shifting, so nothing moves under the visitor's hand.")]
        private bool stationary;
        [SerializeField, Tooltip("False for a topic whose structures are not pointed at or picked, because the pen's tip does something else with them.")]
        private bool selectable = true;
        [SerializeField, Tooltip("Buttons that lead from this topic to others. The body map offers its two activities this way.")]
        private AnatomyTopicLink[] links = new AnatomyTopicLink[0];
        [SerializeField] private AnatomyStructureInfo[] structures = new AnatomyStructureInfo[0];
        [SerializeField] private AnatomyTourStep[] tour = new AnatomyTourStep[0];

        public string Id {
            get { return id; }
        }

        public string Title {
            get { return title; }
        }

        public string Subtitle {
            get { return subtitle; }
        }

        public string Intro {
            get { return intro; }
        }

        public string Behaviour {
            get { return behaviour; }
        }

        public float DisplayScale {
            get { return displayScale; }
        }

        public float PopOut {
            get { return popOut; }
        }

        public float MaxZoom {
            get { return maxZoom; }
        }

        public float HomeYaw {
            get { return homeYaw; }
        }

        public float HomePitch {
            get { return homePitch; }
        }

        public string ExplodeLabel {
            get { return explodeLabel; }
        }

        public string AssembleLabel {
            get { return assembleLabel; }
        }

        public string ResetLabel {
            get { return resetLabel; }
        }

        /// <summary>True when the view, the zoom and the depth are all held still, because the pen works in the room's space.</summary>
        public bool IsStationary {
            get { return stationary; }
        }

        /// <summary>False when structures are not pointed at or picked, because the topic gives the pen's tip another job.</summary>
        public bool IsSelectable {
            get { return selectable; }
        }

        public IReadOnlyList<AnatomyTopicLink> Links {
            get { return links; }
        }

        public IReadOnlyList<AnatomyStructureInfo> Structures {
            get { return structures; }
        }

        public IReadOnlyList<AnatomyTourStep> Tour {
            get { return tour; }
        }

        /// <summary>True when at least one structure has somewhere to go in the exploded view.</summary>
        public bool CanExplode {
            get {
                for (int i = 0; i < structures.Length; i++) {
                    if (structures[i].MovesWhenExploded) {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>The text for a structure, or null when the topic says nothing about it.</summary>
        public AnatomyStructureInfo FindStructure(string structureId) {
            for (int i = 0; i < structures.Length; i++) {
                if (structures[i].Id == structureId) {
                    return structures[i];
                }
            }

            return null;
        }
    }
}