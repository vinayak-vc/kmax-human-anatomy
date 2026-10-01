namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Implemented by a behaviour that restarts when the visitor presses Reset, the R key or the pen's reset button, which
    /// the controller turns into a call here after it has cleared the selection and the view.
    /// </summary>
    public interface IResetListener {
        void OnResetRequested();
    }
}