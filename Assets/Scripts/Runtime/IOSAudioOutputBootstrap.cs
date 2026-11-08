#if UNITY_IOS && !UNITY_EDITOR
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace XanaduYonder.Runtime
{
    // Owns only the initial output restart; Unity still owns later lifecycle changes.
    public sealed class IOSAudioOutputBootstrap : MonoBehaviour
    {
        private static bool initialized;
        private bool ownsStop;

        [DllImport("__Internal", EntryPoint = "XanaduAudio_IsApplicationActive")]
        private static extern int IsApplicationActive();

        // 1 = ready, 0 = no longer active, negative = native error already logged.
        [DllImport("__Internal", EntryPoint = "XanaduAudio_PrepareSession")]
        private static extern int PrepareSession();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            var owner = new GameObject("iOS Audio Startup");
            DontDestroyOnLoad(owner);
            owner.AddComponent<IOSAudioOutputBootstrap>();
        }

        private IEnumerator Start()
        {
            bool stopped;
            while (true)
            {
                // Awake can run before UIKit becomes active. Do not start output
                // that the engine has not started itself.
                while (IsApplicationActive() == 0 || !AudioSettings.Mobile.audioOutputStarted)
                    yield return null;

                int sessionResult = PrepareSession();
                if (sessionResult == 0)
                {
                    yield return null;
                    continue;
                }
                if (sessionResult < 0)
                {
                    Debug.LogError("[XanaduAudio] Startup session preparation failed (stage=" +
                        sessionResult + "); output was not stopped. See the native audio error.");
                    Destroy(gameObject);
                    yield break;
                }

                yield return null;
                if (IsApplicationActive() == 0 || !AudioSettings.Mobile.audioOutputStarted)
                    continue;

                AudioSettings.Mobile.StopAudioOutput();
                ownsStop = true;
                stopped = !AudioSettings.Mobile.audioOutputStarted;
                break;
            }

            yield return null;
            while (ownsStop)
            {
                // If backgrounded between the two calls, keep ownership until
                // the app is active again so our Stop always gets a matching Start.
                if (IsApplicationActive() == 0)
                {
                    yield return null;
                    continue;
                }

                // FMOD may have changed the session while resuming the app.
                int sessionResult = PrepareSession();
                if (sessionResult == 0)
                {
                    yield return null;
                    continue;
                }

                // Even if preparation fails, balance our Stop while active and
                // report the failure instead of leaving Unity output stopped.
                AudioSettings.Mobile.StartAudioOutput();
                ownsStop = false;
                bool started = AudioSettings.Mobile.audioOutputStarted;
                string result = "[XanaduAudio] Startup output restart: session=" +
                    sessionResult + ", stopped=" + stopped + ", started=" + started + ".";
                if (sessionResult < 0 || !stopped || !started) Debug.LogError(result);
                else Debug.Log(result);
            }

            Destroy(gameObject);
        }
    }
}
#endif
