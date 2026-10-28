using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Gomoku;

namespace XanaduYonder.Runtime.Gomoku
{
    internal sealed partial class GomokuPage
    {
        private Text modeInfo, turnText, moveText, hintText, confirmText;
        private readonly Text[] playerText = new Text[2];
        private readonly Image[] playerCards = new Image[2];
        private Button aiMode, localMode, confirmButton, clearButton, undoButton, pauseButton, retryButton;
        private GameObject pauseCover;

        private void Build()
        {
            var nav = UiFactory.Band(root, "Gomoku navigation", 0, 92, 0);
            UiFactory.IconButton(nav, UiIconKind.Home, 56, 34, 58, 58, RequestLeave);
            UiFactory.Label(nav, "XANADU / YONDER", 226, 34, 278, 58, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.IconButton(nav, UiIconKind.Book, 536, 34, 58, 58, ShowRules);
            UiFactory.IconButton(nav, UiIconKind.Settings, 606, 34, 58, 58, Settings);
            var heading = UiFactory.Band(root, "Gomoku heading", 106, 78, .05f);
            UiFactory.Label(heading, "五子棋", 56, 0, 300, 78, 48, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(heading, "一黑一白，落子有声。", 346, 20, 318, 50, 22, UiFactory.Muted, TextAnchor.MiddleRight);
            var modes = UiFactory.Band(root, "Gomoku modes", 194, 58, .12f);
            aiMode = UiFactory.Button(modes, "人机对战", 56, 0, 296, 58, () => SwitchMode(GomokuMode.HumanVsAi));
            localMode = UiFactory.Button(modes, "本地双人", 368, 0, 296, 58, () => SwitchMode(GomokuMode.Local));
            var meta = UiFactory.Band(root, "Gomoku options", 260, 44, .18f);
            modeInfo = UiFactory.Label(meta, "", 56, 0, 608, 44, 21, UiFactory.Muted);
            var players = UiFactory.Band(root, "Gomoku players", 312, 80, .24f);
            for (int i = 0; i < 2; i++)
            {
                playerCards[i] = UiFactory.Panel(players, "Player " + (i + 1), 56 + 312 * i, 0, 296, 80, UiFactory.Hex("E7EDE4"));
                playerText[i] = UiFactory.Label(playerCards[i].transform, "", 16, 8, 264, 64, 23, UiFactory.Ink, TextAnchor.MiddleCenter);
            }
            var boardBand = UiFactory.Band(root, "Gomoku board band", 400, 648, .44f);
            var surface = UiFactory.Panel(boardBand, "Gomoku board", 36, 0, 648, 648, UiFactory.Paper);
            surface.raycastTarget = true;
            board = new GomokuBoardView(surface.rectTransform); board.Selected += Select;
            var cover = UiFactory.Panel(surface.transform, "Paused board", 0, 0, 648, 648, UiFactory.Paper);
            cover.raycastTarget = true; pauseCover = cover.gameObject;
            UiFactory.Label(cover.transform, "对局已暂停", 48, 210, 552, 72, 34, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Label(cover.transform, "准备好后，点击下方继续对局。", 40, 294, 568, 64, 24, UiFactory.Muted, TextAnchor.MiddleCenter);
            var state = UiFactory.Band(root, "Gomoku state", 1052, 56, .66f);
            turnText = UiFactory.Label(state, "", 56, 0, 408, 32, 23, UiFactory.Ink);
            moveText = UiFactory.Label(state, "", 460, 0, 204, 32, 20, UiFactory.Muted, TextAnchor.MiddleRight);
            hintText = UiFactory.Label(state, "", 56, 32, 608, 30, 18, UiFactory.Muted);
            var confirmation = UiFactory.Band(root, "Gomoku confirmation", 1122, 66, .82f);
            confirmButton = UiFactory.Button(confirmation, "先选择交叉点", 56, 0, 430, 66, Confirm, true);
            confirmText = confirmButton.GetComponentInChildren<Text>();
            clearButton = UiFactory.Button(confirmation, "取消选择", 502, 0, 162, 66, ClearSelection);
            clearButton.GetComponentInChildren<Text>().fontSize = 22;
            var actions = UiFactory.Band(root, "Gomoku actions", 1202, 62, 1);
            undoButton = UiFactory.Button(actions, "悔棋", 56, 0, 192, 62, RequestUndo);
            pauseButton = UiFactory.Button(actions, "暂停", 264, 0, 192, 62, Pause);
            UiFactory.Button(actions, "新一局", 472, 0, 192, 62, () => { if (Allowed) ShowSetup(false); });
            // Storage failures remain actionable without placing a second modal over the game.
            retryButton = UiFactory.Button(meta, "重试保存", 510, 0, 154, 44, RetrySave);
            retryButton.GetComponentInChildren<Text>().fontSize = 19;
            Refresh();
        }

        private static string DifficultyName(GomokuDifficulty difficulty)
        { return difficulty == GomokuDifficulty.Easy ? "简单" : difficulty == GomokuDifficulty.Normal ? "普通" : "困难"; }
        private static string StoneName(GomokuStone stone) { return stone == GomokuStone.Black ? "黑棋" : "白棋"; }
        private void PaintChoice(Button button, bool selected)
        {
            button.GetComponent<Image>().color = selected ? UiFactory.Accent : UiFactory.Hex("E7EDE4");
            button.GetComponentInChildren<Text>().color = selected ? Color.white : UiFactory.Ink;
        }
        private void Refresh()
        {
            if (board == null) return;
            bool ai = data.SelectedMode == GomokuMode.HumanVsAi;
            var game = session == null ? new GomokuGame() : session.Game;
            bool terminal = game.State != GomokuState.Playing;
            bool pausedGame = session != null && session.Paused && !terminal;
            PaintChoice(aiMode, ai); PaintChoice(localMode, !ai);
            modeInfo.text = ai ? "电脑 · " + DifficultyName(session == null ? data.PreferredDifficulty : session.Difficulty) + "难度  /  黑棋先手" : "同一台设备，轮流落子  /  黑棋先手";
            retryButton.gameObject.SetActive(saveFailed);
            for (int i = 0; i < 2; i++)
            {
                GomokuStone stone;
                string name;
                if (ai)
                {
                    var human = session == null ? data.PreferredHumanStone : session.HumanStone;
                    stone = i == 0 ? human : human == GomokuStone.Black ? GomokuStone.White : GomokuStone.Black;
                    name = i == 0 ? "你" : "电脑";
                }
                else
                {
                    int blackPlayer = session == null ? data.PreferredBlackPlayer : session.BlackPlayer;
                    stone = blackPlayer == i + 1 ? GomokuStone.Black : GomokuStone.White;
                    name = "玩家 " + (i + 1);
                }
                bool current = session != null && !terminal && game.ToPlay == stone;
                playerCards[i].color = current ? UiFactory.Hex("D6E3D6") : UiFactory.Hex("E7EDE4");
                playerText[i].text = name + " · " + StoneName(stone) + "\n" + (current ? pausedGame ? "已暂停" : "等待落子" : terminal ? "对局结束" : "等待回合");
            }
            board.Render(game, session == null ? null : session.SelectedIndex);
            board.Interactable = Allowed && session != null && session.CanSelect;
            pauseCover.SetActive(pausedGame);
            moveText.text = "共 " + game.MoveCount + " 手";
            hintText.text = game.LastMove < 0 ? "点击交叉点预览，再确认落子。" : "上一手 " + GomokuCoordinates.Format(game.LastMove) + " · 小圆点标记最后落子";
            if (session == null) turnText.text = "选择模式，开始一局";
            else if (terminal) turnText.text = ResultTitle();
            else if (aiError != null) turnText.text = "电脑落子失败 · 可重试";
            else if (pausedGame) turnText.text = "已暂停 · 棋局已保留";
            else if (session.NeedsAi) turnText.text = "电脑思考中…";
            else turnText.text = ai ? "轮到你 · " + StoneName(game.ToPlay) : "轮到玩家 " + PlayerFor(game.ToPlay) + " · " + StoneName(game.ToPlay);
            int? selected = session == null ? null : session.SelectedIndex;
            if (aiError != null) hintText.text = aiError;
            if (selected.HasValue) hintText.text = "已预选 " + GomokuCoordinates.Format(selected.Value) + " · 确认前可以更换位置";
            confirmText.text = session == null ? "开始新一局" : terminal ? "查看结果" : aiError != null ? "重试电脑落子" : pausedGame ? "继续对局" : selected.HasValue ? "确认落子 · " + GomokuCoordinates.Format(selected.Value) : session.NeedsAi ? "等待电脑落子…" : "先选择交叉点";
            confirmButton.interactable = Allowed && (session == null || terminal || pausedGame || session.CanSelect && selected.HasValue);
            clearButton.interactable = Allowed && selected.HasValue;
            undoButton.interactable = Allowed && session != null && session.CanUndo;
            pauseButton.interactable = Allowed && session != null && !terminal && !pausedGame;
            aiMode.interactable = localMode.interactable = Allowed;
            setStatus?.Invoke(SaveError ?? warning ?? "");
        }
    }
}
