using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Gomoku;

namespace XanaduYonder.Runtime.Gomoku
{
    internal sealed partial class GomokuPage
    {
        private GameObject dialog;
        private Action dismissDialog;
        private bool dialogResume;
        private bool dialogBackLeavesGame;

        private RectTransform Overlay(string title, float height, Action setupCancel = null, bool reading = false)
        {
            CloseDialog();
            dialogResume = session != null && !session.Paused;
            CancelSearch(); board.CancelInput(); session?.SetBlocked(true);
            var overlay = DialogUi.ModalOverlay(root, "Gomoku dialog overlay");
            dialog = overlay.gameObject;
            if (setupCancel != null)
            {
                var body = DialogSheetLayout.Create(overlay, "Gomoku setup sheet", 596, height, setupCancel);
                dismissDialog = DialogSheetLayout.AfterClose(body, setupCancel);
                UiFactory.Label(body, title, 32, 18, 532, 64, 33, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                Refresh();
                return body;
            }
            DialogOutsideTap.Bind(overlay, () => dismissDialog?.Invoke());
            var panel = DialogCorners.Panel(overlay.transform, "Gomoku dialog", 0, 0, 596, height, DialogUi.Surface, DialogCorners.Surface);
            UiFactory.Center(panel.rectTransform); panel.raycastTarget = true;
            UiFactory.Label(panel.transform, title, 32, 24, 532, 70, 33, UiFactory.Ink,
                reading ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, FontStyle.Bold);
            Refresh();
            return panel.rectTransform;
        }
        private void CloseDialog(bool resume = true)
        {
            dialogBackLeavesGame = false;
            if (dialog == null) return;
            dialog.SetActive(false); Destroy(dialog); dialog = null; dismissDialog = null;
            session?.SetBlocked(false);
            if (resume && dialogResume && foreground && visible) session?.Resume();
            dialogResume = false;
        }
        private void ShowDialog(string title, string body, string primary, Action action, string secondary = null,
            Action cancel = null, string tertiary = null, Action third = null)
        {
            float height = tertiary != null ? 670 : secondary != null ? 570 : 490;
            var panel = Overlay(title, height);
            UiFactory.Label(panel, body, 36, 112, 524, 200, 24, UiFactory.Muted, TextAnchor.MiddleCenter).supportRichText = false;
            Action<Action> finish = callback => { CloseDialog(); callback?.Invoke(); Refresh(); EnsureSearch(); };
            DialogUi.ActionButton(panel, primary, 32, 330, 532, 70, () => finish(action), true);
            if (secondary != null) DialogUi.ActionButton(panel, secondary, 32, 414, 532, 64, () => finish(cancel));
            if (tertiary != null) DialogUi.ActionButton(panel, tertiary, 32, 498, 532, 64, () => finish(third));
            dismissDialog = () => finish(cancel);
            dialogBackLeavesGame = cancel != null && cancel == returnLobby;
        }
        private void ShowSetup(bool rematch)
        {
            var mode = data.SelectedMode;
            bool ai = mode == GomokuMode.HumanVsAi;
            var difficulty = session == null ? data.PreferredDifficulty : session.Difficulty;
            var human = session == null ? data.PreferredHumanStone : session.HumanStone;
            int blackPlayer = session == null ? data.PreferredBlackPlayer : session.BlackPlayer;
            if (rematch && !ai) blackPlayer = 3 - blackPlayer;
            void CancelSetup() { CloseDialog(); Refresh(); EnsureSearch(); }
            var panel = Overlay(ai ? "开始人机对战" : "开始本地双人", ai ? 726 : 646, CancelSetup);
            string explanation = invalidSave ? "旧存档无法恢复。确认后创建新存档。" : session != null && session.Game.MoveCount > 0 ? "确认后替换本模式对局，另一模式保留。" : "15 路棋盘 · 黑棋先手 · 连成五子获胜";
            UiFactory.Label(panel, explanation, 32, 108, 532, 78, 22, UiFactory.Muted, TextAnchor.MiddleLeft);
            float sideY;
            if (ai)
            {
                UiFactory.Label(panel, "电脑难度", 32, 202, 532, 44, 25, UiFactory.Ink);
                DialogChoices.Create(panel, "电脑难度选项", 32, 258, 532, 74, new[] { "简单", "普通", "困难" },
                    (int)difficulty, index => difficulty = (GomokuDifficulty)index);
                UiFactory.Label(panel, "简单：轻松练习   普通：攻防兼顾\n困难：更深入推演，思考稍久", 32, 334, 532, 84, 21, UiFactory.Muted);
                sideY = 474;
                UiFactory.Label(panel, "你的执子", 32, 420, 532, 44, 25, UiFactory.Ink);
            }
            else
            {
                sideY = 270;
                UiFactory.Label(panel, "谁执黑先手？", 32, 210, 532, 44, 25, UiFactory.Ink);
            }
            var sides = ai ? new[] { "黑棋 · 先手", "白棋 · 后手" } : new[] { "玩家 1 执黑", "玩家 2 执黑" };
            int selectedSide = ai ? (human == GomokuStone.Black ? 0 : 1) : blackPlayer - 1;
            DialogChoices.Create(panel, "执子选项", 32, sideY, 532, 74, sides, selectedSide, index =>
            {
                if (ai) human = index == 0 ? GomokuStone.Black : GomokuStone.White;
                else blackPlayer = index + 1;
            });
            if (!ai) UiFactory.Label(panel, "两位玩家轮流使用同一台设备。\n悔棋需另一位玩家确认；再来一局默认换边。\n玩家身份保持不变，黑白执子随新局调整。", 32, 362, 532, 106, 23, UiFactory.Muted, TextAnchor.MiddleLeft);
            float startY = ai ? 560 : 480;
            DialogUi.ActionButton(panel, "确认开始", 32, startY, 532, 72, DialogSheetLayout.AfterClose(panel, () => StartGame(mode, difficulty, human, blackPlayer)), true);
            DialogUi.ActionButton(panel, "取消", 32, startY + 84, 532, 62, dismissDialog);
        }
        private string ResultTitle()
        {
            var state = session.Game.State;
            if (state == GomokuState.Draw) return "棋逢对手 · 和棋";
            var winner = state == GomokuState.BlackWon ? GomokuStone.Black : GomokuStone.White;
            return session.Mode == GomokuMode.Local ? "玩家 " + PlayerFor(winner) + " 获胜" : winner == session.HumanStone ? "你赢了！" : "电脑获胜";
        }
        private void ShowResult()
        {
            var ended=session;var results=ResultPresentation.For(this,root);
            if(!results.Gate(ended,()=>Allowed&&session==ended&&session.Game.State!=GomokuState.Playing,ShowResult))return;
            resultShown = true;
            var game = session.Game;
            ShowDialog(ResultTitle(), "本局共 " + game.MoveCount + " 手\n" + (game.State == GomokuState.Draw ? "棋盘已满，双方都未连成五子。" : "获胜连线已在棋盘上标记。"),
                "再来一局", () => ShowSetup(true), "查看棋盘", () => results.Inspect(()=>CloseDialog(false)), "返回大厅", RequestLeave);
        }
        private void ShowInvalidSave()
        {
            ShowDialog("存档无法恢复", "主存档和备份均无法读取。\n确认开始新局后才会替换旧文件。", "设置新局", () => ShowSetup(false), "返回大厅", returnLobby);
        }
        private void ShowRules()
        {
            if (!Allowed) return;
            var panel = Overlay("五子棋玩法", 820, reading: true);
            DialogUi.ScrollText(panel, "黑棋先手，双方轮流在交叉点落子。\n横、竖、斜方向连续五子或更多即获胜。\n采用自由规则，无禁手；满盘未分胜负为和棋。\n\n点击预选，再按确认；落子前可取消或换点。\n坐标从左到右 A–O，从下到上 1–15。\n\n人机悔棋撤回你上一回合；双人需对方同意。\n两种模式分别保存一局，恢复后点击继续。", 32, 114, 532, 524, 24);
            Action finish = () => { CloseDialog(); Refresh(); EnsureSearch(); };
            DialogUi.ActionButton(panel, "知道了", 32, 690, 532, 74, finish, true);
            dismissDialog = finish;
        }
    }
}
