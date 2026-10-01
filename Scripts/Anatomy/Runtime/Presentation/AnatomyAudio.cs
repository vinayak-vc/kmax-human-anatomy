using UnityEngine;

using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// The exhibit's content sounds: the cues for pointing at and picking structures, pulling a topic apart and resetting it,
    /// and the loops that belong to a topic (the heartbeat, the ear's tone, the breath) and the puzzle's chimes. Everything is
    /// synthesised at start by <see cref="ProceduralAudio"/>, so there are no audio assets to ship, and every sound has an
    /// override slot so recorded audio is an inspector edit and not a code change.
    ///
    /// <para>The music under everything, and the sound of the buttons, belong to the
    /// <see cref="PersistentAudioDirector"/>, which outlasts any one topic.</para>
    /// </summary>
    public class AnatomyAudio : MonoBehaviour {
        private const float MinimumHoverInterval = 0.08f;

        /// <summary>Semitones above the first chime for each organ put back: a rising run of a pentatonic scale, two octaves long.</summary>
        private static readonly int[] PlacedSemitones = new int[] { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21 };

        [Header("Overrides")]
        [SerializeField, Tooltip("The pointer arrives on a structure. Empty uses a synthesised blip.")]
        private AudioClip hoverClip;
        [SerializeField, Tooltip("A structure is picked, or a tour step is shown. Empty uses a synthesised soft bell, E5 and B5.")]
        private AudioClip selectClip;
        [SerializeField, Tooltip("The view is reset. Empty uses a synthesised falling sweep.")]
        private AudioClip resetClip;
        [SerializeField, Tooltip("A topic is pulled apart. Empty uses a synthesised rising sweep.")]
        private AudioClip expandClip;
        [SerializeField, Tooltip("A topic is put back together. Empty uses a synthesised falling sweep.")]
        private AudioClip collapseClip;
        [SerializeField, Tooltip("One heartbeat as a seamless loop. Empty uses the synthesised beat.")]
        private AudioClip heartbeatClip;
        [SerializeField, Tooltip("One sound going into the ear, with the quiet after it, as a seamless loop. Empty uses the synthesised tone.")]
        private AudioClip hearingClip;
        [SerializeField, Tooltip("One breath as a seamless loop. Empty uses the synthesised rush of air.")]
        private AudioClip breathingClip;
        [SerializeField, Tooltip("An organ settles into its place. Empty uses a synthesised chime, raised a step for each organ put back.")]
        private AudioClip placedClip;
        [SerializeField, Tooltip("The organ puzzle is finished. Empty uses a synthesised rising fanfare.")]
        private AudioClip completeClip;

        [Header("Levels")]
        [SerializeField, Range(0f, 1f)] private float cueVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float heartbeatVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float hearingVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float breathingVolume = 0.3f;

        private AudioSource _cues;
        private AudioSource _heartbeat;
        private AudioSource _hearing;
        private AudioSource _breathing;
        private AudioSource _rewards;
        private AudioClip _placed;
        private AudioClip _complete;
        private AudioClip _hover;
        private AudioClip _select;
        private AudioClip _reset;
        private AudioClip _expand;
        private AudioClip _collapse;
        private float _lastHoverTime = -1f;

        private void Awake() {
            _cues = CreateSource("Cues", false, cueVolume);
            _heartbeat = CreateSource("Heartbeat", true, heartbeatVolume);
            _hearing = CreateSource("Hearing", true, hearingVolume);
            _breathing = CreateSource("Breathing", true, breathingVolume);
            _rewards = CreateSource("Rewards", false, cueVolume);

            _hover = hoverClip != null ? hoverClip : ProceduralAudio.CreateBlip();
            _select = selectClip != null ? selectClip : ProceduralAudio.CreateHotspotChime();
            _reset = resetClip != null ? resetClip : ProceduralAudio.CreateCollapseSweep();
            _expand = expandClip != null ? expandClip : ProceduralAudio.CreateExpandSweep();
            _collapse = collapseClip != null ? collapseClip : ProceduralAudio.CreateCollapseSweep();
            _placed = placedClip != null ? placedClip : ProceduralAudio.CreateChime(523.25f, 1.1f, 0.9f);
            _complete = completeClip != null ? completeClip : ProceduralAudio.CreateFanfare();
        }

        /// <summary>The pointer arrived on a structure. Rate limited, so a sweep across many does not chatter.</summary>
        public void PlayHover() {
            if (Time.unscaledTime - _lastHoverTime < MinimumHoverInterval) {
                return;
            }

            _lastHoverTime = Time.unscaledTime;
            _cues.PlayOneShot(_hover, 0.6f);
        }

        public void PlaySelect() {
            _cues.PlayOneShot(_select, 0.9f);
        }

        public void PlayReset() {
            _cues.PlayOneShot(_reset, 0.8f);
        }

        /// <summary>A topic is being pulled apart.</summary>
        public void PlayExpand() {
            _cues.PlayOneShot(_expand, 0.8f);
        }

        /// <summary>A topic is being put back together.</summary>
        public void PlayCollapse() {
            _cues.PlayOneShot(_collapse, 0.8f);
        }

        /// <summary>An organ has settled home. The chime rises with each one, so the puzzle sounds as if it is building towards its end.</summary>
        /// <param name="placed">How many organs are home, counting this one.</param>
        public void PlayPlaced(int placed) {
            int step = Mathf.Clamp(placed - 1, 0, PlacedSemitones.Length - 1);
            _rewards.pitch = Mathf.Pow(2f, PlacedSemitones[step] / 12f);
            _rewards.PlayOneShot(_placed, 0.9f);
        }

        /// <summary>The last organ is home.</summary>
        public void PlayComplete() {
            _rewards.pitch = 1f;
            _rewards.PlayOneShot(_complete, 1f);
        }

        /// <summary>Starts the heartbeat loop. Its lub and dub fall at the given fractions of the beat.</summary>
        public void StartHeartbeat(float beatsPerMinute, float lubFraction, float dubFraction) {
            _heartbeat.clip = heartbeatClip != null
                ? heartbeatClip
                : ProceduralAudio.CreateHeartbeat(beatsPerMinute, lubFraction, dubFraction);
            _heartbeat.Play();
        }

        public void StopHeartbeat() {
            _heartbeat.Stop();
        }

        /// <summary>Starts the ear's sound: a soft tone, then quiet, repeating every cycle.</summary>
        public void StartHearing(float cycleSeconds) {
            _hearing.clip = hearingClip != null
                ? hearingClip
                : ProceduralAudio.CreateTonePulse(ProceduralAudio.DefaultToneHz, cycleSeconds);
            _hearing.Play();
        }

        public void StopHearing() {
            _hearing.Stop();
        }

        /// <summary>Starts the soft rush of breath, one breath to the cycle, and repeats it.</summary>
        public void StartBreathing(float cycleSeconds) {
            _breathing.clip = breathingClip != null ? breathingClip : ProceduralAudio.CreateBreath(cycleSeconds);
            _breathing.Play();
        }

        public void StopBreathing() {
            _breathing.Stop();
        }

        /// <summary>How far through its cycle the breath's sound is, from 0 to 1, or -1 when it is not playing.</summary>
        public float BreathPhase {
            get {
                if (!_breathing.isPlaying || _breathing.clip == null || _breathing.clip.samples <= 0) {
                    return -1f;
                }

                return (float)_breathing.timeSamples / _breathing.clip.samples;
            }
        }

        /// <summary>How far through its cycle the ear's sound is, from 0 to 1, or -1 when it is not playing.</summary>
        public float HearingPhase {
            get {
                if (!_hearing.isPlaying || _hearing.clip == null || _hearing.clip.samples <= 0) {
                    return -1f;
                }

                return (float)_hearing.timeSamples / _hearing.clip.samples;
            }
        }

        private AudioSource CreateSource(string sourceName, bool loop, float volume) {
            GameObject host = new GameObject(sourceName);
            host.transform.SetParent(transform, false);
            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0f;
            return source;
        }
    }
}