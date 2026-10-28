using System;
using System.Collections.Generic;
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace XanaduYonder.Runtime.GoldMiner
{
    internal sealed class GoldMinerCue
    {
        internal readonly string Id;
        internal readonly double[] Frequencies;
        internal readonly double Duration, NoteSeconds, Gain;
        internal readonly bool Glide, Noise;
        internal readonly int Priority;
        internal GoldMinerCue(string id, double[] frequencies, double duration, double noteSeconds, double gain, int priority, bool glide = false, bool noise = false)
        { Id = id; Frequencies = frequencies; Duration = duration; NoteSeconds = noteSeconds; Gain = gain; Priority = priority; Glide = glide; Noise = noise; }
    }

    // No Unity or gameplay RNG dependency: the production sample generator is tested directly.
    internal static class GoldMinerSoundSynthesis
    {
        internal const int SampleRate = 22050;
        private static readonly GoldMinerCue[] Cues =
        {
            new GoldMinerCue("hook_launch", new[] {440d,660d}, .08, 0, .45, 1, true),
            new GoldMinerCue("gold_collect", new[] {660d,880d,1175d}, .15, .05, .60, 2),
            new GoldMinerCue("diamond_collect", new[] {880d,1175d,1568d}, .18, .06, .60, 2),
            new GoldMinerCue("rock_collect", new[] {180d,100d}, .10, 0, .35, 2, true),
            new GoldMinerCue("explosion", new[] {90d,45d}, .20, 0, .60, 4, true, true),
            new GoldMinerCue("countdown", new[] {880d}, .06, .06, .40, 3),
            new GoldMinerCue("victory", new[] {523.25,659.25,783.99,1046.5}, .36, .09, .65, 5),
            new GoldMinerCue("failure", new[] {392d,293.66,196d}, .33, .11, .50, 5),
            new GoldMinerCue("purchase", new[] {660d,880d}, .12, .06, .45, 2),
            new GoldMinerCue("unavailable", new[] {120d}, .05, .05, .25, 1)
        };
        internal static GoldMinerCue GetCue(string id)
        {
            foreach (var cue in Cues) if (cue.Id == id) return cue;
            throw new ArgumentException("Unknown Gold Miner audio cue: " + id, nameof(id));
        }
        internal static float[] Generate(GoldMinerCue cue)
        {
            var samples = new float[(int)Math.Ceiling(cue.Duration * SampleRate)];
            uint noise = 2048; var start = 0;
            var noteCount = cue.Glide ? 1 : cue.Frequencies.Length;
            for (var note = 0; note < noteCount; note++)
            {
                var end = note == noteCount - 1 ? samples.Length : (int)Math.Round((note + 1) * cue.NoteSeconds * SampleRate, MidpointRounding.AwayFromZero);
                var count = end - start; var phase = 0d;
                for (var i = 0; i < count; i++)
                {
                    var value = Math.Sin(phase);
                    if (cue.Noise)
                    {
                        noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
                        value = .55 * value + .45 * (2 * (noise / 4294967295d) - 1);
                    }
                    samples[start + i] = i == count - 1 ? 0 : (float)(.5 * Math.Sin(Math.PI * i / count) * value);
                    var frequency = cue.Glide ? cue.Frequencies[0] + (cue.Frequencies[1] - cue.Frequencies[0]) * i / Math.Max(1d, count - 1) : cue.Frequencies[note];
                    phase += 2 * Math.PI * frequency / SampleRate;
                }
                start = end;
            }
            return samples;
        }
    }

    internal sealed class GoldMinerVoicePool
    {
        internal const int Capacity = 4;
        private readonly double[] ends = new double[Capacity];
        private readonly long[] orders = new long[Capacity];
        private readonly int[] priorities = new int[Capacity];
        private long nextOrder;
        internal int Acquire(int priority, double now, double duration)
        {
            var slot = -1;
            for (var i = 0; i < Capacity; i++) if (ends[i] <= now) { slot = i; break; }
            if (slot < 0)
            {
                for (var i = 0; i < Capacity; i++)
                    if (slot < 0 || priorities[i] < priorities[slot] || (priorities[i] == priorities[slot] && orders[i] < orders[slot])) slot = i;
                if (priority < priorities[slot]) return -1;
            }
            ends[slot] = now + duration; orders[slot] = ++nextOrder; priorities[slot] = priority;
            return slot;
        }
        internal void Clear() { Array.Clear(ends, 0, ends.Length); Array.Clear(priorities, 0, priorities.Length); Array.Clear(orders, 0, orders.Length); nextOrder = 0; }
    }

#if UNITY_5_3_OR_NEWER
    // Add to the Gold Miner page owner. Call StopAll before pause, sound-off and navigation.
    internal sealed class GoldMinerAudio : MonoBehaviour, IDisposable
    {
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly GoldMinerVoicePool pool = new GoldMinerVoicePool();
        private readonly AudioSource[] voices = new AudioSource[GoldMinerVoicePool.Capacity];
        private Func<bool> soundEnabled;
        private Func<bool> hapticsEnabled;
        private Action<string> haptic;
        private bool initialized, disposed;
        private double lastUnavailable = double.NegativeInfinity;
        public bool SoundEnabled { get { return !disposed && soundEnabled != null && soundEnabled(); } }

        public void Initialize(Func<bool> soundEnabled, Func<bool> hapticsEnabled = null, Action<string> haptic = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(GoldMinerAudio));
            if (soundEnabled == null) throw new ArgumentNullException(nameof(soundEnabled));
            this.soundEnabled = soundEnabled; this.hapticsEnabled = hapticsEnabled; this.haptic = haptic;
            if (initialized) return;
            for (var i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false; voices[i].loop = false;
                voices[i].spatialBlend = 0; voices[i].volume = .2f;
            }
            initialized = true;
        }
        public void Play(string cueId)
        {
            if (!initialized || disposed || !isActiveAndEnabled) return;
            var cue = GoldMinerSoundSynthesis.GetCue(cueId);
            var now = AudioSettings.dspTime;
            if (cueId == "unavailable")
            {
                if (now - lastUnavailable < .2) return;
                lastUnavailable = now;
            }
            // Haptics uses its independent shared setting; the injected shell selects supported events.
            if (haptic != null && hapticsEnabled != null && hapticsEnabled()) haptic(cueId);
            if (!SoundEnabled) { StopVoices(); return; }
            var voice = pool.Acquire(cue.Priority, now, cue.Duration);
            if (voice < 0) return;
            AudioClip clip;
            if (!clips.TryGetValue(cueId, out clip))
            {
                var samples = GoldMinerSoundSynthesis.Generate(cue);
                clip = AudioClip.Create("GoldMiner/" + cueId, samples.Length, 1, GoldMinerSoundSynthesis.SampleRate, false);
                clip.SetData(samples, 0); clips.Add(cueId, clip);
            }
            voices[voice].Stop(); voices[voice].PlayOneShot(clip, (float)cue.Gain);
        }
        private void StopVoices()
        {
            foreach (var source in voices) if (source != null) source.Stop();
            pool.Clear();
        }
        public void StopAll() { StopVoices(); lastUnavailable = double.NegativeInfinity; }
        private void OnDisable() { StopAll(); }
        private void OnApplicationFocus(bool focused) { if (!focused) StopAll(); }
        private void OnApplicationPause(bool paused) { if (paused) StopAll(); }
        public void Dispose()
        {
            if (disposed) return;
            StopAll(); disposed = true;
            foreach (var clip in clips.Values) if (clip != null) Destroy(clip);
            clips.Clear();
            for (var i = 0; i < voices.Length; i++) if (voices[i] != null) { Destroy(voices[i]); voices[i] = null; }
            soundEnabled = null; hapticsEnabled = null; haptic = null;
        }
        private void OnDestroy() { Dispose(); }
    }
#endif
}
