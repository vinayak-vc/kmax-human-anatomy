using UnityEngine;

namespace ViitorCloud.KmaxDisplay {
    /// <summary>
    /// A damped harmonic oscillator that chases a target, solved in closed form so it is exact for any frame time and cannot
    /// blow up on a long frame. It is what gives a button its tactile bounce: with a damping ratio under one the value
    /// overshoots the target a little and settles, and at one or above it settles without overshoot.
    ///
    /// <para>Plain static maths with no Unity objects and no allocation, so it can be checked on its own.</para>
    /// </summary>
    public static class UiSpring {
        /// <summary>
        /// Moves <paramref name="value"/> and <paramref name="velocity"/> on by <paramref name="deltaTime"/> seconds towards
        /// <paramref name="target"/>.
        /// </summary>
        /// <param name="frequency">Undamped angular frequency in radians a second. Larger is stiffer and snappier.</param>
        /// <param name="damping">Damping ratio. Zero never settles, below one overshoots, one is critical, above one is treated as critical.</param>
        public static void Step(ref float value, ref float velocity, float target, float frequency, float damping, float deltaTime) {
            if (deltaTime <= 0f) {
                return;
            }

            float omega = Mathf.Max(0.0001f, frequency);
            float zeta = Mathf.Max(0f, damping);
            float offset = value - target;

            if (zeta < 1f) {
                float dampedOmega = omega * Mathf.Sqrt(1f - zeta * zeta);
                float decay = Mathf.Exp(-zeta * omega * deltaTime);
                float cos = Mathf.Cos(dampedOmega * deltaTime);
                float sin = Mathf.Sin(dampedOmega * deltaTime);
                float sine = (velocity + zeta * omega * offset) / dampedOmega;
                value = target + decay * (offset * cos + sine * sin);
                velocity = decay * ((sine * dampedOmega - zeta * omega * offset) * cos - (zeta * omega * sine + offset * dampedOmega) * sin);
                return;
            }

            float slope = velocity + omega * offset;
            float critical = Mathf.Exp(-omega * deltaTime);
            value = target + critical * (offset + slope * deltaTime);
            velocity = critical * (velocity - omega * slope * deltaTime);
        }
    }
}