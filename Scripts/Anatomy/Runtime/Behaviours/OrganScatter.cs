using System;
using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Works out where to throw the loose organs: a place for each, clear of the torso, of the buttons and of the other organs,
    /// inside the window. Plain C# with no scene in it, so the arrangement can be reasoned about and repeated from a seed.
    ///
    /// <para>Organs are treated as boxes, largest first, and each is given the first random spot that keeps the gap. Boxes pack
    /// far closer than circles do for the long shapes an intestine or a lung has. If a spot with the whole gap cannot be
    /// found the gap is halved and the search repeated, and if no spot clears even a hair the roomiest one seen is used, so
    /// every organ gets a place whatever the window.</para>
    /// </summary>
    public static class OrganScatter {
        private const int Attempts = 1500;
        private const int Rounds = 5;

        /// <summary>One position in the plane for each box, in the same order as <paramref name="halfSizes"/>.</summary>
        /// <param name="halfSizes">Half the width and height of each organ, in metres.</param>
        /// <param name="area">Where the centres may go; each box is kept inside it.</param>
        /// <param name="keepClear">Rectangles no organ may overlap.</param>
        /// <param name="gap">The room to leave between one organ and the next, in metres.</param>
        /// <param name="seed">The same seed gives the same arrangement.</param>
        public static Vector2[] Place(Vector2[] halfSizes, Rect area, IReadOnlyList<Rect> keepClear, float gap, int seed) {
            System.Random random = new System.Random(seed);
            int count = halfSizes.Length;
            int[] order = LargestFirst(halfSizes);
            Vector2[] positions = new Vector2[count];
            bool[] placed = new bool[count];

            for (int k = 0; k < count; k++) {
                int index = order[k];
                Vector2 roomiest = area.center;
                float roomiestClearance = float.MinValue;
                float wanted = gap;
                bool found = false;

                for (int round = 0; round < Rounds && !found; round++) {
                    for (int attempt = 0; attempt < Attempts && !found; attempt++) {
                        Vector2 candidate = RandomCentre(random, area, halfSizes[index]);
                        float clearance = Clearance(candidate, index, halfSizes, positions, placed, keepClear);
                        if (clearance >= wanted) {
                            roomiest = candidate;
                            found = true;
                        } else if (clearance > roomiestClearance) {
                            roomiestClearance = clearance;
                            roomiest = candidate;
                        }
                    }

                    wanted *= 0.5f;
                }

                positions[index] = roomiest;
                placed[index] = true;
            }

            return positions;
        }

        /// <summary>A centre for a box of this size that keeps the whole box inside the area, or the middle of the area if it cannot.</summary>
        private static Vector2 RandomCentre(System.Random random, Rect area, Vector2 halfSize) {
            float x = Mathf.Lerp(area.xMin + halfSize.x, area.xMax - halfSize.x, (float)random.NextDouble());
            float y = Mathf.Lerp(area.yMin + halfSize.y, area.yMax - halfSize.y, (float)random.NextDouble());
            if (area.width < 2f * halfSize.x) {
                x = area.center.x;
            }

            if (area.height < 2f * halfSize.y) {
                y = area.center.y;
            }

            return new Vector2(x, y);
        }

        /// <summary>How much room a box at this spot leaves: the least of its gaps to the rectangles and to the boxes already down.</summary>
        private static float Clearance(Vector2 centre, int index, Vector2[] halfSizes, Vector2[] positions, bool[] placed,
            IReadOnlyList<Rect> keepClear) {
            float least = float.MaxValue;
            for (int i = 0; i < keepClear.Count; i++) {
                least = Mathf.Min(least, BoxGap(centre, halfSizes[index], keepClear[i].center, keepClear[i].size * 0.5f));
            }

            for (int other = 0; other < positions.Length; other++) {
                if (other == index || !placed[other]) {
                    continue;
                }

                least = Mathf.Min(least, BoxGap(centre, halfSizes[index], positions[other], halfSizes[other]));
            }

            return least;
        }

        /// <summary>
        /// The space between two boxes: positive when they are apart, by how much, and negative when they overlap. It is the
        /// larger of the gaps along the two axes, since boxes are clear of each other as soon as they are clear along one.
        /// </summary>
        private static float BoxGap(Vector2 centreA, Vector2 halfA, Vector2 centreB, Vector2 halfB) {
            float gapX = Mathf.Abs(centreA.x - centreB.x) - (halfA.x + halfB.x);
            float gapY = Mathf.Abs(centreA.y - centreB.y) - (halfA.y + halfB.y);
            return Mathf.Max(gapX, gapY);
        }

        private static int[] LargestFirst(Vector2[] halfSizes) {
            int[] order = new int[halfSizes.Length];
            for (int i = 0; i < order.Length; i++) {
                order[i] = i;
            }

            Array.Sort(order, delegate (int a, int b) {
                float areaA = halfSizes[a].x * halfSizes[a].y;
                float areaB = halfSizes[b].x * halfSizes[b].y;
                return areaB.CompareTo(areaA);
            });
            return order;
        }
    }
}