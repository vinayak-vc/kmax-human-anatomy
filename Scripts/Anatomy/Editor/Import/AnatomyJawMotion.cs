using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Bakes what the face needs so the jaw can open: one blend shape, <c>Open</c>, in every muscle of the face and neck.
    ///
    /// <para>The jaw bone and the lower teeth are rigid, so the runtime turns them as a group, about the line through the
    /// two joints in front of the ears, and they need nothing baked. The muscles are soft. One that joins the jaw to the
    /// skull must stretch, and one round the lips must follow the lower lip. So a point below the line where the teeth
    /// meet turns with the jaw by the same turn, one well above it does not move, and between the two it fades; and a
    /// point well ahead of the joint turns, while one behind it, where the neck muscles are fixed to the skull, does not.
    /// The shape is the turn at full weight, weighted either side of zero.</para>
    ///
    /// <para>The joint and the line where the teeth meet are found from the meshes themselves: the joint from the back
    /// of each side of the jaw bone's upper end, the bite line from the two rows of teeth.</para>
    /// </summary>
    public class AnatomyJawMotion : IAnatomyMotion {
        /// <summary>A point turns with the jaw below this height above the bite line, and not above the higher one, in metres.</summary>
        private const float FollowBelow = 0.004f;
        private const float FollowAbove = 0.036f;

        /// <summary>A point does not turn behind this distance behind the joint and turns fully beyond the second one ahead of it, in metres.</summary>
        private const float BehindJoint = 0.010f;
        private const float AheadOfJoint = 0.030f;

        /// <summary>How much of the top of the jaw bone is searched for the joint, from its highest point down, in metres.</summary>
        private const float JointSearchDepth = 0.014f;

        /// <summary>How far back from the rearmost point of the joint's head its middle is, in metres.</summary>
        private const float JointHeadRadius = 0.004f;

        private readonly Vector3 _joint;
        private readonly Vector3 _chin;
        private readonly float _biteLine;
        private readonly Quaternion _turn;

        private AnatomyJawMotion(Vector3 joint, Vector3 chin, float biteLine, Quaternion turn) {
            _joint = joint;
            _chin = chin;
            _biteLine = biteLine;
            _turn = turn;
        }

        /// <summary>
        /// Measures the jaw from its own meshes: where it hinges and where the teeth meet. Returns null, after logging why,
        /// when the parts are not there.
        /// </summary>
        public static AnatomyJawMotion Analyse(List<AnatomyMeshData> parts, Vector3 modelCentre) {
            Vector3[] mandible = VerticesOf(parts, JawBehaviour.MandibleId, modelCentre);
            Vector3[] upper = VerticesOf(parts, JawBehaviour.UpperTeethId, modelCentre);
            Vector3[] lower = VerticesOf(parts, JawBehaviour.LowerTeethId, modelCentre);
            if (mandible == null || upper == null || lower == null) {
                Debug.LogWarning("[Anatomy] The jaw bone or a row of teeth was not found, so the jaw will not open.");
                return null;
            }

            float top = float.MinValue;
            float lowest = float.MaxValue;
            Vector3 chin = mandible[0];
            for (int i = 0; i < mandible.Length; i++) {
                top = Mathf.Max(top, mandible[i].y);
                if (mandible[i].y < lowest) {
                    lowest = mandible[i].y;
                    chin = mandible[i];
                }
            }

            Vector3 rightHead = Rearmost(mandible, top, true);
            Vector3 leftHead = Rearmost(mandible, top, false);
            Vector3 heads = (rightHead + leftHead) * 0.5f;
            Vector3 joint = new Vector3(0f, heads.y, heads.z - JointHeadRadius);

            float upperBottom = float.MaxValue;
            for (int i = 0; i < upper.Length; i++) {
                upperBottom = Mathf.Min(upperBottom, upper[i].y);
            }

            float lowerTop = float.MinValue;
            for (int i = 0; i < lower.Length; i++) {
                lowerTop = Mathf.Max(lowerTop, lower[i].y);
            }

            float biteLine = (upperBottom + lowerTop) * 0.5f;

            Quaternion turn = JawBehaviour.OpenTurn(JawBehaviour.OpenShapeDegrees, joint, chin);
            Debug.Log($"[Anatomy] Jaw: joint at ({joint.x * 1000f:F0}, {joint.y * 1000f:F0}, {joint.z * 1000f:F0}) mm, teeth meet at " +
                $"{biteLine * 1000f:F0} mm, the chin opens {Vector3.Distance(chin, turn * (chin - joint) + joint) * 1000f:F0} mm at full weight.");
            return new AnatomyJawMotion(joint, chin, biteLine, turn);
        }

        public int AddShapes(AnatomyMeshData part, Vector3 modelCentre, Mesh mesh) {
            mesh.ClearBlendShapes();
            if (System.Array.IndexOf(JawBehaviour.SoftIds, part.StructureId) < 0) {
                return 0;
            }

            return AnatomyFieldShapes.AddFieldShape(mesh, JawBehaviour.OpenShape, Follow,
                AnatomyFieldShapes.PositionsOf(part, modelCentre), AnatomyFieldShapes.NormalsOf(part));
        }

        /// <summary>
        /// Gives the model the line from the joint to the chin, which is all the runtime needs to hang the jaw from its hinge
        /// and to know which way is down: the joint is the first point and the chin the second.
        /// </summary>
        public void Finish(GameObject root) {
            AnatomyRoute route = root.AddComponent<AnatomyRoute>();
            SerializedObject serialized = new SerializedObject(route);
            serialized.FindProperty("routeName").stringValue = JawBehaviour.JawRoute;
            SerializedProperty points = serialized.FindProperty("points");
            SerializedProperty radii = serialized.FindProperty("radii");
            points.arraySize = 2;
            radii.arraySize = 2;
            points.GetArrayElementAtIndex(0).vector3Value = _joint;
            points.GetArrayElementAtIndex(1).vector3Value = _chin;
            radii.GetArrayElementAtIndex(0).floatValue = 0f;
            radii.GetArrayElementAtIndex(1).floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A point turns about the joint as far as its place lets it follow the jaw.</summary>
        private Vector3 Follow(Vector3 point) {
            float low = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_biteLine + FollowBelow, _biteLine + FollowAbove, point.y));
            float ahead = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_joint.z + BehindJoint, _joint.z - AheadOfJoint, point.z));
            return (_turn * (point - _joint) + _joint - point) * (low * ahead);
        }

        /// <summary>The vertex furthest back among the highest of one side of the jaw bone, which is the back of the joint's head.</summary>
        private static Vector3 Rearmost(Vector3[] vertices, float top, bool rightSide) {
            Vector3 best = vertices[0];
            float furthestBack = float.MinValue;
            for (int i = 0; i < vertices.Length; i++) {
                bool onSide = rightSide ? vertices[i].x < 0f : vertices[i].x >= 0f;
                if (onSide && vertices[i].y > top - JointSearchDepth && vertices[i].z > furthestBack) {
                    furthestBack = vertices[i].z;
                    best = vertices[i];
                }
            }

            return best;
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