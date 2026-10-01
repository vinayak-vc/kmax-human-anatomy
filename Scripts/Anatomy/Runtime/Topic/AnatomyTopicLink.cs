using System;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// A button on a topic that leads to another topic, with the wording on it. The body map uses them to offer the two
    /// activities, which are places to do something and not parts of the body to point at.
    /// </summary>
    [Serializable]
    public class AnatomyTopicLink {
        [SerializeField] private string label;
        [SerializeField, Tooltip("Id of the topic the button opens.")]
        private string topic;

        public string Label {
            get { return label; }
        }

        public string Topic {
            get { return topic; }
        }
    }
}