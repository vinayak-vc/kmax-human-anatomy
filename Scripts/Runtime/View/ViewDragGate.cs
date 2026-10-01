using System.Collections.Generic;
using UnityEngine;

namespace ViitorCloud.KmaxDisplay {
    /// <summary>
    /// A latch any component can hold to stop the view from orbiting while it owns the pointer.
    ///
    /// <para>A press aimed at a manipulator - a scale handle, a slider, a dragged object - belongs
    /// to that manipulator. Without somewhere to say so, the view starts turning underneath the
    /// drag before the manipulator's own threshold has even recognised it.</para>
    ///
    /// <para>A static latch rather than a reference because neither side should have to know the
    /// other exists: a handle written next year can suppress the view without
    /// <see cref="ViewerFlyController"/> being edited. The SDK's own <c>KmaxPointer</c> registry
    /// works the same way.</para>
    ///
    /// <para>Holds are counted per owner, so two manipulators active at once do not release each
    /// other. <b>Whoever raises a hold must lower it</b>, including from <c>OnDisable</c> - a
    /// component switched off mid-drag would otherwise leave the view frozen for the rest of the
    /// session.</para>
    /// </summary>
    public static class ViewDragGate {
        private static readonly HashSet<object> holders = new HashSet<object>();

        /// <summary>True while at least one component is holding the view still.</summary>
        public static bool IsSuppressed {
            get { return holders.Count > 0; }
        }

        /// <summary>
        /// Raises or lowers this caller's own hold. Repeating the same value is free, so a
        /// component can drive it straight from its per-frame state without tracking edges.
        /// </summary>
        /// <param name="owner">
        /// Identity of the caller, so holds do not cancel each other. Pass <c>this</c>.
        /// </param>
        /// <param name="suppressed">True to hold the view still, false to let it orbit again.</param>
        public static void Set(object owner, bool suppressed) {
            if (owner == null) {
                Debug.LogError($"{nameof(ViewDragGate)}.{nameof(Set)} was called with no owner; " +
                    "the hold was ignored and the view may orbit under a drag that meant to stop it.");
                return;
            }

            if (suppressed) {
                holders.Add(owner);
                return;
            }

            holders.Remove(owner);
        }

        /// <summary>
        /// Drops every hold. For scene teardown only - clearing a gate this caller does not own
        /// unfreezes the view underneath another component's live drag.
        /// </summary>
        public static void ReleaseAll() {
            holders.Clear();
        }
    }
}
