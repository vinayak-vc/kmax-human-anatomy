using System;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// The unattended loop as a clock and three states, with no Unity types so it can be reasoned about on its own: the body
    /// map waits for a visitor and, if none comes, starts showing itself; a topic that nobody is touching goes back to the
    /// body map. Anything a visitor does, which the shell reports as activity, ends the wait.
    ///
    /// <para>It only says what is due, by events. Fading, loading and touring are the shell's.</para>
    /// </summary>
    public class KioskFlow {
        private readonly float _attractAfterSeconds;
        private readonly float _returnAfterSeconds;
        private float _idleSeconds;

        public KioskFlow(float attractAfterSeconds, float returnAfterSeconds) {
            _attractAfterSeconds = attractAfterSeconds;
            _returnAfterSeconds = returnAfterSeconds;
            State = KioskState.Hub;
        }

        /// <summary>Raised when the body map has waited long enough that it should start showing itself.</summary>
        public event Action AttractStarted;

        /// <summary>Raised when a visitor interrupts the body map's showing itself.</summary>
        public event Action AttractStopped;

        /// <summary>Raised when a topic has been left alone long enough that it should give way to the body map.</summary>
        public event Action ReturnDue;

        public KioskState State { get; private set; }

        /// <summary>The body map is now on show, and the wait for a visitor starts again.</summary>
        public void EnterHub() {
            State = KioskState.Hub;
            _idleSeconds = 0f;
        }

        /// <summary>A topic is now on show, and the wait for a visitor starts again.</summary>
        public void EnterTopic() {
            State = KioskState.Topic;
            _idleSeconds = 0f;
        }

        /// <summary>Moves the clock on. <paramref name="active"/> is true when a visitor did anything since the last call.</summary>
        public void Tick(float deltaSeconds, bool active) {
            if (active) {
                _idleSeconds = 0f;
                if (State == KioskState.Attract) {
                    State = KioskState.Hub;
                    Raise(AttractStopped);
                }

                return;
            }

            _idleSeconds += deltaSeconds;
            if (State == KioskState.Hub && _idleSeconds >= _attractAfterSeconds) {
                State = KioskState.Attract;
                Raise(AttractStarted);
            } else if (State == KioskState.Topic && _idleSeconds >= _returnAfterSeconds) {
                _idleSeconds = 0f;
                Raise(ReturnDue);
            }
        }

        private static void Raise(Action handler) {
            if (handler != null) {
                handler();
            }
        }
    }
}