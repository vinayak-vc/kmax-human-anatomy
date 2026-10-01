using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// The parts of a Wavefront material the importer uses: the diffuse tint, how shiny it is and which
    /// texture it is painted with.
    /// </summary>
    public class MtlMaterial {
        public MtlMaterial(string name) {
            Name = name;
            DiffuseColor = Color.white;
            SpecularColor = Color.black;
        }

        public string Name { get; private set; }
        public Color DiffuseColor { get; set; }
        public Color SpecularColor { get; set; }
        public float Shininess { get; set; }

        /// <summary>Absolute path of the diffuse texture, or empty when the material has none.</summary>
        public string DiffuseTexturePath { get; set; }
    }
}