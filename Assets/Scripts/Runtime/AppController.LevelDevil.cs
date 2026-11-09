using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.LevelDevil;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private LevelDevilLauncher levelDevilLauncher;
        private bool IsLevelDevilRunning => levelDevilLauncher != null && levelDevilLauncher.IsBusy;

        private static void AddLevelDevilPreview(Transform parent)
        {
            var root = LobbyPreviews.RoundedLogoFrame(parent, "LevelDevil");
            var image = UiFactory.Rect(root, "LevelDevil artwork", 0, 0, 96, 96)
                .gameObject.AddComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>("LevelDevil/Logo");
            image.raycastTarget = false;
        }

        private void EnterLevelDevil()
        {
            if (activePage != Page.Lobby || paused || !focused || IsExternalGameRunning) return;
            CloseModal();
            RestoreWebGamePendingPreferences();
            Save();
            feedback?.Stop();
            if (levelDevilLauncher == null)
            {
                levelDevilLauncher = gameObject.AddComponent<LevelDevilLauncher>();
                levelDevilLauncher.Returned += OnLevelDevilReturned;
            }
            SetExternalGameLobbyInput(false);
            if (!levelDevilLauncher.TryOpen(data.SoundEnabled, data.HapticsEnabled))
                OnLevelDevilReturned(LevelDevilLauncher.OpenFailureMessage);
        }

        private void OnLevelDevilReturned(string error)
        {
            RestoreWebGamePendingPreferences();
            SetExternalGameLobbyInput(!IsExternalGameRunning);
            if (activePage == Page.Lobby && !string.IsNullOrEmpty(error))
                ShowDialog("暂时无法打开", error, "返回大厅", () => { });
        }

        private void ReleaseLevelDevilLauncher()
        {
            if (levelDevilLauncher != null) levelDevilLauncher.Returned -= OnLevelDevilReturned;
            levelDevilLauncher = null;
        }
    }
}
