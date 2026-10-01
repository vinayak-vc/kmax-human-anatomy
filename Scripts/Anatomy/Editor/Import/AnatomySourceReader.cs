using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Reads source OBJ files from the DOSCH pack and their material libraries into geometry, one piece for each
    /// material group, which in the pack is one named structure. Shared by the topic importer and the body importer.
    /// </summary>
    public static class AnatomySourceReader {
        /// <summary>
        /// Adds every non-empty material group of each file to <paramref name="parts"/>, in file order, and the files'
        /// materials to <paramref name="materials"/>. A material name that two files share keeps the first's. Returns
        /// false, after logging why, when a file cannot be read.
        /// </summary>
        /// <param name="sourceFiles">File paths relative to <see cref="AnatomyPaths.SourceRoot"/>.</param>
        public static bool Read(string[] sourceFiles, List<AnatomyMeshData> parts, Dictionary<string, MtlMaterial> materials) {
            for (int i = 0; i < sourceFiles.Length; i++) {
                string objPath = AnatomyPaths.ToAbsolute(AnatomyPaths.SourceRoot + "/" + sourceFiles[i]);
                ObjModel model = ObjParser.Parse(objPath);
                if (model == null) {
                    return false;
                }

                if (!string.IsNullOrEmpty(model.MaterialLibrary)) {
                    string mtlPath = Path.Combine(Path.GetDirectoryName(objPath), model.MaterialLibrary);
                    MergeMaterials(materials, MtlParser.Parse(mtlPath));
                }

                for (int g = 0; g < model.Groups.Count; g++) {
                    AnatomyMeshData data = AnatomyMeshBuilder.Build(model, model.Groups[g]);
                    if (data.Triangles.Count > 0) {
                        parts.Add(data);
                    }
                }
            }

            return true;
        }

        private static void MergeMaterials(Dictionary<string, MtlMaterial> target, Dictionary<string, MtlMaterial> additions) {
            foreach (KeyValuePair<string, MtlMaterial> pair in additions) {
                if (!target.ContainsKey(pair.Key)) {
                    target.Add(pair.Key, pair.Value);
                }
            }
        }
    }
}