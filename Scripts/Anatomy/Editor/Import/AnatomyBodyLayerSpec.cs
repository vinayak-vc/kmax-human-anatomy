using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// One see-through layer of the body map: the source files whose structures are merged into a single glowing mesh,
    /// and how that glow looks.
    /// </summary>
    public class AnatomyBodyLayerSpec {
        public AnatomyBodyLayerSpec(string id, string label, string[] sourceFiles) {
            Id = id;
            Label = label;
            SourceFiles = sourceFiles;
            Tint = Color.white;
            Swatch = Color.white;
            Intensity = 0.6f;
            RimPower = 2.4f;
            StartsShown = true;
        }

        /// <summary>Names the layer's object in the prefab and its mesh and material assets.</summary>
        public string Id { get; private set; }

        /// <summary>What the layer button says.</summary>
        public string Label { get; private set; }

        /// <summary>File paths relative to <see cref="AnatomyPaths.SourceRoot"/>.</summary>
        public string[] SourceFiles { get; private set; }

        /// <summary>Colour of the glow.</summary>
        public Color Tint { get; set; }

        /// <summary>Colour of the swatch on the layer's button. It differs from the tint for a layer drawn in the pack's own colours.</summary>
        public Color Swatch { get; set; }

        /// <summary>How bright the glow is, from 0 to 1.</summary>
        public float Intensity { get; set; }

        /// <summary>How tightly the glow hugs the edges of a surface. Higher leaves the middle of a surface clearer.</summary>
        public float RimPower { get; set; }

        /// <summary>How much of the glow the middle of a surface keeps, from 0 for edges only to 1 for even.</summary>
        public float Floor { get; set; }

        /// <summary>Whether the layer is shown when the body map opens.</summary>
        public bool StartsShown { get; set; }

        /// <summary>
        /// True to keep the colour the DOSCH pack gives each structure, such as red arteries and blue veins, in place of one
        /// <see cref="Tint"/> for the whole layer.
        /// </summary>
        public bool UsesSourceColours { get; set; }
    }
}