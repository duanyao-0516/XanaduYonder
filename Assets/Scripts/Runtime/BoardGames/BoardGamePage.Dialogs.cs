using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    internal sealed partial class BoardGamePage
    {
        private Action settingsBack;
        private RectTransform settingsBody;
        private bool confirmSettingsUndo;
        private float settingsPosition = 1;

        private void DrawPanel()
        {
            var block = DialogUi.ModalOverlay(view, "Board modal backdrop");
            if (panel == PanelKind.Settings)
            {
                settingsBody = DialogSheetLayout.Create(block, "Board modal sheet", 540, 540, ClosePanelImmediately);
                settingsBack = DialogSheetLayout.AfterClose(settingsBody, ClosePanelImmediately);
                DrawSettings();
                return;
            }
            float width = Mathf.Min(644, ContentWidth);
            float height = panel == PanelKind.Rules || panel == PanelKind.Lessons ? Mathf.Min(980, Height - 80) :
                panel == PanelKind.Result && CanReplay ? 438 : 340;
            DialogOutsideTap.Bind(block, () =>
            {
                if (panel == PanelKind.Result) result.Inspect(ClosePanelImmediately);
                else ClosePanelImmediately();
            });
            var surface = DialogCorners.Panel(block.transform, "Board modal sheet", (Width - width) * .5f,
                (Height - height) * .5f, width, height, DialogUi.Surface, DialogCorners.Surface);
            surface.raycastTarget = true;
            var sheet = surface.rectTransform;
            switch (panel)
            {
                case PanelKind.Rules:
                    DrawRules(sheet, width, height);
                    break;
                case PanelKind.Lessons:
                    DrawLessonDirectory(sheet, width, height);
                    break;
                case PanelKind.Replace:
                    DialogTitle(sheet, width, "开始新一局？");
                    DialogText(sheet, width, 108, 92, store.ProtectDamaged ? "确认后，该棋种将开始新的存档。" :
                        "新局会替换同模式的棋局。其他模式仍可继续。");
                    DialogPair(sheet, width, height, "取消", ClosePanel, "确认新局", BeginNew);
                    break;
                case PanelKind.Promote:
                    DialogTitle(sheet, width, "选择升变棋子");
                    string[] names = { "", "", "马", "象", "车", "后" };
                    float choiceWidth = (width - 76) / 4;
                    for (int i = 0; i < promotions.Length; i++)
                    {
                        BoardMove move = promotions[i];
                        GameButton(sheet, names[move.Choice], 26 + i * (choiceWidth + 8), 112, choiceWidth, 86, () =>
                        {
                            panel = PanelKind.None;
                            pendingMove = move;
                            Commit(move);
                        }, i == 0);
                    }
                    DialogUi.ActionButton(sheet, "取消", 26, height - 101, width - 52, 75, ClosePanel);
                    break;
                case PanelKind.Result:
                    DialogTitle(sheet, width, Outcome());
                    DialogText(sheet, width, 111, 83,
                        match.Position.Status + " · 共 " + match.MoveCount + " 手");
                    if (CanReplay) DialogUi.Disclosure(sheet, "复盘本局", 26, 213, width - 52, 75, StartReplay);
                    DialogPair(sheet, width, height, "回看棋盘", () => result.Inspect(ClosePanel), "再来一局", () =>
                    {
                        panel = PanelKind.None;
                        OpenSetup();
                    });
                    break;
                case PanelKind.Leave:
                    DialogTitle(sheet, width, "棋局尚未保存");
                    DialogText(sheet, width, 108, 90, "可重试保存，也可以直接返回大厅。");
                    DialogPair(sheet, width, height, "直接返回", leave, "重试保存", () =>
                    {
                        Save();
                        if (!saveFailed) leave(); else ClosePanel();
                    });
                    break;
            }
        }

        private void DrawSettings()
        {
            const float width = 540;
            var sheet = settingsBody;
            var layout = sheet.GetComponentInParent<DialogSheetLayout>();
            var events = EventSystem.current;
            var selection = events != null ? events.currentSelectedGameObject : null;
            string focused = selection != null && selection.transform.IsChildOf(sheet) ? selection.name : null;
            if (sheet.childCount > 0) settingsPosition = layout.NormalizedPosition;
            for (int index = sheet.childCount - 1; index >= 0; index--)
            {
                var previous = sheet.GetChild(index).gameObject;
                previous.SetActive(false);
                previous.transform.SetParent(null, false);
                Destroy(previous);
            }
            DialogTitle(sheet, width, "对局设置");
            var rows = new DialogSheetSections(sheet, 96, 30, 66, 8);
            rows.Heading("落子与显示");
            bool immediate = !profile.ConfirmBoardMove;
            Text moveHelp = null;
            rows.Switch("单击落子", immediate, enabled =>
            {
                profile.ConfirmBoardMove = !enabled;
                ClearDraft();
                moveHelp.text = MoveHelp(enabled);
                SaveSettings();
            });
            moveHelp = rows.Body(MoveHelp(immediate), 19);
            rows.Switch("显示提示按钮", profile.HintsEnabled, _ => ToggleHints());
            rows.Switch("显示棋盘坐标", profile.ShowCoordinates, value => { profile.ShowCoordinates = value; SaveSettings(); });
            if (game == BoardGameId.Chess)
                rows.Switch("显示棋子汉字", profile.ShowChessNames, value => { profile.ShowChessNames = value; SaveSettings(); });
            rows.Heading("声音与触感");
            DialogUi.FeedbackSwitches(sheet, rows.Inset, rows.Reserve(66), rows.Width, 66, 14, sound(), haptics(),
                value => SetFeedback(true, value), value => SetFeedback(false, value));
            if (feedbackSaveFailed)
            {
                rows.Body("声音和触感设置暂未保存", 19);
                rows.Action("重试保存", () => SetFeedback(lastFeedbackWasSound, lastFeedbackWasSound ? sound() : haptics()));
            }
            void Command(string title, Action execute, bool enabled = true)
            {
                rows.Action(title, DialogSheetLayout.AfterClose(sheet, execute), enabled: enabled);
            }
            rows.Heading("对局管理");
            Command("新一局", OpenSetup);
            if (InMatch)
            {
                if (game != BoardGameId.Army)
                {
                    if (confirmSettingsUndo)
                    {
                        rows.Body("请由 " + match.SideName(match.Position.CurrentSide) + " 确认撤回上一手。");
                        rows.Action("保留棋局", () => { confirmSettingsUndo = false; DrawSettings(); });
                        Command("同意撤回", Undo);
                    }
                    else if (match.Options.Local)
                        rows.Action("悔棋", () => { confirmSettingsUndo = true; DrawSettings(); }, enabled: match.CanUndo);
                    else Command("悔棋", Undo, match.CanUndo);
                }
                if (match.Terminal) Command("查看结算", () => OpenPanel(PanelKind.Result));
                else Command("暂停", Pause, !paused);
            }
            if (CanClaimDraw) Command("申请和棋", ClaimDraw);
            Command("新手教程", StartTutorial);
            rows.Heading("继续其他对局");
            var savedMatches = profile.Matches.OrderBy(pair => pair.Key).Select(pair => pair.Value).Where(saved => saved != match);
            bool hasOther = false;
            foreach (var saved in savedMatches)
            {
                hasOther = true;
                rows.Entry(Mode(saved.Options) + (saved.Terminal ? " · 已结束" : " · " + saved.MoveCount + " 手"),
                    DialogSheetLayout.AfterClose(sheet, () => { ActivateMatch(saved, true); Save(); Render(); }));
            }
            if (!hasOther) rows.Body("暂无其他可继续的对局。", 19);
            if (InMatch)
            {
                DrawInlineRecord(rows, sheet);
                string replayLabel = game == BoardGameId.Army && !match.Terminal ? "终局后复盘" : "复盘本局";
                Command(replayLabel, () => { settingsPosition = layout.NormalizedPosition; StartReplay(); }, CanReplay);
            }
            if (saveFailed) rows.Action("重试棋局保存", () => { Save(); Render(); });
            rows.Action("完成", ClosePanel, primary: true);
            rows.Finish();
            layout.NormalizedPosition = settingsPosition;
            if (focused != null && events != null && !events.alreadySelecting)
            {
                var next = sheet.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == focused && button.IsInteractable());
                if (next != null) events.SetSelectedGameObject(next.gameObject);
            }
        }

        private string MoveHelp(bool immediate)
        {
            if (!immediate) return "选中棋子和落点，再点同一落点确认。";
            return game == BoardGameId.Checkers ? "点可停的落点即结束本手。关闭可分段连跳。" : "选中棋子后，点一次合法落点即走棋。";
        }

        private void SaveSettings()
        {
            bool previous = saveFailed;
            Save();
            if (previous != saveFailed) DrawSettings();
        }

        private static void DialogTitle(Transform sheet, float width, string title, float inset = 27) =>
            UiFactory.Label(sheet, title, inset, 20, width - 2 * inset, 77, 38, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);

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
