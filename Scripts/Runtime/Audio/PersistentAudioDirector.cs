using UnityEngine;

namespace ViitorCloud.KmaxDisplay {
    /// <summary>
    /// Owns the exhibit's background music and the sounds of its buttons, and keeps them going across scene loads: the music
    /// never restarts when the scene changes, and a scene that carries its own director alongside the first one is
    /// quietly thinned down to one.
    ///
    /// <para>The music is a soothing bed (a low drone, a slow pad and a sparse pentatonic melody, about 66 beats a minute)
    /// synthesised once by <see cref="ProceduralAudio"/>, so there are no audio files to ship. The button sounds are a tick
    /// on hover and a two-tone drop on a press, the press varied by a few per cent each time. Every sound has an override
    /// slot, so recorded audio is an inspector edit and not a code change.</para>
    ///
    /// <para>What happens in a topic (a heartbeat, a chime for a part put back) is not this component's: it is the
    /// content's own audio, and it plays over the music.</para>
    /// </summary>
    public class PersistentAudioDirector : MonoBehaviour {
        private const float HoverMinimumInterval = 0.05f;

        private static PersistentAudioDirector _instance;

        [Header("Overrides")]
        [SerializeField, Tooltip("The music bed, as a seamless loop. Empty uses the synthesised bed.")]
        private AudioClip musicClip;
        [SerializeField, Tooltip("A pointer arrives on a button. Empty uses a synthesised tick.")]
        private AudioClip uiHoverClip;
        [SerializeField, Tooltip("A button is pressed. Empty uses a synthesised two-tone drop.")]
        private AudioClip uiClickClip;

        [Header("Music")]
        [SerializeField, Range(60f, 75f), Tooltip("Tempo of the synthesised bed, in beats a minute.")]
        private float beatsPerMinute = 66f;
        [SerializeField, Range(0f, 1f), Tooltip("Level of the music under everything else.")]
        private float musicVolume = 0.14f;
        [SerializeField, Range(0f, 10f), Tooltip("Seconds the music takes to rise to its level when the exhibit starts.")]
        private float musicFadeInSeconds = 3f;

        [Header("Interface")]
        [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.7f;
        [SerializeField, Range(0f, 0.2f), Tooltip("How far, as a fraction, each press's pitch may stray from the clip's own.")]
        private float clickPitchSpread = 0.05f;

        private AudioSource _music;
        private AudioSource _hover;
        private AudioSource _click;
        private AudioClip _hoverTick;
        private AudioClip _clickDrop;
        private float _musicClock;
        private float _lastHoverTime = -1f;

        /// <summary>The director that is playing, or null before one has started.</summary>
        public static PersistentAudioDirector Instance {
            get { return _instance; }
        }

        /// <summary>
        /// Editor play mode can start without reloading the domain, which would leave the last session's director behind in
        /// this static. Forget it before anything wakes.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetInstance() {
            _instance = null;
        }

        private void Awake() {
            if (_instance != null && _instance != this) {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // Only a root object can outlive a scene.
            transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);

            _music = CreateSource("Music", true, 0f);
            _music.clip = musicClip != null ? musicClip : ProceduralAudio.CreateAmbientMusic(beatsPerMinute);
            _hover = CreateSource("UiHover", false, hoverVolume);
            _click = CreateSource("UiClick", false, clickVolume);
            _hoverTick = uiHoverClip != null ? uiHoverClip : ProceduralAudio.CreateUiHoverTick();
            _clickDrop = uiClickClip != null ? uiClickClip : ProceduralAudio.CreateUiClick();
        }

        private void Start() {
            if (_instance == this) {
                _music.Play();
            }
        }

        /// <summary>Raises the music to its level over the fade-in, then switches itself off.</summary>
        private void Update() {
            // A duplicate that is about to be destroyed built nothing, and has nothing to raise.
            if (_music == null) {
                enabled = false;
                return;
            }

            _musicClock += Time.unscaledDeltaTime;
            float rise = musicFadeInSeconds > 0f ? Mathf.Clamp01(_musicClock / musicFadeInSeconds) : 1f;
            _music.volume = musicVolume * rise;
            if (rise >= 1f) {
                enabled = false;
            }
        }

        private void OnDestroy() {
            if (_instance == this) {
                _instance = null;
            }
        }

        /// <summary>A pointer arrived on a button. Rate limited, so a sweep across a row of them does not chatter.</summary>
        public void PlayUiHover() {
            if (_hover == null || Time.unscaledTime - _lastHoverTime < HoverMinimumInterval) {
                return;
            }

            _lastHoverTime = Time.unscaledTime;
            _hover.PlayOneShot(_hoverTick);
        }

        /// <summary>A button was pressed. The pitch strays a few per cent either way each time.</summary>
        public void PlayUiClick() {
            if (_click == null) {
                return;
            }

            _click.pitch = 1f + Random.Range(-clickPitchSpread, clickPitchSpread);
            _click.PlayOneShot(_clickDrop);
        }

        /// <summary>Sets the level of the music, as a fraction of full scale, and cancels any fade-in still under way.</summary>
        public void SetMusicVolume(float volume) {
            musicVolume = Mathf.Clamp01(volume);
            _musicClock = musicFadeInSeconds;
            _music.volume = musicVolume;
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