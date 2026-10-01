using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Which way the eye's yaw and pitch turn it, in the model's own axes. The importer bakes these same turns into the
    /// soft tissue around the globe and the runtime turns the globe by them, so both take their signs from here and
    /// cannot disagree about which way is which.
    ///
    /// <para>Yaw is an azimuth, about the vertical axis, positive towards +x. Pitch is an elevation, about the
    /// horizontal axis, positive towards +y. Yaw is applied after pitch, so a yaw never changes the elevation.</para>
    /// </summary>
    public static class EyeGazeFrame {
        /// <summary>A turn about the vertical axis that swings the eye's forward direction towards +x when the angle is positive.</summary>
        public static Quaternion Yaw(float degrees, Vector3 forward) {
            float side = Vector3.Dot(Quaternion.AngleAxis(10f, Vector3.up) * forward, Vector3.right) >= 0f ? 1f : -1f;
            return Quaternion.AngleAxis(side * degrees, Vector3.up);
        }

        /// <summary>A turn about the horizontal axis that swings the eye's forward direction towards +y when the angle is positive.</summary>
        public static Quaternion Pitch(float degrees, Vector3 forward) {
            float side = Vector3.Dot(Quaternion.AngleAxis(10f, Vector3.right) * forward, Vector3.up) >= 0f ? 1f : -1f;
            return Quaternion.AngleAxis(side * degrees, Vector3.right);
        }

        /// <summary>The turn from the eye's rest direction to one this many degrees round and up from it.</summary>
        public static Quaternion Gaze(float yawDegrees, float pitchDegrees, Vector3 forward) {
            return Yaw(yawDegrees, forward) * Pitch(pitchDegrees, forward);
        }

        /// <summary>
        /// The yaw and pitch, in degrees, that turn the forward direction to look along this one. The eye looks down
        /// -z at rest, so a direction is measured as an azimuth and an elevation from there, less the same for the
        /// forward direction.
        /// </summary>
        public static void AnglesTo(Vector3 direction, Vector3 forward, out float yawDegrees, out float pitchDegrees) {
            yawDegrees = Azimuth(direction) - Azimuth(forward);
            pitchDegrees = Elevation(direction) - Elevation(forward);
        }

        private static float Azimuth(Vector3 direction) {
            return Mathf.Atan2(direction.x, -direction.z) * Mathf.Rad2Deg;
        }

        private static float Elevation(Vector3 direction) {
            return Mathf.Atan2(direction.y, Mathf.Sqrt(direction.x * direction.x + direction.z * direction.z)) * Mathf.Rad2Deg;
        }
    }
}