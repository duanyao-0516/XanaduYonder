using UnityEngine;
using System.Collections.Generic;

namespace XanaduYonder.Runtime
{
    internal sealed class GameFeedback : MonoBehaviour
    {
        private AudioSource audioSource;
        private AudioClip slide;
        private AudioClip merge;
        private AudioClip charge;
        private AudioClip jump;
        private AudioClip miss;
        private AudioSource chargeSource;
        private AudioClip chargeIntro;
        private AudioClip landing;
        private AudioClip perfect;
        private readonly List<AudioClip> generated = new List<AudioClip>();
        private bool chargingSound;

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = .2f;
            slide = Tone("Slide", 440, .045f);
            merge = Tone("Merge", 660, .085f);
            charge = Resources.Load<AudioClip>("Jump/Audio/scale_loop") ?? Tone("Charge", 210, .3f);
            chargeIntro = Resources.Load<AudioClip>("Jump/Audio/scale_intro");
            jump = Resources.Load<AudioClip>("Jump/Audio/start") ?? Tone("Jump", 520, .12f);
            miss = Resources.Load<AudioClip>("Jump/Audio/fall") ?? Tone("Miss", 130, .24f);
            landing = Resources.Load<AudioClip>("Jump/Audio/pop") ?? slide;
            perfect = Resources.Load<AudioClip>("Jump/Audio/success") ?? merge;
            chargeSource = gameObject.AddComponent<AudioSource>();
            chargeSource.playOnAwake = false;
            chargeSource.loop = true;
            chargeSource.volume = .09f;
            chargeSource.clip = charge;
        }

        public void Play(bool merged, bool soundEnabled, bool hapticsEnabled)
        {
            if (soundEnabled) audioSource.PlayOneShot(merged ? merge : slide);
            if (merged && hapticsEnabled) Haptic();
        }

        public void BeginCharge(bool soundEnabled)
        {
            if (!soundEnabled) return;
            chargingSound = true;
            chargeSource.pitch = 1;
            chargeSource.clip = chargeIntro != null ? chargeIntro : charge;
            chargeSource.loop = chargeIntro == null;
            chargeSource.Play();
        }

        public void Charge(float amount)
        {
            if (!chargingSound) return;
            if (!chargeSource.isPlaying) { chargeSource.clip = charge; chargeSource.loop = true; chargeSource.Play(); }
            chargeSource.pitch = 1 + Mathf.Clamp01(amount) * .4f;
        }
        public void EndCharge() { chargingSound = false; if (chargeSource != null) chargeSource.Stop(); }
        public void Jump(bool soundEnabled) { EndCharge(); if (soundEnabled) audioSource.PlayOneShot(jump); }
        public void Land(bool perfect, bool soundEnabled, bool hapticsEnabled)
        {
            if (soundEnabled) audioSource.PlayOneShot(perfect ? this.perfect : landing);
            if (hapticsEnabled) Haptic();
        }
        public void Miss(bool soundEnabled, bool hapticsEnabled)
        {
            if (soundEnabled) audioSource.PlayOneShot(miss);
            if (hapticsEnabled) Haptic();
        }

        public void Pulse(bool enabled) { if (enabled) Haptic(); }

        private static void Haptic()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                    {
                        using (var currentPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using (var currentActivity = currentPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                        using (var window = currentActivity.Call<AndroidJavaObject>("getWindow"))
                        using (var view = window.Call<AndroidJavaObject>("getDecorView"))
                            view.Call<bool>("performHapticFeedback", 1);
                    }));
                }
            }
#endif
        }

        public void Stop() { if (audioSource != null) audioSource.Stop(); EndCharge(); }

        private AudioClip Tone(string name, float frequency, float duration)
        {
            const int sampleRate = 22050;
            var samples = new float[Mathf.CeilToInt(duration * sampleRate)];
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)sampleRate;
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * Mathf.Sin(Mathf.PI * i / samples.Length) * .5f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            generated.Add(clip);
            return clip;
        }

        private void OnDestroy()
        {
            foreach (var clip in generated) if (clip != null) Destroy(clip);
        }
    }
}
