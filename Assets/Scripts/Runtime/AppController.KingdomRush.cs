using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime.KingdomRush;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController
    {
        private KingdomRushLauncher kingdomRushLauncher;
        private Button kingdomRushStart;

        private void AddKingdomRushCard(Transform content, ref int cardCount)
        {
            var card = AddLobbyCard(content, "KingdomRush", cardCount++, UiFactory.Hex("E9E4D5"));
            UiFactory.Label(card.transform, cardCount.ToString("00") + "   /   初代塔防", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(card.transform, "王国保卫战", 28, 74, 450, 90, 58, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(card.transform, "守住每一条通往王国的路。", 32, 170, 548, 40, 25, UiFactory.Ink);
            UiFactory.Label(card.transform, "32 关 · 三种挑战 · 13 位英雄", 32, 216, 548, 38, 21, UiFactory.Accent);
            var logo = UiFactory.Panel(card.transform, "Kingdom Rush logo", 484, 76, 100, 102, Color.white, false);
            logo.sprite = Resources.Load<Sprite>("KingdomRush/Logo");
            logo.preserveAspect = true;
            kingdomRushStart = UiFactory.Button(card.transform, "开始 / 继续游戏  →", 32, 270, 544, 62, EnterKingdomRush, true);
            kingdomRushLauncher = gameObject.AddComponent<KingdomRushLauncher>();
            kingdomRushLauncher.Returned += OnKingdomRushReturned;
        }

        private void EnterKingdomRush()
        {
            if (activePage != Page.Lobby || kingdomRushLauncher.IsBusy) return;
            CloseModal();
            Save();
            feedback?.Stop();
            kingdomRushStart.interactable = false;
            if (!kingdomRushLauncher.TryOpen(data.SoundEnabled, data.HapticsEnabled))
                OnKingdomRushReturned(KingdomRushLauncher.OpenFailureMessage);
            // The lobby stays underneath the native Activity; Android owns its focus,
            // orientation and pause/resume lifecycle. No second Unity game is entered.
        }

        private void OnKingdomRushReturned(string error)
        {
            if (kingdomRushStart != null) kingdomRushStart.interactable = true;
            if (!string.IsNullOrEmpty(error))
                ShowDialog("暂时无法打开", error, "返回大厅", () => { });
        }
    }
}
