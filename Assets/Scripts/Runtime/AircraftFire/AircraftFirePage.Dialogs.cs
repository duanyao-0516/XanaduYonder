using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.AircraftFire;

namespace XanaduYonder.Runtime.AircraftFire
{
    internal sealed partial class AircraftFirePage
    {
        private enum DialogKind { None, Pause, Restart, Help, Records, Result }
        private GameObject dialog;
        private DialogKind dialogKind;
        private bool reviewingResult;

        private RectTransform Dialog(DialogKind kind, string title, float height)
        {
            CloseDialog(); pointer.Cancel();
            var overlay = DialogUi.ModalOverlay(hud, "Aircraft " + kind + " overlay");
            DialogOutsideTap.Bind(overlay, DismissOutside);
            dialog = overlay.gameObject; dialogKind = kind;
            var panel = DialogCorners.Panel(overlay.transform, "Aircraft dialog", 0, 0, 568, height, DialogUi.Surface, DialogCorners.Surface);
            UiFactory.Center(panel.rectTransform); panel.raycastTarget = true;
            bool reading = kind == DialogKind.Help || kind == DialogKind.Records;
            UiFactory.Label(panel.transform, title, 32, 24, 504, 54, 32, UiFactory.Ink,
                reading ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, FontStyle.Bold);
            return panel.rectTransform;
        }
        private void DismissOutside()
        {
            if (dialogKind == DialogKind.Restart) { ShowPause(); return; }
            if (dialogKind == DialogKind.Pause) { Continue(); return; }
            if (dialogKind == DialogKind.Result) reviewingResult = true;
            CloseDialog();
        }

        private void CloseDialog()
        {
            if (dialog != null) { dialog.SetActive(false); Destroy(dialog); dialog = null; }
            dialogKind = DialogKind.None;
        }
        private void ShowPause()
        {
            if (game == null || settingsBlocked) return;
            var panel = Dialog(DialogKind.Pause, "游戏暂停", 415);
            DialogUi.ActionButton(panel, "继续飞行", 84, 110, 400, 70, Continue, true);
            if (game.Mode == AircraftMode.Classic)
                DialogUi.Disclosure(panel, "重开选项", 84, 205, 400, 70, ShowRestartOptions);
            else
                DialogUi.ActionButton(panel, "重新开始", 84, 205, 400, 70, () => StartRun(selectedMode, selectedCrazy));
            DialogUi.ActionButton(panel, "主菜单", 84, 300, 400, 70, ShowMenu);
        }
        private void ShowRestartOptions()
        {
            var panel = Dialog(DialogKind.Restart, "重新开始经典模式", 480);
            UiFactory.Label(panel, "选择后开始新局，历史成绩保留。", 32, 102, 504, 64, 22, UiFactory.Muted, TextAnchor.MiddleCenter);
            DialogUi.ActionButton(panel, "重新开始", 84, 190, 400, 66, () => StartRun(selectedMode, selectedCrazy), true);
            DialogUi.ActionButton(panel, selectedCrazy ? "普通进攻" : "疯狂进攻", 84, 272, 400, 66,
                () => StartRun(AircraftMode.Classic, !selectedCrazy));
            DialogUi.ActionButton(panel, "返回暂停菜单", 84, 370, 400, 66, ShowPause);
        }
        private void ShowHelp()
        {
            var panel = Dialog(DialogKind.Help, "飞机大战 · 玩法说明", 668);
            var body = "拖动战机移动，子弹自动发射。手指可以落在战场任意位置，战机跟随拖动方向。\n\n"
                + "躲避敌机和子弹。每局有 3 条生命，受击后短暂无敌。击毁小型、中型和大型敌机分别获得 1,000、6,000、30,000 分。\n\n"
                + "红色降落伞补充炸弹，点击左下角清除敌机与敌弹；蓝色降落伞提供 90 次双发射击。炸弹对关卡 Boss 造成 30 点伤害。\n\n"
                + "在入口选择经典或关卡模式。经典可在暂停菜单切换普通／疯狂并开始新局；关卡共 7 关，击败 Boss 进入下一关。";
            UiFactory.Label(panel, body, 32, 102, 504, 438, 22, UiFactory.Ink, TextAnchor.UpperLeft);
            DialogUi.ActionButton(panel, "知道了", 28, 570, 512, 62, CloseDialog, true);
        }
        private void ShowRecords()
        {
            if (!visible || game != null || settingsBlocked || !HostAllows) return;
            var panel = Dialog(DialogKind.Records, "历史成绩", 520);
            var records = "经典模式    " + profile.ClassicBestScore.ToString("N0")
                + "\n疯狂进攻    " + profile.CrazyBestScore.ToString("N0")
                + "\n关卡模式    " + profile.LevelBestScore.ToString("N0")
                + "\n最高到达    第 " + profile.HighestStage + " 关";
            UiFactory.Label(panel, records, 32, 112, 504, 230, 27, UiFactory.Ink, TextAnchor.MiddleLeft);
            DialogUi.ActionButton(panel, "返回", 28, 362, 512, 62, CloseDialog, true);
            if (SaveError != null)
                DialogUi.ActionButton(panel, "纪录未保存 · 点击重试", 28, 444, 512, 42, () => { RetrySave(); ShowRecords(); });
        }
        private void ShowResult()
        {
            reviewingResult = false;
            var won = game.Phase == AircraftPhase.Completed;
            var panel = Dialog(DialogKind.Result, won ? "全部通关" : "游戏结束", 560);
            UiFactory.Label(panel, game.Score.ToString("N0"), 28, 92, 512, 68, 48, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Label(panel, "最高纪录 " + BestScore.ToString("N0"), 28, 174, 512, 34, 22, UiFactory.Muted, TextAnchor.MiddleCenter);
            if (!won && game.CanRevive)
                DialogUi.ActionButton(panel, "继续挑战 · 本局复活一次", 84, 242, 400, 70,
                    () => { if (game.Revive()) { terminalPresentation = 0; CloseDialog(); pointer.Cancel(); Refresh(); } }, true, 24);
            DialogUi.ActionButton(panel, "再来一局", 134, 337, 300, 70, () => StartRun(selectedMode, selectedCrazy), won || !game.CanRevive);
            DialogUi.ActionButton(panel, "主菜单", 134, 432, 300, 70, ShowMenu);
        }
    }
}
