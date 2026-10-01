using System;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Implemented by a behaviour that gives its topic one button of its own, such as a hint or a change of instrument. The
    /// controller shows it where the Explode button would be, with the behaviour's wording, and tells the behaviour when it
    /// is pressed.
    /// </summary>
    public interface ITopicAction {
        /// <summary>Raised when the wording has changed.</summary>
        event Action ActionChanged;

        /// <summary>What the button says now.</summary>
        string ActionLabel { get; }

        /// <summary>The button was pressed.</summary>
        void PerformAction();
    }
}