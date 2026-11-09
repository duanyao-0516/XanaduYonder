using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Tetris;

namespace XanaduYonder.Runtime.Tetris
{
    /// <summary>UGUI projection only. Session, timing, input arbitration and persistence belong to the page.</summary>
    internal sealed class TetrisView
    {
        private sealed class Control
        {
            internal RectTransform Target;
            internal TetrisGraphic Background, Symbol;
            internal Text Label;
            internal Button Button;
            internal CanvasGroup Group;
        }

        private readonly RectTransform root, navigation, information, operations, warningBand, dialog;
        public RectTransform ReviewSlot { get; private set; }
        public Graphic ReviewBrand => brand;
        private readonly TetrisGraphic arenaBackground;
        private readonly TetrisBoardView board;
        private readonly BoardCountdownView countdown;
        private readonly Text brand, title, warningText, nextLabel, overlayTitle, overlayDetail, overlayBest, pauseTitle;
        private readonly Text resultTitle, resultScoreCaption, resultScore, resultLines, resultBest;
        private readonly Control homeControl, helpControl, settingsControl, pauseControl, primaryControl, secondaryControl, inspectControl;
        private readonly Control continueControl, restartControl;
        private readonly Control resultRestartControl, resultInspectControl;
        private readonly RectTransform[] statGroups = new RectTransform[3];
        private readonly Text[] statLabels = new Text[3], statValues = new Text[3];
        private readonly TetrisGraphic holdPreview;
        private readonly TetrisGraphic[] nextPreviews = new TetrisGraphic[3];
        private readonly Dictionary<TetrisAction, Control> controls = new Dictionary<TetrisAction, Control>();
        private readonly List<Text> labels = new List<Text>();
        private readonly List<float> fontSizes = new List<float>();
        private Vector2 lastSize;
        private string warning = "";
        private bool layoutDirty = true, controlsEnabled, canHold, overlayVisible, pauseVisible, resultVisible, swapControls;
        private long? bestScore;
        private string BestLabel => bestScore.HasValue ? "最高纪录 " + bestScore.Value.ToString(CultureInfo.InvariantCulture) : "最高纪录暂不可读";
        private long lastScore = -1;
        private int lastLines = -1;
        private string lastOverlayTitle, lastOverlayDetail, lastPrimaryText, lastSecondaryText;
        private Action lastPrimaryAction, lastSecondaryAction, lastInspectAction;
        private Action lastContinueAction, lastRestartAction;
        private Action lastResultRestartAction, lastResultInspectAction;

        public Dictionary<TetrisAction, RectTransform> ActionTargets { get; private set; }
        public RectTransform BoardRect { get { return board.rectTransform; } }
        public RectTransform CountdownRoot => countdown.Root;
        public RectTransform ArenaRect { get { return arenaBackground.rectTransform; } }
        public RectTransform PauseRoot { get; private set; }
        public RectTransform ResultRoot { get; private set; }
        public RectTransform OverlayRoot { get; private set; }
        public TetrisLayout Layout { get; private set; }
        public bool HasClearFlash { get { return board.HasClearFlash; } }

        public void FlashLines(int[] coreRows) { board.FlashLines(coreRows); }
        public void TickVisuals(float seconds) { board.TickVisuals(seconds); }
        public void ClearVisuals() { board.ClearVisuals(); countdown.Hide(); }
        public void SetCountdown(int number) { countdown.SetNumber(number); }
        public void HideCountdown() { countdown.Hide(); }
        public void HidePause() { SetPaused(false, null, null); }
        public void HideResult() { SetResult(false, null, null); }

        public TetrisView(RectTransform root, Action home, Action help, Action settings, Action pause)
        {
            if (root == null) throw new ArgumentNullException("root");
            this.root = root;
            ActionTargets = new Dictionary<TetrisAction, RectTransform>();
            var paper = UiFactory.Panel(root, "Tetris paper", 0, 0, 1, 1, UiFactory.Paper, false);
            UiFactory.Stretch(paper.rectTransform);
            navigation = UiFactory.Rect(root, "Tetris navigation", 0, 0, 1, 1);
            homeControl = Navigation("Home", UiIconKind.Home, home);
            helpControl = Navigation("Book", UiIconKind.Book, help);
            settingsControl = Navigation("Settings", UiIconKind.Settings, settings);
            brand = Label(navigation, "Tetris brand", "XANADU / YONDER", 10, UiFactory.Muted, TextAnchor.MiddleCenter);
            ReviewSlot = UiFactory.Rect(navigation, "Result review slot", 0, 0, 1, 1);
            title = Label(root, "Tetris title", "俄罗斯方块", 26.5f, UiFactory.Ink);
            pauseControl = MakeControl(root, "Tetris pause", "暂停", pause, false, 12);

            information = UiFactory.Rect(root, "Tetris information", 0, 0, 1, 1);
            string[] names = { "得分", "等级", "消行" };
            for (int i = 0; i < names.Length; i++)
            {
                statGroups[i] = UiFactory.Rect(information, "Tetris stat " + i, 0, 0, 1, 1);
                statLabels[i] = Label(statGroups[i], "Stat label", names[i], 11, UiFactory.Muted);
                statValues[i] = Label(statGroups[i], "Stat value", i == 1 ? "1" : "0", 15, UiFactory.Ink);
            }
            var divider = UiFactory.Panel(information, "Information divider", 0, 0, 1, 1, UiFactory.Hex("DCE2D6"), false);
            divider.rectTransform.anchorMin = new Vector2(0, 0); divider.rectTransform.anchorMax = new Vector2(1, 0);
            divider.rectTransform.pivot = Vector2.zero; divider.rectTransform.anchoredPosition = Vector2.zero;
            divider.rectTransform.sizeDelta = new Vector2(0, 1);

            arenaBackground = Shape(root, "Tetris arena", TetrisGraphicKind.Round, UiFactory.Hex("EBEEE2"));
            // Reuse the arena's exact rounded mesh to clip its presentation layers.
            ArenaRect.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            board = UiFactory.Rect(ArenaRect, "Tetris board 10 x 20", 0, 0, 1, 1).gameObject.AddComponent<TetrisBoardView>();
            board.raycastTarget = false;
            Control hold = MakeControl(ArenaRect, "Tetris Hold", "暂存", null, false, 11);
            controls.Add(TetrisAction.Hold, hold); ActionTargets.Add(TetrisAction.Hold, hold.Target);
            holdPreview = Shape(hold.Target, "Tetris held piece", TetrisGraphicKind.Preview, Color.white);
            nextLabel = Label(ArenaRect, "Tetris next label", "接下来", 11, UiFactory.Muted, TextAnchor.MiddleCenter);
            for (int i = 0; i < nextPreviews.Length; i++)
                nextPreviews[i] = Shape(ArenaRect, "Tetris next " + (i + 1), TetrisGraphicKind.Preview, Color.white);

            // These siblings come after Hold and Next so the whole arena is dimmed.
            countdown = new BoardCountdownView(ArenaRect, "Tetris countdown");
            var pauseDim = UiFactory.Panel(ArenaRect, "Tetris arena pause", 0, 0, 1, 1,
                new Color(UiFactory.Ink.r, UiFactory.Ink.g, UiFactory.Ink.b, .44f), false);
            PauseRoot = pauseDim.rectTransform; UiFactory.Stretch(PauseRoot); pauseDim.raycastTarget = true;
            pauseTitle = Label(PauseRoot, "Tetris pause title", "已暂停", 28, Color.white, TextAnchor.MiddleCenter);
            pauseTitle.fontStyle = FontStyle.Bold;
            continueControl = MakeControl(PauseRoot, "Tetris continue", "继续游戏", null, true, 14);
            restartControl = MakeControl(PauseRoot, "Tetris restart", "重新开始", null, false, 14);
            PauseRoot.gameObject.SetActive(false);

            var resultDim = UiFactory.Panel(ArenaRect, "Tetris arena result", 0, 0, 1, 1,
                new Color(UiFactory.Ink.r, UiFactory.Ink.g, UiFactory.Ink.b, .44f), false);
            ResultRoot = resultDim.rectTransform; UiFactory.Stretch(ResultRoot); resultDim.raycastTarget = true;
            resultTitle = Label(ResultRoot, "Tetris result title", "本局结束", 22, Color.white, TextAnchor.MiddleCenter);
            resultScoreCaption = Label(ResultRoot, "Tetris result score caption", "本局得分", 11, new Color(1, 1, 1, .8f), TextAnchor.MiddleCenter);
            resultScore = Label(ResultRoot, "Tetris result score", "0", 42, Color.white, TextAnchor.MiddleCenter);
            resultScore.fontStyle = FontStyle.Bold;
            resultLines = Label(ResultRoot, "Tetris result lines", "消除 0 行", 13, new Color(1, 1, 1, .86f), TextAnchor.MiddleCenter);
            resultBest = Label(ResultRoot, "Tetris result best", "最高纪录暂不可读", 12, new Color(1, 1, 1, .8f), TextAnchor.MiddleCenter);
            resultRestartControl = MakeControl(ResultRoot, "Tetris result restart", "再来一局", null, true, 14);
            resultInspectControl = MakeControl(ResultRoot, "Tetris result inspect", "查看棋盘", null, false, 14);
            ResultRoot.gameObject.SetActive(false);

            operations = UiFactory.Rect(root, "Tetris operations", 0, 0, 1, 1);
            AddAction(TetrisAction.Left, "", TetrisGraphicKind.Left);
            AddAction(TetrisAction.SoftDrop, "", TetrisGraphicKind.Down);
            AddAction(TetrisAction.Right, "", TetrisGraphicKind.Right);
            AddAction(TetrisAction.Clockwise, "", TetrisGraphicKind.Clockwise, true);

            warningBand = UiFactory.Rect(root, "Tetris save warning", 0, 0, 1, 1);
            warningText = Label(warningBand, "Tetris warning text", "", 10, UiFactory.Hex("B34842"), TextAnchor.MiddleCenter);
            warningText.horizontalOverflow = HorizontalWrapMode.Wrap;
            warningText.verticalOverflow = VerticalWrapMode.Truncate;
            warningBand.gameObject.SetActive(false);

            var dim = UiFactory.Panel(root, "Tetris overlay", 0, 0, 1, 1,
                new Color(UiFactory.Ink.r, UiFactory.Ink.g, UiFactory.Ink.b, .37f), false);
            OverlayRoot = dim.rectTransform; UiFactory.Stretch(OverlayRoot); dim.raycastTarget = true;
            dialog = Shape(OverlayRoot, "Tetris dialog", TetrisGraphicKind.Round, UiFactory.Paper).rectTransform;
            overlayTitle = Label(dialog, "Tetris overlay title", "", 23, UiFactory.Ink);
            overlayDetail = Label(dialog, "Tetris overlay detail", "", 13, UiFactory.Muted);
            overlayDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            overlayDetail.verticalOverflow = VerticalWrapMode.Truncate;
            overlayBest = Label(dialog, "Tetris best score", "最高纪录 0", 12, UiFactory.Muted);
            primaryControl = MakeControl(dialog, "Tetris overlay primary", "", null, true, 14);
            secondaryControl = MakeControl(dialog, "Tetris overlay secondary", "", null, false, 14);
            inspectControl = MakeControl(dialog, "Tetris inspect result", "", null, false, 14);
            inspectControl.Target.gameObject.SetActive(false);
            OverlayRoot.gameObject.SetActive(false);
            // Preparation/error cards retain their existing full-page blocker.
            // Pause, countdown and result only cover the arena beneath navigation.
            navigation.SetAsLastSibling();
            RefreshLayout();
            ApplyControlStates();
        }

        public void Render(TetrisSnapshot state, int ghostY, long? bestScore)
        {
            if (state == null) throw new ArgumentNullException("state");
            board.Render(state, ghostY);
            if (lastScore != state.Score || lastLines != state.Lines)
            {
                lastScore = state.Score; lastLines = state.Lines;
                statValues[0].text = state.Score.ToString(CultureInfo.InvariantCulture);
                statValues[1].text = state.Level.ToString(CultureInfo.InvariantCulture);
                statValues[2].text = state.Lines.ToString(CultureInfo.InvariantCulture);
                resultScore.text = statValues[0].text;
                resultLines.text = "消除 " + statValues[2].text + " 行";
                if (Layout != null) { LayoutStats(); if (resultVisible) LayoutResult(); }
            }
            if (this.bestScore != bestScore)
            {
                this.bestScore = bestScore;
                overlayBest.text = BestLabel;
                resultBest.text = BestLabel;
                if (Layout != null) { FitText(overlayBest, 12, 8); FitText(resultBest, 12, 8); }
            }
            bool nextCanHold = state.CanHold && !state.GameOver;
            if (canHold != nextCanHold) { canHold = nextCanHold; ApplyControlStates(); }
            holdPreview.SetPreview(state.HoldPiece);
            for (int i = 0; i < nextPreviews.Length; i++)
                nextPreviews[i].SetPreview(state.NextQueue[i]);
        }

        public void SetControlsEnabled(bool enabled)
        {
            if (controlsEnabled == enabled) return;
            controlsEnabled = enabled; ApplyControlStates();
        }

        public void SetPauseLabel(bool running, bool ended = false)
        { pauseControl.Label.text = ended ? "再来一局" : running ? "暂停" : "继续"; }

        public void SetSwapControls(bool value)
        { if (swapControls != value) { swapControls = value; layoutDirty = true; } }

        public void SetPaused(bool visible, Action resume, Action restart)
        {
            if (pauseVisible == visible && lastContinueAction == resume && lastRestartAction == restart) return;
            pauseVisible = visible; lastContinueAction = resume; lastRestartAction = restart;
            ConfigureOverlayControl(continueControl, "继续游戏", resume);
            ConfigureOverlayControl(restartControl, "重新开始", restart);
            PauseRoot.gameObject.SetActive(visible);
            ApplyControlStates();
        }

        public void SetResult(bool visible, Action restart, Action inspect)
        {
            if (resultVisible == visible && lastResultRestartAction == restart && lastResultInspectAction == inspect) return;
            resultVisible = visible; lastResultRestartAction = restart; lastResultInspectAction = inspect;
            ConfigureOverlayControl(resultRestartControl, "再来一局", restart);
            ConfigureOverlayControl(resultInspectControl, "查看棋盘", inspect);
            ResultRoot.gameObject.SetActive(visible);
            if (visible && Layout != null) LayoutResult();
            ApplyControlStates();
        }

        public void SetOverlay(string title, string detail, string primaryText, Action primary,
            string secondaryText, Action secondary, bool visible, Action inspect = null)
        {
            if (lastOverlayTitle == title && lastOverlayDetail == detail && lastPrimaryText == primaryText &&
                lastSecondaryText == secondaryText && lastPrimaryAction == primary && lastSecondaryAction == secondary &&
                overlayVisible == visible && lastInspectAction == inspect) return;
            lastOverlayTitle = title; lastOverlayDetail = detail; lastPrimaryText = primaryText;
            lastSecondaryText = secondaryText; lastPrimaryAction = primary; lastSecondaryAction = secondary;
            lastInspectAction = inspect;
            overlayTitle.text = title ?? ""; overlayDetail.text = detail ?? "";
            overlayBest.text = BestLabel;
            ConfigureOverlayControl(primaryControl, primaryText, primary);
            ConfigureOverlayControl(secondaryControl, secondaryText, secondary);
            ConfigureOverlayControl(inspectControl, inspect == null ? null : "查看最终局面", inspect);
            overlayVisible = visible;
            OverlayRoot.gameObject.SetActive(visible);
            ApplyControlStates();
            if (Layout != null) LayoutOverlay();
        }

        public void SetWarning(string message)
        {
            message = message ?? "";
            if (warning == message) return;
            warning = message; warningText.text = message;
            warningBand.gameObject.SetActive(message.Length > 0);
            layoutDirty = true;
        }

        public bool RefreshLayout()
        {
            Vector2 size = root.rect.size;
            if (size.x <= 0 || size.y <= 0) return false;
            if (!layoutDirty && Vector2.SqrMagnitude(size - lastSize) < .001f) return false;
            lastSize = size; layoutDirty = false;
            Layout = new TetrisLayout(size.x, size.y, warning.Length > 0, swapControls);
            float s = (float)Layout.Scale, width = size.x, content = (float)Layout.ContentWidth;
            for (int i = 0; i < labels.Count; i++) labels[i].fontSize = Mathf.Max(1, Mathf.RoundToInt(fontSizes[i] * s));
            Place(navigation, 0, 0, width, 56 * s);
            LayoutNavigation(homeControl, 11.5f * s, s);
            LayoutNavigation(helpControl, width - 107.5f * s, s);
            LayoutNavigation(settingsControl, width - 59.5f * s, s);
            Place(brand.rectTransform, 108 * s, 12 * s, 172 * s, 32 * s);
            Place(ReviewSlot, 128 * s, 6 * s, 132 * s, 44 * s);
            Place(title.rectTransform, 19.5f * s, 56 * s, 264 * s, 44 * s);
            Place(pauseControl.Target, width - 83.5f * s, 54 * s, 64 * s, 48 * s);
            Place(pauseControl.Background.rectTransform, 0, 6 * s, 64 * s, 36 * s);
            pauseControl.Background.Radius = 10 * s;
            Place(pauseControl.Label.rectTransform, 0, 0, 64 * s, 48 * s);
            Place(information, 19.5f * s, 100 * s, content, 36 * s);
            information.Find("Information divider").GetComponent<RectTransform>().sizeDelta = new Vector2(0, s);
            LayoutStats();
            Place(ArenaRect, (float)Layout.Inset, (float)Layout.ArenaTop, content, (float)Layout.ArenaHeight);
            arenaBackground.Radius = 15 * s;
            LayoutPause();
            LayoutResult();
            Place(BoardRect, (float)Layout.BoardLeft, (float)Layout.BoardTop, (float)Layout.BoardWidth, (float)Layout.BoardHeight);
            Control hold = controls[TetrisAction.Hold];
            // Four extra pixels use arena padding; the right edge stops at the board.
            Place(hold.Target, (float)Layout.BoardLeft - 48 * s, 14 * s, 48 * s, 68 * s);
            Place(hold.Background.rectTransform, 4 * s, 0, 40 * s, 68 * s);
            hold.Background.Radius = 8 * s; hold.Background.color = UiFactory.Hex("DFE7D9");
            Place(hold.Label.rectTransform, 4 * s, 7 * s, 40 * s, 18 * s);
            hold.Label.color = UiFactory.Muted;
            Place(holdPreview.rectTransform, 8 * s, 39 * s, 32 * s, 18 * s);
            float nextX = (float)(Layout.BoardLeft + Layout.BoardWidth) + 4 * s;
            Place(nextLabel.rectTransform, nextX, 14 * s, 40 * s, 18 * s);
            for (int i = 0; i < nextPreviews.Length; i++)
                Place(nextPreviews[i].rectTransform, nextX + 4 * s, (50 + i * 37) * s, 32 * s, 18 * s);

            Place(operations, (float)Layout.Inset, (float)Layout.ControlsTop, content, (float)Layout.ControlsHeight);
            TetrisAction[] actions = { TetrisAction.Left, TetrisAction.Right, TetrisAction.SoftDrop };
            float actionWidth = (float)Layout.MinimumTarget, step = (float)Layout.DirectionStep;
            for (int i = 0; i < actions.Length; i++)
            {
                Control control = controls[actions[i]];
                float x = (float)Layout.DirectionGroupLeft + (i == 1 ? 2 * step : i == 2 ? step : 0);
                float y = i == 2 ? step : 0;
                Place(control.Target, x, y, actionWidth, actionWidth);
                Place(control.Background.rectTransform, 0, 0, actionWidth, actionWidth);
                control.Background.Radius = 12 * s;
                Place(control.Symbol.rectTransform, 13 * s, 13 * s, 22 * s, 22 * s);
            }
            Control rotate = controls[TetrisAction.Clockwise];
            float rotateSize = (float)Layout.RotationSize;
            Place(rotate.Target, (float)Layout.RotationLeft, 12 * s, rotateSize, rotateSize);
            Place(rotate.Background.rectTransform, 0, 0, rotateSize, rotateSize); rotate.Background.Radius = rotateSize / 2;
            Place(rotate.Symbol.rectTransform, 23 * s, 23 * s, 34 * s, 34 * s);
            Place(warningBand, (float)Layout.Inset, (float)Layout.StatusTop, content, 26 * s);
            Place(warningText.rectTransform, 0, 0, content, 26 * s);
            warningText.resizeTextForBestFit = true;
            warningText.resizeTextMinSize = Mathf.RoundToInt(8 * s); warningText.resizeTextMaxSize = Mathf.RoundToInt(10 * s);
            LayoutOverlay();
            return true;
        }

        private void LayoutStats()
        {
            float s = (float)Layout.Scale, total = (float)Layout.ContentWidth;
            float[] portions = { .5f, .23f, .27f };
            float x = 0;
            for (int i = 0; i < statGroups.Length; i++)
            {
                float width = total * portions[i], labelWidth = 25 * s;
                Place(statGroups[i], x, 0, width, 35 * s);
                Place(statLabels[i].rectTransform, 0, 0, labelWidth, 35 * s);
                Place(statValues[i].rectTransform, labelWidth, 0, width - labelWidth - (i == 2 ? 0 : 8 * s), 35 * s);
                FitText(statValues[i], 15, 8);
                x += width;
            }
        }

        private void LayoutOverlay()
        {
            float s = (float)Layout.Scale, width = Mathf.Min(308 * s, root.rect.width - 52 * s);
            float inner = width - 44 * s;
            float detailHeight = 72 * s;
            bool hasPrimary = primaryControl.Target.gameObject.activeSelf;
            bool hasSecondary = secondaryControl.Target.gameObject.activeSelf;
            bool hasInspect = inspectControl.Target.gameObject.activeSelf;
            float buttons = ((hasPrimary ? 56 : 0) + (hasSecondary ? 56 : 0) + (hasInspect ? 56 : 0)) * s;
            float height = 26 * s + 34 * s + 8 * s + detailHeight + 27 * s + buttons + 22 * s;
            Place(dialog, (root.rect.width - width) / 2, (root.rect.height - height) / 2, width, height);
            dialog.GetComponent<TetrisGraphic>().Radius = 20 * s;
            Place(overlayTitle.rectTransform, 22 * s, 22 * s, inner, 38 * s);
            Place(overlayDetail.rectTransform, 22 * s, 68 * s, inner, detailHeight);
            overlayDetail.resizeTextForBestFit = true;
            overlayDetail.resizeTextMinSize = Mathf.RoundToInt(10 * s); overlayDetail.resizeTextMaxSize = Mathf.RoundToInt(13 * s);
            Place(overlayBest.rectTransform, 22 * s, 140 * s, inner, 25 * s);
            FitText(overlayTitle, 23, 16); FitText(overlayBest, 12, 8);
            float top = 173 * s;
            if (hasPrimary) { LayoutDialogControl(primaryControl, inner, top, s); top += 56 * s; }
            if (hasSecondary) { LayoutDialogControl(secondaryControl, inner, top, s); top += 56 * s; }
            if (hasInspect) LayoutDialogControl(inspectControl, inner, top, s);
        }

        private void LayoutPause()
        {
            float s = (float)Layout.Scale, width = 268 * s;
            float left = ((float)Layout.ContentWidth - width) / 2;
            float top = ((float)Layout.ArenaHeight - 104 * s) / 2;
            Place(pauseTitle.rectTransform, left, top, width, 40 * s);
            Control[] buttons = { continueControl, restartControl };
            for (int i = 0; i < buttons.Length; i++)
            {
                Control control = buttons[i];
                Place(control.Target, left + i * 140 * s, top + 60 * s, 128 * s, 44 * s);
                Place(control.Background.rectTransform, 0, 0, 128 * s, 44 * s);
                control.Background.Radius = 10 * s;
                Place(control.Label.rectTransform, 8 * s, 0, 112 * s, 44 * s);
            }
        }

        private static void LayoutDialogControl(Control control, float width, float top, float s)
        {
            Place(control.Target, 22 * s, top, width, 48 * s);
            Place(control.Background.rectTransform, 0, 0, width, 48 * s);
            control.Background.Radius = 10 * s;
            Place(control.Label.rectTransform, 8 * s, 0, width - 16 * s, 48 * s);
        }

        private void LayoutResult()
        {
            float s = (float)Layout.Scale, width = 268 * s;
            float left = ((float)Layout.ContentWidth - width) / 2;
            // Font sizes round to whole pixels while the design boxes scale by
            // fractions. Keep the measured line inside its box without shrinking it.
            float titleHeight = Mathf.Max(32 * s, Mathf.Ceil(resultTitle.preferredHeight));
            float titleExtra = titleHeight - 32 * s;
            float top = ((float)Layout.ArenaHeight - 242 * s - titleExtra) / 2;
            Place(resultTitle.rectTransform, left, top, width, titleHeight);
            top += titleExtra;
            Place(resultScoreCaption.rectTransform, left, top + 40 * s, width, 18 * s);
            Place(resultScore.rectTransform, left, top + 60 * s, width, 66 * s);
            Place(resultLines.rectTransform, left, top + 130 * s, width, 22 * s);
            Place(resultBest.rectTransform, left, top + 156 * s, width, 22 * s);
            FitText(resultScore, 42, 18); FitText(resultLines, 13, 10); FitText(resultBest, 12, 8);
            Control[] buttons = { resultRestartControl, resultInspectControl };
            for (int i = 0; i < buttons.Length; i++)
            {
                Control control = buttons[i];
                Place(control.Target, left + i * 140 * s, top + 198 * s, 128 * s, 44 * s);
                Place(control.Background.rectTransform, 0, 0, 128 * s, 44 * s);
                control.Background.Radius = 10 * s;
                Place(control.Label.rectTransform, 8 * s, 0, 112 * s, 44 * s);
            }
        }

        private void FitText(Text text, float nominal, float minimum)
        {
            float s = (float)Layout.Scale;
            int smallest = Mathf.Max(1, Mathf.RoundToInt(minimum * s));
            text.fontSize = Mathf.Max(smallest, Mathf.RoundToInt(nominal * s));
            while (text.fontSize > smallest && text.preferredWidth > text.rectTransform.rect.width) text.fontSize--;
        }

        private void ApplyControlStates()
        {
            foreach (KeyValuePair<TetrisAction, Control> entry in controls)
            {
                bool enabled = controlsEnabled && !overlayVisible && !pauseVisible && !resultVisible && (entry.Key != TetrisAction.Hold || canHold);
                entry.Value.Button.interactable = enabled;
                entry.Value.Group.blocksRaycasts = enabled;
                entry.Value.Group.alpha = enabled ? 1 : .43f;
            }
        }

        private static void ConfigureOverlayControl(Control control, string text, Action action)
        {
            control.Label.text = text ?? "";
            control.Button.onClick.RemoveAllListeners();
            if (action != null) control.Button.onClick.AddListener(() => action());
            control.Button.interactable = action != null;
            control.Target.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private void AddAction(TetrisAction action, string label, TetrisGraphicKind kind, bool primary = false)
        {
            Control control = MakeControl(operations, "Tetris " + action, label, null, primary, primary ? 14 : 12);
            control.Symbol = Shape(control.Target, "Tetris action icon", kind, primary ? UiFactory.Paper : UiFactory.Ink);
            control.Label.gameObject.SetActive(false);
            controls.Add(action, control); ActionTargets.Add(action, control.Target);
        }

        private Control Navigation(string name, UiIconKind kind, Action action)
        {
            Control control = MakeControl(navigation, "Tetris " + name, "", action, false, 12);
            control.Label.gameObject.SetActive(false);
            var icon = UiFactory.Rect(control.Target, "Navigation icon", 0, 0, 1, 1).gameObject.AddComponent<UiIconGraphic>();
            icon.Kind = kind; icon.color = UiFactory.Ink; icon.raycastTarget = false;
            return control;
        }

        private static void LayoutNavigation(Control control, float x, float s)
        {
            Place(control.Target, x, 4 * s, 48 * s, 48 * s);
            Place(control.Background.rectTransform, 8 * s, 8 * s, 32 * s, 32 * s);
            control.Background.Radius = 10 * s;
            Place((RectTransform)control.Target.Find("Navigation icon"), 14 * s, 14 * s, 20 * s, 20 * s);
        }

        private Control MakeControl(Transform parent, string name, string label, Action action, bool primary, float fontSize)
        {
            var target = UiFactory.Panel(parent, name, 0, 0, 1, 1, Color.clear, false);
            target.raycastTarget = true;
            var control = new Control { Target = target.rectTransform };
            control.Background = Shape(target.transform, "Button background", TetrisGraphicKind.Round,
                primary ? UiFactory.Accent : UiFactory.Hex("E7EDE4"));
            control.Button = target.gameObject.AddComponent<Button>();
            control.Button.targetGraphic = control.Background;
            ColorBlock colors = control.Button.colors;
            colors.normalColor = colors.highlightedColor = colors.selectedColor = colors.disabledColor = Color.white;
            colors.pressedColor = new Color(.82f, .88f, .82f);
            control.Button.colors = colors;
            var noNavigation = control.Button.navigation; noNavigation.mode = UnityEngine.UI.Navigation.Mode.None;
            control.Button.navigation = noNavigation;
            if (action != null) control.Button.onClick.AddListener(() => action());
            control.Group = target.gameObject.AddComponent<CanvasGroup>();
            control.Label = Label(target.transform, "Button label", label, fontSize,
                primary ? UiFactory.Paper : UiFactory.Ink, TextAnchor.MiddleCenter);
            return control;
        }

        private Text Label(Transform parent, string name, string value, float size, Color shade,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            Text text = UiFactory.Label(parent, value, 0, 0, 1, 1, 1, shade, alignment);
            text.name = name; text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            labels.Add(text); fontSizes.Add(size);
            return text;
        }

        private static TetrisGraphic Shape(Transform parent, string name, TetrisGraphicKind kind, Color shade)
        {
            var graphic = UiFactory.Rect(parent, name, 0, 0, 1, 1).gameObject.AddComponent<TetrisGraphic>();
            graphic.Kind = kind; graphic.color = shade; graphic.raycastTarget = false;
            return graphic;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
            Graphic graphic = rect.GetComponent<Graphic>();
            if (graphic != null) graphic.SetVerticesDirty();
        }
    }
}
