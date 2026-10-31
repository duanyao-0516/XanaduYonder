using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.AircraftFire;

namespace XanaduYonder.Runtime.AircraftFire
{
    internal sealed partial class AircraftFirePage
    {
        private enum DialogKind { None, Pause, Help, Records, Result }
        private GameObject dialog;
        private DialogKind dialogKind;

        private RectTransform Dialog(DialogKind kind, string title, float height)
        {
            CloseDialog(); pointer.Cancel();
            var overlay = UiFactory.Panel(hud, "Aircraft " + kind + " overlay", 0, 0, 640, WorldHeight, new Color(0, 0, 0, 150f / 255), false);
            UiFactory.Stretch(overlay.rectTransform); overlay.raycastTarget = true;
            dialog = overlay.gameObject; dialogKind = kind;
            var original = kind == DialogKind.Pause || kind == DialogKind.Result;
            var panel = UiFactory.Panel(overlay.transform, "Aircraft dialog", 0, 0, 568, height, original ? Color.clear : UiFactory.Paper, !original);
            UiFactory.Center(panel.rectTransform); panel.raycastTarget = true;
            UiFactory.Label(panel.transform, title, 24, 24, 520, 54, 32, original ? Color.white : UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            return panel.rectTransform;
        }
        private void CloseDialog()
        {
            if (dialog != null) { dialog.SetActive(false); Destroy(dialog); dialog = null; }
            dialogKind = DialogKind.None;
        }
        private void ShowPause()
        {
            if (game == null || settingsBlocked) return;
            var classic = game.Mode == AircraftMode.Classic;
            var panel = Dialog(DialogKind.Pause, "游戏暂停", classic ? 510 : 415);
            OriginalButton(panel, "继续飞行", 134, 108, 300, 70, Continue);
            var next = 203f;
            if (classic)
            {
                OriginalButton(panel, selectedCrazy ? "普通进攻" : "疯狂进攻", 134, next, 300, 70,
                    () => StartRun(AircraftMode.Classic, !selectedCrazy));
                next += 95;
            }
            OriginalButton(panel, "重新开始", 134, next, 300, 70, () => StartRun(selectedMode, selectedCrazy));
            OriginalButton(panel, "主菜单", 134, next + 95, 300, 70, ShowMenu);
        }
        private void ShowHelp()
        {
            var panel = Dialog(DialogKind.Help, "飞机大战 · 玩法说明", 668);
            var body = "拖动战机移动，子弹自动发射。手指可以落在战场任意位置，战机跟随拖动方向。\n\n"
                + "躲避敌机和子弹。每局有 3 条生命，受击后短暂无敌。击毁小型、中型和大型敌机分别获得 1,000、6,000、30,000 分。\n\n"
                + "红色降落伞补充炸弹，点击左下角清除敌机与敌弹；蓝色降落伞提供 90 次双发射击。炸弹对关卡 Boss 造成 30 点伤害。\n\n"
                + "在入口选择经典或关卡模式。经典可在暂停菜单切换普通／疯狂并开始新局；关卡共 7 关，击败 Boss 进入下一关。";
            UiFactory.Label(panel, body, 32, 102, 504, 438, 22, UiFactory.Ink, TextAnchor.UpperLeft);
            UiFactory.Button(panel, "知道了", 28, 570, 512, 62, CloseDialog, true);
        }
        private void ShowRecords()
        {
            if (!visible || game != null || settingsBlocked || !HostAllows) return;
            var panel = Dialog(DialogKind.Records, "历史成绩", 520);
            var records = "经典模式    " + profile.ClassicBestScore.ToString("N0")
                + "\n疯狂进攻    " + profile.CrazyBestScore.ToString("N0")
                + "\n关卡模式    " + profile.LevelBestScore.ToString("N0")
                + "\n最高到达    第 " + profile.HighestStage + " 关";
            UiFactory.Label(panel, records, 42, 112, 484, 230, 27, UiFactory.Ink, TextAnchor.MiddleCenter);
            UiFactory.Button(panel, "返回", 28, 362, 512, 62, CloseDialog, true);
            if (SaveError != null)
                UiFactory.Button(panel, "纪录未保存 · 点击重试", 28, 444, 512, 42, () => { RetrySave(); ShowRecords(); });
        }
        private void ShowResult()
        {
            var won = game.Phase == AircraftPhase.Completed;
            var panel = Dialog(DialogKind.Result, won ? "全部通关" : "游戏结束", 560);
            UiFactory.Label(panel, game.Score.ToString("N0"), 28, 92, 512, 68, 48, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Label(panel, "最高纪录 " + BestScore.ToString("N0"), 28, 174, 512, 34, 22, Color.white, TextAnchor.MiddleCenter);
            if (!won && game.CanRevive)
                OriginalButton(panel, "继续挑战 · 本局复活一次", 84, 242, 400, 70,
                    () => { if (game.Revive()) { terminalPresentation = 0; CloseDialog(); pointer.Cancel(); Refresh(); } }, 24);
            OriginalButton(panel, "再来一局", 134, 337, 300, 70, () => StartRun(selectedMode, selectedCrazy));
            OriginalButton(panel, "主菜单", 134, 432, 300, 70, ShowMenu);
        }
    }
}
