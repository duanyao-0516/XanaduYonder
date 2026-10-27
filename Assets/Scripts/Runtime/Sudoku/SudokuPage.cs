using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Sudoku;

namespace XanaduYonder.Runtime.Sudoku
{
    internal sealed partial class SudokuPage : MonoBehaviour
    {
        private SudokuCatalog catalog;
        private SudokuSession session;
        private SudokuSaveStore store;
        private SudokuBoardView board;
        private RectTransform root;
        private GameObject gameRoot, selectionRoot, dialog;
        private CanvasGroup gameInput, selectionInput;
        private Action returnLobby, openSettings, dismissDialog;
        private Func<bool> acceptsInput, soundEnabled, hapticsEnabled;
        private Action<string> setStatus;
        private GameFeedback feedback;
        private Text remainingText, errorsText, difficultyText, timerText, tipText, quickLabel, pencilLabel, hintLabel;
        private Button undoButton, eraseButton, pencilButton, hintButton, quickButton, pauseButton;
        private readonly Button[] digits = new Button[9];
        private readonly Text[] digitCounts = new Text[9];
        private bool visible, choosing = true, saveFailed, resultDismissed;
        private string warning;
        private float saveElapsed;
        private int inspectionDigit;
        private int[] flashCells;
        private bool flashOn;
        private Coroutine flashRoutine, hintRoutine;
        private CancellationTokenSource hintCancellation;
        private static readonly string[] Difficulties = { "入门", "简单", "中等", "困难" };
        public event Action Changed;
        public int CompletedCount { get { return session == null ? 0 : session.CompletedCount; } }
        public string SaveError { get { return saveFailed ? warning : null; } }
        private bool Interactive { get { return visible && !choosing && dialog == null && acceptsInput() && session.CanPlay; } }

        public void Initialize(RectTransform parent, Action lobby, Action settings, Func<bool> canInput,
            Func<bool> sound, Func<bool> haptics, GameFeedback commonFeedback, Action<string> status)
        {
            root = parent; returnLobby = lobby; openSettings = settings; acceptsInput = canInput;
            soundEnabled = sound; hapticsEnabled = haptics; feedback = commonFeedback; setStatus = status;
            var text = Resources.Load<TextAsset>("Sudoku/puzzles");
            if (text == null) throw new InvalidOperationException("The offline Sudoku bank is missing.");
            catalog = SudokuCatalog.Parse(text.text);
            for (int d = 0; d < 4; d++) if (catalog.Count(d) != 500) throw new InvalidOperationException("The Sudoku bank requires 500 levels per difficulty.");
            store = new SudokuSaveStore(Application.persistentDataPath);
            var saved = store.Load(out warning);
            if (saved != null)
            {
                var original = catalog.Get(saved.Game.Puzzle.Difficulty, saved.Game.Puzzle.Level);
                if (!Matches(original, saved.Game.Puzzle))
                {
                    warning = "数独对局与内置题库不一致，已重新准备第一关。";
                    saved = new SudokuSaveData { Game = new SudokuGame(catalog.Get(0, 1)).GetSnapshot(),
                        CompletedIds = saved.CompletedIds, UnassistedIds = saved.UnassistedIds };
                }
            }
            if (saved == null) saved = new SudokuSaveData { Game = new SudokuGame(catalog.Get(0, 1)).GetSnapshot() };
            session = new SudokuSession(saved, true);
            BuildGame(); BuildSelection();
        }

        private static bool Matches(SudokuPuzzle a, SudokuPuzzle b)
        {
            if (a == null || b == null || a.Id != b.Id) return false;
            for (int i = 0; i < 81; i++) if (a.Givens[i] != b.Givens[i] || a.Solution[i] != b.Solution[i]) return false;
            return true;
        }

        private void BuildGame()
        {
            var page = UiFactory.Rect(root, "Sudoku game", 0, 0, 720, 1320); UiFactory.Stretch(page); gameRoot = page.gameObject;
            gameInput = gameRoot.AddComponent<CanvasGroup>();
            var nav = UiFactory.Band(page, "Navigation", 0, 92, 0);
            UiFactory.Button(nav, "‹  关卡", 36, 30, 150, 58, ReturnToSelection);
            UiFactory.Label(nav, "XANADU / YONDER", 208, 30, 304, 58, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Button(nav, "设置", 560, 30, 124, 58, () => { if (Interactive) { CancelHint(); CloseDialog(); openSettings(); } });
            var heading = UiFactory.Band(page, "Heading", 108, 70, .03f);
            UiFactory.Label(heading, "数独", 36, 0, 200, 70, 49, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(heading, "九宫之间，找到唯一。", 240, 10, 444, 58, 25, UiFactory.Muted, TextAnchor.MiddleRight);
            var stats = UiFactory.Band(page, "Progress", 194, 74, .12f);
            remainingText = UiFactory.Label(stats, "", 36, 0, 210, 36, 25, UiFactory.Ink);
            errorsText = UiFactory.Label(stats, "", 36, 36, 210, 34, 22, UiFactory.Muted);
            difficultyText = UiFactory.Label(stats, "", 245, 0, 230, 70, 24, UiFactory.Accent, TextAnchor.MiddleCenter);
            timerText = UiFactory.Label(stats, "", 480, 16, 126, 50, 25, UiFactory.Ink, TextAnchor.MiddleRight);
            pauseButton = UiFactory.Button(stats, "Ⅱ", 620, 10, 64, 58, Pause);
            var stage = UiFactory.Band(page, "Sudoku board band", 280, 648, .30f);
            board = new SudokuBoardView(UiFactory.Rect(stage, "Sudoku board", 36, 0, 648, 648), Pick);
            var inputMode = UiFactory.Band(page, "Input mode", 946, 52, .61f);
            tipText = UiFactory.Label(inputMode, "", 36, 0, 460, 52, 20, UiFactory.Muted);
            tipText.resizeTextForBestFit = true; tipText.resizeTextMinSize = 16; tipText.resizeTextMaxSize = 20;
            quickButton = UiFactory.Button(inputMode, "快速 OFF", 510, 0, 174, 52, ToggleQuick);
            quickLabel = quickButton.GetComponentInChildren<Text>(); quickLabel.fontSize = 21;
            var tools = UiFactory.Band(page, "Sudoku tools", 1018, 72, .75f);
            undoButton = UiFactory.Button(tools, "撤回", 36, 0, 150, 72, () => { if (Interactive) { inspectionDigit = 0; Move(session.Undo()); } });
            eraseButton = UiFactory.Button(tools, "橡皮", 202, 0, 150, 72, () => { if (Interactive) { inspectionDigit = 0; Move(session.Erase()); } });
            pencilButton = UiFactory.Button(tools, "铅笔", 368, 0, 150, 72, TogglePencil);
            hintButton = UiFactory.Button(tools, "提示 3", 534, 0, 150, 72, OpenHint);
            pencilLabel = pencilButton.GetComponentInChildren<Text>(); hintLabel = hintButton.GetComponentInChildren<Text>();
            var numbers = UiFactory.Band(page, "Number keypad", 1112, 108, .88f);
            for (int i = 0; i < 9; i++)
            {
                int digit = i + 1;
                digits[i] = UiFactory.Button(numbers, digit.ToString(), 36 + i * 72, 0, 64, 72, () => Number(digit));
                digits[i].GetComponentInChildren<Text>().fontSize = 38;
                digitCounts[i] = UiFactory.Label(numbers, "", 36 + i * 72, 75, 64, 30, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
            }
            var footer = UiFactory.Band(page, "Sudoku footer", 1232, 40, 1);
            UiFactory.Button(footer, "重新开始", 36, 0, 180, 40, ConfirmRestart);
            UiFactory.Label(footer, "数字下方为待填个数", 230, 0, 300, 40, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Button(footer, "玩法", 550, 0, 134, 40, ShowRules);
        }

        public void Enter()
        {
            visible = true; gameObject.SetActive(true); ReturnToSelection();
            setStatus(warning ?? "四档难度 · 2,000 道离线数独 · 自动保存");
        }
        public void Leave()
        {
            if (session == null) return;
            CancelHint(); ClearFlash(); CloseDialog(); session.Pause(); Save(); visible = false; gameObject.SetActive(false);
        }
        public void SetForeground(bool foreground)
        {
            if (session == null) return;
            session.SetForeground(foreground);
            if (!foreground) { CancelHint(); ClearFlash(); CloseDialog(); Save(); }
            if (visible && !choosing) { if (session.Paused) ShowPause(); else Refresh(); }
        }
        public void Save()
        {
            if (session == null || store == null) return;
            string error;
            if (!store.TrySave(session.Export(), out error))
            { saveFailed = true; warning = "数独进度暂未保存，请检查设备空间。"; Debug.LogWarning("Sudoku save failed: " + error); setStatus(warning); }
            else if (saveFailed) { saveFailed = false; warning = null; setStatus("数独进度已保存。"); }
            Changed?.Invoke();
        }
        private void Update()
        {
            if (!visible || choosing || session == null) return;
            session.Tick(Time.unscaledDeltaTime);
            timerText.text = FormatTime(session.ElapsedSeconds);
            if (session.CanPlay) { saveElapsed += Time.unscaledDeltaTime; if (saveElapsed >= 10) { saveElapsed = 0; Save(); } }
        }
        private void OnDestroy() { CancelHint(); }

        private void Pick(int cell)
        {
            if (!Interactive) return;
            inspectionDigit = 0;
            bool occupiedQuick = session.Controls.Quick && session.Game.ValueAt(cell) != 0;
            Move(session.SelectCell(cell));
            if (occupiedQuick) Tip("快速模式只填写空格；修改已有数字请先擦除或关闭快速模式。");
            else if (session.Controls.Quick && session.Controls.QuickDigit == 0) Tip("请先选择下方数字，再连续点空格填写。");
        }
        private void Number(int digit) { if (Interactive) { inspectionDigit = digit; Move(session.SelectDigit(digit)); } }
        private void Move(SudokuMove move)
        {
            ClearFlash();
            if (move.Changed) { CancelHint(); Save(); feedback.Play(false, soundEnabled(), hapticsEnabled()); }
            Refresh();
            if (move.Conflicts.Length > 0) flashRoutine = StartCoroutine(Flash(move.Conflicts));
            if (move.Mistake) Tip("这个数字不正确；可用橡皮擦除。错误 " + session.Game.Errors + "/3");
            else if (move.RejectedNote) Tip("行、列或宫已有这个数字，候选未写入，不计错误。");
            CheckEndState();
        }
        private void TogglePencil()
        {
            if (!Interactive) return;
            session.Controls.TogglePencil(); Save(); Refresh();
            Tip(session.Controls.Pencil ? "铅笔 ON：记录候选，再点同一候选可取消。" : "铅笔 OFF：填写正式数字。");
        }
        private void ToggleQuick()
        {
            if (!Interactive) return;
            inspectionDigit = 0;
            if (session.Controls.Quick) { session.Controls.SetQuick(false); Save(); Refresh(); return; }
            ShowDialog("开启快速输入", "先选择下方数字，或点盘面中的正确数字，再连续点击空格填写。\n\n快速模式只填写空格；修改已有数字，请先擦除或关闭快速模式。\n\n也可以与铅笔模式一起使用。", "开启", () => {
                session.Controls.SetQuick(true); Save(); Refresh(); Tip("快速 ON：先选数字，再点空格。");
            }, "取消", () => { });
        }
        private void Tip(string message) { tipText.text = message; }
        private void Refresh()
        {
            var game = session.Game; var controls = session.Controls;
            remainingText.text = "待填 " + game.RemainingCount + " 格";
            errorsText.text = "错误 " + game.Errors + "/3";
            errorsText.color = game.Errors > 0 ? UiFactory.Hex("B34842") : UiFactory.Muted;
            var puzzle = game.Puzzle; difficultyText.text = Difficulties[puzzle.Difficulty] + "\n第 " + puzzle.Level + " 关";
            timerText.text = FormatTime(session.ElapsedSeconds);
            quickLabel.text = "快速 " + (controls.Quick ? "ON" : "OFF");
            pencilLabel.text = controls.Pencil ? "铅笔 ON" : "铅笔";
            hintLabel.text = "提示 " + game.HintsRemaining;
            bool can = session.CanPlay;
            undoButton.interactable = can && game.CanUndo;
            eraseButton.interactable = can && controls.SelectedCell >= 0 && !game.IsGiven(controls.SelectedCell) &&
                (game.ValueAt(controls.SelectedCell) != 0 || game.NotesAt(controls.SelectedCell) != 0);
            pencilButton.interactable = hintButton.interactable = quickButton.interactable = pauseButton.interactable = can;
            for (int i = 0; i < 9; i++)
            {
                digits[i].interactable = can;
                digitCounts[i].text = game.RemainingDigit(i + 1).ToString();
                digits[i].GetComponent<Image>().color = controls.Quick && controls.QuickDigit == i + 1 ? UiFactory.Hex("B5D2B6") : UiFactory.Hex("E7EDE4");
                digits[i].GetComponentInChildren<Text>().color = game.RemainingDigit(i + 1) == 0 ? UiFactory.Muted : UiFactory.Accent;
            }
            Tip(controls.Quick ? controls.QuickDigit == 0 ? "先选数字，再连续点空格。" : "已选择 " + controls.QuickDigit + " · 连续点空格填写" : "先点选格子，再填写数字。");
            RenderBoard();
        }
        private void RenderBoard(int[] evidence = null) { board.Render(session.Game, session.Controls, flashCells, flashOn, evidence, inspectionDigit); }
        private IEnumerator Flash(int[] cells)
        {
            flashCells = cells;
            for (int n = 0; n < 6; n++) { flashOn = n % 2 == 0; RenderBoard(); yield return new WaitForSecondsRealtime(.15f); }
            flashOn = false; flashCells = null; flashRoutine = null; RenderBoard();
        }
        private void ClearFlash()
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = null; flashOn = false; flashCells = null;
        }
        private static string FormatTime(double seconds)
        {
            var total = (long)seconds;
            return total >= 3600 ? (total / 3600) + ":" + (total / 60 % 60).ToString("00") + ":" + (total % 60).ToString("00") : (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }
    }
}
