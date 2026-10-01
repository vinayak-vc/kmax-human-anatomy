using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// A Wavefront OBJ file after parsing: shared vertex pools, plus faces triangulated and grouped by
    /// the material in force when they were declared. Coordinates are still in the source's own axes.
    /// </summary>
    public class ObjModel {
        public ObjModel() {
            Positions = new List<Vector3>();
            Normals = new List<Vector3>();
            Uvs = new List<Vector2>();
            Groups = new List<ObjMaterialGroup>();
        }

        public string MaterialLibrary { get; set; }
        public List<Vector3> Positions { get; private set; }
        public List<Vector3> Normals { get; private set; }
        public List<Vector2> Uvs { get; private set; }
        public List<ObjMaterialGroup> Groups { get; private set; }
    }
}