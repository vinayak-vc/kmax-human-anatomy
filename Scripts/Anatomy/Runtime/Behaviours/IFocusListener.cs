using System.Collections.Generic;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// A topic behaviour that wants to know which structures the visitor is being shown, so it can put on a demonstration
    /// of them: the eye turning the way a picked muscle pulls it, for one.
    /// </summary>
    public interface IFocusListener {
        /// <summary>
        /// The structures now in focus: the picked one, the tour step's, or none. The caller reuses the list, so copy
        /// anything that has to outlive the call.
        /// </summary>
        void OnFocusChanged(IReadOnlyList<string> structureIds);
    }
}