using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>The components a scene needs to wire to the interface after it has been built.</summary>
    public class AnatomyInterfaceParts {
        public AnatomyInterfaceParts(AnatomyInfoPanel panel, AnatomyControls controls, AnatomyMarkers markers,
            AnatomyLayerPanel layers, ScreenFader fader, GameObject topicInterface, AnatomyLauncher launcher) {
            Panel = panel;
            Controls = controls;
            Markers = markers;
            Layers = layers;
            Fader = fader;
            TopicInterface = topicInterface;
            Launcher = launcher;
        }

        public AnatomyInfoPanel Panel { get; private set; }
        public AnatomyControls Controls { get; private set; }
        public AnatomyMarkers Markers { get; private set; }
        public AnatomyLayerPanel Layers { get; private set; }
        public ScreenFader Fader { get; private set; }

        /// <summary>The container of everything that belongs to a topic: its title, caption, badges and buttons.</summary>
        public GameObject TopicInterface { get; private set; }

        public AnatomyLauncher Launcher { get; private set; }
    }
}