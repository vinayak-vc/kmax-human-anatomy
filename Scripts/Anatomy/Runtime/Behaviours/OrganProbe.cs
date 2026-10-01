using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Works out which organ a point is inside of, or which organ a line of sight meets first, from a thinned-out cloud of points
    /// on each organ's surface and the way the surface faces there.
    ///
    /// <para>Colliders cannot say. A convex hull fills in every hollow, so the hull of a windpipe or a food pipe, which curves
    /// down the middle of the chest, contains the heart and both lungs. The surface does not lie: a point is inside an organ if
    /// the nearest bit of its surface faces away from the point, and outside if it faces towards it.</para>
    ///
    /// <para>Some organs are too thin to be inside of: a duct or a windpipe a millimetre or two across, a sheet like the
    /// diaphragm. A point is not inside one of those but at it, within a small distance of its surface, and the pen's tip
    /// cannot be placed more exactly than that.</para>
    ///
    /// <para>The points are taken once, in the world, so a probe belongs to a figure that holds still, as the scan's does. A
    /// thousand or so points an organ keep a probe to a few hundred microseconds. Plain C#, with no scene in it but the
    /// organs it is given.</para>
    /// </summary>
    public class OrganProbe {
        private readonly Vector3[][] _points;
        private readonly Vector3[][] _normals;
        private readonly Bounds[] _bounds;
        private readonly bool[] _thin;
        private readonly float _touchSqr;

        /// <summary>Takes the surface of each organ's mesh, as it stands in the world now.</summary>
        /// <param name="organs">The organs, which each carry a mesh filter.</param>
        /// <param name="samplesPerOrgan">About how many points to keep on each; the surface is thinned evenly to that.</param>
        /// <param name="thinOrgans">Ids of organs too thin to be inside of, which a point is at instead, or null for none.</param>
        /// <param name="touchRadius">How near a thin organ's surface a point must be to be at it, in metres.</param>
        public OrganProbe(IReadOnlyList<AnatomyStructure> organs, int samplesPerOrgan, ICollection<string> thinOrgans, float touchRadius) {
            int count = organs.Count;
            _points = new Vector3[count][];
            _normals = new Vector3[count][];
            _bounds = new Bounds[count];
            _thin = new bool[count];
            _touchSqr = touchRadius * touchRadius;
            for (int i = 0; i < count; i++) {
                Transform host = organs[i].transform;
                Mesh mesh = host.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int step = Mathf.Max(1, vertices.Length / Mathf.Max(1, samplesPerOrgan));
                int kept = (vertices.Length + step - 1) / step;
                _points[i] = new Vector3[kept];
                _normals[i] = new Vector3[kept];
                for (int k = 0; k < kept; k++) {
                    _points[i][k] = host.TransformPoint(vertices[k * step]);
                    _normals[i][k] = host.TransformDirection(normals[k * step]).normalized;
                }

                Bounds bounds = new Bounds(_points[i][0], Vector3.zero);
                for (int k = 1; k < kept; k++) {
                    bounds.Encapsulate(_points[i][k]);
                }

                _thin[i] = thinOrgans != null && thinOrgans.Contains(organs[i].StructureId);
                if (_thin[i]) {
                    bounds.Expand(touchRadius * 2f);
                }

                _bounds[i] = bounds;
            }
        }

        /// <summary>
        /// The index of the organ the point is inside of, or at if the organ is a thin one, or -1. Of two that claim it, the one
        /// whose surface is nearer.
        /// </summary>
        public int InsideOf(Vector3 point) {
            int found = -1;
            float foundDistance = float.MaxValue;
            for (int i = 0; i < _points.Length; i++) {
                if (!_bounds[i].Contains(point)) {
                    continue;
                }

                Vector3[] points = _points[i];
                int nearest = 0;
                float nearestSqr = float.MaxValue;
                for (int k = 0; k < points.Length; k++) {
                    float sqr = (points[k] - point).sqrMagnitude;
                    if (sqr < nearestSqr) {
                        nearestSqr = sqr;
                        nearest = k;
                    }
                }

                bool inside = _thin[i]
                    ? nearestSqr <= _touchSqr
                    : Vector3.Dot(_normals[i][nearest], point - points[nearest]) < 0f;
                if (inside && nearestSqr < foundDistance) {
                    foundDistance = nearestSqr;
                    found = i;
                }
            }

            return found;
        }

        /// <summary>
        /// The index of the organ whose surface the line of sight meets first, or -1: the first organ with a point within
        /// <paramref name="radius"/> of the line, not nearer than the origin and not further than <paramref name="reach"/> along it.
        /// </summary>
        public int FirstAlong(Vector3 origin, Vector3 direction, float radius, float reach) {
            int found = -1;
            float foundAlong = float.MaxValue;
            float radiusSqr = radius * radius;
            for (int i = 0; i < _points.Length; i++) {
                Vector3[] points = _points[i];
                for (int k = 0; k < points.Length; k++) {
                    Vector3 offset = points[k] - origin;
                    float along = Vector3.Dot(offset, direction);
                    if (along < 0f || along > reach || along >= foundAlong) {
                        continue;
                    }

                    if ((offset - direction * along).sqrMagnitude < radiusSqr) {
                        foundAlong = along;
                        found = i;
                    }
                }
            }

            return found;
        }
    }
}