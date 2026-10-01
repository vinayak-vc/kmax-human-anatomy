using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Keeps the colliders of deforming structures where their surfaces are. A collider made from a mesh does not
    /// follow a blend shape, so a muscle that has been stretched round a turning eye would still be picked where it used
    /// to be. The fix is to bake the deformed surface and hand it to the collider, but giving a collider a new mesh makes
    /// the physics engine cook it, which costs, so the work is spread out: one structure a frame.
    /// </summary>
    public class SoftColliderRefresher {
        private readonly SkinnedMeshRenderer[] _renderers;
        private readonly MeshCollider[] _colliders;
        private readonly Mesh[] _baked;
        private int _next;
        private int _remaining;

        public SoftColliderRefresher(SkinnedMeshRenderer[] renderers) {
            _renderers = renderers;
            _colliders = new MeshCollider[renderers.Length];
            _baked = new Mesh[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) {
                _colliders[i] = renderers[i].GetComponent<MeshCollider>();
                _baked[i] = new Mesh();
                _baked[i].name = renderers[i].name + " (collider)";
            }
        }

        /// <summary>Asks for every collider to be brought up to date, one a frame from now.</summary>
        public void RequestRefresh() {
            _remaining = _renderers.Length;
        }

        /// <summary>Brings one collider up to date, if any are waiting. Call it once a frame.</summary>
        public void Tick() {
            if (_remaining <= 0 || _renderers.Length == 0) {
                return;
            }

            MeshCollider collider = _colliders[_next];
            if (collider != null) {
                _renderers[_next].BakeMesh(_baked[_next]);
                collider.sharedMesh = null;
                collider.sharedMesh = _baked[_next];
            }

            _next = (_next + 1) % _renderers.Length;
            _remaining--;
        }

        /// <summary>Frees the baked meshes.</summary>
        public void Release() {
            for (int i = 0; i < _baked.Length; i++) {
                if (_baked[i] != null) {
                    Object.Destroy(_baked[i]);
                }
            }
        }
    }
}