namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// What to import for one topic: the id the prefab is saved under, and the source OBJ files whose
    /// structures make it up, relative to <see cref="AnatomyPaths.SourceRoot"/>.
    /// </summary>
    public class AnatomyImportSpec {
        public AnatomyImportSpec(string modelId, string[] sourceFiles) {
            ModelId = modelId;
            SourceFiles = sourceFiles;
        }

        public string ModelId { get; private set; }
        public string[] SourceFiles { get; private set; }

        /// <summary>True for the heart: its meshes get the beat baked in as blend shapes.</summary>
        public bool BakesHeartbeat { get; set; }

        /// <summary>True for the ear: its drum and bones get their vibration baked in as a blend shape.</summary>
        public bool BakesHearing { get; set; }

        /// <summary>True for the eye: its soft tissue, iris and lens get the shapes that let it look about and focus.</summary>
        public bool BakesGaze { get; set; }

        /// <summary>True for the chest: its ribs, diaphragm, lungs and airway get one breath baked in as a blend shape.</summary>
        public bool BakesBreathing { get; set; }

        /// <summary>True for the skull and face: the muscles get the shape that lets the jaw open under them.</summary>
        public bool BakesJaw { get; set; }
    }
}