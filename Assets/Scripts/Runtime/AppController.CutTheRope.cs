using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Runtime.CutTheRope;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private CutTheRopeLauncher cutTheRopeLauncher;
        private CanvasGroup cutTheRopeLobbyInput;
        private bool cutTheRopeScrollEnabled;
        private bool IsCutTheRopeRunning => cutTheRopeLauncher != null && cutTheRopeLauncher.IsBusy;

        private LobbyEntry CutTheRopeEntry()
        {
            return LobbyGame("cut-the-rope", "CutTheRope", "割绳子", "21", "逻辑策略", "糖果物理",
                "划断绳子，把糖果送到嘴边。", "17 个盒子 · 425 关", "425 关 · 离线游玩", EnterCutTheRope, AddCutTheRopePreview);
        }

        private static void AddCutTheRopePreview(Transform parent)
        {
            var logo = UiFactory.Rect(parent, "Cut the Rope supplied logo", 0, 0, 72, 72)
                .gameObject.AddComponent<Image>();
            logo.sprite = Resources.Load<Sprite>("Lobby/CutTheRopeLogo");
            logo.color = Color.white;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
        }

        private void EnterCutTheRope()
        {
            if (activePage != Page.Lobby || paused || !focused || IsExternalGameRunning) return;
            lobbyView?.CloseDisplayOptions();
            CloseModal();
            Save();
            feedback?.Stop();
            EnsureCutTheRopeLauncher();
            SetCutTheRopeLobbyInput(false);
            if (!cutTheRopeLauncher.TryOpen(data.SoundEnabled))
                OnCutTheRopeReturned(CutTheRopeLauncher.OpenFailureMessage);
        }

        private void EnsureCutTheRopeLauncher()
        {
            if (cutTheRopeLauncher != null) return;
            cutTheRopeLauncher = gameObject.AddComponent<CutTheRopeLauncher>();
            cutTheRopeLauncher.Returned += OnCutTheRopeReturned;
        }

        private void OnCutTheRopeReturned(string error)
        {
            SetCutTheRopeLobbyInput(true);
            if (activePage == Page.Lobby && !IsExternalGameRunning && !string.IsNullOrEmpty(error))
                ShowDialog("暂时无法打开", error, "返回大厅", () => { });
        }

        private void SetCutTheRopeLobbyInput(bool enabled)
        {
            if (lobbyPage == null) return;
            if (cutTheRopeLobbyInput == null)
            {
                cutTheRopeLobbyInput = lobbyPage.GetComponent<CanvasGroup>();
                if (cutTheRopeLobbyInput == null) cutTheRopeLobbyInput = lobbyPage.AddComponent<CanvasGroup>();
            }
            if (!enabled)
            {
                cutTheRopeScrollEnabled = lobbyScroll != null && lobbyScroll.enabled;
                lobbyScroll?.StopMovement();
                if (lobbyScroll != null) lobbyScroll.enabled = false;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }
            else if (lobbyScroll != null) lobbyScroll.enabled = cutTheRopeScrollEnabled;
            // Stable root owns every layout rebuild, settings button and card underneath Android.
            SetExternalGameLobbyInput(enabled);
        }

        private void ReleaseCutTheRopeLauncher()
        {
            if (cutTheRopeLauncher == null) return;
            cutTheRopeLauncher.Returned -= OnCutTheRopeReturned;
            SetCutTheRopeLobbyInput(true);
            Destroy(cutTheRopeLauncher);
            cutTheRopeLauncher = null;
        }
    }
}
