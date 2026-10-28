using System;
using System.Collections.Generic;
using System.Globalization;
using XanaduYonder.Core.Sudoku;

namespace XanaduYonder.Runtime.Sudoku
{
    public sealed class SudokuSavedHint
    {
        public int Cell;
        public int Digit;
        public int Revision;
        public string Technique;
        public string Explanation;
        public int[] Evidence;
        public bool IsDirect;
        public bool Revealed;

        public SudokuSavedHint Copy()
        {
            return new SudokuSavedHint
            {
                Cell = Cell, Digit = Digit, Revision = Revision, Technique = Technique,
                Explanation = Explanation, Evidence = Evidence == null ? null : (int[])Evidence.Clone(),
                IsDirect = IsDirect, Revealed = Revealed
            };
        }
    }

    // The session records completed games before exporting. Pause, panels and animations are transient.
    public sealed class SudokuSaveData
    {
        public SudokuSnapshot Game;
        public double ElapsedSeconds;
        public bool Pencil;
        public bool Quick;
        public int SelectedCell = -1;
        public int QuickDigit;
        public SudokuSavedHint Hint;
        public string[] CompletedIds = new string[0];
        public string[] UnassistedIds = new string[0];

        public SudokuSaveData Copy()
        {
            return new SudokuSaveData
            {
                Game = Game == null ? null : Game.Copy(), ElapsedSeconds = ElapsedSeconds,
                Pencil = Pencil, Quick = Quick, SelectedCell = SelectedCell, QuickDigit = QuickDigit,
                Hint = Hint == null ? null : Hint.Copy(),
                CompletedIds = CompletedIds == null ? null : (string[])CompletedIds.Clone(),
                UnassistedIds = UnassistedIds == null ? null : (string[])UnassistedIds.Clone()
            };
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (Game == null) return Fail("Missing sudoku game state.", out error);
            if (!Game.TryValidate(out error)) return false;
            var puzzle = Game.Puzzle;
            var expectedId = "se-v1-d" + puzzle.Difficulty.ToString(CultureInfo.InvariantCulture) + "-l" + puzzle.Level.ToString("000", CultureInfo.InvariantCulture);
            if (!string.Equals(puzzle.Id, expectedId, StringComparison.Ordinal))
                return Fail("Sudoku puzzle identity does not match its difficulty and level.", out error);
            if (double.IsNaN(ElapsedSeconds) || double.IsInfinity(ElapsedSeconds) || ElapsedSeconds < 0)
                return Fail("Sudoku elapsed time must be finite and nonnegative.", out error);
            if (SelectedCell < -1 || SelectedCell > 80 || QuickDigit < 0 || QuickDigit > 9)
                return Fail("Invalid sudoku input selection.", out error);
            if (!ValidIds(CompletedIds, out var completed) || !ValidIds(UnassistedIds, out var unassisted))
                return Fail("Invalid or repeated sudoku completion identity.", out error);
            if (!unassisted.IsSubsetOf(completed))
                return Fail("Unassisted completions must also be completed.", out error);
            var solved = true;
            var wrong = false;
            for (var cell = 0; cell < 81; cell++)
            {
                if (Game.Values[cell] != puzzle.Solution[cell]) solved = false;
                if (Game.Values[cell] != 0 && Game.Values[cell] != puzzle.Solution[cell]) wrong = true;
            }
            if (solved && (!completed.Contains(puzzle.Id) || (Game.HintsUsed == 0 && !unassisted.Contains(puzzle.Id))))
                return Fail("Sudoku completion must be recorded with its terminal board.", out error);
            if (Hint != null)
            {
                if (solved || wrong || Game.Mistakes >= 3 || Game.HintsUsed <= 0 ||
                    Hint.Revision != Game.Revision || Hint.Cell < 0 || Hint.Cell > 80 ||
                    Hint.Digit < 1 || Hint.Digit > 9 || Hint.Digit != puzzle.Solution[Hint.Cell] ||
                    puzzle.Givens[Hint.Cell] != 0 || Game.Values[Hint.Cell] == Hint.Digit)
                    return Fail("Saved sudoku help is unpaid, expired or inconsistent with the board.", out error);
                if (string.IsNullOrWhiteSpace(Hint.Technique) || Hint.Technique.Length > 128 ||
                    string.IsNullOrWhiteSpace(Hint.Explanation) || Hint.Explanation.Length > 8192 ||
                    Hint.Evidence == null || Hint.Evidence.Length > 81)
                    return Fail("Saved sudoku help has invalid explanation or evidence.", out error);
                foreach (var cell in Hint.Evidence)
                    if (cell < 0 || cell > 80) return Fail("Saved sudoku help references an invalid cell.", out error);
            }
            return true;
        }

        internal static bool ValidIds(string[] ids, out HashSet<string> result)
        {
            result = new HashSet<string>(StringComparer.Ordinal);
            if (ids == null || ids.Length > 2000) return false;
            foreach (var id in ids)
            {
                if (id == null || id.Length != 13 || !id.StartsWith("se-v1-d", StringComparison.Ordinal) ||
                    id[7] < '0' || id[7] > '3' || id[8] != '-' || id[9] != 'l' ||
                    id[10] < '0' || id[10] > '9' || id[11] < '0' || id[11] > '9' || id[12] < '0' || id[12] > '9') return false;
                var level = (id[10] - '0') * 100 + (id[11] - '0') * 10 + id[12] - '0';
                if (level < 1 || level > 500 || !result.Add(id)) return false;
            }
            return true;
        }

        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
