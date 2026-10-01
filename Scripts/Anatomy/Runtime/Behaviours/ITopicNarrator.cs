using System;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Implemented by a behaviour that has its own things to say, such as how many organs are back in place or what the scan
    /// is passing through. The controller shows its message in the caption in place of the pointer's, while it has one.
    /// </summary>
    public interface ITopicNarrator {
        /// <summary>Raised when the message has changed.</summary>
        event Action MessageChanged;

        /// <summary>What the caption should say now, or null when the behaviour has nothing to add.</summary>
        TopicMessage Message { get; }
    }
}