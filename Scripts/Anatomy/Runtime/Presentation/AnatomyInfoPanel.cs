using TMPro;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// The topic title and the caption bar. It only shows what it is told; which structure, tour step or
    /// topic to speak about is decided elsewhere.
    ///
    /// <para>Everything sits on the screen plane, where stereo is sharpest and text reads without effort.
    /// The caption fades in when it changes, and an identical caption does not restart the fade, so a
    /// pointer sweeping back and forth over one structure does not flicker the words.</para>
    /// </summary>
    public class AnatomyInfoPanel : MonoBehaviour {
        [Header("Header")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;

        [Header("Caption")]
        [SerializeField] private CanvasGroup captionGroup;
        [SerializeField] private TMP_Text headingText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text factText;
        [SerializeField] private TMP_Text stepText;
        [SerializeField, Tooltip("Seconds a caption takes to fade in after it changes.")]
        private float fadeInTime = 0.2f;

        private string _shownKey;
        private string _shownHeader;
        private float _fade = 1f;

        private void Update() {
            _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeInTime));
            captionGroup.alpha = _fade;
            if (_fade >= 1f) {
                enabled = false;
            }
        }

        /// <summary>Shows the topic's title and subtitle. Showing the same topic's again changes nothing.</summary>
        public void ShowHeader(AnatomyTopicData data) {
            if (_shownHeader == data.Id) {
                return;
            }

            _shownHeader = data.Id;
            titleText.text = data.Title;
            subtitleText.text = data.Subtitle;
        }

        /// <summary>Shows the topic's title, and its introduction as the caption.</summary>
        public void ShowTopic(AnatomyTopicData data) {
            ShowHeader(data);
            SetCaption("topic:" + data.Id, string.Empty, data.Intro, string.Empty, string.Empty);
        }

        public void ShowStructure(string structureId, string heading, string summary, string fact) {
            SetCaption("structure:" + structureId, heading, summary, fact, string.Empty);
        }

        /// <summary>Shows what a topic's own behaviour has to say, such as how far a puzzle has got.</summary>
        public void ShowMessage(TopicMessage message) {
            if (message.Key == _shownKey) {
                // The same thing said again, perhaps with its progress changed: only the progress is updated, so the caption
                // does not fade in afresh every time a depth readout ticks over.
                if (stepText.text != message.Progress) {
                    stepText.text = message.Progress;
                    stepText.gameObject.SetActive(!string.IsNullOrEmpty(message.Progress));
                }

                return;
            }

            SetCaption(message.Key, message.Heading, message.Body, message.Fact, message.Progress);
        }

        public void ShowTourStep(int index, int count, string caption) {
            SetCaption("step:" + index, string.Empty, caption, string.Empty, $"Step {index + 1} of {count}");
        }

        private void SetCaption(string key, string heading, string body, string fact, string step) {
            if (key == _shownKey) {
                return;
            }

            _shownKey = key;
            headingText.text = heading;
            bodyText.text = body;
            factText.text = fact;
            stepText.text = step;
            headingText.gameObject.SetActive(!string.IsNullOrEmpty(heading));
            factText.gameObject.SetActive(!string.IsNullOrEmpty(fact));
            stepText.gameObject.SetActive(!string.IsNullOrEmpty(step));

            _fade = captionGroup.alpha > 0.5f ? 0.45f : 0f;
            captionGroup.alpha = _fade;
            enabled = true;
        }
    }
}