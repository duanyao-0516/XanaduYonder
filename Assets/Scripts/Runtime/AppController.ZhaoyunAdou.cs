using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.ZhaoyunAdou;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private ZhaoyunAdouLauncher zhaoyunAdouLauncher;
        private bool IsZhaoyunAdouRunning => zhaoyunAdouLauncher != null && zhaoyunAdouLauncher.IsBusy;

        private static void AddZhaoyunAdouPreview(Transform parent)
        {
            var root = LobbyPreviews.RoundedLogoFrame(parent, "ZhaoyunAdou");
            var artwork = UiFactory.Rect(root, "ZhaoyunAdou artwork", 0, 0, 96, 96).gameObject.AddComponent<RawImage>();
            artwork.texture = Resources.Load<Texture2D>("ZhaoyunAdou/Logo");
            artwork.raycastTarget = false;
        }

        private void EnterZhaoyunAdou()
        {
            if (activePage != Page.Lobby || paused || !focused || IsExternalGameRunning) return;
            CloseModal();
            Save();
            feedback?.Stop();
            EnsureZhaoyunAdouLauncher();
            SetExternalGameLobbyInput(false);
            if (!zhaoyunAdouLauncher.TryOpen(data.SoundEnabled, data.HapticsEnabled))
                OnZhaoyunAdouReturned(ZhaoyunAdouLauncher.OpenFailureMessage);
            // The lobby stays underneath the native Activity; Android owns its focus,
            // orientation and pause/resume lifecycle. No second Unity game is entered.
        }

        private void OnZhaoyunAdouReturned(string error)
        {
            SetExternalGameLobbyInput(true);
            if (activePage == Page.Lobby && !IsExternalGameRunning && !string.IsNullOrEmpty(error))
                ShowDialog("暂时无法打开", error, "返回大厅", () => { });
        }

        private void EnsureZhaoyunAdouLauncher()
        {
            if (zhaoyunAdouLauncher != null) return;
            zhaoyunAdouLauncher = gameObject.AddComponent<ZhaoyunAdouLauncher>();
            zhaoyunAdouLauncher.Returned += OnZhaoyunAdouReturned;
        }

        private void ReleaseZhaoyunAdouLauncher()
        {
            if (zhaoyunAdouLauncher != null) zhaoyunAdouLauncher.Returned -= OnZhaoyunAdouReturned;
            zhaoyunAdouLauncher = null;
        }
    }
}
