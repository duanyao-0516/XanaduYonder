using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Sudoku;

namespace XanaduYonder.Runtime.Sudoku
{
    internal sealed class SudokuBoardView
    {
        private readonly Image[] backgrounds = new Image[81];
        private readonly Text[] values = new Text[81];
        private readonly Text[,] notes = new Text[81, 9];
        private readonly Image[,] noteBackings = new Image[81, 9];
        private readonly Outline[] selections = new Outline[81];
        private readonly Color normal = UiFactory.Hex("FFFEF9"), peer = UiFactory.Hex("EDF1E8"), same = UiFactory.Hex("C7DEC8"), selected = UiFactory.Hex("ACCDB2");
        private readonly Color error = UiFactory.Hex("C34A46");

        public SudokuBoardView(Transform parent, Action<int> select)
        {
            UiFactory.Panel(parent, "Grid backing", 0, 0, 648, 648, UiFactory.Ink, false);
            for (int cell = 0; cell < 81; cell++)
            {
                int index = cell;
                var backing = UiFactory.Panel(parent, "Cell " + (cell / 9 + 1) + "," + (cell % 9 + 1), cell % 9 * 72 + 1, cell / 9 * 72 + 1, 70, 70, normal, false);
                backgrounds[cell] = backing; backing.raycastTarget = true;
                var button = backing.gameObject.AddComponent<Button>(); button.targetGraphic = backing;
                button.transition = Selectable.Transition.None; button.onClick.AddListener(() => select(index));
                selections[cell] = backing.gameObject.AddComponent<Outline>();
                selections[cell].effectColor = UiFactory.Accent; selections[cell].effectDistance = new Vector2(2, -2); selections[cell].enabled = false;
                values[cell] = UiFactory.Label(backing.transform, "", 0, -2, 70, 70, 44, UiFactory.Ink, TextAnchor.MiddleCenter);
                for (int digit = 0; digit < 9; digit++)
                {
                    noteBackings[cell, digit] = UiFactory.Panel(backing.transform, "Candidate " + (digit + 1), 3 + digit % 3 * 22, 3 + digit / 3 * 22, 21, 21, Color.clear, false);
                    notes[cell, digit] = UiFactory.Label(noteBackings[cell, digit].transform, "", 0, -1, 21, 21, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
                }
            }
            for (int i = 0; i <= 9; i++)
            {
                float thickness = i % 3 == 0 ? 4 : 1;
                float offset = Mathf.Clamp(i * 72 - thickness / 2, 0, 648 - thickness);
                UiFactory.Panel(parent, "Column boundary " + i, offset, 0, thickness, 648, i % 3 == 0 ? UiFactory.Ink : UiFactory.Hex("BCC8BC"), false);
                UiFactory.Panel(parent, "Row boundary " + i, 0, offset, 648, thickness, i % 3 == 0 ? UiFactory.Ink : UiFactory.Hex("BCC8BC"), false);
            }
        }

        public void Render(SudokuGame game, SudokuControls controls, int[] flashing, bool flashOn, int[] evidence = null, int inspectionDigit = 0)
        {
            int selectedCell = controls.SelectedCell;
            int matching = inspectionDigit > 0 ? inspectionDigit : controls.Quick ? controls.QuickDigit : selectedCell >= 0 ? game.ValueAt(selectedCell) : 0;
            for (int cell = 0; cell < 81; cell++)
            {
                int value = game.ValueAt(cell), mask = game.NotesAt(cell);
                bool selectedHere = cell == selectedCell;
                bool related = selectedCell >= 0 && ArePeers(cell, selectedCell);
                bool sameValue = matching != 0 && value == matching;
                bool hinted = evidence != null && Array.IndexOf(evidence, cell) >= 0;
                bool redFlash = flashOn && flashing != null && Array.IndexOf(flashing, cell) >= 0;
                backgrounds[cell].color = redFlash ? UiFactory.Hex("F2C9BC") : selectedHere ? selected : sameValue ? same : hinted ? UiFactory.Hex("E9E0B9") : related ? peer : normal;
                selections[cell].enabled = selectedHere;
                values[cell].text = value == 0 ? "" : value.ToString();
                values[cell].color = value != 0 && !game.IsCorrect(cell) || redFlash ? error : game.IsGiven(cell) ? UiFactory.Ink : UiFactory.Accent;
                values[cell].fontStyle = game.IsGiven(cell) ? FontStyle.Normal : FontStyle.Bold;
                for (int d = 0; d < 9; d++)
                {
                    bool present = value == 0 && (mask & (1 << d)) != 0;
                    notes[cell, d].text = present ? (d + 1).ToString() : "";
                    noteBackings[cell, d].color = present && matching == d + 1 ? same : Color.clear;
                    notes[cell, d].color = present && matching == d + 1 ? UiFactory.Ink : UiFactory.Muted;
                }
            }
        }

        private static bool ArePeers(int a, int b)
        {
            return a / 9 == b / 9 || a % 9 == b % 9 || a / 27 == b / 27 && a % 9 / 3 == b % 9 / 3;
        }
    }
}
