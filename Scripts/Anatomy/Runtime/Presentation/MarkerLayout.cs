using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// The arithmetic behind the numbered markers, kept apart from anything that draws so it can be reasoned
    /// about on its own. Badges stand in two columns, one either side of the model, and slide up and down to
    /// stay level with the structure they name.
    /// </summary>
    public static class MarkerLayout {
        private const int ScratchCapacity = 32;

        private static readonly float[] BlockSum = new float[ScratchCapacity];
        private static readonly int[] BlockCount = new int[ScratchCapacity];

        /// <summary>
        /// Decides which column each badge stands in: the left half by position goes left, the rest right. It is
        /// decided once, when the topic loads, so a badge never jumps sides as the model is turned.
        /// </summary>
        /// <param name="positions">Where each structure sits across the screen, in any consistent unit.</param>
        /// <param name="count">How many entries of the arrays are in use.</param>
        /// <param name="onRight">Receives true for each badge that belongs in the right-hand column.</param>
        public static void SplitByPosition(float[] positions, int count, bool[] onRight) {
            int leftCount = count / 2;
            for (int i = 0; i < count; i++) {
                int rank = 0;
                for (int other = 0; other < count; other++) {
                    bool before = positions[other] < positions[i] || (positions[other] == positions[i] && other < i);
                    if (before) {
                        rank++;
                    }
                }

                onRight[i] = rank >= leftCount;
            }
        }

        /// <summary>
        /// Moves the badges as little as it can so each is at least <paramref name="gap"/> from the next, keeping them inside
        /// the range and in the order they came. Where the range is too small for the gap, the gap gives way.
        ///
        /// <para>A badge that has room stays exactly where it wants to be. A crowd that wants the same place is spread about
        /// the middle of where it wanted to be, and pushed along as a group, not squeezed, when it meets the end of the
        /// range. Measured in a space where each badge's height has its place in the order added back in, "at least a gap
        /// apart" becomes "never going up", and the closest such arrangement is found by pooling neighbours that break it.</para>
        /// </summary>
        /// <param name="sortedDescending">Heights, highest first. Adjusted in place.</param>
        public static void Spread(float[] sortedDescending, int count, float gap, float lowest, float highest) {
            if (count <= 0) {
                return;
            }

            if (count > ScratchCapacity) {
                Clamp(sortedDescending, count, lowest, highest);
                return;
            }

            float spacing = count > 1 ? Mathf.Min(gap, (highest - lowest) / (count - 1)) : gap;
            for (int i = 0; i < count; i++) {
                sortedDescending[i] = Mathf.Clamp(sortedDescending[i], lowest, highest) + i * spacing;
            }

            int blocks = 0;
            for (int i = 0; i < count; i++) {
                BlockSum[blocks] = sortedDescending[i];
                BlockCount[blocks] = 1;
                blocks++;
                while (blocks > 1 && BlockSum[blocks - 2] / BlockCount[blocks - 2] < BlockSum[blocks - 1] / BlockCount[blocks - 1]) {
                    BlockSum[blocks - 2] += BlockSum[blocks - 1];
                    BlockCount[blocks - 2] += BlockCount[blocks - 1];
                    blocks--;
                }
            }

            int index = 0;
            for (int block = 0; block < blocks; block++) {
                float mean = BlockSum[block] / BlockCount[block];
                for (int member = 0; member < BlockCount[block]; member++) {
                    sortedDescending[index] = mean - index * spacing;
                    index++;
                }
            }

            ShiftIntoRange(sortedDescending, count, lowest, highest);
        }

        /// <summary>Slides the whole column down if its top is above the range, and then up if its bottom is below it.</summary>
        private static void ShiftIntoRange(float[] values, int count, float lowest, float highest) {
            float shift = 0f;
            if (values[0] > highest) {
                shift = highest - values[0];
            }

            if (values[count - 1] + shift < lowest) {
                shift = lowest - values[count - 1];
            }

            if (shift != 0f) {
                for (int i = 0; i < count; i++) {
                    values[i] += shift;
                }
            }
        }

        private static void Clamp(float[] values, int count, float lowest, float highest) {
            for (int i = 0; i < count; i++) {
                values[i] = Mathf.Clamp(values[i], lowest, highest);
            }
        }
    }
}