using System;
using UnityEngine;

namespace XanaduYonder.Runtime.Dino
{
    // Dino owns these sources; public sound settings remain owned by the shell.
    internal sealed class DinoAudio : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip jump;
        private AudioClip hit;
        private AudioClip score;
        private Func<bool> soundEnabled;
        private Func<bool> hapticsEnabled;

        public void Initialize(Func<bool> isSoundEnabled, Func<bool> isHapticsEnabled)
        {
            Stop();
            soundEnabled = isSoundEnabled;
            hapticsEnabled = isHapticsEnabled;
            if (source != null) return;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0;
            source.volume = .35f;
            jump = Resources.Load<AudioClip>("Dino/Audio/Jump");
            hit = Resources.Load<AudioClip>("Dino/Audio/Hit");
            score = Resources.Load<AudioClip>("Dino/Audio/Score");
            if (jump == null || hit == null || score == null)
                Debug.LogError("Dino audio resources are missing. Expected Dino/Audio/Jump, Hit and Score.");
        }

        public void PlayJump() { Play(jump); }
        public void PlayScore() { Play(score); }

        public void PlayHit()
        {
            Stop();
            Play(hit);
            if (isActiveAndEnabled && hapticsEnabled != null && hapticsEnabled()) Haptic();
        }

        private void Play(AudioClip clip)
        {
            if (isActiveAndEnabled && source != null && clip != null && soundEnabled != null && soundEnabled())
                source.PlayOneShot(clip);
        }

        public void Stop()
        {
            if (source != null) source.Stop();
        }

        private static void Haptic()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
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
#endif
        }

        private void OnDisable() { Stop(); }

        private void OnDestroy()
        {
            Stop();
            if (source != null) Destroy(source);
            source = null;
            // Resources clips are shared assets, not generated objects owned by this component.
            jump = hit = score = null;
            soundEnabled = hapticsEnabled = null;
        }
    }
}
