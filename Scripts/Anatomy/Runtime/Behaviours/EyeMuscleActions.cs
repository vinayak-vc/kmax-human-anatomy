using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Which way each muscle of the eye turns it, for a left eye: yaw positive towards the outer corner, pitch positive
    /// upward. The pull is what makes a muscle light up as the eye turns that way, and what the eye turns to when that
    /// muscle is being explained.
    /// </summary>
    public static class EyeMuscleActions {
        public const string SuperiorRectusId = "eye_superior_rectus_muscle";
        public const string InferiorRectusId = "eye_inferior_rectus_muscle";
        public const string MedialRectusId = "eye_medial_rectus_muscle";
        public const string LateralRectusId = "eye_lateral_rectus_muscle";
        public const string SuperiorObliqueId = "eye_superior_oblique_muscle";
        public const string InferiorObliqueId = "eye_inferior_oblique_muscle";
        public const string LevatorId = "eye_levator_palpae_superioris_m";

        /// <summary>The six muscles that turn the eye, then the one that lifts the lid.</summary>
        public static readonly string[] MuscleIds = new string[] {
            LateralRectusId, SuperiorRectusId, MedialRectusId, InferiorRectusId, SuperiorObliqueId, InferiorObliqueId, LevatorId
        };

        /// <summary>
        /// The direction the muscle pulls, as a yaw and a pitch each from -1 to 1. Returns false for a structure that is
        /// not one of the eye's muscles.
        /// </summary>
        public static bool TryGetPull(string structureId, out Vector2 pull) {
            switch (structureId) {
                case LateralRectusId:
                    pull = new Vector2(1f, 0f);
                    return true;
                case MedialRectusId:
                    pull = new Vector2(-1f, 0f);
                    return true;
                case SuperiorRectusId:
                case LevatorId:
                    pull = new Vector2(0f, 1f);
                    return true;
                case InferiorRectusId:
                    pull = new Vector2(0f, -1f);
                    return true;
                case SuperiorObliqueId:
                    pull = new Vector2(0.7f, -0.7f);
                    return true;
                case InferiorObliqueId:
                    pull = new Vector2(0.7f, 0.7f);
                    return true;
                default:
                    pull = Vector2.zero;
                    return false;
            }
        }

        /// <summary>
        /// How hard the muscle is working, from 0 to 1, when the eye is turned this far round and up, as a share of the
        /// furthest it turns.
        /// </summary>
        public static float Activation(Vector2 pull, float yawShare, float pitchShare) {
            float along = pull.x * yawShare + pull.y * pitchShare;
            return Mathf.Clamp01(along / Mathf.Max(0.01f, pull.sqrMagnitude));
        }
    }
}