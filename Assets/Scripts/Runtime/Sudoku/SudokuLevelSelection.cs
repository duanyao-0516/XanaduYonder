using System;
using System.Collections.Generic;
using System.Globalization;

namespace XanaduYonder.Runtime.Sudoku
{
    public enum SudokuLevelActionKind { Start, Continue, Replay }
    public struct SudokuLevelAction
    {
        public SudokuLevelActionKind Kind;
        public int Level;
    }

    public static class SudokuLevelSelection
    {
        public static SudokuLevelAction Resolve(int difficulty, SudokuSaveData slot, string[] completed)
        {
            if (difficulty < 0 || difficulty > 3) throw new ArgumentOutOfRangeException(nameof(difficulty));
            if (slot != null && slot.Game.Puzzle.Difficulty == difficulty)
                return new SudokuLevelAction { Kind = SudokuLevelActionKind.Continue, Level = slot.Game.Puzzle.Level };
            var done = new HashSet<string>(completed ?? new string[0], StringComparer.Ordinal);
            for (int level = 1; level <= 500; level++)
                if (!done.Contains("se-v1-d" + difficulty.ToString(CultureInfo.InvariantCulture) + "-l" + level.ToString("000", CultureInfo.InvariantCulture)))
                    return new SudokuLevelAction { Kind = SudokuLevelActionKind.Start, Level = level };
            return new SudokuLevelAction { Kind = SudokuLevelActionKind.Replay, Level = 1 };
        }
        public static bool NeedsReplacement(SudokuSaveData slot, int difficulty, int level)
        {
            return slot != null && slot.Game.Puzzle.Difficulty == difficulty && slot.Game.Puzzle.Level != level;
        }
    }
}
