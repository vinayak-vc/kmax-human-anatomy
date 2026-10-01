using System.IO;

using UnityEngine;

using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Where the DOSCH source pack lives and where everything generated from it is written.
    ///
    /// The source folder ends in a tilde so Unity does not import it: the pack is 526 OBJ files and the
    /// importer reads only the ones a topic needs. Both folders are ignored by git.
    /// </summary>
    public static class AnatomyPaths {
        public const string SourceRoot = KmaxRigBuilder.ModuleRoot + "/Source~/DOSCH 3D HUMAN ANATOMY/OBJ/Male";
        public const string GeneratedRoot = KmaxRigBuilder.ModuleRoot + "/Generated";
        public const string PrefabFolder = GeneratedRoot + "/Resources/Anatomy";
        public const string MeshFolder = GeneratedRoot + "/Meshes";
        public const string MaterialFolder = GeneratedRoot + "/Materials";
        public const string TextureFolder = GeneratedRoot + "/Textures";

        /// <summary>Absolute path of a project-relative path such as Assets/Games/...</summary>
        public static string ToAbsolute(string projectRelativePath) {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, projectRelativePath);
        }
    }
}