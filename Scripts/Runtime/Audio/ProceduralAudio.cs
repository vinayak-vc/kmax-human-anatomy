using UnityEngine;

namespace ViitorCloud.KmaxDisplay {
    /// <summary>
    /// Builds ambience and interface sounds as audio clips at runtime.
    ///
    /// Synthesised rather than imported so the module ships with no audio assets and no licence
    /// question attached to them. Callers are expected to expose an override clip for each
    /// sound, so swapping in recorded audio later is an inspector edit and no code change.
    /// </summary>
    public static class ProceduralAudio {
        /// <summary>The pitch of the tone the ear exhibit plays by default: C above middle C.</summary>
        public const float DefaultToneHz = 523.25f;

        /// <summary>The sample rate the ambient music is made at, in hertz.</summary>
        private const int MusicSampleRate = 24000;

        /// <summary>
        /// One voice of the pad: a frequency relative to the root, how loud it is, and how far it
        /// is detuned and panned.
        /// </summary>
        public struct PadVoice {
            public float Ratio;
            public float Gain;
            public float DetuneCents;
            public float Pan;

            public PadVoice(float ratio, float gain, float detuneCents, float pan) {
                Ratio = ratio;
                Gain = gain;
                DetuneCents = detuneCents;
                Pan = pan;
            }
        }

        /// <summary>
        /// A slow, warm stereo pad that loops without a seam.
        ///
        /// Seamlessness is the whole constraint here: every partial and every tremolo rate is
        /// snapped to a whole number of cycles across the loop, so the waveform and its envelope
        /// both arrive back exactly where they started. Without the snap the loop point clicks.
        /// </summary>
        /// <param name="rootHz">Fundamental of the chord, in hertz.</param>
        /// <param name="loopSeconds">Loop length. Longer is less repetitive but costs memory.</param>
        /// <param name="voices">Chord voicing. Null uses a calm suspended-ninth voicing.</param>
        public static AudioClip CreatePad(float rootHz = 110f, float loopSeconds = 16f, PadVoice[] voices = null) {
            if (voices == null) {
                // Root, fifth, octave, ninth and twelfth: open and unresolved, so it never sounds
                // like it is about to end - which matters for something playing all day.
                // Weighted towards the upper partials rather than the root. Most of a pad's warmth
                // lives in the fundamental, but a display's panel speakers roll that away entirely -
                // so the octave and the fifth above it carry the chord and the root only colours it.
                voices = new PadVoice[] {
                    new PadVoice(1.00f, 0.30f, 0f, -0.15f),
                    new PadVoice(1.50f, 0.34f, +4f, 0.35f),
                    new PadVoice(2.00f, 0.38f, -3f, -0.40f),
                    new PadVoice(2.25f, 0.22f, +6f, 0.55f),
                    new PadVoice(3.00f, 0.24f, -5f, -0.60f),
                    new PadVoice(4.00f, 0.15f, +7f, 0.25f)
                };
            }

            int sampleRate = GetSampleRate();
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * loopSeconds));
            float actualLoop = frames / (float)sampleRate;
            float[] data = new float[frames * 2];

            for (int v = 0; v < voices.Length; v++) {
                PadVoice voice = voices[v];
                float frequency = rootHz * voice.Ratio * CentsToRatio(voice.DetuneCents);
                frequency = SnapToLoop(frequency, actualLoop);

                // Each voice breathes at its own slow rate, which keeps the chord moving without
                // anything ever arriving or departing.
                float tremoloHz = SnapToLoop(0.045f + v * 0.021f, actualLoop);
                float tremoloPhase = v * 0.9f;

                float leftGain = voice.Gain * Mathf.Sqrt(Mathf.Clamp01(0.5f - voice.Pan * 0.5f));
                float rightGain = voice.Gain * Mathf.Sqrt(Mathf.Clamp01(0.5f + voice.Pan * 0.5f));

                float angularStep = 2f * Mathf.PI * frequency / sampleRate;
                float tremoloStep = 2f * Mathf.PI * tremoloHz / sampleRate;

                for (int i = 0; i < frames; i++) {
                    float sample = Mathf.Sin(angularStep * i);
                    float tremolo = 0.72f + 0.28f * Mathf.Sin(tremoloStep * i + tremoloPhase);
                    sample *= tremolo;

                    data[i * 2] += sample * leftGain;
                    data[i * 2 + 1] += sample * rightGain;
                }
            }

            NormalisePeak(data, 0.72f);
            SoftClip(data);

            AudioClip clip = AudioClip.Create("KmaxPad", frames, 2, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// A soft bell used for selecting a part: a few inharmonic partials under an exponential
        /// decay, which is what gives a struck body its shimmer rather than an organ tone.
        /// </summary>
        public static AudioClip CreateChime(float rootHz = 880f, float durationSeconds = 0.9f, float brightness = 1f) {
            float[] ratios = new float[] { 1f, 2.01f, 2.99f, 4.18f, 5.42f };
            float[] gains = new float[] { 1f, 0.46f, 0.28f, 0.15f, 0.08f };
            float[] decays = new float[] { 1f, 1.5f, 2.1f, 3.0f, 4.2f };

            int sampleRate = GetSampleRate();
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] data = new float[frames];

            for (int p = 0; p < ratios.Length; p++) {
                float frequency = rootHz * ratios[p];
                if (frequency >= sampleRate * 0.45f) {
                    continue;
                }

                float gain = gains[p] * Mathf.Pow(brightness, p);
                float decayRate = decays[p] * 4.5f / Mathf.Max(durationSeconds, 0.001f);
                float angularStep = 2f * Mathf.PI * frequency / sampleRate;

                for (int i = 0; i < frames; i++) {
                    float t = i / (float)sampleRate;
                    data[i] += Mathf.Sin(angularStep * i) * gain * Mathf.Exp(-decayRate * t);
                }
            }

            ApplyFadeIn(data, sampleRate, 0.004f);
            NormalisePeak(data, 0.8f);
            return ToClip("KmaxChime", data, 1, sampleRate);
        }

        /// <summary>
        /// A short pitched blip for hovers and navigation steps. Deliberately quiet and brief - it
        /// fires often, so anything with a tail becomes noise.
        /// </summary>
        public static AudioClip CreateBlip(float frequencyHz = 1320f, float durationSeconds = 0.07f) {
            int sampleRate = GetSampleRate();
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] data = new float[frames];

            float angularStep = 2f * Mathf.PI * frequencyHz / sampleRate;
            float decayRate = 26f;

            for (int i = 0; i < frames; i++) {
                float t = i / (float)sampleRate;
                // A touch of second harmonic stops it sounding like a test tone.
                float sample = Mathf.Sin(angularStep * i) + 0.25f * Mathf.Sin(angularStep * 2f * i);
                data[i] = sample * Mathf.Exp(-decayRate * t);
            }

            ApplyFadeIn(data, sampleRate, 0.002f);
            ApplyFadeOut(data, sampleRate, 0.008f);
            NormalisePeak(data, 0.65f);
            return ToClip("KmaxBlip", data, 1, sampleRate);
        }

        /// <summary>
        /// Air moving past: filtered noise swept by a resonant one-pole, used for a part opening
        /// and for the camera flying to a part.
        /// </summary>
        /// <param name="rising">True sweeps the filter upward, false sweeps it down.</param>
        public static AudioClip CreateWhoosh(float durationSeconds = 0.55f, bool rising = true) {
            int sampleRate = GetSampleRate();
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] data = new float[frames];

            // Deterministic so the exhibit sounds identical on every launch.
            Random.State previousState = Random.state;
            Random.InitState(20260925);

            float lowpass = 0f;
            float highpassMemory = 0f;
            float previousInput = 0f;

            for (int i = 0; i < frames; i++) {
                float t = i / (float)frames;
                float noise = Random.value * 2f - 1f;

                // Sweep the cutoff across the body of the sound, which is what reads as movement.
                float sweep = rising ? t : 1f - t;
                float cutoff = Mathf.Lerp(0.02f, 0.38f, sweep * sweep);

                lowpass += (noise - lowpass) * cutoff;

                // A gentle high-pass underneath keeps the rumble out of the display's speakers.
                highpassMemory = 0.92f * (highpassMemory + lowpass - previousInput);
                previousInput = lowpass;

                // Swell in and out so there is no edge at either end.
                float envelope = Mathf.Sin(t * Mathf.PI);
                data[i] = highpassMemory * envelope * envelope;
            }

            Random.state = previousState;

            NormalisePeak(data, 0.55f);
            return ToClip("KmaxWhoosh", data, 1, sampleRate);
        }

        /// <summary>
        /// A low, short thud for returning and resetting - the sound of something settling back.
        /// </summary>
        public static AudioClip CreateThud(float frequencyHz = 190f, float durationSeconds = 0.3f) {
            int sampleRate = GetSampleRate();
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] data = new float[frames];

            for (int i = 0; i < frames; i++) {
                float t = i / (float)sampleRate;
                // The pitch falls away as it decays, which is what makes a thud read as weight.
                float sweep = frequencyHz * Mathf.Exp(-6f * t);
                data[i] = Mathf.Sin(2f * Mathf.PI * sweep * t) * Mathf.Exp(-11f * t);
            }

            ApplyFadeIn(data, sampleRate, 0.003f);
            NormalisePeak(data, 0.7f);
            return ToClip("KmaxThud", data, 1, sampleRate);
        }

        /// <summary>
        /// A seamless idle loop for a modern turbocharged four, which is what an S90 has: a
        /// two-litre inline-four in every variant it was sold with.
        ///
        /// Built from the firing rate rather than from an engine note. A four-stroke four fires
        /// twice per revolution, so an idle around 780 rpm puts a combustion pulse every 26 Hz, and
        /// that pulse train is what the ear identifies as a particular engine.
        ///
        /// What makes it read as *this* engine rather than a generic one is the balance. A large
        /// saloon's exhaust is muffled and low: the note is mostly second order with a soft attack,
        /// the harmonics fall away as 1/n squared rather than 1/n, and there is very little rasp.
        /// Underneath sits a component at half the firing rate - once per crank revolution - which
        /// is where the low beat of a four at idle comes from, and a small per-cylinder variation,
        /// because four cylinders are never quite identical and a perfectly even pulse train sounds
        /// synthetic within a second of hearing it.
        ///
        /// The noise is generated once per firing cycle and replayed for every cycle, which is both
        /// truer - each combustion event really is much like the last - and what makes the clip
        /// loop without a click. The firing rate is snapped so the loop holds a whole multiple of
        /// four cycles, which is what keeps the half-rate component and the per-cylinder table
        /// periodic across the loop point as well.
        /// </summary>
        public static AudioClip CreateEngineIdle(float firingHz = 26f, float loopSeconds = 2f) {
            int sampleRate = GetSampleRate();
            float snapped = SnapToLoop(firingHz, loopSeconds, 4);
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * loopSeconds));
            float[] data = new float[frames * 2];

            int cycleFrames = Mathf.Max(1, Mathf.RoundToInt(sampleRate / snapped));
            float[] cycleNoise = new float[cycleFrames];
            System.Random random = new System.Random(20260929);
            float filtered = 0f;
            for (int i = 0; i < cycleFrames; i++) {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                // One-pole low pass, tighter than a naturally aspirated engine's would be: a
                // silenced turbo four has almost no top end, and unfiltered noise reads as hiss.
                filtered += (white - filtered) * 0.06f;
                cycleNoise[i] = filtered;
            }

            // Four cylinders, never quite matched. Averages to 1, so the level is unaffected.
            float[] cylinderGain = new float[] { 1.06f, 0.95f, 1.02f, 0.97f };

            for (int i = 0; i < frames; i++) {
                float t = i / (float)sampleRate;
                float phase = t * snapped;
                float withinCycle = phase - Mathf.Floor(phase);
                int cylinder = Mathf.FloorToInt(phase) & 3;

                // Softer than a crack: this engine is behind a silencer and two metres of pipe.
                float pulse = Mathf.Exp(-withinCycle * 4.5f) * cylinderGain[cylinder];

                float body = 0f;
                for (int harmonic = 1; harmonic <= 5; harmonic++) {
                    body += Mathf.Sin(2f * Mathf.PI * snapped * harmonic * t) / (harmonic * harmonic);
                }

                // Once per crank revolution: the low beat under the firing note.
                float beat = Mathf.Sin(2f * Mathf.PI * snapped * 0.5f * t) * 0.5f;

                float rasp = cycleNoise[i % cycleFrames];
                float sample = (body * 0.46f + beat * 0.30f + rasp * 0.24f) * pulse;

                // A slow wobble, because a real idle never holds perfectly steady.
                sample *= 1f + 0.05f * Mathf.Sin(2f * Mathf.PI * SnapToLoop(1.5f, loopSeconds) * t);

                data[i * 2] = sample;
                data[i * 2 + 1] = sample * 0.94f;
            }

            NormalisePeak(data, 0.5f);
            SoftClip(data);
            return ToClip("KmaxEngineIdle", data, 2, sampleRate);
        }

        /// <summary>
        /// The starter turning over, the engine catching, and the blip of revs as it settles.
        ///
        /// Three overlapping stages rather than three clips, so the catch lands in the middle of
        /// the starter rather than after it - which is what a start actually sounds like.
        /// </summary>
        public static AudioClip CreateEngineStart(float durationSeconds = 2.2f) {
            int sampleRate = GetSampleRate();
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] data = new float[frames];

            System.Random random = new System.Random(19661014);
            float filtered = 0f;
            float crankEnd = durationSeconds * 0.45f;
            float catchAt = durationSeconds * 0.38f;

            for (int i = 0; i < frames; i++) {
                float t = i / (float)sampleRate;
                float sample = 0f;

                // Starter: a slow chug plus the gear whine that rides on it, both fading as the
                // engine takes over.
                if (t < crankEnd) {
                    float crankFade = 1f - Mathf.Clamp01(t / crankEnd);
                    float chug = Mathf.Sin(2f * Mathf.PI * 9.5f * t);
                    float chugGate = Mathf.Max(0f, chug);
                    float whine = Mathf.Sin(2f * Mathf.PI * (1450f - 220f * t) * t) * 0.18f;
                    sample += (chugGate * 0.5f + whine) * crankFade;
                }

                // The engine catching: firing rate flares above idle and settles back down to it.
                if (t > catchAt) {
                    float since = t - catchAt;
                    float flare = Mathf.Exp(-since * 2.2f);
                    float firing = 26f + 34f * flare;
                    float withinCycle = (t * firing) - Mathf.Floor(t * firing);
                    float pulse = Mathf.Exp(-withinCycle * 7f);

                    float body = 0f;
                    for (int harmonic = 1; harmonic <= 6; harmonic++) {
                        body += Mathf.Sin(2f * Mathf.PI * firing * harmonic * t) / harmonic;
                    }

                    float white = (float)(random.NextDouble() * 2.0 - 1.0);
                    filtered += (white - filtered) * 0.12f;

                    float rise = Mathf.Clamp01(since * 6f);
                    sample += (body * 0.22f + filtered * 0.55f) * pulse * rise;
                }

                data[i] = sample;
            }

            ApplyFadeIn(data, sampleRate, 0.01f);
            ApplyFadeOut(data, sampleRate, 0.12f);
            NormalisePeak(data, 0.8f);
            SoftClip(data);
            return ToClip("KmaxEngineStart", data, 1, sampleRate);
        }

        /// <summary>
        /// One heartbeat as a seamless loop: the "lub" of the valves between atria and ventricles closing,
        /// then the softer, higher "dub" of the outflow valves. Each thump is a falling sine with two
        /// harmonics on top, because a display's panel speakers roll off the fundamental entirely and a
        /// pure low sine would be inaudible.
        /// </summary>
        /// <param name="lubFraction">Where in the beat the first sound falls, from 0 to 1.</param>
        /// <param name="dubFraction">Where in the beat the second sound falls, from 0 to 1.</param>
        public static AudioClip CreateHeartbeat(float beatsPerMinute = 72f, float lubFraction = 0.10f, float dubFraction = 0.42f) {
            int sampleRate = GetSampleRate();
            float beatSeconds = 60f / Mathf.Max(30f, beatsPerMinute);
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * beatSeconds));
            float[] data = new float[frames];

            AddThump(data, sampleRate, beatSeconds * lubFraction, 62f, 1f, 0.05f);
            AddThump(data, sampleRate, beatSeconds * dubFraction, 80f, 0.6f, 0.04f);

            NormalisePeak(data, 0.8f);
            SoftClip(data);
            return ToClip("KmaxHeartbeat", data, 1, sampleRate);
        }

        /// <summary>A soft thump: a sine that starts higher and falls, with its second and third harmonics.</summary>
        private static void AddThump(float[] data, int sampleRate, float startSeconds, float frequencyHz, float gain, float decaySeconds) {
            int start = Mathf.RoundToInt(startSeconds * sampleRate);
            int length = Mathf.RoundToInt(decaySeconds * 6f * sampleRate);
            for (int i = 0; i < length && start + i < data.Length; i++) {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t / decaySeconds) * Mathf.Min(1f, t / 0.004f);
                float phase = 2f * Mathf.PI * frequencyHz * (t + 0.012f * (1f - Mathf.Exp(-t / 0.03f)));
                float tone = Mathf.Sin(phase) + 0.55f * Mathf.Sin(2f * phase) + 0.3f * Mathf.Sin(3f * phase);
                data[start + i] += tone * envelope * gain;
            }
        }

        /// <summary>
        /// A short rising arpeggio of struck tones, a root, its major third, its fifth and its octave, each ringing on into
        /// the next: the sound of a puzzle completed. Built from the same inharmonic partials as the chime.
        /// </summary>
        public static AudioClip CreateFanfare(float rootHz = 523.25f, float durationSeconds = 2f) {
            float[] ratios = new float[] { 1f, 1.25f, 1.5f, 2f };
            float[] starts = new float[] { 0f, 0.13f, 0.26f, 0.42f };
            float[] partialRatios = new float[] { 1f, 2.01f, 2.99f, 4.18f };
            float[] partialGains = new float[] { 1f, 0.42f, 0.24f, 0.12f };
            float[] partialDecays = new float[] { 1f, 1.5f, 2.1f, 3f };

            int sampleRate = GetSampleRate();
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] data = new float[frames];

            for (int n = 0; n < ratios.Length; n++) {
                int first = Mathf.RoundToInt(starts[n] * sampleRate);
                for (int p = 0; p < partialRatios.Length; p++) {
                    float frequency = rootHz * ratios[n] * partialRatios[p];
                    if (frequency >= sampleRate * 0.45f) {
                        continue;
                    }

                    float angularStep = 2f * Mathf.PI * frequency / sampleRate;
                    float decayRate = partialDecays[p] * 3.4f / Mathf.Max(durationSeconds, 0.001f);
                    for (int i = 0; first + i < frames; i++) {
                        float t = i / (float)sampleRate;
                        float attack = Mathf.Min(1f, t / 0.004f);
                        data[first + i] += Mathf.Sin(angularStep * i) * partialGains[p] * attack * Mathf.Exp(-decayRate * t);
                    }
                }
            }

            ApplyFadeOut(data, sampleRate, 0.12f);
            NormalisePeak(data, 0.8f);
            return ToClip("KmaxFanfare", data, 1, sampleRate);
        }

        /// <summary>
        /// One cycle of a soft struck tone followed by quiet, as a seamless loop: the sound the ear exhibit shows
        /// travelling in. The second and third harmonics sit on top, because a display's panel speakers roll off low
        /// pitches, and the tone has died away long before the cycle ends, so the loop point is silent.
        /// </summary>
        /// <param name="ringSeconds">How long the tone takes to die away.</param>
        public static AudioClip CreateTonePulse(float frequencyHz = DefaultToneHz, float cycleSeconds = 4f, float ringSeconds = 2.4f) {
            int sampleRate = GetSampleRate();
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * cycleSeconds));
            float[] data = new float[frames];
            float decay = Mathf.Max(0.05f, ringSeconds) / 5f;
            int toneFrames = Mathf.Min(frames, Mathf.RoundToInt(sampleRate * ringSeconds * 1.6f));
            float angularStep = 2f * Mathf.PI * frequencyHz / sampleRate;
            for (int i = 0; i < toneFrames; i++) {
                float t = i / (float)sampleRate;
                float attack = Mathf.Min(1f, t / 0.02f);
                float fundamental = Mathf.Sin(angularStep * i) * Mathf.Exp(-t / decay);
                float second = 0.32f * Mathf.Sin(2f * angularStep * i) * Mathf.Exp(-t / (decay * 0.6f));
                float third = 0.12f * Mathf.Sin(3f * angularStep * i) * Mathf.Exp(-t / (decay * 0.4f));
                data[i] = (fundamental + second + third) * attack;
            }

            NormalisePeak(data, 0.75f);
            return ToClip("KmaxTonePulse", data, 1, sampleRate);
        }

        /// <summary>
        /// One breath as a seamless loop: air drawn in, a short pause, air let out, and a rest. Noise through a band filter,
        /// under an envelope that is brighter at the height of each flow. It starts and ends in silence, so it wraps without a
        /// click, and the noise is generated from a fixed seed, so every breath sounds the same on every run.
        /// </summary>
        /// <param name="cycleSeconds">Length of the breath. The timing matches <c>BreathingBehaviour</c>.</param>
        public static AudioClip CreateBreath(float cycleSeconds = 5f) {
            int sampleRate = GetSampleRate();
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * cycleSeconds));
            float[] data = new float[frames];
            uint seed = 1234567u;
            float fast = 0f;
            float slow = 0f;
            float slowStep = 1f - Mathf.Exp(-2f * Mathf.PI * 300f / sampleRate);
            for (int i = 0; i < frames; i++) {
                float envelope = BreathEnvelope(i / (float)frames);
                seed = seed * 1664525u + 1013904223u;
                float noise = (seed >> 8) / 8388608f - 1f;
                float fastStep = 1f - Mathf.Exp(-2f * Mathf.PI * (500f + 1800f * envelope) / sampleRate);
                fast += fastStep * (noise - fast);
                slow += slowStep * (fast - slow);
                data[i] = (fast - slow) * envelope;
            }

            NormalisePeak(data, 0.6f);
            return ToClip("KmaxBreath", data, 1, sampleRate);
        }

        /// <summary>How strong the flow of air is at this point of the breath: in over the first two fifths, out over the next half.</summary>
        private static float BreathEnvelope(float phase) {
            if (phase < 0.40f) {
                return 0.75f * Mathf.Pow(Mathf.Sin(Mathf.PI * phase / 0.40f), 1.6f);
            }

            if (phase >= 0.46f && phase < 0.94f) {
                return Mathf.Pow(Mathf.Sin(Mathf.PI * (phase - 0.46f) / 0.48f), 1.4f);
            }

            return 0f;
        }

        /// <summary>
        /// The tick of a pointer arriving on a button: a sine that glides a little upward from <paramref name="frequencyHz"/>
        /// and dies away exponentially, to about one per cent by its end. Fifteen milliseconds, so it can fire as often as a
        /// pointer sweeps across buttons without becoming a noise.
        /// </summary>
        public static AudioClip CreateUiHoverTick(float frequencyHz = 1200f, float durationSeconds = 0.015f) {
            int sampleRate = GetSampleRate();
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] data = new float[frames];
            float decayRate = Mathf.Log(100f) / Mathf.Max(durationSeconds, 0.001f);
            float phase = 0f;
            for (int i = 0; i < frames; i++) {
                float progress = i / (float)frames;
                phase += 2f * Mathf.PI * frequencyHz * (1f + 0.12f * progress) / sampleRate;
                data[i] = Mathf.Sin(phase) * Mathf.Exp(-decayRate * i / sampleRate);
            }

            ApplyFadeIn(data, sampleRate, 0.001f);
            NormalisePeak(data, 0.5f);
            return ToClip("KmaxUiTick", data, 1, sampleRate);
        }

        /// <summary>
        /// The sound of a press: two tones a fifth apart that both fall an octave, <paramref name="startHz"/> to
        /// <paramref name="endHz"/> for the lower one, in thirty-five milliseconds. The caller varies its pitch a little each
        /// time it plays, so a run of presses does not sound mechanical.
        /// </summary>
        public static AudioClip CreateUiClick(float startHz = 880f, float endHz = 440f, float durationSeconds = 0.035f) {
            int sampleRate = GetSampleRate();
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] data = new float[frames];
            float decayRate = Mathf.Log(20f) / Mathf.Max(durationSeconds, 0.001f);
            float lowerPhase = 0f;
            float upperPhase = 0f;
            for (int i = 0; i < frames; i++) {
                float progress = i / (float)frames;
                float lowerHz = startHz * Mathf.Pow(endHz / startHz, progress);
                lowerPhase += 2f * Mathf.PI * lowerHz / sampleRate;
                upperPhase += 2f * Mathf.PI * lowerHz * 1.5f / sampleRate;
                data[i] = (Mathf.Sin(lowerPhase) + 0.45f * Mathf.Sin(upperPhase)) * Mathf.Exp(-decayRate * i / sampleRate);
            }

            ApplyFadeIn(data, sampleRate, 0.001f);
            NormalisePeak(data, 0.6f);
            return ToClip("KmaxUiClick", data, 1, sampleRate);
        }

        /// <summary>A smooth frequency-modulated sweep that rises, for a part moving out or opening: 280 Hz to 720 Hz in 120 milliseconds.</summary>
        public static AudioClip CreateExpandSweep(float startHz = 280f, float endHz = 720f, float durationSeconds = 0.12f) {
            return CreateFmSweep("KmaxExpand", startHz, endHz, durationSeconds);
        }

        /// <summary>The same sweep falling, for a part moving back or the view being reset: 720 Hz to 280 Hz in 120 milliseconds.</summary>
        public static AudioClip CreateCollapseSweep(float startHz = 720f, float endHz = 280f, float durationSeconds = 0.12f) {
            return CreateFmSweep("KmaxCollapse", startHz, endHz, durationSeconds);
        }

        /// <summary>
        /// A sine whose pitch glides exponentially from one frequency to the other, bent by a modulator an octave below it. The
        /// modulation is strongest at the start and fades, so the sweep begins bright and ends as a pure tone, and the whole thing
        /// swells in and out under a sine envelope so it has no edge at either end.
        /// </summary>
        private static AudioClip CreateFmSweep(string name, float startHz, float endHz, float durationSeconds) {
            int sampleRate = GetSampleRate();
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] data = new float[frames];
            float carrierPhase = 0f;
            float modulatorPhase = 0f;
            for (int i = 0; i < frames; i++) {
                float progress = i / (float)frames;
                float frequency = startHz * Mathf.Pow(endHz / startHz, progress);
                carrierPhase += 2f * Mathf.PI * frequency / sampleRate;
                modulatorPhase += 2f * Mathf.PI * frequency * 0.5f / sampleRate;
                float index = 0.2f + 1.6f * (1f - progress);
                float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * progress), 1.3f);
                data[i] = Mathf.Sin(carrierPhase + index * Mathf.Sin(modulatorPhase)) * envelope;
            }

            NormalisePeak(data, 0.55f);
            return ToClip(name, data, 1, sampleRate);
        }

        /// <summary>
        /// A soft bell for a hotspot coming into focus: E5 and, a moment later, B5 a fifth above it, each a fundamental with two
        /// quiet overtones that die away at their own rates.
        /// </summary>
        public static AudioClip CreateHotspotChime(float durationSeconds = 0.9f) {
            float[] roots = new float[] { 659.25f, 987.77f };
            float[] rootGains = new float[] { 1f, 0.7f };
            float[] starts = new float[] { 0f, 0.045f };
            float[] ratios = new float[] { 1f, 2.01f, 3f };
            float[] gains = new float[] { 1f, 0.28f, 0.08f };
            float[] decays = new float[] { 1f, 1.6f, 2.4f };

            int sampleRate = GetSampleRate();
            int frames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] data = new float[frames];
            for (int r = 0; r < roots.Length; r++) {
                int first = Mathf.RoundToInt(starts[r] * sampleRate);
                for (int p = 0; p < ratios.Length; p++) {
                    float angularStep = 2f * Mathf.PI * roots[r] * ratios[p] / sampleRate;
                    float decayRate = decays[p] * 4.5f / Mathf.Max(durationSeconds, 0.001f);
                    for (int i = 0; first + i < frames; i++) {
                        float t = i / (float)sampleRate;
                        float attack = Mathf.Min(1f, t / 0.005f);
                        data[first + i] += Mathf.Sin(angularStep * i) * rootGains[r] * gains[p] * attack * Mathf.Exp(-decayRate * t);
                    }
                }
            }

            ApplyFadeOut(data, sampleRate, 0.05f);
            NormalisePeak(data, 0.7f);
            return ToClip("KmaxHotspot", data, 1, sampleRate);
        }

        /// <summary>
        /// A soothing bed of music that loops without a seam: a low drone on A and its fifth, a pad that moves between four chords
        /// of the A minor pentatonic scale (A, C, D, E, G), and a sparse, soft melody on the same scale that echoes. The tempo sits
        /// between 60 and 75 beats a minute, a chord lasts two bars and the loop is eight.
        ///
        /// <para>Everything is periodic over the loop: every pitch and slow tremolo is snapped to a whole number of cycles, the
        /// chords cross-fade under windows that sum to one, and a note or echo that runs past the end wraps round to the start.
        /// Each oscillator is a rotation updated by multiplication, which costs a few operations a sample, so the whole bed is
        /// made in a fraction of a second.</para>
        /// </summary>
        /// <param name="beatsPerMinute">Tempo. The loop is 32 beats long.</param>
        /// <param name="rootHz">The drone's fundamental, A2 by default.</param>
        public static AudioClip CreateAmbientMusic(float beatsPerMinute = 66f, float rootHz = 110f) {
            const int Bars = 8;
            const int ChordCount = 4;
            const int NoteSlots = 16;

            // Slow, low music has nothing above a few kilohertz, so it is made at a low rate and the audio system raises it on playback.
            int sampleRate = Mathf.Min(GetSampleRate(), MusicSampleRate);
            float beatSeconds = 60f / Mathf.Clamp(beatsPerMinute, 40f, 120f);
            int frames = Mathf.Max(ChordCount, Mathf.RoundToInt(sampleRate * beatSeconds * 4f * Bars));
            frames -= frames % ChordCount;
            float loopSeconds = frames / (float)sampleRate;
            float[] data = new float[frames * 2];

            float[][] chords = new float[][] {
                new float[] { 110f, 164.81f, 220f, 261.63f, 329.63f },
                new float[] { 146.83f, 220f, 293.66f, 329.63f, 392f },
                new float[] { 130.81f, 196f, 261.63f, 293.66f, 329.63f },
                new float[] { 196f, 293.66f, 329.63f, 440f, 587.33f }
            };
            float[] melody = new float[] {
                659.25f, 0f, 783.99f, 587.33f, 0f, 523.25f, 0f, 440f,
                587.33f, 0f, 659.25f, 0f, 783.99f, 659.25f, 0f, 523.25f
            };

            // The drone: the root, its fifth and its octave, each breathing at its own slow rate.
            float[] droneRatios = new float[] { 1f, 1.5f, 2f };
            float[] droneGains = new float[] { 0.30f, 0.26f, 0.24f };
            float[] dronePans = new float[] { -0.15f, 0.3f, -0.35f };
            for (int v = 0; v < droneRatios.Length; v++) {
                float frequency = SnapToLoop(rootHz * droneRatios[v], loopSeconds);
                float tremoloHz = SnapToLoop(0.04f + v * 0.023f, loopSeconds);
                AddVoice(data, frames, sampleRate, frequency, tremoloHz, v * 1.3f, droneGains[v], dronePans[v], 0, frames, null);
            }

            // The pad: each chord fades in over two bars and out over the next two, under a Hann window, and two neighbours always
            // sum to one, so the chords melt into each other and the last runs into the first.
            int chordFrames = frames / ChordCount;
            float[] window = new float[2 * chordFrames];
            for (int i = 0; i < window.Length; i++) {
                window[i] = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * i / window.Length);
            }

            for (int c = 0; c < ChordCount; c++) {
                int start = c * chordFrames - chordFrames;
                for (int n = 0; n < chords[c].Length; n++) {
                    float frequency = SnapToLoop(chords[c][n] * CentsToRatio((n % 2 == 0 ? 1f : -1f) * 3f), loopSeconds);
                    float tremoloHz = SnapToLoop(0.05f + n * 0.019f, loopSeconds);
                    float pan = Mathf.Lerp(-0.6f, 0.6f, n / (float)(chords[c].Length - 1));
                    AddVoice(data, frames, sampleRate, frequency, tremoloHz, c * 0.7f + n * 0.4f, 0.13f, pan, start, window.Length, window);
                }
            }

            // The melody: a soft note every two beats, each followed by three fainter echoes three quarters of a beat apart.
            for (int slot = 0; slot < NoteSlots; slot++) {
                if (melody[slot] <= 0f) {
                    continue;
                }

                float noteSeconds = slot * 2f * beatSeconds;
                float pan = slot % 2 == 0 ? -0.4f : 0.4f;
                for (int echo = 0; echo < 4; echo++) {
                    float gain = 0.22f * Mathf.Pow(0.42f, echo);
                    AddPluck(data, frames, sampleRate, noteSeconds + echo * 0.75f * beatSeconds, melody[slot], gain, echo % 2 == 0 ? pan : -pan);
                }
            }

            NormalisePeak(data, 0.62f);
            SoftClip(data);
            return ToClip("KmaxAmbientMusic", data, 2, sampleRate);
        }

        /// <summary>
        /// Adds a sine voice with a slow tremolo to a stereo buffer, over a stretch of the loop that may begin before its start
        /// and wrap round its end. A <paramref name="window"/> of the stretch's length shapes the voice, so it fades in and out
        /// across it; null runs it at full level.
        /// </summary>
        private static void AddVoice(float[] stereo, int frames, int sampleRate, float frequencyHz, float tremoloHz, float tremoloPhase,
            float gain, float pan, int startFrame, int lengthFrames, float[] window) {
            float leftGain = gain * Mathf.Sqrt(Mathf.Clamp01(0.5f - pan * 0.5f));
            float rightGain = gain * Mathf.Sqrt(Mathf.Clamp01(0.5f + pan * 0.5f));
            Oscillator tone = new Oscillator(frequencyHz, sampleRate, 0.0);
            Oscillator tremolo = new Oscillator(tremoloHz, sampleRate, tremoloPhase);
            int length = Mathf.Min(lengthFrames, frames);
            for (int i = 0; i < length; i++) {
                int index = ((startFrame + i) % frames + frames) % frames;
                float shape = window != null ? window[i] : 1f;
                float sample = tone.Next() * (0.72f + 0.28f * tremolo.Next()) * shape;
                stereo[index * 2] += sample * leftGain;
                stereo[index * 2 + 1] += sample * rightGain;
            }
        }

        /// <summary>
        /// Adds one soft struck note, a fundamental and a quiet octave under an exponential decay, to a stereo buffer. The note
        /// runs on past the end of the loop and wraps round to its start, so the loop has no seam.
        /// </summary>
        private static void AddPluck(float[] stereo, int frames, int sampleRate, float startSeconds, float frequencyHz, float gain, float pan) {
            float leftGain = gain * Mathf.Sqrt(Mathf.Clamp01(0.5f - pan * 0.5f));
            float rightGain = gain * Mathf.Sqrt(Mathf.Clamp01(0.5f + pan * 0.5f));
            const float DecaySeconds = 0.9f;
            int noteFrames = Mathf.Min(frames, Mathf.RoundToInt(sampleRate * DecaySeconds * 4f));
            int first = Mathf.RoundToInt(startSeconds * sampleRate) % frames;
            float decayStep = Mathf.Exp(-1f / (sampleRate * DecaySeconds));
            Oscillator fundamental = new Oscillator(frequencyHz, sampleRate, 0.0);
            Oscillator octave = new Oscillator(frequencyHz * 2f, sampleRate, 0.0);
            float envelope = 1f;
            int attackFrames = Mathf.Max(1, Mathf.RoundToInt(sampleRate * 0.015f));
            for (int i = 0; i < noteFrames; i++) {
                int index = (first + i) % frames;
                float attack = Mathf.Min(1f, (i + 1) / (float)attackFrames);
                float sample = (fundamental.Next() + 0.22f * octave.Next() * envelope) * envelope * attack;
                stereo[index * 2] += sample * leftGain;
                stereo[index * 2 + 1] += sample * rightGain;
                envelope *= decayStep;
            }
        }

        /// <summary>
        /// A sine oscillator made by rotating a point round a circle, a handful of multiplications a sample, in double precision so
        /// it holds its pitch over a loop of a million samples. Its radius is restored now and then to stop rounding from walking it.
        /// </summary>
        private struct Oscillator {
            private const int RenormaliseEvery = 4096;

            private readonly double _stepCos;
            private readonly double _stepSin;
            private double _cos;
            private double _sin;
            private int _count;

            public Oscillator(float frequencyHz, int sampleRate, double startPhase) {
                double step = 2.0 * System.Math.PI * frequencyHz / sampleRate;
                _stepCos = System.Math.Cos(step);
                _stepSin = System.Math.Sin(step);
                _cos = System.Math.Cos(startPhase);
                _sin = System.Math.Sin(startPhase);
                _count = 0;
            }

            /// <summary>The current sample, then on one step.</summary>
            public float Next() {
                float value = (float)_sin;
                double nextCos = _cos * _stepCos - _sin * _stepSin;
                double nextSin = _sin * _stepCos + _cos * _stepSin;
                _cos = nextCos;
                _sin = nextSin;
                _count++;
                if (_count >= RenormaliseEvery) {
                    double radius = System.Math.Sqrt(_cos * _cos + _sin * _sin);
                    _cos /= radius;
                    _sin /= radius;
                    _count = 0;
                }

                return value;
            }
        }

        private static int GetSampleRate() {
            int rate = AudioSettings.outputSampleRate;
            return rate > 0 ? rate : 48000;
        }

        /// <summary>
        /// Rounds a frequency to the nearest whole number of cycles across the loop, so the clip
        /// wraps without a discontinuity.
        /// </summary>
        private static float SnapToLoop(float frequency, float loopSeconds) {
            if (loopSeconds <= 0f) {
                return frequency;
            }

            float cycles = Mathf.Max(1f, Mathf.Round(frequency * loopSeconds));
            return cycles / loopSeconds;
        }

        /// <summary>
        /// As above, but rounded to a whole multiple of <paramref name="multiple"/> cycles.
        ///
        /// Anything in the waveform that repeats over several firing cycles - the half-rate beat
        /// under a four's idle, the per-cylinder variation - is only periodic across the loop point
        /// if the loop holds a whole number of those groups, not just a whole number of cycles.
        /// </summary>
        private static float SnapToLoop(float frequency, float loopSeconds, int multiple) {
            if (loopSeconds <= 0f || multiple <= 1) {
                return SnapToLoop(frequency, loopSeconds);
            }

            float groups = Mathf.Max(1f, Mathf.Round(frequency * loopSeconds / multiple));
            return groups * multiple / loopSeconds;
        }

        private static float CentsToRatio(float cents) {
            return Mathf.Pow(2f, cents / 1200f);
        }

        private static void NormalisePeak(float[] data, float target) {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++) {
                float magnitude = Mathf.Abs(data[i]);
                if (magnitude > peak) {
                    peak = magnitude;
                }
            }

            if (peak <= Mathf.Epsilon) {
                return;
            }

            float scale = target / peak;
            for (int i = 0; i < data.Length; i++) {
                data[i] *= scale;
            }
        }

        /// <summary>
        /// Rounds off anything that still pokes past full scale, rather than letting it clip hard.
        /// </summary>
        private static void SoftClip(float[] data) {
            for (int i = 0; i < data.Length; i++) {
                data[i] = (float)System.Math.Tanh(data[i]);
            }
        }

        private static void ApplyFadeIn(float[] data, int sampleRate, float seconds) {
            int fade = Mathf.Min(data.Length, Mathf.RoundToInt(sampleRate * seconds));
            for (int i = 0; i < fade; i++) {
                data[i] *= i / (float)fade;
            }
        }

        private static void ApplyFadeOut(float[] data, int sampleRate, float seconds) {
            int fade = Mathf.Min(data.Length, Mathf.RoundToInt(sampleRate * seconds));
            for (int i = 0; i < fade; i++) {
                data[data.Length - 1 - i] *= i / (float)fade;
            }
        }

        private static AudioClip ToClip(string name, float[] data, int channels, int sampleRate) {
            AudioClip clip = AudioClip.Create(name, data.Length / channels, channels, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
