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
            float height = panel == PanelKind.Record ? 608 : panel == PanelKind.Rules ? 544 : panel == PanelKind.Settings ? 460 : 340;
            var sheet = UiFactory.Panel(block.transform, "Board modal sheet", (Width - width) * .5f, (Height - height) * .5f,
                width, height, UiFactory.Paper);
            switch (panel)
            {
                case PanelKind.Rules:
                    DialogTitle(sheet.transform, width, "玩法说明");
                    DialogText(sheet.transform, width, 107, 230, RuleText());
                    GameButton(sheet.transform, "新手教程", 26, height - 202, width - 52, 79, StartTutorial, true);
                    GameButton(sheet.transform, "返回", 26, height - 105, width - 52, 79, ClosePanel);
                    break;
                case PanelKind.Settings:
                    DialogTitle(sheet.transform, width, "对局设置");
                    if (game == BoardGameId.Army)
                    {
                        GameButton(sheet.transform, "走棋二次确认 · " + (profile.ConfirmArmyMove ? "开启" : "关闭"),
                            26, 102, width - 52, 83, ToggleArmyConfirm, profile.ConfirmArmyMove);
                        DialogText(sheet.transform, width, 187, 60, profile.ConfirmArmyMove ? "先预选落点，再确认走棋。" :
                            "点合法落点即走棋，同机双人立即遮挡交接。");
                    }
                    GameButton(sheet.transform, "声音与触感", 26, height - 199, width - 52, 76, OpenGlobal);
                    GameButton(sheet.transform, "完成", 26, height - 100, width - 52, 76, ClosePanel, true);
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
                    DialogPair(sheet.transform, width, height, "回看棋盘", () => result.Inspect(ClosePanel), "再来一局", () =>
                    {
                        panel = PanelKind.None;
                        OpenSetup();
                    });
                    break;
                case PanelKind.Record:
                    DialogTitle(sheet.transform, width, "本局棋谱");
                    var history = match.History.ToArray();
                    int first = Math.Max(0, history.Length - 8);
                    for (int i = first; i < history.Length; i++)
                        UiFactory.Label(sheet.transform, (i + 1) + ".  " + DescribeMove(history[i]), 28,
                            105 + (i - first) * 45, width - 56, 42, 21, UiFactory.Ink);
                    if (history.Length == 0) DialogText(sheet.transform, width, 108, 80, "还没有已提交的走棋");
                    GameButton(sheet.transform, "返回棋盘", 26, height - 102, width - 52, 76, ClosePanel);
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

        private void ToggleArmyConfirm()
        {
            profile.ConfirmArmyMove = !profile.ConfirmArmyMove;
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

        private string DescribeMove(BoardMove move)
        {
            if (move == BoardMove.Pass) return "跳过";
            if (move.From == BoardMove.ClaimDraw.From) return "申请和棋";
            if (game == BoardGameId.Checkers && move.PathLength != 0)
                return Coordinate(move.From) + " → " + Coordinate(move.To) + " · " + move.PathLength + " 跳";
            string description = Coordinate(move.From) + " → " + Coordinate(move.To);
            if (game == BoardGameId.Chess && move.Choice != 0) description += " · 升变";
            return description;
        }

        private string Coordinate(int point)
        {
            switch (game)
            {
                case BoardGameId.Chess: return ((char)('a' + point % 8)).ToString() + (point / 8 + 1);
                case BoardGameId.Xiangqi: return (point % 9 + 1) + "路" + (10 - point / 9) + "行";
                case BoardGameId.Army: return (point / 5 + 1) + "行" + (point % 5 + 1) + "站";
                default: return (ChineseCheckersGame.RowAt(point) + 1) + "行" + (ChineseCheckersGame.DoubledXAt(point) + 13) + "位";
            }
        }

        private string RuleText()
        {
            switch (game)
            {
                case BoardGameId.Army: return "秘密布阵后轮流走棋。公路线每次一站，铁路无阻挡可直走，工兵可以转弯。行营中的棋子受保护，大本营中的棋子不能再移动。扛旗或对手无棋可走时获胜。司令阵亡亮己方军旗。连续 60 手未交战判和。";
                case BoardGameId.Xiangqi: return "红方先走。点己子可查看合法落点。马受马腿限制，象受象眼限制，炮隔一个棋子才能吃子。将帅不能照面。将死或困毙的一方判负。重复局面按长将、长捉责任裁决。";
                case BoardGameId.Chess: return "白方先走，目标是将死对方。兵到达底线可升变，符合条件时可王车易位或吃过路兵。逼和和死局判和。三次重复或五十回合可申请和棋，五次重复或七十五回合自动判和。";
                default: return "先让十颗棋珠进入对角目标营。每手走到相邻空孔，或连续跳过相邻棋珠。可以跨过第三方营区，须在本方营区、中央或目标营停手。目标营满且仅余对方原营棋子堵点时，允许合法末段换位。";
            }
        }
    }
}
