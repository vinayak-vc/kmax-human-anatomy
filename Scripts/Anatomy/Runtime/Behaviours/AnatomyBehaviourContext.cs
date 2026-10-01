using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// What the scene gives a topic's behaviour when it is attached: the topic's own data, and the sound, the pen's haptics
    /// and the pen's tip, any of which may be missing (the exhibit is silent without a sound, and works with the mouse
    /// standing in for a pen). A behaviour takes what it needs and tolerates what is not there.
    /// </summary>
    public class AnatomyBehaviourContext {
        public AnatomyBehaviourContext(AnatomyTopicData data, AnatomyAudio sound, StylusHaptics haptics, StylusTip tip) {
            Data = data;
            Sound = sound;
            Haptics = haptics;
            Tip = tip;
        }

        public AnatomyTopicData Data { get; private set; }
        public AnatomyAudio Sound { get; private set; }
        public StylusHaptics Haptics { get; private set; }
        public StylusTip Tip { get; private set; }
    }
}