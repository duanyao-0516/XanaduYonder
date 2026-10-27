using System;
using System.Collections.Generic;
using XanaduYonder.Core.Sudoku;

namespace XanaduYonder.Runtime.Sudoku
{
    public sealed class SudokuProfile
    {
        public int SelectedDifficulty;
        public int[] Pages = new int[4];
        public string[] CompletedIds = new string[0];
        public string[] UnassistedIds = new string[0];
        public SudokuSaveData[] Slots = new SudokuSaveData[4];
        public int CompletedCount { get { return CompletedIds.Length; } }

        public SudokuSaveData GetSlot(int difficulty)
        {
            if (difficulty < 0 || difficulty >= 4) throw new ArgumentOutOfRangeException(nameof(difficulty));
            var slot = Slots[difficulty];
            if (slot == null) return null;
            var result = slot.Copy();
            // A suspended game can predate a completion in another difficulty.
            result.CompletedIds = (string[])CompletedIds.Clone();
            result.UnassistedIds = (string[])UnassistedIds.Clone();
            return result;
        }

        public void Capture(SudokuSaveData data)
        {
            if (data == null) throw new ArgumentException("Missing sudoku session.", nameof(data));
            if (!data.TryValidate(out var error)) throw new ArgumentException(error, nameof(data));
            if (!TryValidate(out error)) throw new ArgumentException(error, nameof(data));
            var saved = data.Copy();
            var completed = Union(CompletedIds, saved.CompletedIds);
            var unassisted = Union(UnassistedIds, saved.UnassistedIds);
            // Finish all validation and snapshot construction before replacing any owned state.
            CompletedIds = completed;
            UnassistedIds = unassisted;
            Slots[saved.Game.Puzzle.Difficulty] = IsPlaying(saved) ? saved : null;
        }

        public SudokuSaveData CreateGame(SudokuPuzzle puzzle)
        {
            if (!TryValidate(out var error)) throw new ArgumentException(error, nameof(puzzle));
            var result = new SudokuSaveData
            {
                Game = new SudokuGame(puzzle).GetSnapshot(),
                CompletedIds = (string[])CompletedIds.Clone(),
                UnassistedIds = (string[])UnassistedIds.Clone()
            };
            if (!result.TryValidate(out error)) throw new ArgumentException(error, nameof(puzzle));
            return result;
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (SelectedDifficulty < 0 || SelectedDifficulty >= 4)
                return Fail("Invalid selected sudoku difficulty.", out error);
            if (Pages == null || Pages.Length != 4 || Slots == null || Slots.Length != 4)
                return Fail("Sudoku profile must have exactly four pages and slots.", out error);
            foreach (var page in Pages)
                if (page < 0 || page >= 10) return Fail("Sudoku selection page is outside its range.", out error);
            if (!SudokuSaveData.ValidIds(CompletedIds, out var completed) || !SudokuSaveData.ValidIds(UnassistedIds, out var unassisted))
                return Fail("Invalid or repeated sudoku profile completion identity.", out error);
            if (!unassisted.IsSubsetOf(completed))
                return Fail("Unassisted profile completions must also be completed.", out error);
            for (var difficulty = 0; difficulty < 4; difficulty++)
            {
                var slot = Slots[difficulty];
                if (slot == null) continue;
                if (!slot.TryValidate(out error)) return false;
                if (slot.Game.Puzzle.Difficulty != difficulty)
                    return Fail("Sudoku slot does not belong to its difficulty.", out error);
                if (!IsPlaying(slot)) return Fail("Only unfinished sudoku games may occupy a slot.", out error);
                foreach (var id in slot.CompletedIds)
                    if (!completed.Contains(id)) return Fail("Slot completions must be included in the profile.", out error);
                foreach (var id in slot.UnassistedIds)
                    if (!unassisted.Contains(id)) return Fail("Slot unassisted completions must be included in the profile.", out error);
            }
            return true;
        }

        public SudokuProfile Copy()
        {
            var slots = Slots == null ? null : new SudokuSaveData[Slots.Length];
            if (slots != null)
                for (var index = 0; index < slots.Length; index++) slots[index] = Slots[index] == null ? null : Slots[index].Copy();
            return new SudokuProfile
            {
                SelectedDifficulty = SelectedDifficulty,
                Pages = Pages == null ? null : (int[])Pages.Clone(),
                CompletedIds = CompletedIds == null ? null : (string[])CompletedIds.Clone(),
                UnassistedIds = UnassistedIds == null ? null : (string[])UnassistedIds.Clone(),
                Slots = slots
            };
        }

        private static bool IsPlaying(SudokuSaveData data)
        {
            if (data.Game.Mistakes == 3) return false;
            for (var cell = 0; cell < 81; cell++)
                if (data.Game.Values[cell] != data.Game.Puzzle.Solution[cell]) return true;
            return false;
        }

        private static string[] Union(string[] current, string[] incoming)
        {
            var seen = new HashSet<string>(current, StringComparer.Ordinal);
            var result = new List<string>(current);
            foreach (var id in incoming) if (seen.Add(id)) result.Add(id);
            return result.ToArray();
        }

        private static bool Fail(string message, out string error) { error = message; return false; }
    }
}
