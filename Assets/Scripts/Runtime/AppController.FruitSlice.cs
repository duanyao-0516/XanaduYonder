using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.FruitSlice;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private FruitSliceLauncher fruitSliceLauncher;
        private bool IsFruitSliceRunning => fruitSliceLauncher != null && fruitSliceLauncher.IsBusy;

        private static void AddFruitSlicePreview(Transform parent)
        {
            var root = LobbyPreviews.RoundedLogoFrame(parent, "FruitSlice");
            var artwork = UiFactory.Rect(root, "FruitSlice original logo", 0, 0, 96, 96).gameObject.AddComponent<RawImage>();
            artwork.texture = Resources.Load<Texture2D>("Lobby/Icons/fruit-slice-logo");
            artwork.raycastTarget = false;
        }

        private void EnterFruitSlice()
        {
            if (activePage != Page.Lobby || paused || !focused || IsExternalGameRunning) return;
            LogFruitSliceStartup("unity_entry_accepted");
            CloseModal();
            Save();
            LogFruitSliceStartup("unity_save_finished");
            feedback?.Stop();
            if (fruitSliceLauncher == null)
            {
                fruitSliceLauncher = gameObject.AddComponent<FruitSliceLauncher>();
                fruitSliceLauncher.Returned += OnFruitSliceReturned;
            }
            SetExternalGameLobbyInput(false);
            LogFruitSliceStartup("unity_try_open_begin");
            var opened = fruitSliceLauncher.TryOpen(data.SoundEnabled, data.HapticsEnabled);
            LogFruitSliceStartup("unity_try_open_returned");
            if (!opened)
                OnFruitSliceReturned(FruitSliceLauncher.OpenFailureMessage);
        }

        private static void LogFruitSliceStartup(string stage)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Four local records per accepted entry, using the same clock as the native bridge.
            try
            {
                using (var clock = new AndroidJavaClass("android.os.SystemClock"))
                using (var log = new AndroidJavaClass("android.util.Log"))
                {
                    var atMs = clock.CallStatic<long>("elapsedRealtime");
                    log.CallStatic<int>("i", "FruitSliceStartup", "stage=" + stage + " atMs=" + atMs);
                }
            }
            catch (System.Exception) { /* Diagnostics must not affect the launch path. */ }
#endif
        }

        private void OnFruitSliceReturned(string error)
        {
            SetExternalGameLobbyInput(!IsExternalGameRunning);
            if (activePage == Page.Lobby && !IsExternalGameRunning && !string.IsNullOrEmpty(error)) ShowDialog("暂时无法打开", error, "返回大厅", () => { });
        }

        private void ReleaseFruitSliceLauncher()
        {
            if (fruitSliceLauncher != null) fruitSliceLauncher.Returned -= OnFruitSliceReturned;
            fruitSliceLauncher = null;
        }
    }
}
