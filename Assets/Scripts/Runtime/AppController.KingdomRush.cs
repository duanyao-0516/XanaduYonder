using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.KingdomRush;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private KingdomRushLauncher kingdomRushLauncher;
        private CanvasGroup kingdomRushLobbyInput;
        private bool IsKingdomRushRunning => kingdomRushLauncher != null && kingdomRushLauncher.IsBusy;

        private static void AddKingdomRushPreview(Transform parent)
        {
            // Match Zuma's source rectangle; LobbyArtwork fits both into the same logo slot.
            var root = LobbyPreviews.Frame(parent, "KingdomRush");
            var artwork = UiFactory.Rect(root, "KingdomRush artwork", 0, 0, 96, 96).gameObject.AddComponent<RawImage>();
            UiFactory.Center(artwork.rectTransform);
            artwork.texture = Resources.Load<Texture2D>("KingdomRush/Logo");
            artwork.raycastTarget = false;
        }

        private void EnterKingdomRush()
        {
            if (activePage != Page.Lobby || paused || !focused || IsKingdomRushRunning) return;
            CloseModal();
            Save();
            feedback?.Stop();
            EnsureKingdomRushLauncher();
            SetKingdomRushLobbyInput(false);
            if (!kingdomRushLauncher.TryOpen(data.SoundEnabled, data.HapticsEnabled))
                OnKingdomRushReturned(KingdomRushLauncher.OpenFailureMessage);
            // The lobby stays underneath the native Activity; Android owns its focus,
            // orientation and pause/resume lifecycle. No second Unity game is entered.
        }

        private void OnKingdomRushReturned(string error)
        {
            SetKingdomRushLobbyInput(true);
            if (activePage == Page.Lobby && !string.IsNullOrEmpty(error))
                ShowDialog("暂时无法打开", error, "返回大厅", () => { });
        }

        private void EnsureKingdomRushLauncher()
        {
            if (kingdomRushLauncher != null) return;
            kingdomRushLauncher = gameObject.AddComponent<KingdomRushLauncher>();
            kingdomRushLauncher.Returned += OnKingdomRushReturned;
        }

        private void SetKingdomRushLobbyInput(bool enabled)
        {
            if (lobbyPage == null) return;
            if (kingdomRushLobbyInput == null)
            {
                kingdomRushLobbyInput = lobbyPage.GetComponent<CanvasGroup>();
                // Unity missing components can be non-null managed wrappers.
                if (kingdomRushLobbyInput == null) kingdomRushLobbyInput = lobbyPage.AddComponent<CanvasGroup>();
            }
            // The stable lobby root survives display-mode rebuilds; individual cards do not.
            kingdomRushLobbyInput.interactable = enabled;
            kingdomRushLobbyInput.blocksRaycasts = enabled;
        }

        private void ReleaseKingdomRushLauncher()
        {
            if (kingdomRushLauncher != null) kingdomRushLauncher.Returned -= OnKingdomRushReturned;
            kingdomRushLauncher = null;
        }
    }
}
