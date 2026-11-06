using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.ClassicGoldMiner;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private ClassicGoldMinerLauncher classicGoldMinerLauncher;
        private bool IsClassicGoldMinerRunning => classicGoldMinerLauncher != null && classicGoldMinerLauncher.IsBusy;

        private static void AddClassicGoldMinerPreview(Transform parent)
        {
            var root = LobbyPreviews.RoundedLogoFrame(parent, "Classic Gold Miner");
            var logo = UiFactory.Rect(root, "Classic Gold Miner logo", 0, 0, 96, 96).gameObject.AddComponent<Image>();
            logo.sprite = Resources.Load<Sprite>("Lobby/ClassicGoldMinerLogo");
            logo.preserveAspect = true;
            logo.raycastTarget = false;
        }

        private void EnterClassicGoldMiner()
        {
            if (activePage != Page.Lobby || paused || !focused || IsExternalGameRunning) return;
            CloseModal();
            Save();
            feedback?.Stop();
            EnsureClassicGoldMinerLauncher();
            SetExternalGameLobbyInput(false);
            if (!classicGoldMinerLauncher.TryOpen(data.SoundEnabled, data.HapticsEnabled))
                OnClassicGoldMinerReturned(ClassicGoldMinerLauncher.OpenFailureMessage);
            // The lobby stays underneath the native Activity; Android owns its focus,
            // orientation and pause/resume lifecycle. No second Unity game is entered.
        }

        private void OnClassicGoldMinerReturned(string error)
        {
            SetExternalGameLobbyInput(true);
            if (activePage == Page.Lobby && !IsExternalGameRunning && !string.IsNullOrEmpty(error))
                ShowDialog("暂时无法打开", error, "返回大厅", () => { });
        }

        private void EnsureClassicGoldMinerLauncher()
        {
            if (classicGoldMinerLauncher != null) return;
            classicGoldMinerLauncher = gameObject.AddComponent<ClassicGoldMinerLauncher>();
            classicGoldMinerLauncher.Returned += OnClassicGoldMinerReturned;
        }

        private void ReleaseClassicGoldMinerLauncher()
        {
            if (classicGoldMinerLauncher != null) classicGoldMinerLauncher.Returned -= OnClassicGoldMinerReturned;
            classicGoldMinerLauncher = null;
        }
    }
}
