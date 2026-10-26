using UnityEngine;

namespace XanaduYonder.Runtime
{
    internal sealed class GameFeedback : MonoBehaviour
    {
        private AudioSource audioSource;
        private AudioClip slide;
        private AudioClip merge;

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.volume = .2f;
            slide = Tone("Slide", 440, .045f);
            merge = Tone("Merge", 660, .085f);
        }

        public void Play(bool merged, bool soundEnabled, bool hapticsEnabled)
        {
            if (soundEnabled) audioSource.PlayOneShot(merged ? merge : slide);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (merged && hapticsEnabled)
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

        public void Stop() { if (audioSource != null) audioSource.Stop(); }

        private static AudioClip Tone(string name, float frequency, float duration)
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
            return clip;
        }

        private void OnDestroy()
        {
            if (slide != null) Destroy(slide);
            if (merge != null) Destroy(merge);
        }
    }
}
