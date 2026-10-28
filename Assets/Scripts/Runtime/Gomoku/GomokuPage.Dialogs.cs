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

        private RectTransform Overlay(string title, float height)
        {
            CloseDialog();
            dialogResume = session != null && !session.Paused;
            CancelSearch(); board.CancelInput(); session?.SetBlocked(true);
            var overlay = UiFactory.Panel(root, "Gomoku dialog overlay", 0, 0, 720, 1320, new Color(.06f, .14f, .1f, .48f), false);
            UiFactory.Stretch(overlay.rectTransform); overlay.raycastTarget = true; dialog = overlay.gameObject;
            var panel = UiFactory.Panel(overlay.transform, "Gomoku dialog", 0, 0, 596, height, UiFactory.Paper);
            UiFactory.Center(panel.rectTransform); panel.raycastTarget = true;
            UiFactory.Label(panel.transform, title, 32, 24, 532, 70, 33, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Refresh();
            return panel.rectTransform;
        }
        private void CloseDialog(bool resume = true)
        {
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
            UiFactory.Button(panel, primary, 32, 330, 532, 70, () => finish(action), true);
            if (secondary != null) UiFactory.Button(panel, secondary, 32, 414, 532, 64, () => finish(cancel));
            if (tertiary != null) UiFactory.Button(panel, tertiary, 32, 498, 532, 64, () => finish(third));
            dismissDialog = () => finish(cancel);
        }
        private void ShowSetup(bool rematch)
        {
            var mode = data.SelectedMode;
            bool ai = mode == GomokuMode.HumanVsAi;
            var difficulty = session == null ? data.PreferredDifficulty : session.Difficulty;
            var human = session == null ? data.PreferredHumanStone : session.HumanStone;
            int blackPlayer = session == null ? data.PreferredBlackPlayer : session.BlackPlayer;
            if (rematch && !ai) blackPlayer = 3 - blackPlayer;
            var panel = Overlay(ai ? "开始人机对战" : "开始本地双人", ai ? 850 : 760);
            string explanation = invalidSave ? "旧存档无法恢复。确认后创建新存档。" : session != null && session.Game.MoveCount > 0 ? "确认后替换本模式对局，另一模式保留。" : "15 路棋盘 · 黑棋先手 · 连成五子获胜";
            UiFactory.Label(panel, explanation, 32, 108, 532, 78, 22, UiFactory.Muted, TextAnchor.MiddleCenter);
            Button[] difficulties = new Button[3], sides = new Button[2];
            Action repaint = () =>
            {
                if (ai) for (int i = 0; i < 3; i++) PaintChoice(difficulties[i], (int)difficulty == i);
                for (int i = 0; i < 2; i++) PaintChoice(sides[i], ai ? human == (i == 0 ? GomokuStone.Black : GomokuStone.White) : blackPlayer == i + 1);
            };
            float sideY;
            if (ai)
            {
                UiFactory.Label(panel, "电脑难度", 32, 202, 532, 44, 25, UiFactory.Ink);
                for (int i = 0; i < 3; i++)
                {
                    int option = i;
                    difficulties[i] = UiFactory.Button(panel, DifficultyName((GomokuDifficulty)i), 32 + 182 * i, 258, 168, 64,
                        () => { difficulty = (GomokuDifficulty)option; repaint(); });
                }
                UiFactory.Label(panel, "简单：轻松练习   普通：攻防兼顾\n困难：更深入推演，思考稍久", 32, 334, 532, 84, 21, UiFactory.Muted);
                sideY = 474;
                UiFactory.Label(panel, "你的执子", 32, 420, 532, 44, 25, UiFactory.Ink);
            }
            else
            {
                sideY = 270;
                UiFactory.Label(panel, "谁执黑先手？", 32, 210, 532, 44, 25, UiFactory.Ink);
            }
            sides[0] = UiFactory.Button(panel, ai ? "黑棋 · 先手" : "玩家 1 执黑", 32, sideY, 258, 68, () => { if (ai) human = GomokuStone.Black; else blackPlayer = 1; repaint(); });
            sides[1] = UiFactory.Button(panel, ai ? "白棋 · 后手" : "玩家 2 执黑", 306, sideY, 258, 68, () => { if (ai) human = GomokuStone.White; else blackPlayer = 2; repaint(); });
            if (!ai) UiFactory.Label(panel, "两位玩家轮流使用同一台设备。\n悔棋需另一位玩家确认；再来一局默认换边。\n玩家身份保持不变，黑白执子随新局调整。", 32, 362, 532, 142, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
            float startY = ai ? 648 : 554;
            UiFactory.Button(panel, "确认开始", 32, startY, 532, 72, () => StartGame(mode, difficulty, human, blackPlayer), true);
            Action cancel = () => { CloseDialog(); Refresh(); EnsureSearch(); };
            UiFactory.Button(panel, "取消", 32, startY + 88, 532, 62, cancel); dismissDialog = cancel;
            repaint();
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
            resultShown = true;
            var game = session.Game;
            ShowDialog(ResultTitle(), "本局共 " + game.MoveCount + " 手\n" + (game.State == GomokuState.Draw ? "棋盘已满，双方都未连成五子。" : "获胜连线已在棋盘上标记。"),
                "再来一局", () => ShowSetup(true), "查看棋盘", () => { }, "返回大厅", RequestLeave);
        }
        private void ShowInvalidSave()
        {
            ShowDialog("存档无法恢复", "主存档和备份均无法读取。\n确认开始新局后才会替换旧文件。", "设置新局", () => ShowSetup(false), "返回大厅", returnLobby);
        }
        private void ShowRules()
        {
            if (!Allowed) return;
            var panel = Overlay("五子棋玩法", 820);
            UiFactory.Label(panel, "黑棋先手，双方轮流在交叉点落子。\n横、竖、斜方向连续五子或更多即获胜。\n采用自由规则，无禁手；满盘未分胜负为和棋。\n\n点击预选，再按确认；落子前可取消或换点。\n坐标从左到右 A–O，从下到上 1–15。\n\n人机悔棋撤回你上一回合；双人需对方同意。\n两种模式分别保存一局，恢复后点击继续。", 32, 114, 532, 524, 24, UiFactory.Muted, TextAnchor.MiddleCenter);
            Action finish = () => { CloseDialog(); Refresh(); EnsureSearch(); };
            UiFactory.Button(panel, "知道了", 32, 690, 532, 74, finish, true);
            dismissDialog = finish;
        }
    }
}
