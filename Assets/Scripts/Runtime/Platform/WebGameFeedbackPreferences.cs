using System;
using UnityEngine;

namespace XanaduYonder.Runtime
{
    // A native WebView can change preferences while Unity is suspended. Native
    // keeps a small durable journal; AppController alone merges it into SaveData.
    internal static class WebGameFeedbackPreferences
    {
        private const string BridgeClass = "com.xanaduyonder.webgames.WebGameFeedbackPreferences";

        [Serializable]
        internal sealed class Pending
        {
            public int schemaVersion;
            public string revision;
            public bool soundEnabled;
            public bool hapticsEnabled;
        }

        internal static bool TryRead(out Pending pending)
        {
            pending = null;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            try
            {
                string raw;
#if UNITY_IOS
                raw = IOSWebGameBridge.ReadWebFeedbackPending();
#else
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass(BridgeClass))
                    raw = bridge.CallStatic<string>("readPendingFeedback", activity);
#endif
                // Native readers validate exact JSON boolean types and schema.
                if (string.IsNullOrEmpty(raw) || raw.Length > 1024) return false;
                var value = JsonUtility.FromJson<Pending>(raw);
                if (value == null || value.schemaVersion != 1 || !Guid.TryParse(value.revision, out _)) return false;
                pending = value;
                return true;
            }
            catch (Exception error) { Debug.LogWarning("Web game preferences could not be read: " + error.Message); }
#endif
            return false;
        }

        internal static bool Acknowledge(string revision)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            try
            {
#if UNITY_IOS
                return IOSWebGameBridge.AcknowledgeWebFeedbackPending(revision);
#else
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass(BridgeClass))
                    return bridge.CallStatic<bool>("acknowledgePendingFeedback", activity, revision);
#endif
            }
            catch (Exception error) { Debug.LogWarning("Web game preferences could not be acknowledged: " + error.Message); }
#endif
            return false;
        }
    }
}
