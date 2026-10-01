namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// One organ of the torso, which the two activities draw: the source files that make it up (a high-resolution mesh, since the
    /// organ is looked at close and cannot be zoomed away from) and whether it is one of the pieces of the organ puzzle.
    /// </summary>
    public class AnatomyTorsoOrganSpec {
        public AnatomyTorsoOrganSpec(string id, string[] sourceFiles, bool isPuzzlePiece) {
            Id = id;
            SourceFiles = sourceFiles;
            IsPuzzlePiece = isPuzzlePiece;
        }

        /// <summary>The structure's id, which the topics' text is written against.</summary>
        public string Id { get; private set; }

        /// <summary>File paths relative to <see cref="AnatomyPaths.SourceRoot"/>. Files that use one atlas become one structure.</summary>
        public string[] SourceFiles { get; private set; }

        /// <summary>True for the ten organs that are loose in the puzzle; the scan shows all of them.</summary>
        public bool IsPuzzlePiece { get; private set; }
    }
}