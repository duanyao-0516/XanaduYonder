using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.BombIt;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private BombItLauncher bombItLauncher;
        private bool IsBombItRunning => bombItLauncher != null && bombItLauncher.IsBusy;

        private static void AddBombItPreview(Transform parent)
        {
            var root = LobbyPreviews.RoundedLogoFrame(parent, "BombIt");
            var image = UiFactory.Rect(root, "Bomb It original artwork", 0, 0, 96, 96)
                .gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("Lobby/BombItLogo");
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private void EnterBombIt()
        {
            if (activePage != Page.Lobby || paused || !focused || IsExternalGameRunning) return;
            CloseModal();
            RestoreWebGamePendingPreferences();
            Save();
            feedback?.Stop();
            if (bombItLauncher == null)
            {
                bombItLauncher = gameObject.AddComponent<BombItLauncher>();
                bombItLauncher.Returned += OnBombItReturned;
            }
            SetExternalGameLobbyInput(false);
            if (!bombItLauncher.TryOpen(data.SoundEnabled, data.HapticsEnabled))
                OnBombItReturned(BombItLauncher.OpenFailureMessage);
        }

        private void OnBombItReturned(string error)
        {
            RestoreWebGamePendingPreferences();
            SetExternalGameLobbyInput(!IsExternalGameRunning);
            if (activePage == Page.Lobby && !string.IsNullOrEmpty(error))
                ShowDialog("暂时无法打开", error, "返回大厅", () => { });
        }

        private void ReleaseBombItLauncher()
        {
            if (bombItLauncher != null) bombItLauncher.Returned -= OnBombItReturned;
            bombItLauncher = null;
        }
    }
}
