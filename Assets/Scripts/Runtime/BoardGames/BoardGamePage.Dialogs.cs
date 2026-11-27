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
            var block = DialogUi.ModalOverlay(view, "Board modal backdrop");
            float width = Mathf.Min(644, ContentWidth);
            float height = panel == PanelKind.Record ? 664 : panel == PanelKind.Rules || panel == PanelKind.Lessons ? Mathf.Min(980, Height - 80) :
                panel == PanelKind.Settings ? 540 + (InMatch ? 74 : 0) + (saveFailed ? 74 : 0) :
                panel == PanelKind.Preferences ? (game == BoardGameId.Chess ? 580 : 504) :
                panel == PanelKind.Feedback ? (feedbackSaveFailed ? 492 : 370) :
                panel == PanelKind.Manage ? 366 + (InMatch ? 76 : 0) + (InMatch && game != BoardGameId.Army ? 76 : 0) +
                    (CanClaimDraw ? 76 : 0) :
                panel == PanelKind.Resume ? 202 + 76 * profile.Matches.Count : panel == PanelKind.Result && CanReplay ? 438 : 340;
            var sheet = UiFactory.Panel(block.transform, "Board modal sheet", (Width - width) * .5f, (Height - height) * .5f,
                width, height, DialogUi.Surface);
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
                case PanelKind.Preferences:
                    DrawPreferences(sheet.transform, width, height);
                    break;
                case PanelKind.Feedback:
                    DrawFeedback(sheet.transform, width, height);
                    break;
                case PanelKind.Manage:
                    DrawMatchActions(sheet.transform, width, height);
                    break;
                case PanelKind.Resume:
                    DialogTitle(sheet.transform, width, "继续对局");
                    float y = 100;
                    foreach (var saved in profile.Matches.OrderBy(pair => pair.Key).Select(pair => pair.Value))
                    {
                        DialogUi.Disclosure(sheet.transform, Mode(saved.Options) + (saved.Terminal ? " · 已结束" : " · " + saved.MoveCount + " 手"),
                            26, y, width - 52, 66, () => { ActivateMatch(saved, true); Save(); Render(); });
                        y += 76;
                    }
                    DialogUi.ActionButton(sheet.transform, "返回对局管理", 26, height - 100, width - 52, 76, ClosePanel);
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
                    DialogUi.ActionButton(sheet.transform, "取消", 26, height - 101, width - 52, 75, ClosePanel);
                    break;
                case PanelKind.Result:
                    DialogTitle(sheet.transform, width, Outcome());
                    DialogText(sheet.transform, width, 111, 83,
                        match.Position.Status + " · 共 " + match.MoveCount + " 手");
                    if (CanReplay) DialogUi.Disclosure(sheet.transform, "复盘本局", 26, 213, width - 52, 75, StartReplay);
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

        private void DrawSettings(Transform sheet, float width, float height)
        {
            DialogTitle(sheet, width, "对局设置");
            float y = 100;
            void Row(string title, Action open)
            {
                DialogUi.Disclosure(sheet, title, 26, y, width - 52, 66, open);
                y += 74;
            }
            Row("落子与显示", () => OpenPanel(PanelKind.Preferences));
            Row("声音与触感", () => OpenPanel(PanelKind.Feedback));
            Row("对局管理", () => OpenPanel(PanelKind.Manage));
            if (InMatch) Row("棋谱", () => OpenPanel(PanelKind.Record));
            Row("新手教程", StartTutorial);
            if (saveFailed) DialogUi.ActionButton(sheet, "重试保存", 26, y, width - 52, 66, () => { Save(); Render(); });
            DialogUi.ActionButton(sheet, "完成", 26, height - 88, width - 52, 66, ClosePanel, true);
        }

        private void DrawPreferences(Transform sheet, float width, float height)
        {
            DialogTitle(sheet, width, "落子与显示");
            var immediate = !profile.ConfirmBoardMove;
            DialogUi.SwitchRow(sheet, "单击落子", 26, 100, width - 52, 66, immediate, _ => ToggleMoveConfirm());
            string hint = immediate ? "选中棋子后，点一次合法落点即走棋。" : "选中棋子和落点，再点同一落点确认。";
            if (immediate && game == BoardGameId.Checkers) hint = "点可停的落点即结束本手。关闭可分段连跳。";
            UiFactory.Label(sheet, hint, 30, 172, width - 60, 44, 22, UiFactory.Muted);
            DialogUi.SwitchRow(sheet, "显示提示按钮", 26, 224, width - 52, 66, profile.HintsEnabled, _ => ToggleHints());
            DialogUi.SwitchRow(sheet, "显示棋盘坐标", 26, 300, width - 52, 66, profile.ShowCoordinates, value =>
            {
                profile.ShowCoordinates = value;
                Save();
                Render();
            });
            if (game == BoardGameId.Chess)
                DialogUi.SwitchRow(sheet, "显示棋子汉字", 26, 376, width - 52, 66, profile.ShowChessNames, value =>
                {
                    profile.ShowChessNames = value;
                    Save();
                    Render();
                });
            DialogUi.ActionButton(sheet, "返回设置", 26, height - 88, width - 52, 66, ClosePanel);
        }

        private void DrawFeedback(Transform sheet, float width, float height)
        {
            DialogTitle(sheet, width, "声音与触感");
            DialogUi.SwitchRow(sheet, "声音", 26, 100, width - 52, 66, sound(), value => SetFeedback(true, value));
            DialogUi.SwitchRow(sheet, "触感", 26, 176, width - 52, 66, haptics(), value => SetFeedback(false, value));
            if (feedbackSaveFailed)
            {
                UiFactory.Label(sheet, "声音和触感设置暂未保存", 28, 252, width - 56, 40, 22, UiFactory.Muted);
                DialogUi.ActionButton(sheet, "重试保存", 26, 302, width - 52, 66,
                    () => SetFeedback(lastFeedbackWasSound, lastFeedbackWasSound ? sound() : haptics()));
            }
            DialogUi.ActionButton(sheet, "返回设置", 26, height - 88, width - 52, 66, ClosePanel);
        }

        private void DrawMatchActions(Transform sheet, float width, float height)
        {
            DialogTitle(sheet, width, "对局管理");
            float y = 100;
            void Row(string title, Action action, bool enabled = true)
            {
                DialogUi.Disclosure(sheet, title, 26, y, width - 52, 66, action).interactable = enabled;
                y += 76;
            }
            Row("新一局", OpenSetup);
            Row("继续其他对局", () => OpenPanel(PanelKind.Resume), profile.Matches.Values.Any(saved => saved != match));
            if (InMatch)
            {
                if (game != BoardGameId.Army) Row("悔棋", RequestUndo, match.CanUndo);
                if (match.Terminal) Row("查看结算", () => OpenPanel(PanelKind.Result));
                else Row("暂停", Pause, !paused);
            }
            if (CanClaimDraw) Row("申请和棋", ClaimDraw);
            DialogUi.ActionButton(sheet, "返回设置", 26, height - 88, width - 52, 66, ClosePanel);
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
            DialogUi.ActionButton(sheet, left, 26, height - 107, half, 80, first);
            DialogUi.ActionButton(sheet, right, 36 + half, height - 107, half, 80, second, true);
        }

    }
}
