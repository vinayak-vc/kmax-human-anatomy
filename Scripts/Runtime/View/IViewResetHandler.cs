namespace ViitorCloud.KmaxDisplay {
    /// <summary>
    /// Implemented by whatever owns the exhibit's overall state, so that the reset gesture -
    /// the R key, the Reset button, the stylus secondary button - clears that state as well as
    /// the camera.
    ///
    /// <para><see cref="ViewerFlyController"/> only knows how to put the camera back. A scene
    /// usually has more to undo: a focused part, a swapped material, an open panel. Rather than
    /// the camera knowing about any of that, the scene's controller implements this and the
    /// camera calls it, falling back to a plain camera reset when nothing does.</para>
    /// </summary>
    public interface IViewResetHandler {
        /// <summary>
        /// Return the exhibit to its opening state. Implementations are expected to call
        /// <see cref="ViewerFlyController.ResetView"/> themselves as part of that, so the camera
        /// does not move twice.
        /// </summary>
        void ResetToHome();
    }
}
