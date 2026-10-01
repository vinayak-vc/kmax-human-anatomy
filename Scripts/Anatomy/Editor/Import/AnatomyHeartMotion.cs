using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Bakes the motion of a beating heart into the imported meshes as blend shapes, so at runtime the beat is
    /// only four weights turned up and down.
    ///
    /// <para>Scaling each chamber about its own centre, the obvious way to fake a beat, tears the heart apart:
    /// neighbouring chambers pull away from each other and dark seams open between them. Here every vertex of
    /// every structure is moved by the same smooth field, a function of where the vertex is in space and nothing
    /// else, so surfaces that touch keep touching.</para>
    ///
    /// <para>The field does what a real heart does. In systole the ventricles shorten along the line from base to
    /// apex, the base being drawn towards a nearly still apex; their walls squeeze in; and the apex turns against
    /// the base, wringing the heart like a cloth. The atria contract just before, and the arteries swell as blood is
    /// pushed into them. Each is its own shape so the runtime can time them separately.</para>
    ///
    /// <para>Every shape also stores how the surface normals turn, worked out from the field's own derivative, so
    /// the lighting stays right while the heart moves.</para>
    /// </summary>
    public class AnatomyHeartMotion : IAnatomyMotion {
        /// <summary>How far the base is drawn towards the apex, as a share of the ventricles' length.</summary>
        private const float Shortening = 0.11f;

        /// <summary>How far the ventricular wall squeezes towards the axis at its widest, as a share of its distance from it.</summary>
        private const float WallSqueeze = 0.1f;

        /// <summary>Turn of the apex and of the base, in degrees about the base-to-apex axis. They turn against each other.</summary>
        private const float ApexTwist = -12f;
        private const float BaseTwist = 5f;

        /// <summary>How far an atrium shrinks about its own centre, as a share of its size.</summary>
        private const float AtrialShrink = 0.09f;

        /// <summary>How far above the base, in ventricle lengths, the base's motion still carries the atria and vessels.</summary>
        private const float Reach = 0.9f;

        /// <summary>
        /// Within this distance of the axis, in metres, the base's motion applies in full. Beyond it it fades out over
        /// <see cref="HoldFade"/>, so the far ends of the vessels, which run off into the lungs, stay where they are.
        /// </summary>
        private const float HoldRadius = 0.055f;
        private const float HoldFade = 0.05f;

        /// <summary>Metres a great artery's wall moves out at the height of the pulse.</summary>
        private const float LargeArteryPulse = 0.0012f;
        private const float SmallArteryPulse = 0.0006f;

        private const float WeldResolution = 10000f;

        private readonly Vector3 _axis;
        private readonly Vector3 _apex;
        private readonly float _length;
        private readonly Dictionary<string, Vector3> _atrialCentres;

        private AnatomyHeartMotion(Vector3 axis, Vector3 apex, float length, Dictionary<string, Vector3> atrialCentres) {
            _axis = axis;
            _apex = apex;
            _length = length;
            _atrialCentres = atrialCentres;
        }

        /// <summary>
        /// Finds the heart's long axis from the shapes of its chambers: it runs from the atria towards the ventricles,
        /// and ends at the ventricle vertex furthest along it, the apex. Returns null, after logging why, when the
        /// chambers are not there to measure.
        /// </summary>
        public static AnatomyHeartMotion Analyse(List<AnatomyMeshData> parts, Vector3 modelCentre) {
            List<Vector3> ventricles = new List<Vector3>();
            List<Vector3> atria = new List<Vector3>();
            Dictionary<string, Vector3> atrialCentres = new Dictionary<string, Vector3>();
            for (int p = 0; p < parts.Count; p++) {
                AnatomyMeshData part = parts[p];
                List<Vector3> target = null;
                if (Contains(HeartbeatBehaviour.VentricleIds, part.StructureId)) {
                    target = ventricles;
                } else if (Contains(HeartbeatBehaviour.AtriumIds, part.StructureId)) {
                    target = atria;
                }

                if (target == null) {
                    continue;
                }

                List<Vector3> own = new List<Vector3>(part.Vertices.Count);
                for (int i = 0; i < part.Vertices.Count; i++) {
                    own.Add(part.Vertices[i] - modelCentre);
                }

                target.AddRange(own);
                if (target == atria) {
                    atrialCentres[part.StructureId] = Mean(own);
                }
            }

            if (ventricles.Count == 0 || atria.Count == 0) {
                Debug.LogWarning("[Anatomy] The heart's ventricles or atria were not found, so it will not beat.");
                return null;
            }

            Vector3 atrialCentre = Mean(atria);
            Vector3 axis = (Mean(ventricles) - atrialCentre).normalized;

            Vector3 apex = ventricles[0];
            float furthest = float.MinValue;
            for (int i = 0; i < ventricles.Count; i++) {
                float along = Vector3.Dot(ventricles[i] - atrialCentre, axis);
                if (along > furthest) {
                    furthest = along;
                    apex = ventricles[i];
                }
            }

            float length = 0f;
            for (int i = 0; i < ventricles.Count; i++) {
                length = Mathf.Max(length, Vector3.Dot(apex - ventricles[i], axis));
            }

            Debug.Log($"[Anatomy] Heart axis ({axis.x:F2}, {axis.y:F2}, {axis.z:F2}), ventricles {length * 1000f:F0} mm from base to apex.");
            return new AnatomyHeartMotion(axis, apex, length, atrialCentres);
        }

        /// <summary>
        /// Adds this structure's shapes to its mesh and returns how many were added. A shape is added only where the
        /// structure actually moves in it.
        /// </summary>
        public int AddShapes(AnatomyMeshData part, Vector3 modelCentre, Mesh mesh) {
            mesh.ClearBlendShapes();
            Vector3[] positions = AnatomyFieldShapes.PositionsOf(part, modelCentre);
            Vector3[] normals = AnatomyFieldShapes.NormalsOf(part);

            int added = 0;
            added += AddFieldShape(mesh, HeartbeatBehaviour.ContractShape, Field.Contract, positions, normals, Vector3.zero);
            added += AddFieldShape(mesh, HeartbeatBehaviour.TwistShape, Field.Twist, positions, normals, Vector3.zero);

            Vector3 centre;
            if (_atrialCentres.TryGetValue(part.StructureId, out centre)) {
                added += AddFieldShape(mesh, HeartbeatBehaviour.SqueezeShape, Field.Shrink, positions, normals, centre);
            }

            float pulse = PulseOf(part.StructureId);
            if (pulse > 0f) {
                added += AddPulseShape(mesh, positions, normals, pulse);
            }

            return added;
        }

        /// <summary>The beat needs nothing besides its shapes.</summary>
        public void Finish(GameObject root) {
        }

        private int AddFieldShape(Mesh mesh, string shapeName, Field field, Vector3[] positions, Vector3[] normals, Vector3 centre) {
            BoundField bound = new BoundField(this, field, centre);
            return AnatomyFieldShapes.AddFieldShape(mesh, shapeName, bound.Displace, positions, normals);
        }

        /// <summary>
        /// Moves the wall outward along its surface normal. Vertices at the same position are given the same
        /// normal, averaged between them, so a seam in the mesh cannot split open as the wall swells.
        /// </summary>
        private static int AddPulseShape(Mesh mesh, Vector3[] positions, Vector3[] normals, float distance) {
            Dictionary<Vector3Int, Vector3> welded = new Dictionary<Vector3Int, Vector3>();
            for (int i = 0; i < positions.Length; i++) {
                Vector3Int key = Vector3Int.RoundToInt(positions[i] * WeldResolution);
                Vector3 sum;
                welded.TryGetValue(key, out sum);
                welded[key] = sum + normals[i];
            }

            Vector3[] deltaVertices = new Vector3[positions.Length];
            for (int i = 0; i < positions.Length; i++) {
                Vector3Int key = Vector3Int.RoundToInt(positions[i] * WeldResolution);
                deltaVertices[i] = welded[key].normalized * distance;
            }

            mesh.AddBlendShapeFrame(HeartbeatBehaviour.PulseShape, AnatomyFieldShapes.FullWeight, deltaVertices, new Vector3[positions.Length], null);
            Debug.Log($"[Anatomy]   {mesh.name}: '{HeartbeatBehaviour.PulseShape}' moves {distance * 1000f:F1} mm.");
            return 1;
        }

        private static float PulseOf(string structureId) {
            if (structureId == "ascending_aorta" || structureId == "pulmonary_trunk") {
                return LargeArteryPulse;
            }

            return structureId == "pulmonary_arteries" ? SmallArteryPulse : 0f;
        }

        private Vector3 Delta(Field field, Vector3 point, Vector3 centre) {
            switch (field) {
                case Field.Contract:
                    return ContractDelta(point);
                case Field.Twist:
                    return TwistDelta(point);
                default:
                    return (centre - point) * AtrialShrink;
            }
        }

        /// <summary>The base is drawn towards the apex, and the walls squeeze in towards the axis.</summary>
        private Vector3 ContractDelta(Vector3 point) {
            Vector3 radial;
            float t = Locate(point, out radial);
            float held = Anchored(radial);
            float drawn = (t <= 1f ? Mathf.Max(0f, t) : Carried(t)) * held;
            float wall = t > 0f && t < 1f ? Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.7f) * held : 0f;
            return _axis * (Shortening * _length * drawn) - radial * (WallSqueeze * wall);
        }

        /// <summary>The apex and base turn against each other about the axis.</summary>
        private Vector3 TwistDelta(Vector3 point) {
            Vector3 radial;
            float t = Locate(point, out radial);
            float angle = Mathf.Lerp(ApexTwist, BaseTwist, Mathf.Clamp01(t)) * Carried(t) * Anchored(radial);
            return Quaternion.AngleAxis(angle, _axis) * radial - radial;
        }

        /// <summary>
        /// Where a point is along the ventricles: 0 at the apex, 1 at the base and above 1 beyond it. The radial
        /// part is its offset from the axis.
        /// </summary>
        private float Locate(Vector3 point, out Vector3 radial) {
            Vector3 fromApex = point - _apex;
            float along = Vector3.Dot(fromApex, _axis);
            radial = fromApex - _axis * along;
            return -along / _length;
        }

        /// <summary>How much of the motion reaches a point this far from the axis: all of it near, none far out.</summary>
        private static float Anchored(Vector3 radial) {
            return 1f - Mathf.SmoothStep(0f, 1f, (radial.magnitude - HoldRadius) / HoldFade);
        }

        /// <summary>How much of the base's motion reaches a point: all of it at the base, none a reach above it.</summary>
        private static float Carried(float t) {
            if (t <= 1f) {
                return 1f;
            }

            return 1f - Mathf.SmoothStep(0f, 1f, (t - 1f) / Reach);
        }

        private static Vector3 Mean(List<Vector3> points) {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < points.Count; i++) {
                sum += points[i];
            }

            return sum / points.Count;
        }

        private static bool Contains(string[] ids, string id) {
            for (int i = 0; i < ids.Length; i++) {
                if (ids[i] == id) {
                    return true;
                }
            }

            return false;
        }

        private enum Field {
            Contract,
            Twist,
            Shrink
        }

        /// <summary>One of the heart's fields with its centre fixed, so it can be handed on as a plain displacement field.</summary>
        private sealed class BoundField {
            private readonly AnatomyHeartMotion _owner;
            private readonly Field _field;
            private readonly Vector3 _centre;

            public BoundField(AnatomyHeartMotion owner, Field field, Vector3 centre) {
                _owner = owner;
                _field = field;
                _centre = centre;
            }

            public Vector3 Displace(Vector3 point) {
                return _owner.Delta(_field, point, _centre);
            }
        }
    }
}