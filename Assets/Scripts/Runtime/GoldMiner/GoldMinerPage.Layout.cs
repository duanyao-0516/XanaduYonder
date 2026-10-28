using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.GoldMiner;

namespace XanaduYonder.Runtime.GoldMiner
{
    internal sealed partial class GoldMinerPage
    {
        private static readonly Color Forest = UiFactory.Hex("123C30");
        private static readonly Color Cream = UiFactory.Hex("F4EBD3");
        private static readonly Color Amber = UiFactory.Hex("E99A22");
        private static readonly Color Teal = UiFactory.Hex("238F8A");

        private void PrepareCanvas(bool landscape)
        {
            scaler.referenceResolution = landscape ? new Vector2(1320, 720) : new Vector2(720, 1320);
            Canvas.ForceUpdateCanvases();
            previousWindow = new Vector2(Screen.width, Screen.height);
            previousSafe = Screen.safeArea;
        }

        private void ClearView()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            diagnostics?.Finish();
            diagnostics = null;
#endif
            input?.ResetGeneration();
            input = null;
            scene = null;
            notice = null;
            noticePanel = null;
            modal = null;
            modalBack = null;
            bombButtons = null;
            if (view != null)
            {
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
                view = null;
            }
            art?.ReleaseView();
            if (visible && isActiveAndEnabled && releaseAssets == null)
                releaseAssets = StartCoroutine(ReleasePreviousViewAssets());
        }

        private void NewView(string backgroundKey, GoldMinerMode mode)
        {
            ClearView();
            PrepareCanvas(mode == GoldMinerMode.Coop);
            view = UiFactory.Rect(canvasRoot, "Gold Miner content", 0, 0, 1, 1);
            UiFactory.Stretch(view);
            layout = new GoldMinerLayout(canvasRoot.rect.width, canvasRoot.rect.height,
                GoldMinerLayout.WindowSafeArea(canvasRoot), mode);
            var sprite = art.Get("backgrounds/" + backgroundKey);
            if (backgroundKey == "bg-solo" || backgroundKey == "bg-coop")
            {
                // Keep the painted ground at the hook origins on tall and wide phones.
                GoldMinerSceneView.Picture(view, "Sky extension", art.Get("backgrounds/layer-sky"),
                    0, 0, layout.Width, layout.Mine.y, false);
                float groundFraction = backgroundKey == "bg-solo" ? .165f : .223f;
                float height = Mathf.Max(layout.Width * sprite.rect.height / sprite.rect.width,
                    (layout.Height - layout.Mine.y) / (1 - groundFraction));
                float width = height * sprite.rect.width / sprite.rect.height;
                GoldMinerSceneView.Picture(view, "Gold Miner background", sprite,
                    (layout.Width - width) * .5f, layout.Mine.y - height * groundFraction, width, height, false);
                return;
            }
            var background = GoldMinerSceneView.Picture(view, "Gold Miner background", sprite,
                0, 0, layout.Width, layout.Height, false);
            // Cover preserves the painted proportions; only decoration can leave the screen.
            var fit = background.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = background.sprite.rect.width / background.sprite.rect.height;
        }

        private void ShowSelection()
        {
            FlushBeforePause();
            Save();
            selecting = true;
            session.Suspend(GoldMinerSuspendReason.NeedsExplicitContinue);
            session.Suspend(GoldMinerSuspendReason.OrientationPending);
            sound?.StopAll();
            PrepareCanvas(false);
            ShowWaiting("返回模式选择…");
            orientation.Request(false, generation =>
            {
                if (!visible || !selecting) return;
                session.Resume(GoldMinerSuspendReason.OrientationPending);
                BuildSelection();
            }, generation => ShowOperationError(ShowSelection));
        }

        private void BuildSelection()
        {
            NewView("bg-mode-select", GoldMinerMode.Solo);
            var safe = layout.Safe;
            TextButton(view, "‹ 大厅", safe.x + 24, safe.y + 16, 132, 58, lobby);
            TextLabel(view, "XANADU", safe.xMax - 154, safe.y + 20, 130, 36, 17, Forest, TextAnchor.MiddleRight, true);
            GoldMinerSceneView.Picture(view, "黄金矿工", art.Get("ui/gold-miner-wordmark-v2"),
                safe.center.x - 280, safe.y + safe.height * .048f, 560, 170);
            TextLabel(view, "—  选择玩法  —", safe.x + 30, safe.y + safe.height * .181f, safe.width - 60, 45, 24, Forest, TextAnchor.MiddleCenter, true);
            // The approved composition gives the mining illustration its own band above the cards.
            float heroY = safe.y + safe.height * .222f;
            float heroHeight = safe.height * .258f;
            GoldMinerSceneView.Picture(view, "Selection hero", art.Get("backgrounds/mode-solo-illustration"),
                safe.x + safe.width * .22f, heroY, safe.width * .56f, heroHeight);
            GoldMinerSceneView.Picture(view, "Hero diamond", art.Get("objects/ore_diamond"),
                safe.x + safe.width * .13f, heroY + heroHeight * .66f, 75, 75);
            GoldMinerSceneView.Picture(view, "Hero mole", art.Get("characters/mole-idle"),
                safe.x + safe.width * .7f, heroY + heroHeight * .7f, 105, 75);
            float cardHeight = safe.height * .208f;
            ModeCard(GoldMinerMode.Solo, safe.y + safe.height * .49f, cardHeight);
            ModeCard(GoldMinerMode.Coop, safe.y + safe.height * .711f, cardHeight);
            TextLabel(view, "看准时机，点击出钩", safe.x + 30, safe.yMax - 68, safe.width - 60, 38, 21,
                Forest, TextAnchor.MiddleCenter);
            Notice(safe.yMax - 24);
        }

        private void ModeCard(GoldMinerMode mode, float y, float height)
        {
            var safe = layout.Safe;
            bool coop = mode == GoldMinerMode.Coop;
            var panel = ArtPanel(view, "Mode " + mode, safe.x + 28, y, safe.width - 56, height);
            if (coop) panel.GetComponent<Image>().color = UiFactory.Hex("E6EBD9");
            var root = session.GetSnapshot();
            var run = root.GetRun(mode);
            bool hasRun = run != null && run.Current.Phase != GoldMinerPhase.Completed;
            float width = panel.rect.width;
            float textX = width * .42f;
            ArtIcon(panel, "Mode illustration", "backgrounds/mode-" + (coop ? "coop" : "solo") + "-illustration",
                20, height * .14f, width * .39f, height * .66f);
            TextLabel(panel, coop ? "双人合作" : "单人闯关", textX, 17, width - textX - 20, 55, 34, Forest, TextAnchor.MiddleLeft, true);
            var badge = BorderedPanel(panel, "Mode badge", textX, 76, 134, 30, coop ? UiFactory.Hex("D6E8D9") : UiFactory.Hex("FFE3A0"),
                coop ? Teal : Amber, 15, 1.5f);
            TextLabel(badge.transform, coop ? "2人 · 横屏" : "1人 · 竖屏", 5, 0, 124, 30, 17, Forest, TextAnchor.MiddleCenter, true);
            TextLabel(panel, coop ? "左右各自出钩，合力闯关" : "看准时机，挑战你的眼力", textX, 110, width - textX - 22, 40, 18,
                UiFactory.Hex("716B59"));
            float bottom = height - 78;
            if (hasRun)
            {
                TextLabel(panel, "第 " + run.Current.LevelIndex + " 关 · 资金 " + run.Current.Cash, textX, 150,
                    width - textX - 22, Mathf.Max(26, bottom - 150), 17, Forest);
                TextButton(panel, "重新开始", 24, height - 49, textX - 45, 32,
                    () => Confirm("重新开始挑战？", "只替换当前模式的挑战，历史纪录会保留。", "重新开始", () => ChooseMode(mode, true), BuildSelection), 17);
            }
            ActionButton(panel, hasRun ? "继续游戏" : "开始游戏", textX, bottom, width - textX - 24, 58,
                () => ChooseMode(mode, !hasRun), true);
        }

        private void RebuildRun()
        {
            var run = session.GetRunSnapshot();
            if (run == null) { ShowSelection(); return; }
            renderedPhase = run.Current.Phase;
            if (renderedPhase == GoldMinerPhase.Shop) { BuildShop(run); return; }
            BuildGameplay(run);
            if (renderedPhase == GoldMinerPhase.Ready) ShowReady();
            else if (renderedPhase == GoldMinerPhase.Won || renderedPhase == GoldMinerPhase.Failed || renderedPhase == GoldMinerPhase.Completed) ShowResult(run);
            else if (session.IsSuspended) ShowPause();
        }

        private void BuildGameplay(GoldMinerRunSnapshot run)
        {
            NewView(run.Mode == GoldMinerMode.Coop ? "bg-coop" : "bg-solo", run.Mode);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            diagnostics = new GoldMinerDiagnostics(run.Mode.ToString());
#endif
            var inputArea = UiFactory.Panel(view, "Mine input", 0, 0, layout.Width, layout.Height, Color.clear, false);
            inputArea.raycastTarget = true;
            input = inputArea.gameObject.AddComponent<GoldMinerInputRouter>();
            input.Initialize(run.Mode == GoldMinerMode.Coop, () => CanAct, Launch, Dynamite);
            var stage = UiFactory.Rect(view, "Mine world", 0, 0, layout.Width, layout.Height);
            scene = new GoldMinerSceneView(stage, art, layout, run.Mode);
            scene.Build(run);
            var hud = BorderedPanel(view, "Mine HUD", layout.Hud.x, layout.Hud.y, layout.Hud.width, layout.Hud.height,
                UiFactory.Hex("FFF9E9"), UiFactory.Hex("C5B591"), 25, 1.5f).rectTransform;
            hud.GetComponent<Image>().raycastTarget = true;
            float hudWidth = layout.Hud.width, hudHeight = layout.Hud.height;
            float modeWidth = hudWidth * (layout.Cooperative ? .29f : .245f);
            TextLabel(hud, layout.Cooperative ? "双人合作  |  第 " + run.Current.LevelIndex + " 关" : "单人闯关\n第 " + run.Current.LevelIndex + " 关",
                16, 0, modeWidth - 26, hudHeight, layout.Cooperative ? 26 : 23, Forest, TextAnchor.MiddleCenter, true);
            Divider(hud, modeWidth, 17, 1.5f, hudHeight - 34);
            float statsWidth = hudWidth - modeWidth - 76;
            float group = statsWidth / 3;
            cashText = Stat(hud, "objects/ui_gold_coin", "资金", modeWidth + 10, 4, group - 10, hudHeight - 8);
            Divider(hud, modeWidth + group, 17, 1.5f, hudHeight - 34);
            targetText = Stat(hud, "ui/hud-icon-atlas", "目标", modeWidth + group + 10, 4, group - 10, hudHeight - 8, "target_flag");
            Divider(hud, modeWidth + group * 2, 17, 1.5f, hudHeight - 34);
            timeText = Stat(hud, "ui/hud-icon-atlas", "", modeWidth + group * 2 + 10, 4, group - 6, hudHeight - 8, "clock");
            var pause = UiFactory.CirclePanel(hud, "Pause button", hudWidth - 65, (hudHeight - 50) * .5f, 50,
                layout.Cooperative ? UiFactory.Hex("C2E6DF") : Forest);
            ConfigureButton(pause, ShowPause);
            if (layout.Cooperative)
                ArtIcon(pause.transform, "Pause icon", "ui/hud-icon-atlas", 15, 12, 20, 26, "pause");
            else
            {
                var pauseIcon = UiFactory.Rect(pause.transform, "Pause icon", 15, 12, 20, 26);
                BorderedPanel(pauseIcon, "Left pause bar", 0, 0, 7, 26, Cream, Cream, 3.5f, 0);
                BorderedPanel(pauseIcon, "Right pause bar", 13, 0, 7, 26, Cream, Cream, 3.5f, 0);
            }
            var safe = layout.Safe;
            int players = GoldMinerRules.PlayerCount(run.Mode);
            bombButtons = new Button[players];
            bombLabels = new Text[players];
            for (int i = 0; i < players; i++)
            {
                float width = layout.Cooperative ? 218 : 222;
                float x = layout.Cooperative && i == 0 ? safe.x + 22 : safe.xMax - width - 22;
                Color edge = layout.Cooperative ? (i == 0 ? Amber : Teal) : UiFactory.Hex("B4A387");
                Color fill = layout.Cooperative ? (i == 0 ? UiFactory.Hex("FFE6AF") : UiFactory.Hex("D5F0EE")) : UiFactory.Hex("FFF9E9");
                var image = BorderedPanel(view, "Dynamite P" + (i + 1), x, safe.yMax - 88, width, 64, fill, edge, 30, 2);
                var button = ConfigureButton(image, () => { });
                button.onClick.RemoveAllListeners();
                input.BindDynamite(button.gameObject, i);
                ArtIcon(button.transform, "Dynamite", "objects/item_dynamite", 12, 4, 55, 55);
                bombLabels[i] = TextLabel(button.transform, "", 72, 0, width - 80, 64, 22, Forest, TextAnchor.MiddleCenter, true);
                bombButtons[i] = button;
                var origin = layout.World(GoldMinerRules.OriginX(run.Mode, i), 0);
                if (layout.Cooperative)
                {
                    var badge = BorderedPanel(view, "Player badge P" + (i + 1), origin.x + (i == 0 ? -149 : 96), layout.Mine.y - 85,
                        56, 40, i == 0 ? Amber : Teal, Cream, 16, 2);
                    var label = TextLabel(badge.transform, "P" + (i + 1), 0, 0, 56, 40, 24, Color.white, TextAnchor.MiddleCenter, true);
                    label.gameObject.AddComponent<Outline>().effectColor = i == 0 ? UiFactory.Hex("8C4B0B") : Forest;
                }
            }
            if (layout.Cooperative)
            {
                TextLabel(view, "左半屏点击出钩", safe.x + 260, safe.yMax - 87, (safe.width - 560) * .5f, 62, 19, Amber, TextAnchor.MiddleLeft, true);
                TextLabel(view, "右半屏点击出钩", safe.center.x + 30, safe.yMax - 87, (safe.width - 560) * .5f, 62, 19, Teal, TextAnchor.MiddleRight, true);
                contributionText = TextLabel(view, "", safe.center.x - 150, safe.yMax - 44, 300, 21, 14, Cream, TextAnchor.MiddleCenter);
            }
            else
            {
                var hint = BorderedPanel(view, "Mine touch hint", safe.x + 24, safe.yMax - 76, Mathf.Min(320, safe.width - 285), 42,
                    new Color(.12f, .08f, .035f, .72f), UiFactory.Hex("9B8051"), 21, 1);
                contributionText = TextLabel(hint.transform, "", 8, 0, hint.rectTransform.rect.width - 16, 42, 18, Cream, TextAnchor.MiddleCenter);
            }
            Notice(safe.yMax - 22);
            RefreshHud(run.Current);
        }

        private Text Stat(RectTransform parent, string resource, string label, float x, float y, float width, float height, string slice = null)
        {
            float iconSize = layout.Cooperative ? 35 : 31;
            ArtIcon(parent, label + " icon", resource, x, y + (height - iconSize) * .5f, iconSize, iconSize, slice);
            if (layout.Cooperative)
            {
                float labelWidth = string.IsNullOrEmpty(label) ? 0 : 42;
                if (labelWidth > 0) TextLabel(parent, label, x + iconSize + 6, y, labelWidth, height, 20, Forest, TextAnchor.MiddleLeft, true);
                return TextLabel(parent, "", x + iconSize + 8 + labelWidth, y, width - iconSize - labelWidth - 8, height, 28, Forest, TextAnchor.MiddleLeft, true);
            }
            if (string.IsNullOrEmpty(label)) return TextLabel(parent, "", x + iconSize + 4, y, width - iconSize - 4, height, 22, Forest, TextAnchor.MiddleLeft, true);
            TextLabel(parent, label, x + iconSize + 5, y + 1, width - iconSize - 5, 26, 16, Forest);
            return TextLabel(parent, "", x + iconSize + 5, y + 25, width - iconSize - 5, height - 25, 23, Forest, TextAnchor.MiddleLeft, true);
        }

        private void RefreshHud(GoldMinerState state)
        {
            if (cashText != null) cashText.text = state.Cash.ToString();
            if (targetText != null) targetText.text = state.Target.ToString();
            if (timeText != null)
            {
                int seconds = (state.RemainingTicks + 59) / 60;
                timeText.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
                timeText.color = seconds <= 10 ? UiFactory.Hex("AC3E28") : Forest;
                timeText.rectTransform.localScale = Vector3.one * (seconds <= 5
                    ? 1 + Mathf.Sin(state.SimulationTick * Mathf.PI / 30) * .05f : 1);
            }
            if (bombButtons != null)
            {
                for (int i = 0; i < bombButtons.Length; i++)
                {
                    bombLabels[i].text = (layout.Cooperative ? "P" + (i + 1) + " " : "") + "炸药 ×" + state.Players[i].Bombs;
                    bombButtons[i].interactable = CanAct && state.Players[i].Bombs > 0 && state.Hooks[i].State == GoldMinerHookState.RetractingLoaded;
                }
            }
            if (contributionText != null)
                contributionText.text = state.Phase == GoldMinerPhase.Draining ? "时间到 · 等待钩子收回" : layout.Cooperative ? "" : "— 点击矿区出钩 —";
        }


        private void BuildShop(GoldMinerRunSnapshot run)
        {
            NewView(run.Mode == GoldMinerMode.Coop ? "bg-shop-landscape" : "bg-shop-portrait", run.Mode);
            var safe = layout.Safe;
            long nextTarget = GoldMinerRules.Target(run.Mode, run.Current.LevelIndex + 1);
            if (layout.Cooperative)
            {
                var sign = ArtPanel(view, "Shop title", safe.x + 100, safe.y + 34, 360, 88);
                sign.GetComponent<Image>().pixelsPerUnitMultiplier = 3.2f;
                TextLabel(sign, "合作商店", 18, 0, 324, 88, 42, Forest, TextAnchor.MiddleCenter, true);
                var wallet = BorderedPanel(view, "Shop wallet", safe.x + 506, safe.y + 44, safe.width - 610, 68,
                    UiFactory.Hex("FFF9E9"), UiFactory.Hex("B39C73"), 32, 2).rectTransform;
                ArtIcon(wallet, "Wallet coin", "objects/ui_gold_coin", 16, 10, 48, 48);
                TextLabel(wallet, "公共资金  " + run.Current.Cash, 70, 0, wallet.rect.width * .48f - 70, 68, 25, Forest, TextAnchor.MiddleLeft, true);
                Divider(wallet, wallet.rect.width * .5f, 15, 1, 38);
                ArtIcon(wallet, "Next target flag", "ui/hud-icon-atlas", wallet.rect.width * .5f + 17, 16, 32, 36, "target_flag");
                TextLabel(wallet, "下一关目标  " + nextTarget, wallet.rect.width * .5f + 58, 0, wallet.rect.width * .5f - 72, 68, 24, Forest, TextAnchor.MiddleLeft, true);
                float gap = 12, width = (safe.width - 168 - gap * 4) / 5;
                float top = safe.y + 157, height = safe.height - 285;
                for (int i = 0; i < 5; i++) ShopCard(run, i, safe.x + 84 + i * (width + gap), top, width, height);
                TextLabel(view, "增益对两人共同生效", safe.x + 240, safe.yMax - 119, safe.width - 480, 33, 21,
                    UiFactory.Hex("845023"), TextAnchor.MiddleCenter, true);
                ActionButton(view, "‹  模式选择", safe.x + 28, safe.yMax - 86, 250, 62, () => ConfirmExit(ShowSelection), false);
                ActionButton(view, "一起进入下一关  ›", safe.xMax - 388, safe.yMax - 95, 360, 70, NextLevel, true);
            }
            else
            {
                float headerY = safe.y + safe.height * .078f;
                var header = ArtPanel(view, "Shop title", safe.x + 116, headerY, safe.width - 232, 94);
                header.GetComponent<Image>().pixelsPerUnitMultiplier = 2.8f;
                var back = ActionButton(header, "‹", 18, 19, 56, 56, () => ConfirmExit(ShowSelection), true);
                TextLabel(header, "矿工商店", 94, 9, 170, 46, 31, Forest, TextAnchor.MiddleLeft, true);
                TextLabel(header, "下一关目标 " + nextTarget, 94, 52, 186, 30, 17, UiFactory.Hex("817459"));
                Divider(header, header.rect.width * .62f, 20, 1, 54);
                ArtIcon(header, "Wallet coin", "objects/ui_gold_coin", header.rect.width * .65f, 28, 37, 37);
                TextLabel(header, "资金\n" + run.Current.Cash, header.rect.width * .65f + 43, 13, header.rect.width * .35f - 51, 68, 22, Forest, TextAnchor.MiddleLeft, true);
                float top = safe.y + safe.height * .195f;
                float gridHeight = safe.height * .66f;
                float rowHeight = (gridHeight - 30) * .375f;
                float cardWidth = (safe.width - 104) * .5f;
                for (int i = 0; i < 4; i++)
                    ShopCard(run, i, safe.x + 40 + (i % 2) * (cardWidth + 24), top + (i / 2) * (rowHeight + 15), cardWidth, rowHeight);
                ShopCard(run, 4, safe.x + 130, top + (rowHeight + 15) * 2, safe.width - 260, (gridHeight - 30) * .25f);
                ActionButton(view, "进入下一关", safe.x + 56, safe.yMax - 132, safe.width - 112, 84, NextLevel, true);
                TextLabel(view, "购买会扣除当前资金", safe.x + 48, safe.yMax - 49, safe.width - 96, 24, 17, Cream, TextAnchor.MiddleCenter);
            }
            Notice(safe.yMax - 22);
        }

        private void ShopCard(GoldMinerRunSnapshot run, int index, float x, float y, float width, float height)
        {
            string[] ids = { "炸药", "力量药水", "幸运四叶草", "石头收藏书", "钻石油" };
            string[] names = { "炸药", "大力药", "幸运草", "石头收藏书", "钻石增值剂" };
            string[] files = { "item_dynamite", "item_strength", "item_lucky_clover", "item_rock_book", "item_diamond_polish" };
            string[] descriptions = { "炸掉钩上物品", "下关回收更快", "下关福袋更幸运", "下关石头价值 ×3", "下关钻石更值钱" };
            var offer = run.Current.Shop.Offers[index];
            var panel = ArtPanel(view, ids[index], x, y, width, height);
            // Shop cards have tighter corners than the large glowing dialog frame.
            panel.GetComponent<Image>().pixelsPerUnitMultiplier = 2.8f;
            bool compact = !layout.Cooperative && index == 4;
            float buttonHeight = layout.Cooperative ? 52 : 54;
            float buttonY = height - buttonHeight - 15;
            float priceY = buttonY - 45;
            if (compact)
            {
                ArtIcon(panel, names[index] + " icon", "objects/" + files[index], 22, 13, width * .38f, priceY + 24);
                TextLabel(panel, names[index], width * .46f, priceY - 76, width * .5f - 10, 36, 24, Forest, TextAnchor.MiddleLeft, true);
                TextLabel(panel, descriptions[index], width * .46f, priceY - 34, width * .5f - 10, 26, 17, UiFactory.Hex("736A52"));
            }
            else
            {
                float iconTop = 18;
                float iconHeight = Mathf.Max(58, priceY - 108);
                ArtIcon(panel, names[index] + " icon", "objects/" + files[index], 26, iconTop, width - 52, iconHeight);
                TextLabel(panel, names[index], 10, iconTop + iconHeight + 5, width - 20, 42, 26, Forest, TextAnchor.MiddleCenter, true);
                TextLabel(panel, descriptions[index], 10, priceY - 40, width - 20, 34, 19, UiFactory.Hex("736A52"), TextAnchor.MiddleCenter);
            }
            float priceX = compact ? width * .46f : 16;
            float priceWidth = compact ? width * .5f - 10 : width - 32;
            var price = BorderedPanel(panel, "Price row", priceX, priceY, priceWidth, 36,
                UiFactory.Hex("F4DEB0"), UiFactory.Hex("E8CDA0"), 14, 0);
            ArtIcon(price.transform, "Price coin", "objects/ui_gold_coin", priceWidth * .5f - 57, 1, 34, 34);
            TextLabel(price.transform, offer.Remaining == 0 ? "已售罄" : offer.Price.ToString(), priceWidth * .5f - 15, 0,
                priceWidth * .5f + 6, 36, 24, Forest, TextAnchor.MiddleLeft, true);
            bool affordable = offer.Remaining > 0 && run.Current.Cash >= offer.Price;
            if (index == 0 && run.Mode == GoldMinerMode.Coop)
            {
                float bw = (width - 32) * .5f;
                for (int playerIndex = 0; playerIndex < 2; playerIndex++)
                {
                    int player = playerIndex;
                    var button = ActionButton(panel, "给 P" + (player + 1), 12 + player * (bw + 8), buttonY, bw, buttonHeight,
                        () => Purchase(offer.ItemId, player), true, player == 0 ? "button-p1-amber" : "button-p2-teal");
                    button.interactable = affordable && run.Current.Players[player].Bombs < GoldMinerRules.MaxBombs;
                }
            }
            else
            {
                bool full = index == 0 && run.Current.Players[0].Bombs >= GoldMinerRules.MaxBombs;
                string label = offer.Remaining == 0 ? "已购买" : full ? "库存已满" : affordable ? "购买" : "资金不足";
                var button = ActionButton(panel, label, 16, buttonY, width - 32, buttonHeight, () => Purchase(offer.ItemId, 0), true);
                button.interactable = affordable && !full;
            }
        }

        private void Purchase(GoldMinerItem item, int recipient)
        {
            if (Apply(() => session.PreparePurchase(item, recipient)))
            {
                sound?.Play("purchase");
                RebuildRun();
            }
        }

        private RectTransform ArtPanel(Transform parent, string name, float x, float y, float width, float height)
        {
            var image = GoldMinerSceneView.Picture(parent, name, art.Get("ui/panel-ivory"), x, y, width, height, false);
            image.type = Image.Type.Sliced;
            // Borders are authored in source pixels, even when the imported texture is reduced.
            image.pixelsPerUnitMultiplier = Mathf.Max(2, 684f / height);
            return image.rectTransform;
        }

        private Image BorderedPanel(Transform parent, string name, float x, float y, float width, float height,
            Color fill, Color border, float radius, float thickness)
        {
            var outer = UiFactory.Panel(parent, name, x, y, width, height, border);
            outer.sprite = RoundedUiSprite;
            outer.pixelsPerUnitMultiplier = 48 / Mathf.Max(1, radius);
            if (thickness <= 0) { outer.color = fill; return outer; }
            var inner = UiFactory.Panel(outer.transform, "Inset", thickness, thickness, width - thickness * 2, height - thickness * 2, fill);
            inner.sprite = RoundedUiSprite;
            inner.pixelsPerUnitMultiplier = 48 / Mathf.Max(1, radius - thickness);
            return outer;
        }

        private static Sprite roundedUiSprite;
        private static Sprite RoundedUiSprite
        {
            get
            {
                if (roundedUiSprite != null) return roundedUiSprite;
                // Higher-resolution mask keeps the large cream outlined pills smooth.
                const int size = 128; const float radius = 48;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "Gold Miner rounded UI mask";
                texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius));
                    float dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius));
                    float distance = new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - distance + .5f));
                }
                texture.SetPixels(pixels); texture.Apply();
                roundedUiSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
                    SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                return roundedUiSprite;
            }
        }

        private void Divider(Transform parent, float x, float y, float width, float height)
        {
            UiFactory.Panel(parent, "Separator", x, y, width, height, UiFactory.Hex("C7B990"), false);
        }

        private Image ArtIcon(Transform parent, string name, string key, float x, float y, float width, float height, string slice = null)
        {
            var sprite = art.Get(key, slice);
            var bounds = slice == null ? GoldMinerArtwork.VisibleBoundsNormalized(key) : new Rect(0, 0, 1, 1);
            // Atlas cells retain substantial transparent padding. Fit visible artwork,
            // otherwise a 26-unit pause icon becomes a pair of unreadable tiny dots.
            if (key == "ui/hud-icon-atlas")
            {
                if (slice == "clock") bounds = new Rect(.2121212f, .0558214f, .6475279f, .7703349f);
                else if (slice == "target_flag") bounds = new Rect(.1866029f, .0622010f, .6650718f, .7240829f);
                else if (slice == "pause") bounds = new Rect(.2583732f, .1770335f, .5757576f, .6666667f);
                else if (slice == "back") bounds = new Rect(.1212121f, .1977671f, .7496013f, .6044657f);
            }
            float factor = Mathf.Min(width / (sprite.rect.width * bounds.width), height / (sprite.rect.height * bounds.height));
            float fullWidth = sprite.rect.width * factor, fullHeight = sprite.rect.height * factor;
            return GoldMinerSceneView.Picture(parent, name, sprite,
                x + (width - fullWidth * bounds.width) * .5f - fullWidth * bounds.x,
                y + (height - fullHeight * bounds.height) * .5f - fullHeight * (1 - bounds.yMax), fullWidth, fullHeight);
        }

        private Button ConfigureButton(Image image, Action action)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(.88f, .83f, .73f);
            colors.disabledColor = new Color(.72f, .72f, .72f, .75f);
            button.colors = colors;
            button.onClick.AddListener(() => action());
            image.gameObject.AddComponent<GoldMinerButtonFeedback>();
            return button;
        }

        private Button TextButton(Transform parent, string title, float x, float y, float width, float height, Action action, int size = 24)
        {
            var image = UiFactory.Panel(parent, title + " button", x, y, width, height, Color.clear, false);
            var button = ConfigureButton(image, action);
            TextLabel(image.transform, title, 0, 0, width, height, size, Forest, TextAnchor.MiddleCenter, true);
            return button;
        }

        private Button ActionButton(Transform parent, string title, float x, float y, float width, float height,
            Action action, bool primary, string texture = "button-primary-green")
        {
            Image image;
            if (primary)
            {
                image = GoldMinerSceneView.Picture(parent, title + " button", art.Get("ui/" + texture), x, y, width, height, false);
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 437f / height;
            }
            else image = BorderedPanel(parent, title + " button", x, y, width, height,
                UiFactory.Hex("FFF6DF"), UiFactory.Hex("A2834B"), height * .48f, 3);
            var button = ConfigureButton(image, action);
            var textColor = primary ? Cream : Forest;
            var label = TextLabel(image.transform, title, 10, 0, width - 20, height, height >= 76 ? 33 : 25, textColor, TextAnchor.MiddleCenter, true);
            if (primary)
            {
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = texture == "button-primary-green" ? UiFactory.Hex("143B30") : UiFactory.Hex("76511B");
                outline.effectDistance = new Vector2(1, -1);
            }
            return button;
        }

        private Text TextLabel(Transform parent, string text, float x, float y, float width, float height, int size,
            Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
        {
            var label = UiFactory.Label(parent, text, x, y, width, height, size, color, anchor, bold ? FontStyle.Bold : FontStyle.Normal);
            label.supportRichText = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(14, size - 6);
            label.resizeTextMaxSize = size;
            return label;
        }

        private void Notice(float y, bool compact = true)
        {
            float height = compact ? 22 : 44;
            // A solid strip keeps storage warnings readable over both light and dark artwork.
            // It only appears while a message exists and never intercepts touch input.
            noticePanel = UiFactory.Panel(view, "Save notice", layout.Safe.x + 12, y, layout.Safe.width - 24, height, Cream);
            noticePanel.raycastTarget = false;
            notice = TextLabel(noticePanel.transform, message ?? "", 6, 0, layout.Safe.width - 36, height,
                compact ? 14 : 17, Forest, TextAnchor.MiddleCenter);
            noticePanel.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }
}
