using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Bakes what the eye needs to move into the imported meshes as blend shapes.
    ///
    /// <para><b>Gaze.</b> The rigid parts of the globe are turned at runtime as a group, so they need nothing baked. The
    /// soft tissue round it does: each muscle and the sheath of the optic nerve is stretched between the part that
    /// follows the globe and the part that stays put. A point within a fixed distance of the globe's middle turns with it,
    /// one further off than another fixed distance does not move, and between the two it fades, so a muscle bends from
    /// its ring of tendon at the back of the socket to where it grips the eyeball. Two shapes per structure, yaw and
    /// pitch, each the turn the runtime calls 30 degrees (<see cref="EyeBehaviour.GazeShapeDegrees"/>), weighted either
    /// side of zero.</para>
    ///
    /// <para><b>Pupil.</b> The inner edge of the iris moves in or out, most at the pupil and not at all at the rim.</para>
    ///
    /// <para><b>Focus.</b> The lens grows thicker along the eye's axis and a little narrower across it, which is how an
    /// eye focuses on something near.</para>
    /// </summary>
    public class AnatomyEyeMotion : IAnatomyMotion {
        /// <summary>Soft tissue turns with the globe within this distance of its middle, in metres, and stays put beyond the outer one.</summary>
        private const float FollowInner = 0.014f;
        private const float FollowOuter = 0.024f;

        /// <summary>How far the edge of the pupil moves at full weight, in metres.</summary>
        private const float PupilTravel = 0.0018f;

        /// <summary>How much thicker, and how much narrower, the lens is at full weight, as shares of its size.</summary>
        private const float LensThickening = 0.35f;
        private const float LensNarrowing = 0.06f;

        private readonly Vector3 _globe;
        private readonly Vector3 _forward;
        private readonly Vector3 _irisCentre;
        private readonly float _pupilRadius;
        private readonly float _irisRadius;
        private readonly Vector3 _lensCentre;

        private AnatomyEyeMotion(Vector3 globe, Vector3 forward, Vector3 irisCentre, float pupilRadius, float irisRadius, Vector3 lensCentre) {
            _globe = globe;
            _forward = forward;
            _irisCentre = irisCentre;
            _pupilRadius = pupilRadius;
            _irisRadius = irisRadius;
            _lensCentre = lensCentre;
        }

        /// <summary>
        /// Measures the eye from its own meshes: the middle of the eyeball, the way it faces, the width of the pupil and of
        /// the iris, and the middle of the lens. Returns null, after logging why, when the parts are not there.
        /// </summary>
        public static AnatomyEyeMotion Analyse(List<AnatomyMeshData> parts, Vector3 modelCentre) {
            Vector3[] sclera = VerticesOf(parts, EyeBehaviour.ScleraId, modelCentre);
            Vector3[] cornea = VerticesOf(parts, EyeBehaviour.CorneaId, modelCentre);
            Vector3[] iris = VerticesOf(parts, EyeBehaviour.IrisId, modelCentre);
            Vector3[] lens = VerticesOf(parts, EyeBehaviour.LensId, modelCentre);
            if (sclera == null || cornea == null || iris == null || lens == null) {
                Debug.LogWarning("[Anatomy] The eye's sclera, cornea, iris or lens were not found, so it will not move.");
                return null;
            }

            Vector3 globe = BoundsCentre(sclera);
            Vector3 forward = (BoundsCentre(cornea) - globe).normalized;
            Vector3 irisCentre = Mean(iris);
            Vector3 lensCentre = Mean(lens);

            float pupil = float.MaxValue;
            float rim = 0f;
            for (int i = 0; i < iris.Length; i++) {
                float radius = AcrossAxis(iris[i] - irisCentre, forward).magnitude;
                pupil = Mathf.Min(pupil, radius);
                rim = Mathf.Max(rim, radius);
            }

            Debug.Log($"[Anatomy] Eye: globe centre ({globe.x * 1000f:F1}, {globe.y * 1000f:F1}, {globe.z * 1000f:F1}) mm, facing " +
                $"({forward.x:F2}, {forward.y:F2}, {forward.z:F2}); pupil {pupil * 2000f:F1} mm, iris {rim * 2000f:F1} mm across.");
            return new AnatomyEyeMotion(globe, forward, irisCentre, pupil, rim, lensCentre);
        }

        public int AddShapes(AnatomyMeshData part, Vector3 modelCentre, Mesh mesh) {
            mesh.ClearBlendShapes();
            Vector3[] positions = AnatomyFieldShapes.PositionsOf(part, modelCentre);
            Vector3[] normals = AnatomyFieldShapes.NormalsOf(part);
            if (System.Array.IndexOf(EyeBehaviour.SoftIds, part.StructureId) >= 0) {
                int added = AnatomyFieldShapes.AddFieldShape(mesh, EyeBehaviour.YawShape, SoftYaw, positions, normals);
                return added + AnatomyFieldShapes.AddFieldShape(mesh, EyeBehaviour.PitchShape, SoftPitch, positions, normals);
            }

            if (part.StructureId == EyeBehaviour.IrisId) {
                return AnatomyFieldShapes.AddFieldShape(mesh, EyeBehaviour.PupilShape, Pupil, positions, normals);
            }

            if (part.StructureId == EyeBehaviour.LensId) {
                return AnatomyFieldShapes.AddFieldShape(mesh, EyeBehaviour.FocusShape, Focus, positions, normals);
            }

            return 0;
        }

        /// <summary>The eye needs nothing besides its shapes: the runtime finds the rest from the structures' own positions.</summary>
        public void Finish(GameObject root) {
        }

        private Vector3 SoftYaw(Vector3 point) {
            return Soft(point, EyeGazeFrame.Yaw(EyeBehaviour.GazeShapeDegrees, _forward));
        }

        private Vector3 SoftPitch(Vector3 point) {
            return Soft(point, EyeGazeFrame.Pitch(EyeBehaviour.GazeShapeDegrees, _forward));
        }

        /// <summary>A point turns about the middle of the globe by as much as its distance from there lets it follow.</summary>
        private Vector3 Soft(Vector3 point, Quaternion turn) {
            float distance = (point - _globe).magnitude;
            float follow = 1f - Mathf.SmoothStep(0f, 1f, (distance - FollowInner) / (FollowOuter - FollowInner));
            return (turn * (point - _globe) + _globe - point) * follow;
        }

        /// <summary>The iris is pushed out from the pupil, most at the edge of the pupil and not at all at its rim.</summary>
        private Vector3 Pupil(Vector3 point) {
            Vector3 across = AcrossAxis(point - _irisCentre, _forward);
            float radius = across.magnitude;
            if (radius < 0.000001f) {
                return Vector3.zero;
            }

            float edge = 1f - Mathf.SmoothStep(_pupilRadius, _irisRadius, radius);
            return across / radius * (PupilTravel * edge);
        }

        /// <summary>The lens swells along the axis and draws in across it.</summary>
        private Vector3 Focus(Vector3 point) {
            Vector3 offset = point - _lensCentre;
            float along = Vector3.Dot(offset, _forward);
            return _forward * (along * LensThickening) - AcrossAxis(offset, _forward) * LensNarrowing;
        }

        private static Vector3 AcrossAxis(Vector3 offset, Vector3 axis) {
            return offset - axis * Vector3.Dot(offset, axis);
        }

        private static Vector3 BoundsCentre(Vector3[] points) {
            Vector3 low = points[0];
            Vector3 high = points[0];
            for (int i = 1; i < points.Length; i++) {
                low = Vector3.Min(low, points[i]);
                high = Vector3.Max(high, points[i]);
            }

            return (low + high) * 0.5f;
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
    }
}