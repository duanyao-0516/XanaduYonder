using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Sudoku;

namespace XanaduYonder.Runtime.Sudoku
{
    internal sealed partial class SudokuPage
    {
        private readonly Button[] levelButtons = new Button[50];
        private Text selectionSummary, levelPageLabel, resumeLabel;
        private int selectedDifficulty, levelPage;
        private Button previousPage, nextPage;
        private SudokuDifficultyDropdown difficultyDropdown;
        private bool CanSelect { get { return visible && choosing && dialog == null && acceptsInput(); } }

        private void BuildSelection()
        {
            var page = UiFactory.Rect(root, "Sudoku level selection", 0, 0, 720, 1320); UiFactory.Stretch(page); selectionRoot = page.gameObject;
            selectionInput = page.gameObject.AddComponent<CanvasGroup>();
            var nav = UiFactory.Band(page, "Selection navigation", 0, 92, 0);
            UiFactory.IconButton(nav, UiIconKind.Home, 56, 34, 58, 58, () => { if (CanSelect) returnLobby(); });
            UiFactory.Label(nav, "XANADU / YONDER", 210, 34, 300, 58, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.IconButton(nav, UiIconKind.Book, 536, 34, 58, 58, ShowRules);
            UiFactory.IconButton(nav, UiIconKind.Settings, 606, 34, 58, 58, () => { if (CanSelect) { difficultyDropdown.Hide(); openSettings(); } });
            var title = UiFactory.Band(page, "Selection heading", 108, 96, .05f);
            UiFactory.Label(title, "数独", 36, 0, 260, 80, 52, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(title, "基础九宫 · 离线 2,000 关", 280, 20, 404, 50, 24, UiFactory.Muted, TextAnchor.MiddleRight);
            var controls = UiFactory.Band(page, "Difficulty and game action", 228, 76, .12f);
            difficultyDropdown = BuildDifficultyDropdown(controls);
            resumeLabel = UiFactory.Button(controls, "开始", 290, 0, 394, 76, ActivateSelection, true).GetComponentInChildren<Text>();
            selectionSummary = UiFactory.Label(UiFactory.Band(page, "Selection summary", 326, 42, .18f), "", 36, 0, 648, 42, 22, UiFactory.Muted, TextAnchor.MiddleLeft);
            var grid = UiFactory.Band(page, "Level grid", 390, 748, .40f);
            for (int i = 0; i < 50; i++)
            {
                int offset = i;
                levelButtons[i] = UiFactory.Button(grid, "", 36 + i % 5 * 132, i / 5 * 76, 120, 64, () => { if (CanSelect) SelectLevel(levelPage * 50 + offset + 1); });
                levelButtons[i].GetComponentInChildren<Text>().fontSize = 21;
            }
            var pagination = UiFactory.Band(page, "Level pages", 1164, 66, .90f);
            previousPage = SudokuIcon.Button(pagination, "上一页", SudokuIconKind.Left, 214, 0, 66, () => ChangePage(-1), true);
            levelPageLabel = UiFactory.Label(pagination, "", 286, 0, 148, 66, 25, UiFactory.Ink, TextAnchor.MiddleCenter);
            nextPage = SudokuIcon.Button(pagination, "下一页", SudokuIconKind.Right, 440, 0, 66, () => ChangePage(1), true);
            UiFactory.Label(UiFactory.Band(page, "Legend", 1238, 30, 1), "每个难度保留 1 场对局 · 独立 / 提示完成", 36, 0, 648, 30, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
        }

        private SudokuDifficultyDropdown BuildDifficultyDropdown(Transform parent)
        {
            var surface = UiFactory.Panel(parent, "Difficulty dropdown", 36, 0, 230, 76, UiFactory.Hex("E7EDE4")); surface.raycastTarget = true;
            var dropdown = surface.gameObject.AddComponent<SudokuDifficultyDropdown>(); dropdown.targetGraphic = surface; dropdown.CanOpen = () => CanSelect;
            dropdown.captionText = UiFactory.Label(surface.transform, Difficulties[selectedDifficulty], 22, 0, 158, 76, 28, UiFactory.Ink);
            SudokuIcon.Add(surface.transform, SudokuIconKind.Down, 186, 25, 26);
            var template = UiFactory.Panel(surface.transform, "Difficulty options", 0, 82, 230, 272, UiFactory.Hex("D6C9AE")).rectTransform;
            var viewport = UiFactory.Rect(template, "Viewport", 8, 0, 214, 272);
            viewport.gameObject.AddComponent<RectMask2D>();
            // Dropdown derives all row positions from this single-item template. Its 8-unit
            // padding keeps square rows inside the rounded outer panel, including the ends.
            var content = UiFactory.Rect(viewport, "Content", 0, 0, 214, 80);
            var item = UiFactory.Panel(content, "Option", 0, 8, 214, 64, UiFactory.Hex("F7F0E2"), false); item.raycastTarget = true;
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = item;
            toggle.graphic = UiFactory.Panel(item.transform, "Selected band", 0, 0, 214, 64, UiFactory.Hex("DBC7A3"), false);
            dropdown.itemText = UiFactory.Label(item.transform, "", 22, 0, 170, 64, 26, UiFactory.Ink);
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            dropdown.template = template;
            foreach (var name in Difficulties) dropdown.options.Add(new Dropdown.OptionData(name));
            dropdown.SetValueWithoutNotify(selectedDifficulty); dropdown.RefreshShownValue(); template.gameObject.SetActive(false);
            dropdown.onValueChanged.AddListener(d => {
                if (!CanSelect) { dropdown.SetValueWithoutNotify(selectedDifficulty); return; }
                profile.Pages[selectedDifficulty] = levelPage;
                selectedDifficulty = d; levelPage = profile.Pages[d]; Save(); RefreshSelection();
            });
            return dropdown;
        }

        private void ChangePage(int step)
        {
            if (!CanSelect) return;
            levelPage = Mathf.Clamp(levelPage + step, 0, 9); Save(); RefreshSelection();
        }
        private void RefreshSelection()
        {
            var slot = profile.GetSlot(selectedDifficulty);
            int completed = profile.CompletedCountForDifficulty(selectedDifficulty);
            selectionSummary.text = "已完成 " + completed + " / 500" + (slot == null ? "  ·  暂无进行中的对局" : "  ·  第 " + slot.Game.Puzzle.Level + " 关进行中  " + FormatTime(slot.ElapsedSeconds));
            difficultyDropdown.SetValueWithoutNotify(selectedDifficulty);
            var action = SudokuLevelSelection.Resolve(selectedDifficulty, slot, profile.CompletedIds);
            resumeLabel.text = (action.Kind == SudokuLevelActionKind.Continue ? "继续" : action.Kind == SudokuLevelActionKind.Replay ? "重玩" : "开始") + " · 第 " + action.Level + " 关";
            for (int i = 0; i < 50; i++)
            {
                var puzzle = catalog.Get(selectedDifficulty, levelPage * 50 + i + 1);
                bool done = Array.IndexOf(profile.CompletedIds, puzzle.Id) >= 0;
                bool independent = Array.IndexOf(profile.UnassistedIds, puzzle.Id) >= 0;
                bool playing = slot != null && slot.Game.Puzzle.Id == puzzle.Id;
                levelButtons[i].GetComponentInChildren<Text>().text = puzzle.Level + (playing ? "\n进行中" : done ? independent ? "\n独立完成" : "\n提示完成" : "");
                levelButtons[i].GetComponent<Image>().color = playing ? UiFactory.Hex("E7DAB3") : done ? UiFactory.Hex("D0E0C2") : UiFactory.Hex("E7EDE4");
            }
            previousPage.interactable = levelPage > 0; nextPage.interactable = levelPage < 9;
            previousPage.GetComponentInChildren<SudokuIcon>().color = levelPage > 0 ? UiFactory.Ink : UiFactory.Hex("BCC6BF");
            nextPage.GetComponentInChildren<SudokuIcon>().color = levelPage < 9 ? UiFactory.Ink : UiFactory.Hex("BCC6BF");
            levelPageLabel.text = (levelPage + 1) + " / 10";
        }
        private void ReturnToSelection()
        {
            CancelHint(); ClearFlash(); CloseDialog(); session.Pause(); choosing = true;
            gameRoot.SetActive(false); selectionRoot.SetActive(true); Save(); RefreshSelection();
        }
        private void ActivateSelection()
        {
            if (!CanSelect) return;
            var action = SudokuLevelSelection.Resolve(selectedDifficulty, profile.GetSlot(selectedDifficulty), profile.CompletedIds);
            SelectLevel(action.Level);
        }
        private void SelectLevel(int level)
        {
            var puzzle = catalog.Get(selectedDifficulty, level);
            var slot = profile.GetSlot(selectedDifficulty);
            if (slot != null && slot.Game.Puzzle.Id == puzzle.Id) { ResumeSlot(slot); return; }
            if (SudokuLevelSelection.NeedsReplacement(slot, selectedDifficulty, level))
                ShowDialog("替换这个难度的对局？", Difficulties[selectedDifficulty] + " · 第 " + slot.Game.Puzzle.Level + " 关尚未完成。\n\n每个难度最多保存 1 场对局，开始第 " + level + " 关将替换它。其他难度的对局和历史通关记录会保留。", "开始新关卡", () => StartLevel(puzzle, true), "取消", () => { });
            else StartLevel(puzzle, true);
        }
        private void ResumeSlot(SudokuSaveData saved)
        {
            CancelHint(); ClearFlash(); CloseDialog(); session.Pause(); Save();
            session = new SudokuSession(saved, true); sessionStarted = true; ShowBoard(); session.Resume(); Save(); Refresh();
        }
        private void StartLevel(SudokuPuzzle puzzle, bool fresh)
        {
            if (puzzle == null) return;
            CancelHint(); ClearFlash(); CloseDialog(); session.Pause(); Save();
            session = new SudokuSession(profile.CreateGame(puzzle), false); sessionStarted = true;
            ShowBoard(); Save(); Refresh();
        }
        private void ShowBoard()
        {
            difficultyDropdown.Hide(); choosing = false; resultDismissed = false; saveElapsed = 0; inspectionDigit = 0;
            selectedDifficulty = session.Game.Puzzle.Difficulty; levelPage = (session.Game.Puzzle.Level - 1) / 50;
            selectionRoot.SetActive(false); gameRoot.SetActive(true);
        }
    }
}
