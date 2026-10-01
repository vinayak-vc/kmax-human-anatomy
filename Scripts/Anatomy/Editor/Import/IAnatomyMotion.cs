using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// A motion the import bakes into a topic's meshes as blend shapes, so at runtime it is only weights turned up and
    /// down. The heart's beat and the ear's vibration are the two there are.
    /// </summary>
    public interface IAnatomyMotion {
        /// <summary>Adds this structure's shapes to its mesh and returns how many were added.</summary>
        int AddShapes(AnatomyMeshData part, Vector3 modelCentre, Mesh mesh);

        /// <summary>Called once the model is assembled, for anything the runtime needs besides the shapes.</summary>
        void Finish(GameObject root);
    }
}