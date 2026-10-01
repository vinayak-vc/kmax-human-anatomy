using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Bakes one breath in into the imported meshes as a single blend shape, and measures the route the air takes down the
    /// windpipe from the airway's own shape. Breathing out is the rest pose.
    ///
    /// <para>Every structure moves by a smooth field of position, so what touches stays touching. The ribs swing up and
    /// out, their front ends most: upper ribs like a pump handle, lower ones like a bucket handle, and the breastbone and
    /// the cartilage that joins it to the ribs go with them. The diaphragm's dome flattens and drops while its rim rides
    /// the lower ribs. Each lung fills out sideways and front to back from its inner edge, which stays by the heart, and its
    /// base follows the diaphragm down. The airway runs from the larynx, which stays, to the lungs, which move: the
    /// windpipe lengthens a little and the branches inside a lung move as that lung does.</para>
    ///
    /// <para>The amounts are a little larger than real so the breath reads across a room. The lungs and the airway use
    /// the same lung field where they overlap, which is what keeps the branches inside the lung as it expands.</para>
    /// </summary>
    public class AnatomyBreathMotion : IAnatomyMotion {
        /// <summary>How far the front of the ribs moves forward and up, and the side of them out, at full weight, in metres.</summary>
        private const float RibForward = 0.017f;
        private const float RibUp = 0.013f;
        private const float RibOutward = 0.013f;

        /// <summary>How far the top of the diaphragm's dome drops.</summary>
        private const float DiaphragmDrop = 0.048f;

        /// <summary>How far a lung's base drops, and how much it widens and deepens as a share of its size from its inner edge.</summary>
        private const float LungDescent = 0.042f;
        private const float LungWidening = 0.090f;
        private const float LungDeepening = 0.060f;

        /// <summary>How far the windpipe lengthens at the fork, where it meets the lungs.</summary>
        private const float TracheaDescent = 0.026f;

        /// <summary>The airway moves as a lung does beyond the outer distance from the midline, as the windpipe does within the inner one.</summary>
        private const float AirwayBlendInner = 0.014f;
        private const float AirwayBlendOuter = 0.040f;

        /// <summary>The share of the diaphragm's rim that rides the ribs, and of its dome's lowest part that counts as rim.</summary>
        private const float RimFollow = 0.85f;
        private const float RimPercentile = 0.15f;

        /// <summary>How many slices of the windpipe the route is measured from, and how far it runs out above into the air.</summary>
        private const int WindpipeSlices = 14;
        private const float AirLength = 0.055f;
        private const float AirRadius = 0.016f;
        private const float RingShare = 0.8f;
        private const float SliceThickness = 0.004f;

        private readonly float _xHalf;
        private readonly float _ribLow;
        private readonly float _ribHigh;
        private readonly float _back;
        private readonly float _front;
        private readonly LungFrame _left;
        private readonly LungFrame _right;
        private readonly string _leftId;
        private readonly float _domeApex;
        private readonly float _domeRim;
        private readonly float _airwayTop;
        private readonly float _fork;
        private readonly Vector3[] _routePoints;
        private readonly float[] _routeRadii;

        private AnatomyBreathMotion(float xHalf, float ribLow, float ribHigh, float back, float front, LungFrame left,
            LungFrame right, string leftId, float domeApex, float domeRim, float airwayTop, float fork,
            Vector3[] routePoints, float[] routeRadii) {
            _xHalf = xHalf;
            _ribLow = ribLow;
            _ribHigh = ribHigh;
            _back = back;
            _front = front;
            _left = left;
            _right = right;
            _leftId = leftId;
            _domeApex = domeApex;
            _domeRim = domeRim;
            _airwayTop = airwayTop;
            _fork = fork;
            _routePoints = routePoints;
            _routeRadii = routeRadii;
        }

        /// <summary>
        /// Measures the chest from its own meshes: its width, height and depth, which lung is on which side and how big
        /// each is, the dome of the diaphragm, and the windpipe and the fork where it divides. Returns null, after logging
        /// why, when the parts are not there.
        /// </summary>
        public static AnatomyBreathMotion Analyse(List<AnatomyMeshData> parts, Vector3 modelCentre) {
            Vector3[] ribs = VerticesOf(parts, BreathingBehaviour.RibsId, modelCentre);
            Vector3[] sternum = VerticesOf(parts, BreathingBehaviour.SternumId, modelCentre);
            Vector3[] lungA = VerticesOf(parts, BreathingBehaviour.LeftLungId, modelCentre);
            Vector3[] lungB = VerticesOf(parts, BreathingBehaviour.RightLungId, modelCentre);
            Vector3[] airway = VerticesOf(parts, BreathingBehaviour.AirwayId, modelCentre);
            Vector3[] diaphragm = VerticesOf(parts, BreathingBehaviour.DiaphragmId, modelCentre);
            if (ribs == null || sternum == null || lungA == null || lungB == null || airway == null || diaphragm == null) {
                Debug.LogWarning("[Anatomy] The chest's ribs, breastbone, lungs, airway or diaphragm were not found, so it will not breathe.");
                return null;
            }

            float xHalf = 0f;
            float ribLow = float.MaxValue;
            float ribHigh = float.MinValue;
            float back = float.MinValue;
            for (int i = 0; i < ribs.Length; i++) {
                xHalf = Mathf.Max(xHalf, Mathf.Abs(ribs[i].x));
                ribLow = Mathf.Min(ribLow, ribs[i].y);
                ribHigh = Mathf.Max(ribHigh, ribs[i].y);
                back = Mathf.Max(back, ribs[i].z);
            }

            float front = float.MaxValue;
            for (int i = 0; i < sternum.Length; i++) {
                front = Mathf.Min(front, sternum[i].z);
            }

            bool firstIsLeft = Mean(lungA).x >= Mean(lungB).x;
            LungFrame left = MeasureLung(firstIsLeft ? lungA : lungB, true);
            LungFrame right = MeasureLung(firstIsLeft ? lungB : lungA, false);
            string leftId = firstIsLeft ? BreathingBehaviour.LeftLungId : BreathingBehaviour.RightLungId;

            float[] heights = new float[diaphragm.Length];
            float domeApex = float.MinValue;
            for (int i = 0; i < diaphragm.Length; i++) {
                heights[i] = diaphragm[i].y;
                domeApex = Mathf.Max(domeApex, diaphragm[i].y);
            }

            System.Array.Sort(heights);
            float domeRim = heights[Mathf.Clamp(Mathf.FloorToInt(RimPercentile * heights.Length), 0, heights.Length - 1)];

            float airwayTop;
            float fork;
            Vector3[] routePoints;
            float[] routeRadii;
            MeasureWindpipe(airway, out airwayTop, out fork, out routePoints, out routeRadii);

            Debug.Log($"[Anatomy] Chest: ribs {xHalf * 2000f:F0} mm wide, {(ribHigh - ribLow) * 1000f:F0} mm tall; the left lung is " +
                $"'{leftId}'; the diaphragm's dome rises {(domeApex - domeRim) * 1000f:F0} mm above its rim; the windpipe runs " +
                $"{(airwayTop - fork) * 1000f:F0} mm to the fork; the air route has {routePoints.Length} points.");
            return new AnatomyBreathMotion(xHalf, ribLow, ribHigh, back, front, left, right, leftId, domeApex, domeRim,
                airwayTop, fork, routePoints, routeRadii);
        }

        public int AddShapes(AnatomyMeshData part, Vector3 modelCentre, Mesh mesh) {
            mesh.ClearBlendShapes();
            DisplacementField field = FieldFor(part.StructureId);
            if (field == null) {
                return 0;
            }

            return AnatomyFieldShapes.AddFieldShape(mesh, BreathingBehaviour.InhaleShape, field,
                AnatomyFieldShapes.PositionsOf(part, modelCentre), AnatomyFieldShapes.NormalsOf(part));
        }

        /// <summary>Gives the model the route the air takes, for the runtime to draw a breath along.</summary>
        public void Finish(GameObject root) {
            AnatomyRoute route = root.AddComponent<AnatomyRoute>();
            SerializedObject serialized = new SerializedObject(route);
            serialized.FindProperty("routeName").stringValue = BreathingBehaviour.AirRoute;
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
                case BreathingBehaviour.RibsId:
                case BreathingBehaviour.RibCartilageId:
                case BreathingBehaviour.SternumId:
                    return Ribcage;
                case BreathingBehaviour.DiaphragmId:
                    return Diaphragm;
                case BreathingBehaviour.AirwayId:
                    return Airway;
                case BreathingBehaviour.LeftLungId:
                case BreathingBehaviour.RightLungId:
                    return structureId == _leftId ? (DisplacementField)LeftLung : RightLung;
                default:
                    return null;
            }
        }

        /// <summary>The front of the ribs swings forward and up, and their sides out, more towards the top and bottom respectively.</summary>
        private Vector3 Ribcage(Vector3 point) {
            float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_back, _front, point.z));
            float height = Mathf.InverseLerp(_ribLow, _ribHigh, point.y);
            float side = Mathf.SmoothStep(0.25f, 1f, Mathf.Abs(point.x) / _xHalf);
            float outward = Mathf.Sign(point.x) * RibOutward * side * (1f - 0.45f * height);
            float up = RibUp * (0.35f * front + 0.65f * side) * (0.7f + 0.3f * height);
            float forward = -RibForward * front * (0.55f + 0.45f * height);
            return new Vector3(outward, up, forward);
        }

        /// <summary>The dome drops at its top and stays with the lower ribs at its rim.</summary>
        private Vector3 Diaphragm(Vector3 point) {
            float dome = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_domeRim, _domeApex, point.y));
            return Vector3.Lerp(Ribcage(point) * RimFollow, new Vector3(0f, -DiaphragmDrop, 0f), dome);
        }

        private Vector3 LeftLung(Vector3 point) {
            return Lung(point, _left);
        }

        private Vector3 RightLung(Vector3 point) {
            return Lung(point, _right);
        }

        /// <summary>A lung fills out from its inner edge, most about its middle, and its base drops.</summary>
        private static Vector3 Lung(Vector3 point, LungFrame lung) {
            float belly = 1f - Mathf.Clamp01(Mathf.Abs(point.y - lung.MiddleY) / lung.HalfHeight);
            belly = Mathf.SmoothStep(0f, 1f, belly);
            float basal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lung.MiddleY, lung.Bottom, point.y));
            return new Vector3((point.x - lung.InnerX) * LungWidening * belly, -LungDescent * basal,
                (point.z - lung.MiddleZ) * LungDeepening * belly);
        }

        /// <summary>The windpipe lengthens down to the fork, and the branches beyond it move with the lung they are in.</summary>
        private Vector3 Airway(Vector3 point) {
            LungFrame lung = point.x >= 0f ? _left : _right;
            float inLung = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(AirwayBlendInner, AirwayBlendOuter, Mathf.Abs(point.x)));
            float down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_airwayTop, _fork, point.y));
            return Vector3.Lerp(new Vector3(0f, -TracheaDescent * down, 0f), Lung(point, lung), inLung);
        }

        /// <summary>
        /// Finds the top of the airway and the fork where the windpipe divides, and the route from out in the air above the
        /// larynx down the windpipe to the fork. The windpipe is cut into slices from the top; the fork is the first slice
        /// that is much wider than the windpipe was.
        /// </summary>
        private static void MeasureWindpipe(Vector3[] airway, out float top, out float fork, out Vector3[] points, out float[] radii) {
            top = float.MinValue;
            float bottom = float.MaxValue;
            for (int i = 0; i < airway.Length; i++) {
                top = Mathf.Max(top, airway[i].y);
                bottom = Mathf.Min(bottom, airway[i].y);
            }

            int sliceCount = Mathf.Max(1, Mathf.CeilToInt((top - bottom) / SliceThickness));
            float[] low = new float[sliceCount];
            float[] high = new float[sliceCount];
            bool[] seen = new bool[sliceCount];
            for (int i = 0; i < sliceCount; i++) {
                low[i] = float.MaxValue;
                high[i] = float.MinValue;
            }

            for (int i = 0; i < airway.Length; i++) {
                int slice = Mathf.Clamp(Mathf.FloorToInt((top - airway[i].y) / SliceThickness), 0, sliceCount - 1);
                low[slice] = Mathf.Min(low[slice], airway[i].x);
                high[slice] = Mathf.Max(high[slice], airway[i].x);
                seen[slice] = true;
            }

            float windpipeWidth = 0f;
            int sampled = Mathf.Max(1, sliceCount / 8);
            int counted = 0;
            for (int i = 0; i < sampled; i++) {
                if (seen[i]) {
                    windpipeWidth += high[i] - low[i];
                    counted++;
                }
            }

            windpipeWidth = counted > 0 ? windpipeWidth / counted : 0.02f;
            fork = top - 0.45f * (top - bottom);
            for (int i = sampled; i < sliceCount; i++) {
                if (seen[i] && high[i] - low[i] > 2f * windpipeWidth) {
                    fork = top - i * SliceThickness;
                    break;
                }
            }

            List<Vector3> routePoints = new List<Vector3>();
            List<float> routeRadii = new List<float>();
            for (int s = 0; s < WindpipeSlices; s++) {
                float from = top - (top - fork) * s / WindpipeSlices;
                float to = top - (top - fork) * (s + 1) / WindpipeSlices;
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < airway.Length; i++) {
                    if (airway[i].y <= from && airway[i].y > to) {
                        sum += airway[i];
                        count++;
                    }
                }

                if (count == 0) {
                    continue;
                }

                Vector3 centre = sum / count;
                float spread = 0f;
                for (int i = 0; i < airway.Length; i++) {
                    if (airway[i].y <= from && airway[i].y > to) {
                        spread += new Vector2(airway[i].x - centre.x, airway[i].z - centre.z).magnitude;
                    }
                }

                if (routePoints.Count == 0) {
                    routePoints.Add(centre + Vector3.up * AirLength);
                    routeRadii.Add(AirRadius);
                }

                routePoints.Add(centre);
                routeRadii.Add(spread / count * RingShare);
            }

            points = routePoints.ToArray();
            radii = routeRadii.ToArray();
        }

        private static LungFrame MeasureLung(Vector3[] vertices, bool isLeft) {
            Vector3 mean = Mean(vertices);
            float top = float.MinValue;
            float bottom = float.MaxValue;
            float inner = isLeft ? float.MaxValue : float.MinValue;
            for (int i = 0; i < vertices.Length; i++) {
                top = Mathf.Max(top, vertices[i].y);
                bottom = Mathf.Min(bottom, vertices[i].y);
                inner = isLeft ? Mathf.Min(inner, vertices[i].x) : Mathf.Max(inner, vertices[i].x);
            }

            return new LungFrame(inner, (top + bottom) * 0.5f, (top - bottom) * 0.5f, bottom, mean.z);
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

        /// <summary>Where one lung is: its inner edge by the heart, its middle and its base.</summary>
        private sealed class LungFrame {
            public readonly float InnerX;
            public readonly float MiddleY;
            public readonly float HalfHeight;
            public readonly float Bottom;
            public readonly float MiddleZ;

            public LungFrame(float innerX, float middleY, float halfHeight, float bottom, float middleZ) {
                InnerX = innerX;
                MiddleY = middleY;
                HalfHeight = Mathf.Max(0.001f, halfHeight);
                Bottom = bottom;
                MiddleZ = middleZ;
            }
        }
    }
}