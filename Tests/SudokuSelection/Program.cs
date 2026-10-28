using System;
using XanaduYonder.Core.Sudoku;
using XanaduYonder.Runtime.Sudoku;

internal static class Program
{
    private static int passed;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Run(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
    private static SudokuSaveData Saved(int difficulty, int level)
    {
        return new SudokuSaveData { Game = new SudokuSnapshot { Puzzle = new SudokuPuzzle { Difficulty = difficulty, Level = level } } };
    }
    private static void Main()
    {
        Run("new difficulty starts first level", () => {
            var a = SudokuLevelSelection.Resolve(0, null, new string[0]);
            Check(a.Kind == SudokuLevelActionKind.Start && a.Level == 1, "fresh must start");
        });
        Run("start uses first unfinished level only within selected difficulty", () => {
            var a = SudokuLevelSelection.Resolve(1, null, new[] { "se-v1-d0-l003", "se-v1-d1-l001", "se-v1-d1-l002", "se-v1-d1-l004" });
            Check(a.Kind == SudokuLevelActionKind.Start && a.Level == 3, "wrong level");
        });
        Run("entered zero revision board continues by slot existence", () => {
            var a = SudokuLevelSelection.Resolve(2, Saved(2, 26), new string[0]);
            Check(a.Kind == SudokuLevelActionKind.Continue && a.Level == 26, "empty started board lost");
        });
        Run("ongoing replay takes priority over completed badge", () => {
            var a = SudokuLevelSelection.Resolve(0, Saved(0, 1), new[] { "se-v1-d0-l001" });
            Check(a.Kind == SudokuLevelActionKind.Continue && a.Level == 1, "replay should continue");
        });
        Run("all completed gives replay first level", () => {
            var ids = new string[500]; for (int i = 0; i < 500; i++) ids[i] = "se-v1-d3-l" + (i + 1).ToString("000");
            var a = SudokuLevelSelection.Resolve(3, null, ids);
            Check(a.Kind == SudokuLevelActionKind.Replay && a.Level == 1, "replay missing");
        });
        Run("different level same difficulty requests replacement", () => {
            Check(SudokuLevelSelection.NeedsReplacement(Saved(1, 4), 1, 5), "overwrite unconfirmed");
        });
        Run("same level resumes and other difficulty never replaces", () => {
            Check(!SudokuLevelSelection.NeedsReplacement(Saved(1, 4), 1, 4), "same level");
            Check(!SudokuLevelSelection.NeedsReplacement(Saved(1, 4), 2, 5), "different difficulty");
            Check(!SudokuLevelSelection.NeedsReplacement(null, 2, 5), "empty slot");
        });
        Console.WriteLine("Sudoku selection checks: " + passed + " passed, 0 failed.");
    }
}
