namespace ViitorCloud.KmaxDisplay {
    /// <summary>What the pen is doing, as its beam shows it: one colour for each.</summary>
    public enum StylusBeamState {
        /// <summary>No button is down. The pen is scanning; the beam is cyan.</summary>
        Calm,

        /// <summary>The select button is down on something that can be selected. The beam is green.</summary>
        Select,

        /// <summary>The secondary button, which resets or goes back, is down. The beam is amber.</summary>
        Secondary,

        /// <summary>The tertiary button, or the select button held over nothing as the zoom gesture, is down. The beam is violet.</summary>
        Tertiary
    }
}