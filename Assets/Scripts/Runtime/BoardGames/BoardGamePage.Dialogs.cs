using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal sealed partial class BoardGamePage
    {
        private void DrawPanel()
        {
            var block = UiFactory.Panel(view, "Board modal backdrop", 0, 0, Width, Height, new Color(.08f, .18f, .14f, .59f), false);
            block.raycastTarget = true;
            float width = Mathf.Min(644, ContentWidth);
            float height = panel == PanelKind.Record ? 664 : panel == PanelKind.Rules || panel == PanelKind.Lessons ? Mathf.Min(980, Height - 80) :
                panel == PanelKind.Settings ? 700 + (InMatch ? 76 : 0) + (feedbackSaveFailed ? 42 : 0) + (HasSettingsActions ? 76 : 0) :
                panel == PanelKind.Resume ? 202 + 76 * profile.Matches.Count : panel == PanelKind.Result && CanReplay ? 438 : 340;
            var sheet = UiFactory.Panel(block.transform, "Board modal sheet", (Width - width) * .5f, (Height - height) * .5f,
                width, height, UiFactory.Paper);
            switch (panel)
            {
                case PanelKind.Rules:
                    DrawRules(sheet.transform, width, height);
                    break;
                case PanelKind.Lessons:
                    DrawLessonDirectory(sheet.transform, width, height);
                    break;
                case PanelKind.Settings:
                    DrawSettings(sheet.transform, width, height);
                    break;
                case PanelKind.Resume:
                    DialogTitle(sheet.transform, width, "继续对局");
                    float y = 100;
                    foreach (var saved in profile.Matches.OrderBy(pair => pair.Key).Select(pair => pair.Value))
                    {
                        GameButton(sheet.transform, Mode(saved.Options) + (saved.Terminal ? " · 已结束" : " · " + saved.MoveCount + " 手"),
                            26, y, width - 52, 66, () => { ActivateMatch(saved, true); Save(); Render(); });
                        y += 76;
                    }
                    GameButton(sheet.transform, "返回设置", 26, height - 100, width - 52, 76, () => OpenPanel(PanelKind.Settings));
                    break;
                case PanelKind.Replace:
                    DialogTitle(sheet.transform, width, "开始新一局？");
                    DialogText(sheet.transform, width, 108, 92, store.ProtectDamaged ? "确认后，该棋种将开始新的存档。" :
                        "新局会替换同模式的棋局。其他模式仍可继续。");
                    DialogPair(sheet.transform, width, height, "取消", ClosePanel, "确认新局", BeginNew);
                    break;
                case PanelKind.Undo:
                    DialogTitle(sheet.transform, width, "同意悔棋？");
                    DialogText(sheet.transform, width, 108, 90, "请由 " + match.SideName(match.Position.CurrentSide) + " 确认撤回上一手。");
                    DialogPair(sheet.transform, width, height, "保留棋局", ClosePanel, "同意撤回", Undo);
                    break;
                case PanelKind.Promote:
                    DialogTitle(sheet.transform, width, "选择升变棋子");
                    string[] names = { "", "", "马", "象", "车", "后" };
                    float choiceWidth = (width - 76) / 4;
                    for (int i = 0; i < promotions.Length; i++)
                    {
                        BoardMove move = promotions[i];
                        GameButton(sheet.transform, names[move.Choice], 26 + i * (choiceWidth + 8), 112, choiceWidth, 86, () =>
                        {
                            panel = PanelKind.None;
                            pendingMove = move;
                            Commit(move);
                        }, i == 0);
                    }
                    GameButton(sheet.transform, "取消", 26, height - 101, width - 52, 75, ClosePanel);
                    break;
                case PanelKind.Result:
                    DialogTitle(sheet.transform, width, Outcome());
                    DialogText(sheet.transform, width, 111, 83,
                        match.Position.Status + " · 共 " + match.MoveCount + " 手");
                    if (CanReplay) GameButton(sheet.transform, "复盘本局", 26, 213, width - 52, 75, StartReplay);
                    DialogPair(sheet.transform, width, height, "回看棋盘", () => result.Inspect(ClosePanel), "再来一局", () =>
                    {
                        panel = PanelKind.None;
                        OpenSetup();
                    });
                    break;
                case PanelKind.Record:
                    DrawRecord(sheet.transform, width, height);
                    break;
                case PanelKind.Leave:
                    DialogTitle(sheet.transform, width, "棋局尚未保存");
                    DialogText(sheet.transform, width, 108, 90, "可重试保存，也可以直接返回大厅。");
                    DialogPair(sheet.transform, width, height, "直接返回", leave, "重试保存", () =>
                    {
                        Save();
                        if (!saveFailed) leave(); else ClosePanel();
                    });
                    break;
            }
        }

        private bool HasSettingsActions => saveFailed || InMatch;

        private void DrawSettings(Transform sheet, float width, float height)
        {
            DialogTitle(sheet, width, "对局设置");
            bool immediate = !profile.ConfirmBoardMove;
            PreferenceToggle(sheet, "单击落子", 26, 100, width - 52, immediate, ToggleMoveConfirm);
            string moveHint = immediate ? "选中棋子后，点一次合法落点即走棋。" : "选中棋子和落点，再点同一落点确认。";
            if (game == BoardGameId.Checkers && immediate) moveHint = "点可停的落点即结束本手。关闭可分段连跳。";
            UiFactory.Label(sheet, moveHint, 28, 172, width - 56, 44, 22, UiFactory.Muted);

            float column = (width - 62) / 2;
            float right = 36 + column;
            float y = 224;
            PreferenceToggle(sheet, "声音", 26, y, column, sound(), () => ToggleFeedback(true));
            PreferenceToggle(sheet, "触感", right, y, column, haptics(), () => ToggleFeedback(false));
            y += 76;
            if (feedbackSaveFailed)
            {
                UiFactory.Label(sheet, "声音和触感设置暂未保存", 28, y, width - 56, 36, 22, UiFactory.Muted);
                y += 42;
            }
            PreferenceToggle(sheet, "显示提示按钮", 26, y, width - 52, profile.HintsEnabled, ToggleHints);
            y += 76;
            bool chess = game == BoardGameId.Chess;
            PreferenceToggle(sheet, "显示棋盘坐标", 26, y, chess ? column : width - 52, profile.ShowCoordinates, () =>
            {
                profile.ShowCoordinates = !profile.ShowCoordinates;
                Save();
                Render();
            });
            if (chess)
                PreferenceToggle(sheet, "显示棋子汉字", right, y, column, profile.ShowChessNames, () =>
                {
                    profile.ShowChessNames = !profile.ShowChessNames;
                    Save();
                    Render();
                });
            y += 76;
            GameButton(sheet, "新手教程", 26, y, column, 66, StartTutorial);
            GameButton(sheet, "新一局", right, y, column, 66, OpenSetup);
            GameButton(sheet, "继续其他对局", 26, y + 76, column, 66, () => OpenPanel(PanelKind.Resume), false,
                profile.Matches.Values.Any(saved => saved != match));
            if (InMatch)
            {
                GameButton(sheet, "棋谱", right, y + 76, column, 66, () => OpenPanel(PanelKind.Record));
                if (game != BoardGameId.Army) GameButton(sheet, "悔棋", 26, y + 152, column, 66, RequestUndo, false, match.CanUndo);
                if (match.Terminal) GameButton(sheet, "查看结算", right, y + 152, column, 66, () => OpenPanel(PanelKind.Result));
                else GameButton(sheet, "暂停", right, y + 152, column, 66, Pause, false, !paused);
            }
            float more = y + (InMatch ? 228 : 152);
            if (HasSettingsActions)
            {
                bool replayEntry = InMatch;
                bool canDraw = CanClaimDraw;
                int count = (replayEntry ? 1 : 0) + (canDraw ? 1 : 0) + (saveFailed ? 1 : 0);
                float actionWidth = (width - 52 - (count - 1) * 10) / count;
                float x = 26;
                if (replayEntry)
                {
                    GameButton(sheet, game == BoardGameId.Army && !match.Terminal ? "终局后复盘" : "复盘本局",
                        x, more, actionWidth, 66, StartReplay, false, CanReplay);
                    x += actionWidth + 10;
                }
                if (canDraw)
                {
                    GameButton(sheet, "申请和棋", x, more, actionWidth, 66, ClaimDraw);
                    x += actionWidth + 10;
                }
                if (saveFailed) GameButton(sheet, "重试保存", x, more, actionWidth, 66, () => { Save(); Render(); });
            }
            GameButton(sheet, "完成", 26, height - 88, width - 52, 66, ClosePanel, true);
        }

        private static void PreferenceToggle(Transform sheet, string title, float x, float y, float width, bool enabled, Action toggle)
        {
            var row = UiFactory.Panel(sheet, title + " preference", x, y, width, 66, MutedCard);
            UiFactory.ConfigureButton(row, toggle);
            UiFactory.Label(row.transform, title, 18, 0, width - 116, 66, 25, UiFactory.Ink);
            var track = UiFactory.Panel(row.transform, title + " switch", width - 88, 15, 72, 36,
                enabled ? UiFactory.Accent : UiFactory.Hex("83948C"));
            UiFactory.CirclePanel(track.transform, title + " switch thumb", enabled ? 38 : 4, 4, 28, Color.white);
        }

        private void ToggleMoveConfirm()
        {
            profile.ConfirmBoardMove = !profile.ConfirmBoardMove;
            ClearDraft();
            Save();
            Render();
        }

        private static void DialogTitle(Transform sheet, float width, string title) =>
            UiFactory.Label(sheet, title, 27, 20, width - 54, 77, 38, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);

        private static void DialogText(Transform sheet, float width, float y, float height, string text) =>
            UiFactory.Label(sheet, text, 28, y, width - 56, height, 24, UiFactory.Ink, TextAnchor.UpperLeft);

        private static void DialogPair(Transform sheet, float width, float height, string left, Action first, string right, Action second)
        {
            float half = (width - 62) * .5f;
            GameButton(sheet, left, 26, height - 107, half, 80, first);
            GameButton(sheet, right, 36 + half, height - 107, half, 80, second, true);
        }

    }
}
