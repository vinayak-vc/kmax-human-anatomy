using System.Collections.Generic;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Every triangle a mesh assigns to one named material. In DOSCH files the material name is the
    /// anatomical structure name, so one group is one selectable structure.
    /// </summary>
    public class ObjMaterialGroup {
        public ObjMaterialGroup(string materialName) {
            MaterialName = materialName;
            Corners = new List<ObjCorner>();
        }

        public string MaterialName { get; private set; }

        /// <summary>Three corners per triangle, in declaration order.</summary>
        public List<ObjCorner> Corners { get; private set; }
    }
}