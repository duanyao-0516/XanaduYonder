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
                if (OpenNative(game, Path.Combine(Application.streamingAssetsPath, "web-games", game), soundEnabled ? 1 : 0) != 0)
                    return true;
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
