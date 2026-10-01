namespace ViitorCloud.KmaxAnatomy {
    /// <summary>Where the exhibit is in its unattended loop.</summary>
    public enum KioskState {
        /// <summary>The body map is on show and waiting for a visitor.</summary>
        Hub,

        /// <summary>A topic is on show.</summary>
        Topic,

        /// <summary>Nobody has touched the exhibit for a while, so the body map is showing itself.</summary>
        Attract
    }
}