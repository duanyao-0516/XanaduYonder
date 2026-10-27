using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Sheep;

namespace XanaduYonder.Runtime.Sheep
{
    internal sealed partial class SheepPage
    {
        private void BuildLayout(RectTransform root)
        {
            var nav = UiFactory.Band(root, "Sheep navigation", 0, 92, 0);
            UiFactory.IconButton(nav, UiIconKind.Home, 56, 34, 58, 58, returnLobby);
            UiFactory.IconButton(nav, UiIconKind.Restart, 126, 34, 58, 58, ConfirmRestart);
            UiFactory.Label(nav, "XANADU / YONDER", 210, 34, 300, 58, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.IconButton(nav, UiIconKind.Book, 536, 34, 58, 58, ShowHelp);
            UiFactory.IconButton(nav, UiIconKind.Settings, 606, 34, 58, 58, OpenSettings);
            var heading = UiFactory.Band(root, "Sheep heading", 100, 84, 0);
            UiFactory.Label(heading, "旸了个旸", 56, 0, 430, 80, 56, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Button(heading, "切换模式", 520, 20, 144, 48, () => { if (AcceptsInput) ShowModes(); });
            var body = UiFactory.Rect(root, "Sheep play area", 0, 0, 720, 1320);
            UiFactory.Stretch(body); playArea = body.gameObject;
            var stats = UiFactory.Band(body, "Sheep progress", 184, 34, 0);
            levelText = UiFactory.Label(stats, "", 56, 0, 320, 34, 25, UiFactory.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            progress = UiFactory.Label(stats, "", 376, 0, 288, 34, 22, UiFactory.Muted, TextAnchor.MiddleRight);
            var tip = UiFactory.Band(body, "Sheep tip", 218, 28, 0);
            hint = UiFactory.Label(tip, "", 56, 0, 608, 28, 18, UiFactory.Muted);
            var stage = UiFactory.Panel(body, "Layered tile board", 32, 250, 656, 738, UiFactory.Hex("E9EEDB")).rectTransform;
            stage.anchorMin = new Vector2(0, 0); stage.anchorMax = new Vector2(1, 1);
            stage.offsetMin = new Vector2(32, 332); stage.offsetMax = new Vector2(-32, -250);
            var reserveBand = UiFactory.Band(body, "Sheep reserve", 996, 96, 1);
            var reserve = UiFactory.Rect(reserveBand, "Move-out reserve", 56, 0, 608, 96);
            var trayBand = UiFactory.Band(body, "Sheep tray", 1100, 100, 1);
            var tray = UiFactory.Panel(trayBand, "Seven-slot tray", 48, 0, 624, 100, UiFactory.Hex("E1E8CE")).rectTransform;
            board = new SheepBoardView(body, stage, tray, reserve, Pick);
            var tools = UiFactory.Band(body, "Sheep tools", 1208, 64, 1);
            removeButton = UiFactory.Button(tools, "移出", 56, 0, 192, 64, () => UseTool(SheepTool.RemoveThree));
            undoButton = UiFactory.Button(tools, "撤回", 264, 0, 192, 64, () => UseTool(SheepTool.Undo));
            shuffleButton = UiFactory.Button(tools, "洗牌", 472, 0, 192, 64, () => UseTool(SheepTool.Shuffle));
            removeLabel = removeButton.GetComponentInChildren<Text>();
            undoLabel = undoButton.GetComponentInChildren<Text>();
            shuffleLabel = shuffleButton.GetComponentInChildren<Text>();
            // Only the board is covered: reserve and tools remain available after a full tray.
            var panel = UiFactory.Panel(stage, "Round result", 0, 0, 544, 390, UiFactory.Paper);
            UiFactory.Center(panel.rectTransform); panel.raycastTarget = true; resultPanel = panel.gameObject;
            resultTitle = UiFactory.Label(panel.transform, "", 24, 20, 496, 64, 34, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            resultMessage = UiFactory.Label(panel.transform, "", 32, 92, 480, 106, 22, UiFactory.Muted, TextAnchor.MiddleCenter);
            resultPrimary = UiFactory.Button(panel.transform, "", 32, 210, 480, 68, ResultAction, true);
            resultPrimaryLabel = resultPrimary.GetComponentInChildren<Text>();
            UiFactory.Button(panel.transform, "重新开始", 32, 300, 232, 60, ConfirmRestart);
            UiFactory.Button(panel.transform, "返回大厅", 280, 300, 232, 60, returnLobby);
        }
    }
}
