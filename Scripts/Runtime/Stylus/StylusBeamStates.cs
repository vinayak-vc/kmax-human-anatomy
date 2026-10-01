using UnityEngine;

namespace ViitorCloud.KmaxDisplay {
    /// <summary>
    /// The rules that turn the pen's buttons into a <see cref="StylusBeamState"/> and a state into a colour, as plain functions
    /// so they can be checked without a pen.
    /// </summary>
    public static class StylusBeamStates {
        /// <summary>
        /// The state for what is held. A secondary press wins over a tertiary one, and both over the select button: a visitor
        /// who presses Reset while holding select should see that the reset is what the pen is doing. The select button
        /// held over nothing selects nothing, so it is the zoom gesture and shows as a function.
        /// </summary>
        /// <param name="overTarget">True when the ray rests on something a press could select: a button, a badge, a structure.</param>
        public static StylusBeamState Resolve(bool selectHeld, bool secondaryHeld, bool tertiaryHeld, bool overTarget) {
            if (secondaryHeld) {
                return StylusBeamState.Secondary;
            }

            if (tertiaryHeld) {
                return StylusBeamState.Tertiary;
            }

            if (selectHeld) {
                return overTarget ? StylusBeamState.Select : StylusBeamState.Tertiary;
            }

            return StylusBeamState.Calm;
        }

        /// <summary>
        /// The pen's third button: whichever of the three is neither the select button nor the secondary one. The pen
        /// reports three buttons, indices 0 to 2.
        /// </summary>
        public static int TertiaryIndex(int selectIndex, int secondaryIndex) {
            for (int index = 0; index < 3; index++) {
                if (index != selectIndex && index != secondaryIndex) {
                    return index;
                }
            }

            return 0;
        }

        /// <summary>
        /// Brightens a colour by an emission multiplier. With <paramref name="limitToDisplayRange"/> the multiplier stops where
        /// the brightest channel reaches one, because a display without bloom clamps each channel on its own and a bright green
        /// would turn cyan; the hue is what tells the states apart. Turn the limit off once the cameras have bloom, and the
        /// multiplier is then a true HDR emission.
        /// </summary>
        public static Color ApplyEmission(Color colour, float emission, bool limitToDisplayRange) {
            float scale = Mathf.Max(0f, emission);
            if (limitToDisplayRange) {
                float brightest = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
                if (brightest > Mathf.Epsilon) {
                    scale = Mathf.Min(scale, 1f / brightest);
                }
            }

            return new Color(colour.r * scale, colour.g * scale, colour.b * scale, colour.a);
        }
    }
}