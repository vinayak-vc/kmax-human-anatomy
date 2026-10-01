using System;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>One card of the launcher: the topic it opens and the short name it carries.</summary>
    [Serializable]
    public class AnatomyLauncherEntry {
        [SerializeField, Tooltip("Id of the topic the card opens, for example heart.")]
        private string topic;
        [SerializeField, Tooltip("The name on the card and on the Load button.")]
        private string label;

        public string Topic {
            get { return topic; }
        }

        public string Label {
            get { return label; }
        }
    }
}