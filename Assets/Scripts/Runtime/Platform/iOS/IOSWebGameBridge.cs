#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace XanaduYonder.Runtime
{
    // Owns the iOS host boundary only. Gameplay and serialization stay in each web game.
    internal static class IOSWebGameBridge
    {
        [DllImport("__Internal", EntryPoint = "XanaduWebGame_Open")]
        private static extern int OpenNative(string game, string root, int soundEnabled);
        [DllImport("__Internal", EntryPoint = "XanaduBombIt_Open")]
        private static extern int OpenBombItNative(string root, int soundEnabled, int hapticsEnabled);
        [DllImport("__Internal", EntryPoint = "XanaduLevelDevil_Open")]
        private static extern int OpenLevelDevilNative(string root, int soundEnabled, int hapticsEnabled);
        [DllImport("__Internal", EntryPoint = "XanaduWebFeedback_ReadPending")]
        private static extern IntPtr ReadWebFeedbackPendingNative();
        [DllImport("__Internal", EntryPoint = "XanaduWebFeedback_Acknowledge")]
        private static extern int AcknowledgeWebFeedbackPendingNative(string revision);
        [DllImport("__Internal", EntryPoint = "XanaduBombIt_ReadPendingFeedback")]
        private static extern IntPtr ReadBombItPendingFeedbackNative();
        [DllImport("__Internal", EntryPoint = "XanaduBombIt_AcknowledgePendingFeedback")]
        private static extern int AcknowledgeBombItPendingFeedbackNative(string revision);
        [DllImport("__Internal", EntryPoint = "XanaduWebGame_IsRunning")]
        private static extern int IsRunningNative();
        [DllImport("__Internal", EntryPoint = "XanaduWebGame_LastError")]
        private static extern IntPtr LastErrorNative();
        [DllImport("__Internal", EntryPoint = "XanaduWebGame_GetRequestedOrientation")]
        private static extern ScreenOrientation GetRequestedOrientationNative();

        private static bool ownsOrientation;
        private static ScreenOrientation previousOrientation;
        private static bool previousPortrait, previousUpsideDown, previousLeft, previousRight;

        internal static bool Open(string game, bool soundEnabled)
        {
            return OpenCore(game, soundEnabled, false, false);
        }

        internal static bool OpenBombIt(bool soundEnabled, bool hapticsEnabled)
        {
            return OpenCore("bomb-it", soundEnabled, true, hapticsEnabled);
        }

        internal static bool OpenLevelDevil(bool soundEnabled, bool hapticsEnabled)
        {
            return OpenCore("level-devil", soundEnabled, true, hapticsEnabled);
        }

        private static bool OpenCore(string game, bool soundEnabled, bool feedbackGame, bool hapticsEnabled)
        {
            if (IsRunningNative() != 0) return false;
            // A prior return may still be waiting for Unity's next repaint. Save
            // its requested policy, including AutoRotation, rather than the old
            // screen direction that is still visible during that transition.
            previousOrientation = GetRequestedOrientationNative();
            previousPortrait = Screen.autorotateToPortrait;
            previousUpsideDown = Screen.autorotateToPortraitUpsideDown;
            previousLeft = Screen.autorotateToLandscapeLeft;
            previousRight = Screen.autorotateToLandscapeRight;
            ownsOrientation = true;
            if (game == "cut-the-rope")
            {
                Screen.autorotateToPortrait = Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
            else Screen.orientation = (game == "level-devil" || game == "bomb-it") ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
            try
            {
                var root = Path.Combine(Application.streamingAssetsPath, "web-games", game);
                int opened = feedbackGame ? (game == "bomb-it"
                    ? OpenBombItNative(root, soundEnabled ? 1 : 0, hapticsEnabled ? 1 : 0)
                    : OpenLevelDevilNative(root, soundEnabled ? 1 : 0, hapticsEnabled ? 1 : 0))
                    : OpenNative(game, root, soundEnabled ? 1 : 0);
                if (opened != 0) return true;
            }
            catch
            {
                RestoreOrientation();
                throw;
            }
            RestoreOrientation();
            return false;
        }

        internal static bool IsRunning()
        {
            if (IsRunningNative() != 0) return true;
            RestoreOrientation();
            return false;
        }

        internal static string GetLastError()
        {
            var value = LastErrorNative();
            return value == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(value);
        }

        internal static string ReadBombItPendingFeedback()
        {
            var value = ReadBombItPendingFeedbackNative();
            return value == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(value);
        }

        internal static bool AcknowledgeBombItPendingFeedback(string revision)
        {
            return AcknowledgeBombItPendingFeedbackNative(revision) != 0;
        }

        internal static string ReadWebFeedbackPending()
        {
            var value = ReadWebFeedbackPendingNative();
            return value == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(value);
        }

        internal static bool AcknowledgeWebFeedbackPending(string revision)
        {
            return AcknowledgeWebFeedbackPendingNative(revision) != 0;
        }

        private static void RestoreOrientation()
        {
            if (!ownsOrientation) return;
            ownsOrientation = false;
            Screen.autorotateToPortrait = previousPortrait;
            Screen.autorotateToPortraitUpsideDown = previousUpsideDown;
            Screen.autorotateToLandscapeLeft = previousLeft;
            Screen.autorotateToLandscapeRight = previousRight;
            Screen.orientation = previousOrientation;
        }
    }
}
#endif
