using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.GoldMiner;

namespace XanaduYonder.Runtime.GoldMiner
{
    internal sealed partial class GoldMinerPage
    {
        private Action modalBack;

        private RectTransform ModalPanel(float width, float height, float shadeAlpha = .6f)
        {
            FlushBeforePause();
            session.Suspend(GoldMinerSuspendReason.BlockingModal);
            input?.ResetGeneration();
            sound?.StopAll();
            CloseModal();
            var shade = UiFactory.Panel(view, "Gold Miner modal", 0, 0, layout.Width, layout.Height, new Color(.08f, .055f, .025f, shadeAlpha), false);
            shade.raycastTarget = true;
            modal = shade.rectTransform;
            width = Mathf.Min(width, layout.Safe.width - 48);
            height = Mathf.Min(height, layout.Safe.height - 72);
            return ArtPanel(modal, "Dialog panel", layout.Safe.center.x - width * .5f,
                layout.Safe.center.y - height * .5f, width, height);
        }

        private RectTransform Dialog(string title, string description, float height = 510, float descriptionTop = 100)
        {
            var panel = ModalPanel(layout.Cooperative ? 650 : 636, height);
            float width = panel.rect.width;
            TextLabel(panel, title, 28, 24, width - 56, 65, 36, Forest, TextAnchor.MiddleCenter, true);
            TextLabel(panel, description, 32, descriptionTop, width - 64, panel.rect.height - descriptionTop - 100, 24, Forest, TextAnchor.UpperCenter);
            return panel;
        }

        private Rect FitHeaderArtwork(RectTransform panel, float width, float height, float top, float bottom)
        {
            // A wide phone has less vertical canvas space than the landscape mockup.
            // Keep the entire image above its caption and below the actual safe top.
            float panelTop = -panel.anchoredPosition.y;
            top = Mathf.Max(top, layout.Safe.y + 8 - panelTop);
            float scale = Mathf.Min(1, Mathf.Max(1, bottom - top) / height);
            width *= scale;
            height *= scale;
            return new Rect((panel.rect.width - width) * .5f, top, width, height);
        }

        private void AmountRow(Transform parent, string name, float x, float y, float width, float height,
            string label, long value, bool target = false, Color? valueColor = null)
        {
            var row = BorderedPanel(parent, name, x, y, width, height, UiFactory.Hex("FAEED2"), UiFactory.Hex("DFC599"), 25, 2);
            ArtIcon(row.transform, name + " icon", target ? "ui/hud-icon-atlas" : "objects/ui_gold_coin", 17, 12, height - 24, height - 24, target ? "target_flag" : null);
            TextLabel(row.transform, label, height + 2, 0, width * .42f - height + 14, height, 25, UiFactory.Hex("6B421F"), TextAnchor.MiddleLeft, true);
            TextLabel(row.transform, value.ToString(), width * .44f, 0, width * .52f, height, 45,
                valueColor ?? Forest, TextAnchor.MiddleCenter, true);
        }

        private void CloseModal()
        {
            modalBack = null;
            SetGameplayElementsVisible(true, "Mine HUD", "Mine world", "Mine touch hint", "Dynamite P1", "Dynamite P2",
                "Player badge P1", "Player badge P2", "左半屏点击出钩", "右半屏点击出钩");
            if (modal == null) return;
            modal.gameObject.SetActive(false);
            Destroy(modal.gameObject);
            modal = null;
        }

        private void ContinueRun(bool start)
        {
            if (!focused || backgrounded || orientation == null || orientation.IsPending) return;
            // Down contacts from the dialog must lift before the mine accepts them.
            input?.QuarantineActiveTouches();
            CloseModal();
            session.Resume(GoldMinerSuspendReason.BlockingModal | GoldMinerSuspendReason.NeedsExplicitContinue);
            accumulator = 0;
            lastRealTime = Time.realtimeSinceStartupAsDouble;
            lastFrameRealTime = lastRealTime;
            if (start && !session.StartLevel()) { ShowReady(); return; }
            renderedPhase = session.GetStateSnapshot().Phase;
            Save();
        }

        private void ShowReady()
        {
            var run = session.GetRunSnapshot();
            if (run == null) return;
            bool coop = run.Mode == GoldMinerMode.Coop;
            var state = run.Current;
            var panel = ModalPanel(coop ? 830 : 610, coop ? 520 : 920);
            SetGameplayElementsVisible(false, "Mine HUD", "Mine world", "Mine touch hint", "Dynamite P1", "Dynamite P2",
                "Player badge P1", "Player badge P2", "左半屏点击出钩", "右半屏点击出钩");
            float w = panel.rect.width, h = panel.rect.height;
            string tip = state.LevelIndex >= 5 ? "留意移动的钻石" : state.LevelIndex >= 3 ? "小鼹鼠会来回跑动" : state.LevelIndex >= 2 ? "钻石价值高，看准再出钩" : "大金块更值钱，回收也更慢";
            if (coop)
            {
                GoldMinerSceneView.Picture(panel, "Ready miners", art.Get("backgrounds/mode-coop-illustration"), 32, 80, w * .39f, 245);
                TextLabel(panel, "第 " + state.LevelIndex + " 关", w * .43f, 26, w * .53f, 78, 48, Forest, TextAnchor.MiddleCenter, true);
                AmountRow(panel, "Ready target", w * .44f, 120, w * .51f, 74, "目标", state.Target, true, UiFactory.Hex("BA3B1F"));
                AmountRow(panel, "Ready funds", w * .44f, 208, w * .51f, 66, "资金", state.Cash);
                TextLabel(panel, tip, w * .42f, 285, w * .55f, 47, 23, UiFactory.Hex("6B421F"), TextAnchor.MiddleCenter, true);
                TextLabel(panel, "P1 点击左半屏 · P2 点击右半屏 · 60 秒合力达标", 30, h - 169, w - 60, 40, 22, Forest, TextAnchor.MiddleCenter);
                ActionButton(panel, "返回模式", 30, h - 99, 175, 64, ShowSelection, false);
                ActionButton(panel, "开始采矿", 225, h - 99, w - 255, 64, () => ContinueRun(true), true);
            }
            else
            {
                var readyArt = FitHeaderArtwork(panel, 450, 300, -150, 152);
                GoldMinerSceneView.Picture(panel, "Ready miner", art.Get("characters/ready-thumbsup-miner-v2"), readyArt.x, readyArt.y, readyArt.width, readyArt.height);
                TextLabel(panel, "第 " + state.LevelIndex + " 关", 25, 160, w - 50, 105, 66, Forest, TextAnchor.MiddleCenter, true);
                AmountRow(panel, "Ready target", 36, 280, w - 72, 98, "目标金额", state.Target, true, UiFactory.Hex("BA3B1F"));
                AmountRow(panel, "Ready funds", 52, 393, w - 104, 78, "当前资金", state.Cash);
                string tipKey = state.LevelIndex >= 3 ? "characters/mole-idle" : state.LevelIndex >= 2 ? "objects/ore_diamond" : "objects/ore_gold_large";
                ArtIcon(panel, "Level feature", tipKey, w * .5f - 100, 492, 200, h - 730);
                if (state.LevelIndex >= 5) ArtIcon(panel, "Moving diamond", "objects/ore_diamond", w * .5f + 15, 532, 80, 70);
                TextLabel(panel, tip, 25, h - 231, w - 50, 44, 26, UiFactory.Hex("6B421F"), TextAnchor.MiddleCenter, true);
                ActionButton(panel, "开始采矿", 48, h - 171, w - 96, 84, () => ContinueRun(true), true);
                ActionButton(panel, "返回模式", w * .5f - 110, h - 70, 220, 48, ShowSelection, false);
            }
            if (!state.ActiveBuffs.IsEmpty)
                TextLabel(panel, "补给已生效 · 重试本关仍保留", 28, h - (coop ? 128 : 274), w - 56, 30, 17, Teal, TextAnchor.MiddleCenter);
            modalBack = ShowSelection;
        }

        private void ShowPause()
        {
            FlushBeforePause();
            var state = session.GetStateSnapshot();
            if (state == null) { ShowSelection(); return; }
            if (state.Phase == GoldMinerPhase.Ready) { ShowReady(); return; }
            if (state.Phase == GoldMinerPhase.Shop) { ConfirmExit(ShowSelection); return; }
            if (state.Phase == GoldMinerPhase.Won || state.Phase == GoldMinerPhase.Failed || state.Phase == GoldMinerPhase.Completed)
            { ShowResult(session.GetRunSnapshot()); return; }
            var panel = ModalPanel(500, 560);
            float w = panel.rect.width, h = panel.rect.height;
            float fit = Mathf.Min(1, h / 560);
            TextLabel(panel, "休息一下", 28, 30 * fit, w - 56, 76 * fit, Mathf.RoundToInt(46 * fit), Forest, TextAnchor.MiddleCenter, true);
            TextLabel(panel, "游戏已暂停", 28, 106 * fit, w - 56, 42 * fit, Mathf.RoundToInt(24 * fit), UiFactory.Hex("96866A"), TextAnchor.MiddleCenter, true);
            ActionButton(panel, "继续游戏", 36, 165 * fit, w - 72, 76 * fit, () => ContinueRun(false), true);
            ActionButton(panel, "重新开始", 36, 257 * fit, w - 72, 66 * fit,
                () => Confirm("重试本关？", "将恢复本关开始时的资金、矿场和道具。\n本次获得的金额会撤销。", "重试本关", RetryLevel, ShowPause), false);
            ActionButton(panel, "返回大厅", 36, 338 * fit, w - 72, 66 * fit, () => ConfirmExit(lobby), false);
            Divider(panel, 36, 424 * fit, w - 72, 1);
            SettingToggle(panel, "声音", 36, 443 * fit, (w - 82) * .5f, soundEnabled(), () => { setSound(!soundEnabled()); SettingsChanged(); ShowPause(); }, fit);
            SettingToggle(panel, "触感", w * .5f + 5, 443 * fit, (w - 82) * .5f, hapticsEnabled(), () => { setHaptics(!hapticsEnabled()); ShowPause(); }, fit);
            TextButton(panel, "玩法说明", w * .5f - 90, 510 * fit, 180, 34 * fit, ShowHelp, Mathf.RoundToInt(19 * fit));
            modalBack = () => ContinueRun(false);
            Save();
        }

        private void SettingToggle(Transform parent, string label, float x, float y, float width, bool enabled, Action action, float scale)
        {
            var button = TextButton(parent, "", x, y, width, 45 * scale, action);
            TextLabel(button.transform, label, 0, 0, width - 78, 45 * scale, Mathf.RoundToInt(23 * scale), Forest, TextAnchor.MiddleCenter, true);
            var track = BorderedPanel(button.transform, label + " switch", width - 75, 6 * scale, 72, 34 * scale,
                enabled ? Forest : UiFactory.Hex("B9AF96"), enabled ? Forest : UiFactory.Hex("B9AF96"), 17 * scale, 0);
            UiFactory.CirclePanel(track.transform, "Switch thumb", enabled ? 72 - 31 * scale : 3 * scale, 3 * scale, 28 * scale, Color.white);
        }

        private void SetGameplayElementsVisible(bool visible, params string[] names)
        {
            if (view == null) return;
            foreach (string name in names)
            {
                var item = view.Find(name);
                if (item != null) item.gameObject.SetActive(visible);
            }
        }

        private void SetHelpBackdropVisible(bool visible)
        {
            SetGameplayElementsVisible(visible, "Mine HUD", "Mine touch hint", "左半屏点击出钩", "右半屏点击出钩");
            if (layout != null && !layout.Cooperative) SetGameplayElementsVisible(visible, "Dynamite P1");
        }

        private void ShowHelp()
        {
            bool coop = session.ActiveMode == GoldMinerMode.Coop;
            // Controls are taught in their actual left/right positions on the mine.
            var ignored = ModalPanel(1, 1, .12f);
            ignored.gameObject.SetActive(false);
            SetHelpBackdropVisible(false);
            var safe = layout.Safe;
            var title = ArtPanel(modal, "Help heading", safe.center.x - Mathf.Min(300, safe.width * .45f), safe.y + 14,
                Mathf.Min(600, safe.width * .9f), 68);
            title.GetComponent<Image>().pixelsPerUnitMultiplier = 3.2f;
            TextLabel(title, coop ? "双人合作 · 玩法说明" : "单人闯关 · 玩法说明", 20, 0, title.rect.width - 40, 68, 31, Forest, TextAnchor.MiddleCenter, true);
            float cardY = layout.Mine.y + 28;
            if (coop)
            {
                HelpControl(modal, safe.x + safe.width * .14f, cardY, safe.width * .31f, "左半屏点击", "P1 出钩", Amber);
                HelpControl(modal, safe.x + safe.width * .55f, cardY, safe.width * .31f, "右半屏点击", "P2 出钩", Teal);
            }
            else HelpControl(modal, safe.x + 75, cardY, safe.width - 150, "看准摆动方向", "点击矿区出钩", Amber);
            var tips = ArtPanel(modal, "Help tips", safe.center.x - Mathf.Min(380, (safe.width - 72) * .5f),
                safe.yMax - (coop ? 280 : 500), Mathf.Min(760, safe.width - 72), coop ? 120 : 245);
            TextLabel(tips, "矿物回到地面才入账 · 重物回收更慢\n炸药可丢弃钩上物品，加快回收\n时间到后，已出发的钩子会完成最后一趟", 22, 16, tips.rect.width - 44, tips.rect.height - 32,
                coop ? 20 : 23, Forest, TextAnchor.MiddleCenter);
            TextLabel(modal, coop ? "共同达成目标，炸药各自使用" : "达成目标后，还能继续赚取资金", safe.x + 30, safe.yMax - 146,
                safe.width - 60, 38, 24, UiFactory.Hex("FFE6A0"), TextAnchor.MiddleCenter, true);
            ActionButton(modal, "知道了", safe.center.x - 170, safe.yMax - 95, 340, 72, ShowPause, true);
            modalBack = ShowPause;
        }

        private void HelpControl(Transform parent, float x, float y, float width, string title, string action, Color playerColor)
        {
            var panel = BorderedPanel(parent, "Help control " + action, x, y, width, 110,
                playerColor == Amber ? UiFactory.Hex("FFF0C9") : UiFactory.Hex("E1F4EC"), playerColor, 28, 2);
            var tap = UiFactory.CirclePanel(panel.transform, "Tap target", 22, 24, 60, playerColor);
            UiFactory.CirclePanel(tap.transform, "Tap ring", 7, 7, 46, Cream);
            UiFactory.CirclePanel(tap.transform, "Tap centre", 15, 15, 30, playerColor);
            TextLabel(panel.transform, title, 99, 14, width - 112, 38, 24, Forest, TextAnchor.MiddleLeft, true);
            TextLabel(panel.transform, action, 99, 54, width - 112, 40, 27, playerColor, TextAnchor.MiddleLeft, true);
        }

        private void ShowResult(GoldMinerRunSnapshot run)
        {
            var state = run.Current;
            bool failed = state.Phase == GoldMinerPhase.Failed;
            bool complete = state.Phase == GoldMinerPhase.Completed;
            bool coop = run.Mode == GoldMinerMode.Coop;
            var panel = ModalPanel(coop ? 740 : 636, coop ? 520 : 920);
            float w = panel.rect.width, h = panel.rect.height;
            float titleY = coop ? 86 : 204;
            float titleHeight = coop ? 70 : 92;
            float titleX = 20, titleWidth = w - 40;
            int titleSize = coop ? 44 : 52;
            if (!failed)
            {
                SetGameplayElementsVisible(false, "Mine HUD", "Mine touch hint", "Dynamite P1", "Dynamite P2", "左半屏点击出钩", "右半屏点击出钩");
                var victoryArt = FitHeaderArtwork(panel, coop ? 300 : 360, coop ? 200 : 260, coop ? -95 : -85, titleY - 8);
                GoldMinerSceneView.Picture(panel, "Victory badge", art.Get("ui/badge-victory"), victoryArt.x, victoryArt.y, victoryArt.width, victoryArt.height);
            }
            else
            {
                float artWidth = coop ? 370 : w - 36;
                float artHeight = artWidth * 1064 / 1478;
                var failureArt = FitHeaderArtwork(panel, artWidth, artHeight, coop ? -90 : -120, coop ? 172 : 302);
                float artScale = failureArt.height / artHeight;
                GoldMinerSceneView.Picture(panel, "Retry miner", art.Get("characters/result-failed-miner-sign-v2"), failureArt.x, failureArt.y, failureArt.width, failureArt.height);
                titleX = failureArt.x + failureArt.width * .11f;
                titleWidth = failureArt.width * .78f;
                titleY = failureArt.y + (coop ? 201 : 325) * artScale;
                titleHeight = (coop ? 46 : 78) * artScale;
                titleSize = Mathf.FloorToInt((coop ? 31 : 48) * artScale);
            }
            var heading = TextLabel(panel, failed ? "再试一次吧" : complete ? "满载而归！" : coop ? "合作过关！" : "顺利过关！", titleX, titleY,
                titleWidth, titleHeight, titleSize, failed ? UiFactory.Hex("FFF0BC") : Forest, TextAnchor.MiddleCenter, true);
            heading.name = "Result heading";
            if (failed)
            {
                var outline = heading.gameObject.AddComponent<Outline>();
                outline.effectColor = UiFactory.Hex("5E3416"); outline.effectDistance = new Vector2(2, -2);
            }
            float subtitleY = failed ? (coop ? 180 : 310) : titleY + (coop ? 65 : 90);
            TextLabel(panel, failed ? "这次还差一点" : "—  第 " + state.LevelIndex + " 关完成  —", 28, subtitleY, w - 56, failed && coop ? 32 : 40, 25,
                UiFactory.Hex("7E643D"), TextAnchor.MiddleCenter, true);
            float statsY = coop ? (failed ? 225 : 198) : 360;
            var stats = BorderedPanel(panel, "Result amounts", 38, statsY, w - 76, coop ? 86 : 138,
                UiFactory.Hex("F7EACF"), UiFactory.Hex("DEC99E"), 23, 2);
            float sw = stats.rectTransform.rect.width, sh = stats.rectTransform.rect.height;
            ArtIcon(stats.transform, "Result coin", "objects/ui_gold_coin", 16, sh * .24f, sh * .52f, sh * .52f);
            ArtIcon(stats.transform, "Result target", "ui/hud-icon-atlas", sw * .51f + 10, sh * .24f, sh * .49f, sh * .52f, "target_flag");
            TextLabel(stats.transform, coop ? "团队资金" : "本局资金", sh * .75f, sh * .1f, sw * .5f - sh * .8f, sh * .32f, 20, Forest, TextAnchor.MiddleLeft, true);
            TextLabel(stats.transform, state.Cash.ToString(), sh * .75f, sh * .42f, sw * .5f - sh * .8f, sh * .5f, 35, Forest, TextAnchor.MiddleLeft, true);
            Divider(stats.transform, sw * .5f, 18, 1, sh - 36);
            TextLabel(stats.transform, "过关目标", sw * .51f + sh * .66f, sh * .1f, sw * .49f - sh * .72f, sh * .32f, 20, Forest, TextAnchor.MiddleLeft, true);
            TextLabel(stats.transform, state.Target.ToString(), sw * .51f + sh * .66f, sh * .42f, sw * .49f - sh * .72f, sh * .5f, 35, Forest, TextAnchor.MiddleLeft, true);
            if (failed)
                TextLabel(panel, "还差  " + Math.Max(0, state.Target - state.Cash), 38, coop ? 323 : 522, w - 76, coop ? 62 : 88, 40,
                    UiFactory.Hex("B97508"), TextAnchor.MiddleCenter, true);
            else if (coop)
            {
                TextLabel(panel, "—  本关贡献  —", 38, 291, w - 76, 34, 21, Forest, TextAnchor.MiddleCenter, true);
                Contribution(panel, 0, 38, 334, (w - 88) * .5f, state.Players[0].LevelCollected);
                Contribution(panel, 1, w * .5f + 6, 334, (w - 88) * .5f, state.Players[1].LevelCollected);
            }
            else TextLabel(panel, "本关收获  +" + state.Players[0].LevelCollected, 38, 525, w - 76, 72, 31, Forest, TextAnchor.MiddleCenter, true);
            Action next = () =>
            {
                if (failed) RetryLevel();
                else if (complete) ChooseMode(run.Mode, true);
                else if (Apply(() => session.PrepareEnterShop())) RebuildRun();
                else ShowOperationError(() => ShowResult(session.GetRunSnapshot()));
            };
            string label = failed ? "重新挑战" : complete ? "再开一轮" : "进入商店";
            if (coop)
            {
                ActionButton(panel, "返回大厅", 38, h - 90, (w - 90) * .5f, 64, lobby, false);
                ActionButton(panel, label, w * .5f + 7, h - 90, (w - 90) * .5f, 64, next, true);
            }
            else
            {
                ActionButton(panel, label, 68, h - 232, w - 136, 95, next, true);
                ActionButton(panel, "返回大厅", 68, h - 117, w - 136, 72, lobby, false);
            }
            modalBack = ShowSelection;
        }

        private void Contribution(Transform parent, int player, float x, float y, float width, long value)
        {
            var panel = BorderedPanel(parent, "Contribution P" + (player + 1), x, y, width, 62,
                player == 0 ? UiFactory.Hex("FFE5AC") : UiFactory.Hex("CFEEEA"), player == 0 ? Amber : Teal, 17, 2);
            TextLabel(panel.transform, "P" + (player + 1), 12, 0, 56, 62, 26, player == 0 ? UiFactory.Hex("91500C") : Teal, TextAnchor.MiddleCenter, true);
            ArtIcon(panel.transform, "Contribution coin", "objects/ui_gold_coin", 75, 8, 44, 44);
            TextLabel(panel.transform, "+" + value, 130, 0, width - 145, 62, 30, Forest, TextAnchor.MiddleLeft, true);
        }

        private void RetryLevel()
        {
            if (Apply(() => session.PrepareRetryLevel())) RebuildRun();
            else ShowOperationError(RetryLevel);
        }

        private void Confirm(string title, string detail, string actionLabel, Action action, Action cancel)
        {
            var panel = Dialog(title, detail, 440, 110);
            float w = panel.rect.width, h = panel.rect.height;
            ActionButton(panel, "取消", 38, h - 175, w - 76, 64, () => { CloseModal(); cancel(); }, true);
            ActionButton(panel, actionLabel, 38, h - 96, w - 76, 64, () => { CloseModal(); action(); }, false);
            modalBack = cancel;
        }

        private void ConfirmExit(Action destination)
        {
            bool toLobby = destination == lobby;
            var panel = ModalPanel(layout.Cooperative ? 650 : 636, 440);
            float w = panel.rect.width, h = panel.rect.height;
            TextLabel(panel, toLobby ? "返回大厅？" : "返回模式选择？", 28, 28, w - 56, 82, 48, Forest, TextAnchor.MiddleCenter, true);
            TextLabel(panel, "本局进度会自动保存", 32, 130, w - 64, 52, 29, Forest, TextAnchor.MiddleCenter);
            ActionButton(panel, "继续游戏", 38, h - 208, w - 76, 86, () => { CloseModal(); RebuildRun(); }, true);
            ActionButton(panel, toLobby ? "返回大厅" : "返回模式", 38, h - 104, w - 76, 72, () =>
            {
                Save();
                if (SaveError != null) { ShowOperationError(() => ConfirmExit(destination)); return; }
                destination();
            }, false);
            modalBack = RebuildRun;
        }

        private void ShowWaiting(string text)
        {
            NewView("bg-mode-select", selecting ? GoldMinerMode.Solo : session.ActiveMode);
            TextLabel(view, text, layout.Safe.x + 30, layout.Safe.center.y - 40, layout.Safe.width - 60, 80,
                32, Forest, TextAnchor.MiddleCenter, true);
        }

        private void ShowOperationError(Action retry)
        {
            var panel = Dialog("暂时无法继续", message ?? "屏幕切换尚未完成，请重试。", 410);
            float w = panel.rect.width;
            ActionButton(panel, "返回模式", 28, panel.rect.height - 82, 170, 58, ShowSelection, false);
            ActionButton(panel, "重试", 214, panel.rect.height - 82, w - 242, 58, retry, true);
            modalBack = ShowSelection;
        }

        private void ShowSaveFailure()
        {
            ShowOperationError(() =>
            {
                if (Apply(() => session.PrepareSettlement())) RebuildRun();
                else ShowSaveFailure();
            });
        }

        private void ShowRecovery(Action resume)
        {
            Confirm("进度文件无法读取", "当前存档及备份均不可用。\n可以先归档原文件，再建立新进度。\n其他游戏的存档不会改变。", "归档并重建", () =>
            {
                string error;
                if (!store.TryArchiveInvalid(out error))
                {
                    Debug.LogWarning("Gold Miner archive: " + error);
                    SetMessage("原进度无法归档，请检查设备剩余空间后重试。");
                    ShowOperationError(() => ShowRecovery(resume));
                    return;
                }
                session = new GoldMinerSession();
                message = null;
                SaveError = null;
                resume();
            }, BuildSelection);
        }
    }
}
