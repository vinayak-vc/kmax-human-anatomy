using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Bakes the vibration of a hearing ear into the imported meshes as one blend shape, and measures the route a sound
    /// takes into it from the canal's own shape.
    ///
    /// <para>The eardrum bulges in and out along the ear canal, most where the hammer's handle is fixed to it and not at
    /// all at its rim. The hammer and anvil, joined, rock about a line through the top of the hammer's head, so the
    /// handle swings with the drum and the tip of the anvil swings with it. The stirrup is carried by the tip of the
    /// anvil, moving exactly as that tip does, so the joints stay closed at every point of the swing.</para>
    ///
    /// <para>Because the bones are moved by one field and not by a transform, the motion is part of their meshes: it
    /// grows with them when they are enlarged, and the joints stay joined at any size. Weights swing either side of
    /// zero, so one shape serves for the drum being pushed in and drawn out.</para>
    /// </summary>
    public class AnatomyEarMotion : IAnatomyMotion {
        /// <summary>How far the middle of the eardrum moves at full weight, in metres. Exaggerated: the real movement is under a micrometre.</summary>
        private const float DrumTravel = 0.0003f;

        /// <summary>How many slices of the ear canal the route through it is measured from.</summary>
        private const int CanalSlices = 10;

        /// <summary>How far the route runs out into the air from the canal's mouth, and how wide a wave is at its start.</summary>
        private const float AirLength = 0.024f;
        private const float AirRadius = 0.008f;

        /// <summary>The share of the canal's width that a ring spans.</summary>
        private const float RingShare = 0.72f;

        /// <summary>Bones that should touch are reported when they are further apart than this, in metres.</summary>
        private const float LooseJoint = 0.0005f;

        private readonly Vector3 _inward;
        private readonly Vector3 _umbo;
        private readonly float _drumRadius;
        private readonly Vector3 _pivot;
        private readonly Vector3 _axis;
        private readonly float _rockDegrees;
        private readonly Vector3 _stapesTravel;
        private readonly Vector3[] _routePoints;
        private readonly float[] _routeRadii;

        private AnatomyEarMotion(Vector3 inward, Vector3 umbo, float drumRadius, Vector3 pivot, Vector3 axis,
            float rockDegrees, Vector3 stapesTravel, Vector3[] routePoints, float[] routeRadii) {
            _inward = inward;
            _umbo = umbo;
            _drumRadius = drumRadius;
            _pivot = pivot;
            _axis = axis;
            _rockDegrees = rockDegrees;
            _stapesTravel = stapesTravel;
            _routePoints = routePoints;
            _routeRadii = routeRadii;
        }

        /// <summary>
        /// Measures the ear from its own meshes: the direction the sound travels, where the hammer meets the drum, where
        /// it turns, and where the anvil meets the stirrup. Returns null, after logging why, when the parts are not there
        /// to measure.
        /// </summary>
        public static AnatomyEarMotion Analyse(List<AnatomyMeshData> parts, Vector3 modelCentre) {
            Vector3[] drum = VerticesOf(parts, HearingBehaviour.MembraneId, modelCentre);
            Vector3[] malleus = VerticesOf(parts, HearingBehaviour.MalleusId, modelCentre);
            Vector3[] incus = VerticesOf(parts, HearingBehaviour.IncusId, modelCentre);
            Vector3[] stapes = VerticesOf(parts, HearingBehaviour.StapesId, modelCentre);
            Vector3[] canal = VerticesOf(parts, HearingBehaviour.CanalId, modelCentre);
            if (drum == null || malleus == null || incus == null || stapes == null || canal == null) {
                Debug.LogWarning("[Anatomy] The ear's drum, bones or canal were not found, so it will not vibrate.");
                return null;
            }

            Vector3 drumCentre = Mean(drum);
            Vector3 mouth = Furthest(canal, drumCentre);
            Vector3 outward = (mouth - drumCentre).normalized;
            Vector3 inward = -outward;

            float hammerGap;
            Vector3 umbo = Contact(drum, malleus, out hammerGap);
            float drumRadius = Vector3.Distance(Furthest(drum, umbo), umbo);

            Vector3 pivot = Furthest(malleus, umbo);
            Vector3 lever = umbo - pivot;
            Vector3 axis = Vector3.Cross(lever, inward).normalized;
            if (axis.sqrMagnitude < 0.5f) {
                Debug.LogWarning("[Anatomy] The ear's hammer lies along the ear canal, so it cannot be made to rock.");
                return null;
            }

            float rockDegrees = Mathf.Asin(Mathf.Min(0.99f, DrumTravel / lever.magnitude)) * Mathf.Rad2Deg;
            if (Vector3.Dot(Turn(umbo, pivot, axis, rockDegrees), inward) < 0f) {
                rockDegrees = -rockDegrees;
            }

            float hingeGap;
            float jointGap;
            Contact(malleus, incus, out hingeGap);
            Vector3 joint = Contact(incus, stapes, out jointGap);
            Vector3 stapesTravel = Turn(joint, pivot, axis, rockDegrees);

            Vector3[] routePoints;
            float[] routeRadii;
            MeasureRoute(canal, drumCentre, outward, out routePoints, out routeRadii);

            Debug.Log($"[Anatomy] Ear: sound runs along ({outward.x:F2}, {outward.y:F2}, {outward.z:F2}); the hammer rocks " +
                $"{Mathf.Abs(rockDegrees):F1} degrees about a line {lever.magnitude * 1000f:F1} mm above the drum's middle; the " +
                $"stirrup moves {stapesTravel.magnitude * 1000f:F2} mm; the route has {routePoints.Length} points.");
            WarnIfLoose("hammer and drum", hammerGap);
            WarnIfLoose("hammer and anvil", hingeGap);
            WarnIfLoose("anvil and stirrup", jointGap);
            return new AnatomyEarMotion(inward, umbo, drumRadius, pivot, axis, rockDegrees, stapesTravel, routePoints, routeRadii);
        }

        public int AddShapes(AnatomyMeshData part, Vector3 modelCentre, Mesh mesh) {
            mesh.ClearBlendShapes();
            DisplacementField field = FieldFor(part.StructureId);
            if (field == null) {
                return 0;
            }

            return AnatomyFieldShapes.AddFieldShape(mesh, HearingBehaviour.VibrateShape, field,
                AnatomyFieldShapes.PositionsOf(part, modelCentre), AnatomyFieldShapes.NormalsOf(part));
        }

        /// <summary>Gives the model the route a sound takes into it, for the runtime to draw the waves along.</summary>
        public void Finish(GameObject root) {
            AnatomyRoute route = root.AddComponent<AnatomyRoute>();
            SerializedObject serialized = new SerializedObject(route);
            serialized.FindProperty("routeName").stringValue = HearingBehaviour.SoundRoute;
            SerializedProperty points = serialized.FindProperty("points");
            SerializedProperty radii = serialized.FindProperty("radii");
            points.arraySize = _routePoints.Length;
            radii.arraySize = _routeRadii.Length;
            for (int i = 0; i < _routePoints.Length; i++) {
                points.GetArrayElementAtIndex(i).vector3Value = _routePoints[i];
                radii.GetArrayElementAtIndex(i).floatValue = _routeRadii[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private DisplacementField FieldFor(string structureId) {
            switch (structureId) {
                case HearingBehaviour.MembraneId:
                    return Flex;
                case HearingBehaviour.MalleusId:
                case HearingBehaviour.IncusId:
                    return Rock;
                case HearingBehaviour.StapesId:
                    return Push;
                default:
                    return null;
            }
        }

        /// <summary>The drum bulges inward, most at the hammer and not at all at its rim.</summary>
        private Vector3 Flex(Vector3 point) {
            float reach = Mathf.Clamp01((point - _umbo).magnitude / _drumRadius);
            float bell = (1f - reach * reach) * (1f - reach * reach);
            return _inward * (DrumTravel * bell);
        }

        /// <summary>The hammer and anvil turn together about the line through the top of the hammer's head.</summary>
        private Vector3 Rock(Vector3 point) {
            return Turn(point, _pivot, _axis, _rockDegrees);
        }

        /// <summary>The stirrup moves as the tip of the anvil does, without turning.</summary>
        private Vector3 Push(Vector3 point) {
            return _stapesTravel;
        }

        private static Vector3 Turn(Vector3 point, Vector3 pivot, Vector3 axis, float degrees) {
            return Quaternion.AngleAxis(degrees, axis) * (point - pivot) + pivot - point;
        }

        /// <summary>
        /// The route from out in the air, into the canal's mouth and down it, to the middle of the drum. The canal is cut
        /// into slices along its length; the middle and the width of each slice give a point of the route and how wide
        /// a wavefront is there.
        /// </summary>
        private static void MeasureRoute(Vector3[] canal, Vector3 drumCentre, Vector3 outward, out Vector3[] points, out float[] radii) {
            float length = 0f;
            for (int i = 0; i < canal.Length; i++) {
                length = Mathf.Max(length, Vector3.Dot(canal[i] - drumCentre, outward));
            }

            Vector3[] centres = new Vector3[CanalSlices];
            float[] widths = new float[CanalSlices];
            bool[] filled = new bool[CanalSlices];
            for (int slice = 0; slice < CanalSlices; slice++) {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < canal.Length; i++) {
                    float along = Vector3.Dot(canal[i] - drumCentre, outward);
                    int inSlice = Mathf.Clamp(Mathf.FloorToInt(along / length * CanalSlices), 0, CanalSlices - 1);
                    if (inSlice == slice) {
                        sum += canal[i];
                        count++;
                    }
                }

                if (count == 0) {
                    continue;
                }

                centres[slice] = sum / count;
                float spread = 0f;
                for (int i = 0; i < canal.Length; i++) {
                    float along = Vector3.Dot(canal[i] - drumCentre, outward);
                    int inSlice = Mathf.Clamp(Mathf.FloorToInt(along / length * CanalSlices), 0, CanalSlices - 1);
                    if (inSlice == slice) {
                        Vector3 off = canal[i] - centres[slice];
                        spread += (off - outward * Vector3.Dot(off, outward)).magnitude;
                    }
                }

                widths[slice] = spread / count;
                filled[slice] = true;
            }

            int outer = -1;
            int inner = -1;
            for (int slice = 0; slice < CanalSlices; slice++) {
                if (filled[slice]) {
                    outer = slice;
                    inner = inner < 0 ? slice : inner;
                }
            }

            List<Vector3> routePoints = new List<Vector3>();
            List<float> routeRadii = new List<float>();
            routePoints.Add(centres[outer] + outward * AirLength);
            routeRadii.Add(AirRadius);
            routePoints.Add(centres[outer] + outward * (AirLength * 0.4f));
            routeRadii.Add(Mathf.Lerp(widths[outer] * RingShare, AirRadius, 0.4f));
            for (int slice = outer; slice >= inner; slice--) {
                if (filled[slice]) {
                    routePoints.Add(centres[slice]);
                    routeRadii.Add(widths[slice] * RingShare);
                }
            }

            routePoints.Add(drumCentre);
            routeRadii.Add(widths[inner] * RingShare * 0.7f);
            points = routePoints.ToArray();
            radii = routeRadii.ToArray();
        }

        /// <summary>The vertex of each set nearest the other, and the point midway between them.</summary>
        private static Vector3 Contact(Vector3[] first, Vector3[] second, out float gap) {
            float nearest = float.MaxValue;
            Vector3 a = first[0];
            Vector3 b = second[0];
            for (int i = 0; i < first.Length; i++) {
                for (int j = 0; j < second.Length; j++) {
                    float distance = (first[i] - second[j]).sqrMagnitude;
                    if (distance < nearest) {
                        nearest = distance;
                        a = first[i];
                        b = second[j];
                    }
                }
            }

            gap = Mathf.Sqrt(nearest);
            return (a + b) * 0.5f;
        }

        private static Vector3 Furthest(Vector3[] points, Vector3 from) {
            Vector3 result = points[0];
            float furthest = 0f;
            for (int i = 0; i < points.Length; i++) {
                float distance = (points[i] - from).sqrMagnitude;
                if (distance > furthest) {
                    furthest = distance;
                    result = points[i];
                }
            }

            return result;
        }

        private static Vector3 Mean(Vector3[] points) {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < points.Length; i++) {
                sum += points[i];
            }

            return sum / points.Length;
        }

        private static Vector3[] VerticesOf(List<AnatomyMeshData> parts, string structureId, Vector3 modelCentre) {
            for (int i = 0; i < parts.Count; i++) {
                if (parts[i].StructureId == structureId) {
                    return AnatomyFieldShapes.PositionsOf(parts[i], modelCentre);
                }
            }

            return null;
        }

        private static void WarnIfLoose(string joint, float gap) {
            if (gap > LooseJoint) {
                Debug.LogWarning($"[Anatomy] Ear: the {joint} are {gap * 1000f:F2} mm apart, so the joint will open as they vibrate.");
            }
        }
    }
}