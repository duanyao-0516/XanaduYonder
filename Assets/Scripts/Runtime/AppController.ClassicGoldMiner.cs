using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.ClassicGoldMiner;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private ClassicGoldMinerLauncher classicGoldMinerLauncher;
        private CanvasGroup classicGoldMinerLobbyInput;
        private bool IsClassicGoldMinerRunning => classicGoldMinerLauncher != null && classicGoldMinerLauncher.IsBusy;

        private static void AddClassicGoldMinerPreview(Transform parent)
        {
            var logo = UiFactory.Rect(parent, "Classic Gold Miner logo", 0, 0, 72, 72).gameObject.AddComponent<Image>();
            logo.sprite = Resources.Load<Sprite>("Lobby/ClassicGoldMinerLogo");
            logo.preserveAspect = true;
            logo.raycastTarget = false;
        }

        private void EnterClassicGoldMiner()
        {
            if (activePage != Page.Lobby || paused || !focused || IsClassicGoldMinerRunning || IsKingdomRushRunning) return;
            CloseModal();
            Save();
            feedback?.Stop();
            EnsureClassicGoldMinerLauncher();
            SetClassicGoldMinerLobbyInput(false);
            if (!classicGoldMinerLauncher.TryOpen(data.SoundEnabled, data.HapticsEnabled))
                OnClassicGoldMinerReturned(ClassicGoldMinerLauncher.OpenFailureMessage);
            // The lobby stays underneath the native Activity; Android owns its focus,
            // orientation and pause/resume lifecycle. No second Unity game is entered.
        }

        private void OnClassicGoldMinerReturned(string error)
        {
            SetClassicGoldMinerLobbyInput(true);
            if (activePage == Page.Lobby && !string.IsNullOrEmpty(error))
                ShowDialog("暂时无法打开", error, "返回大厅", () => { });
        }

        private void EnsureClassicGoldMinerLauncher()
        {
            if (classicGoldMinerLauncher != null) return;
            classicGoldMinerLauncher = gameObject.AddComponent<ClassicGoldMinerLauncher>();
            classicGoldMinerLauncher.Returned += OnClassicGoldMinerReturned;
        }

        private void SetClassicGoldMinerLobbyInput(bool enabled)
        {
            if (lobbyPage == null) return;
            if (classicGoldMinerLobbyInput == null)
            {
                classicGoldMinerLobbyInput = lobbyPage.GetComponent<CanvasGroup>();
                // Unity missing components can be non-null managed wrappers.
                if (classicGoldMinerLobbyInput == null) classicGoldMinerLobbyInput = lobbyPage.AddComponent<CanvasGroup>();
            }
            // The stable lobby root survives display-mode rebuilds; individual cards do not.
            classicGoldMinerLobbyInput.interactable = enabled;
            classicGoldMinerLobbyInput.blocksRaycasts = enabled;
        }

        private void ReleaseClassicGoldMinerLauncher()
        {
            if (classicGoldMinerLauncher != null) classicGoldMinerLauncher.Returned -= OnClassicGoldMinerReturned;
            classicGoldMinerLauncher = null;
        }
    }
}
