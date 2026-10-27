using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Sudoku;

namespace XanaduYonder.Runtime.Sudoku
{
    internal sealed partial class SudokuPage
    {
        private readonly Button[] levelButtons = new Button[50];
        private readonly Button[] difficultyButtons = new Button[4];
        private Text selectionSummary, levelPageLabel, resumeLabel;
        private int selectedDifficulty, levelPage;
        private Button previousPage, nextPage;

        private void BuildSelection()
        {
            var page = UiFactory.Rect(root, "Sudoku level selection", 0, 0, 720, 1320); UiFactory.Stretch(page); selectionRoot = page.gameObject;
            var nav = UiFactory.Band(page, "Selection navigation", 0, 92, 0);
            UiFactory.Button(nav, "‹  大厅", 36, 30, 150, 58, returnLobby);
            UiFactory.Label(nav, "XANADU / YONDER", 208, 30, 304, 58, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Button(nav, "设置", 560, 30, 124, 58, openSettings);
            var title = UiFactory.Band(page, "Selection heading", 108, 96, .05f);
            UiFactory.Label(title, "数独", 36, 0, 260, 80, 52, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(title, "基础九宫 · 离线 2,000 关", 280, 20, 404, 50, 24, UiFactory.Muted, TextAnchor.MiddleRight);
            var difficulties = UiFactory.Band(page, "Difficulties", 226, 62, .12f);
            for (int i = 0; i < 4; i++)
            {
                int d = i;
                difficultyButtons[i] = UiFactory.Button(difficulties, Difficulties[i], 36 + i * 166, 0, 150, 62, () => {
                    selectedDifficulty = d; levelPage = 0; RefreshSelection();
                });
            }
            var resume = UiFactory.Band(page, "Current session", 308, 66, .18f);
            resumeLabel = UiFactory.Button(resume, "继续当前对局", 36, 0, 648, 66, ContinueCurrent, true).GetComponentInChildren<Text>();
            selectionSummary = UiFactory.Label(UiFactory.Band(page, "Selection summary", 388, 40, .22f), "", 36, 0, 648, 40, 21, UiFactory.Muted, TextAnchor.MiddleCenter);
            var grid = UiFactory.Band(page, "Level grid", 442, 696, .40f);
            for (int i = 0; i < 50; i++)
            {
                int offset = i;
                levelButtons[i] = UiFactory.Button(grid, "", 36 + i % 5 * 132, i / 5 * 70, 120, 60, () => SelectLevel(levelPage * 50 + offset + 1));
                levelButtons[i].GetComponentInChildren<Text>().fontSize = 21;
            }
            var pagination = UiFactory.Band(page, "Level pages", 1174, 64, .90f);
            previousPage = UiFactory.Button(pagination, "上一页", 36, 0, 172, 64, () => { levelPage--; RefreshSelection(); });
            levelPageLabel = UiFactory.Label(pagination, "", 224, 0, 272, 64, 25, UiFactory.Ink, TextAnchor.MiddleCenter);
            nextPage = UiFactory.Button(pagination, "下一页", 512, 0, 172, 64, () => { levelPage++; RefreshSelection(); });
            UiFactory.Label(UiFactory.Band(page, "Legend", 1250, 34, 1), "完成标记：独立完成 / 使用提示完成", 36, 0, 648, 34, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
        }
        private void RefreshSelection()
        {
            var data = session.Export();
            int completed = 0;
            foreach (var id in data.CompletedIds) if (id.StartsWith("se-v1-d" + selectedDifficulty + "-", StringComparison.Ordinal)) completed++;
            selectionSummary.text = Difficulties[selectedDifficulty] + " · 已完成 " + completed + "/500";
            for (int d = 0; d < 4; d++) difficultyButtons[d].GetComponent<Image>().color = d == selectedDifficulty ? UiFactory.Hex("B5D2B6") : UiFactory.Hex("E7EDE4");
            var current = session.Game.Puzzle;
            resumeLabel.text = (session.Game.Phase == SudokuPhase.Playing ? "开始 / 继续  " : "查看当前结果  ") + Difficulties[current.Difficulty] + " · 第 " + current.Level + " 关  " + FormatTime(session.ElapsedSeconds);
            for (int i = 0; i < 50; i++)
            {
                var puzzle = catalog.Get(selectedDifficulty, levelPage * 50 + i + 1);
                bool done = Array.IndexOf(data.CompletedIds, puzzle.Id) >= 0;
                bool independent = Array.IndexOf(data.UnassistedIds, puzzle.Id) >= 0;
                bool playing = session.Game.Phase == SudokuPhase.Playing && current.Id == puzzle.Id && session.Game.Revision > 0;
                levelButtons[i].GetComponentInChildren<Text>().text = puzzle.Level + (done ? independent ? "\n独立完成" : "\n提示完成" : playing ? "\n进行中" : "");
                levelButtons[i].GetComponent<Image>().color = done ? UiFactory.Hex("D0E0C2") : playing ? UiFactory.Hex("E7DAB3") : UiFactory.Hex("E7EDE4");
            }
            previousPage.interactable = levelPage > 0; nextPage.interactable = levelPage < 9;
            levelPageLabel.text = (levelPage + 1) + " / 10";
        }
        private void ReturnToSelection()
        {
            CancelHint(); ClearFlash(); CloseDialog(); session.Pause(); choosing = true;
            gameRoot.SetActive(false); selectionRoot.SetActive(true); Save(); RefreshSelection();
        }
        private void ContinueCurrent()
        {
            choosing = false; selectionRoot.SetActive(false); gameRoot.SetActive(true);
            CloseDialog(); session.Resume(); resultDismissed = false; Refresh(); CheckEndState();
        }
        private void SelectLevel(int level)
        {
            var puzzle = catalog.Get(selectedDifficulty, level);
            if (puzzle.Id == session.Game.Puzzle.Id) { ContinueCurrent(); return; }
            if (session.Game.Phase == SudokuPhase.Playing && (session.Game.Revision > 0 || session.ElapsedSeconds > 0))
                ShowDialog("切换到新的关卡？", "当前未完成的对局将被替换。\n已经取得的通关记录会保留。", "开始新关卡", () => StartLevel(puzzle, true), "取消", () => { });
            else StartLevel(puzzle, true);
        }
        private void StartLevel(SudokuPuzzle puzzle, bool fresh)
        {
            if (puzzle == null) return;
            CancelHint(); ClearFlash(); CloseDialog();
            var previous = session.Export();
            session = new SudokuSession(new SudokuSaveData { Game = new SudokuGame(puzzle).GetSnapshot(),
                CompletedIds = previous.CompletedIds, UnassistedIds = previous.UnassistedIds }, false);
            choosing = false; resultDismissed = false; saveElapsed = 0; inspectionDigit = 0;
            selectedDifficulty = puzzle.Difficulty; levelPage = (puzzle.Level - 1) / 50;
            selectionRoot.SetActive(false); gameRoot.SetActive(true); Save(); Refresh();
        }
    }
}
