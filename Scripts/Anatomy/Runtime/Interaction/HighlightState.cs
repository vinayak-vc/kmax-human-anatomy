namespace ViitorCloud.KmaxAnatomy {
    /// <summary>How a structure is drawn relative to its neighbours.</summary>
    public enum HighlightState {
        /// <summary>Nothing has the viewer's attention; drawn as authored.</summary>
        Normal,

        /// <summary>The pointer is on it.</summary>
        Hovered,

        /// <summary>It is what is being explained: selected, or part of the current tour step.</summary>
        Focused,

        /// <summary>Something else is being explained, so this one recedes to glass.</summary>
        Dimmed,

        /// <summary>The pointer is on a structure that has receded: it comes part-way forward so it can be read.</summary>
        Previewed
    }
}