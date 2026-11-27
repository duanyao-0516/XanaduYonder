using System;
using UnityEngine;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private readonly BackExitConfirmation backExitConfirmation = new BackExitConfirmation();
        private bool requestedLobbyExit;
        private Page backExitPage;
        private bool modalBackReturnsToLobby;

        private void SetModalBack(Action back)
        {
            dismissModal = back;
            modalBackReturnsToLobby = back == (Action)ShowLobby;
        }

        private void HandleBack()
        {
            requestedLobbyExit = false;
            try { HandleBackCore(); }
            finally
            {
                // Closing a dialog, pausing or navigating inside a game is not an exit press.
                if (!requestedLobbyExit) ResetBackExitConfirmation();
            }
        }

        private bool ConfirmSystemBackExit()
        {
            requestedLobbyExit = true;
            backExitPage = activePage;
            if (backExitConfirmation.TryConfirm((int)activePage, Time.realtimeSinceStartupAsDouble))
            {
                SetBackExitToast(false);
                return true;
            }
            SetBackExitToast(true);
            return false;
        }

        private void ResetBackExitConfirmation()
        {
            backExitConfirmation.Reset();
            SetBackExitToast(false);
        }

        private static void SetBackExitToast(bool visible)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var toast = new AndroidJavaClass("com.duanyao.xanaduyonder.SystemBackToast"))
                    toast.CallStatic("setVisible", activity, visible);
            }
            catch (Exception error) { Debug.LogWarning("System back toast: " + error.Message); }
#else
            if (visible) Debug.Log("再按一次返回大厅");
#endif
        }
    }
}
