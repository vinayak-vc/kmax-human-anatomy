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

        /// <summary>
        /// Adds the named behaviour in the quiet form a preview shows it in, for the topics whose behaviour is a motion and
        /// nothing more: the heart beats, the chest breathes, sound travels into the ear and the body map's organs glow, all
        /// without a sound. A behaviour that is for the visitor's hands, or that works on the model's colliders, is left off,
        /// and the preview of that topic is still. Returns whether one was attached.
        /// </summary>
        public static bool AttachForPreview(GameObject model, string key) {
            switch (key) {
                case HeartbeatKey:
                    model.AddComponent<HeartbeatBehaviour>().Begin(null);
                    return true;
                case HearingKey:
                    model.AddComponent<HearingBehaviour>().Begin(null);
                    return true;
                case BreathingKey:
                    model.AddComponent<BreathingBehaviour>().Begin(null);
                    return true;
                case BodyKey:
                    model.AddComponent<BodyBehaviour>().Begin();
                    return true;
                default:
                    return false;
            }
        }

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