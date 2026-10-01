using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// One place on the body map that leads into a topic: an organ drawn solid inside the see-through layers, and the
    /// invisible target a pointer has to be on. A structure this small, such as an eye, would be hard to hit at the size
    /// the bust is shown at, so the target is a sphere larger than the organ, placed by hand from the pack's measurements.
    /// </summary>
    public class AnatomyBodyRegionSpec {
        public AnatomyBodyRegionSpec(string id, string[] sourceFiles) {
            Id = id;
            SourceFiles = sourceFiles;
        }

        /// <summary>The structure's id, which the body's topic text is written against.</summary>
        public string Id { get; private set; }

        /// <summary>File paths relative to <see cref="AnatomyPaths.SourceRoot"/>.</summary>
        public string[] SourceFiles { get; private set; }

        /// <summary>Centre of the target, in metres in Unity's axes at the DOSCH pack's own position.</summary>
        public Vector3 HotspotCentre { get; set; }

        /// <summary>Radius of the target in metres. Zero means the organ's own surface is the target, which suits a lung.</summary>
        public float HotspotRadius { get; set; }

        public bool HasSphereHotspot {
            get { return HotspotRadius > 0f; }
        }
    }
}