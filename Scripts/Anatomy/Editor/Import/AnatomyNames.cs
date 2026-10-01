namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>Turns source identifiers into text a person could read.</summary>
    public static class AnatomyNames {
        /// <summary>left_ventricle becomes "Left ventricle".</summary>
        public static string Prettify(string structureId) {
            if (string.IsNullOrEmpty(structureId)) {
                return string.Empty;
            }

            string spaced = structureId.Replace('_', ' ').Trim();
            if (spaced.Length == 0) {
                return structureId;
            }

            return char.ToUpperInvariant(spaced[0]) + spaced.Substring(1);
        }
    }
}