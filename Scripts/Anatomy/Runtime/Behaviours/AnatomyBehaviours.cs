using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Attaches the animation a topic asks for by name in its data. Each new topic behaviour is one class and
    /// one case here; the controller never learns what any of them do.
    /// </summary>
    public static class AnatomyBehaviours {
        private const string HeartbeatKey = "heartbeat";
        private const string HearingKey = "hearing";
        private const string EyeKey = "eye";
        private const string BreathingKey = "breathing";
        private const string JawKey = "jaw";
        private const string BodyKey = "body";
        private const string OrgansKey = "organs";
        private const string ScanKey = "scan";

        /// <summary>Adds the named behaviour to the model. An empty key means the topic has none.</summary>
        public static void Attach(GameObject model, string key, AnatomyBehaviourContext context) {
            if (string.IsNullOrEmpty(key)) {
                return;
            }

            switch (key) {
                case HeartbeatKey:
                    model.AddComponent<HeartbeatBehaviour>().Begin(context.Sound);
                    break;
                case HearingKey:
                    model.AddComponent<HearingBehaviour>().Begin(context.Sound);
                    break;
                case EyeKey:
                    model.AddComponent<EyeBehaviour>().Begin();
                    break;
                case BreathingKey:
                    model.AddComponent<BreathingBehaviour>().Begin(context.Sound);
                    break;
                case JawKey:
                    model.AddComponent<JawBehaviour>().Begin();
                    break;
                case BodyKey:
                    model.AddComponent<BodyBehaviour>().Begin();
                    break;
                case OrgansKey:
                    model.AddComponent<OrganPuzzleBehaviour>().Begin(context);
                    break;
                case ScanKey:
                    model.AddComponent<ScanBehaviour>().Begin(context);
                    break;
                default:
                    Debug.LogWarning($"[Anatomy] Topic asks for an unknown behaviour '{key}'.", model);
                    break;
            }
        }
    }
}