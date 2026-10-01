using System;
using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// What the launcher says and which exhibits it offers, read from a JSON file so the wording and the order can be changed
    /// without touching code. The order of the entries is the order of the cards, reading along the first row and then the
    /// second, and it is the order the Next button steps through the exhibits.
    /// </summary>
    [Serializable]
    public class AnatomyLauncherData {
        [SerializeField, Tooltip("The heading of the screen.")]
        private string title = "SELECT AN EXHIBIT";
        [SerializeField, Tooltip("The wording of the Load button. {0} is the name of the chosen exhibit.")]
        private string loadFormat = "Load {0}";
        [SerializeField] private AnatomyLauncherEntry[] entries = new AnatomyLauncherEntry[0];

        public string Title {
            get { return title; }
        }

        public string LoadFormat {
            get { return loadFormat; }
        }

        public IReadOnlyList<AnatomyLauncherEntry> Entries {
            get { return entries; }
        }
    }
}