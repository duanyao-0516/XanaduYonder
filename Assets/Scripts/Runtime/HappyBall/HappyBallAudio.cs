using System;
using UnityEngine;
using XanaduYonder.Core.HappyBall;

namespace XanaduYonder.Runtime.HappyBall
{
    /// <summary>Original short synthesized cues, owned and released by this page.</summary>
    internal sealed class HappyBallAudio : MonoBehaviour
    {
        private readonly AudioClip[] clips = new AudioClip[6];
        private readonly AudioSource[] voices = new AudioSource[4];
        private bool initialized;
        private int nextVoice;

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            for (var i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0;
                voices[i].volume = .27f;
                voices[i].priority = 90;
            }
            clips[(int)HappyBallEventKind.Bounce] = Make("HappyBall soft bounce", .1f, 510, 740, .02f);
            clips[(int)HappyBallEventKind.LayerPassed] = Make("HappyBall pass", .14f, 690, 1040, .015f);
            clips[(int)HappyBallEventKind.LayerBroken] = Make("HappyBall smash", .2f, 185, 88, .27f);
            clips[(int)HappyBallEventKind.Blocked] = Make("HappyBall wall tap", .07f, 230, 180, .07f);
            clips[(int)HappyBallEventKind.Armed] = Make("HappyBall armed", .24f, 780, 1420, 0);
            clips[(int)HappyBallEventKind.Died] = Make("HappyBall end", .32f, 370, 125, .045f);
        }

        public void Play(HappyBallEventKind kind, bool soundEnabled)
        {
            if (!soundEnabled) { Stop(); return; }
            if (!initialized) Initialize();
            var index = (int)kind;
            if (index < 0 || index >= clips.Length || clips[index] == null) return;
            if (kind == HappyBallEventKind.Died) Stop();
            var source = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            source.Stop();
            source.clip = clips[index];
            source.Play();
        }

        public void Stop() { foreach (var source in voices) if (source != null) source.Stop(); }

        private static AudioClip Make(string name, float duration, float from, float to, float noiseAmount)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            var phase = 0.0;
            uint random = 0x5d41407f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)(samples.Length - 1);
                phase += 2 * Math.PI * Mathf.Lerp(from, to, t) / rate;
                random = random * 1664525u + 1013904223u;
                var noise = ((random >> 8) / 16777216f * 2 - 1) * noiseAmount;
                var envelope = Math.Sin(Math.PI * Math.Min(1, t * 10)) * Math.Exp(-t * 4.5);
                // After the soft attack, sustain a decaying bell rather than a second attack.
                if (t > .05f) envelope = Math.Exp(-t * 4.5);
                envelope *= Math.Min(1, (1 - t) * 20);
                var tone = Math.Sin(phase) * .65 + Math.Sin(phase * 2) * .13 + Math.Sin(phase * 3) * .025;
                samples[i] = (float)((tone + noise) * envelope * .65);
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDisable() { Stop(); }
        private void OnDestroy()
        {
            Stop();
            foreach (var clip in clips) if (clip != null) Destroy(clip);
            foreach (var source in voices) if (source != null) Destroy(source);
        }
    }
}
