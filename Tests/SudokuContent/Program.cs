using System;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using XanaduYonder.Core.Sudoku;

internal static class Program
{
    internal const string Sample = "530070000600195000098000060800060003400803001700020006060000280000419005000080079";
    internal const string Answer = "534678912672195348198342567859761423426853791713924856961537284287419635345286179";
    private static int checks;
    internal static string Envelope(string rows)
    {
        string body = rows.Trim() + "\n";
        using (var hash = SHA256.Create()) return "# " + SudokuCatalog.Version + "|" + body.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length + "|" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(body))).Replace("-", "").ToLowerInvariant() + "\n" + body;
    }
    internal static int[] Digits(string s) { var a = new int[s.Length]; for (int i = 0; i < s.Length; i++) a[i] = s[i] - '0'; return a; }
    internal static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--import") { Importer.Run(args); return; }
        int[] solution;
        Check(SudokuSolver.CountSolutions(Digits(Sample), out solution) == 1, "A standard unique Sudoku must have exactly one solution");
        Check(string.Join("", solution) == Answer, "Solver must return the actual solution");
        Check(SudokuSolver.CountSolutions(new int[81], out solution) == 2, "Ambiguous grids must stop after two solutions");
        var invalid = Digits(Sample); invalid[1] = 5;
        Check(SudokuSolver.CountSolutions(invalid, out solution) == 0, "Repeated givens must be rejected");
        Check(SudokuSolver.CountSolutions(new int[80], out solution) == 0, "Invalid shape must be rejected");
        var puzzle = new SudokuPuzzle { Id = "test", Difficulty = 0, Level = 1, Givens = Digits(Sample), Solution = Digits(Answer) };
        string error;
        Check(puzzle.TryValidate(out error), "Valid unique puzzle must validate: " + error);
        var ambiguous = puzzle.Copy(); ambiguous.Givens = new int[81];
        Check(ambiguous.TryValidate(out error), "Runtime validation checks the stored solution, without searching uniqueness");
        var copy = puzzle.Copy(); copy.Givens[0] = 0;
        Check(puzzle.Givens[0] == 5, "Puzzle copy must own its arrays");
        var bad = puzzle.Copy(); bad.Solution[0] = 6;
        Check(!bad.TryValidate(out error), "Solution inconsistent with givens must fail");
        bad = puzzle.Copy(); bad.Level = 501;
        Check(!bad.TryValidate(out error), "Levels are bounded to 1 through 500");
        string row = "test|0|1|" + Sample + "|" + Answer;
        var catalog = SudokuCatalog.Parse(Envelope(row));
        Check(catalog.Count(0) == 1 && catalog.Puzzles.Count == 1, "Catalog parses one validated puzzle");
        Check(catalog.Get(0, 1).Id == "test" && catalog.Get(0, 2) == null, "Lookup respects difficulty and level");
        bool rejected = false;
        try { SudokuCatalog.Parse(Envelope(row + "\n" + row)); } catch (FormatException) { rejected = true; }
        Check(rejected, "Duplicate puzzle IDs or slots must be rejected");
        rejected = false;
        try { SudokuCatalog.Parse("test|0|1|x|x"); } catch (FormatException) { rejected = true; }
        Check(rejected, "Malformed catalog must be rejected");
        rejected = false;
        try { SudokuCatalog.Parse(Envelope(row).Replace("test|", "changed|")); } catch (FormatException) { rejected = true; }
        Check(rejected, "Changed payload must fail manifest integrity check");
        rejected = false;
        try { SudokuCatalog.Parse(Envelope(row).Replace(SudokuCatalog.Version, "unknown-version")); } catch (FormatException) { rejected = true; }
        Check(rejected, "Unknown bank versions must be rejected");
        var single = puzzle.Copy(); single.Givens = Digits(Answer); single.Givens[40] = 0;
        var hint = SudokuHintEngine.Find(single, single.Givens, 40);
        Check(hint != null && !hint.IsDirect && hint.Cell == 40 && hint.Digit == 5, "A single missing cell yields an explained placement");
        Check(hint.Evidence.Length > 0 && !string.IsNullOrWhiteSpace(hint.Explanation), "Logical hint contains visible evidence and explanation");
        bool canceled = false;
        try { SudokuHintEngine.Find(single, single.Givens, 40, new CancellationToken(true), 200); } catch (OperationCanceledException) { canceled = true; }
        Check(canceled, "Cancellation must throw without producing a paid hint");
        hint = SudokuHintEngine.Find(single, single.Givens, 40, CancellationToken.None, 0);
        Check(hint.IsDirect && hint.Explanation.Contains("预算"), "Exhausted budget must explicitly report direct reveal");
        int difficulty, steps;
        var brokenComplete = puzzle.Copy(); brokenComplete.Givens = Digits(Answer); brokenComplete.Solution[0] = 6;
        Check(!SudokuHintEngine.TrySolveLogically(brokenComplete, out difficulty, out steps), "Logical analysis rejects malformed completed puzzles");
        Check(SudokuHintEngine.TrySolveLogically(puzzle, out difficulty, out steps) && steps == 51, "A basic puzzle must replay to completion using logical placements");
        Check(SudokuHintEngine.Find(puzzle, Digits(Answer), -1) == null, "Solved boards must not issue hints");
        var wrong = Digits(Sample); wrong[2] = 1;
        Check(SudokuHintEngine.Find(puzzle, wrong, -1) == null, "Wrong formal digits require correction, not a paid logical hint");
        const string escargot = "100007090030020008009600500005300900010080002600004000300000010040000007007000300";
        int[] hardSolution; Check(SudokuSolver.CountSolutions(Digits(escargot), out hardSolution) == 1, "Fallback fixture is unique");
        var hard = new SudokuPuzzle { Id = "hard", Difficulty = 3, Level = 1, Givens = Digits(escargot), Solution = hardSolution };
        var replay = (int[])hard.Givens.Clone(); bool reachedDirect = false;
        for (int move = 0; move < 81; move++) { hint = SudokuHintEngine.Find(hard, replay, -1); if (hint == null) break; if (hint.IsDirect) { reachedDirect = true; break; } replay[hint.Cell] = hint.Digit; }
        Check(reachedDirect && hint.Technique == "直接揭晓", "Unsupported reasoning must be labelled direct reveal");
        TechniqueTests.Run();
        if (args.Length > 0 && args[0] == "--bank") ValidateBank();
        Console.WriteLine("PASS Sudoku content: " + checks + " assertions");
    }
    private static void ValidateBank()
    {
        const string path = "Assets/Resources/Sudoku/puzzles.txt";
        Check(File.Exists(path), "Bundled 2,000-puzzle bank must exist");
        var bank = SudokuCatalog.Parse(File.ReadAllText(path));
        Check(bank.Puzzles.Count == 2000, "Offline bank must contain exactly 2,000 puzzles");
        var boards = new HashSet<string>();
        var techniqueCounts = new Dictionary<string, int>();
        long maximumHintTicks = 0; int maximumExplanation = 0; int maximumProofSteps = 0;
        for (int difficulty = 0; difficulty < 4; difficulty++)
        {
            Check(bank.Count(difficulty) == 500, "Each difficulty must contain exactly 500 puzzles");
            int totalSteps = 0;
            for (int level = 1; level <= 500; level++)
            {
                var puzzle = bank.Get(difficulty, level);
                Check(puzzle != null && puzzle.Id == "se-v1-d" + difficulty + "-l" + level.ToString("000"), "Stable ID and contiguous levels");
                string error; Check(puzzle.TryValidate(out error), "Data validation: " + error);
                int[] verifiedSolution; Check(SudokuSolver.CountSolutions(puzzle.Givens, out verifiedSolution) == 1 && string.Join("", verifiedSolution) == string.Join("", puzzle.Solution), "Offline unique solution verification");
                Check(boards.Add(string.Join("", puzzle.Givens)), "No duplicate puzzle boards");
                var replay = (int[])puzzle.Givens.Clone(); int actual = 0, steps = 0;
                while (Array.IndexOf(replay, 0) >= 0)
                {
                    long started = Stopwatch.GetTimestamp();
                    var hint = SudokuHintEngine.Find(puzzle, replay, -1);
                    maximumHintTicks = Math.Max(maximumHintTicks, Stopwatch.GetTimestamp() - started);
                    maximumExplanation = Math.Max(maximumExplanation, hint == null ? 0 : hint.Explanation.Length);
                    maximumProofSteps = Math.Max(maximumProofSteps, hint == null ? 0 : hint.Steps.Length);
                    Check(hint != null && !hint.IsDirect && replay[hint.Cell] == 0 && hint.Digit == puzzle.Solution[hint.Cell], "Every bank puzzle must replay without direct reveals: " + puzzle.Id);
                    Check(hint.Explanation.Length <= 8192 && hint.Technique.Length <= 128 && hint.Evidence.Length <= 81, "Generated help fits the persisted hint contract: " + puzzle.Id);
                    ProofChecker.CheckHint(replay, hint);
                    foreach (var step in hint.Steps) { int count; techniqueCounts.TryGetValue(step.Technique, out count); techniqueCounts[step.Technique] = count + 1; }
                    actual = Math.Max(actual, hint.Difficulty); replay[hint.Cell] = hint.Digit; steps++;
                    Check(steps <= 81, "Logical replay progresses");
                }
                Check(string.Join("", replay) == string.Join("", puzzle.Solution), "Replay reaches stored solution");
                Check(actual == difficulty, "Difficulty follows actual strongest logical technique: " + puzzle.Id + " got " + actual);
                totalSteps += steps;
            }
            Console.WriteLine("BANK difficulty " + difficulty + ": 500 unique puzzles, " + totalSteps + " logical placements independently proven");
        }
        foreach (var entry in techniqueCounts) Console.WriteLine("PROOF " + entry.Key + ": " + entry.Value);
        Console.WriteLine("HINT maximum ms=" + (maximumHintTicks * 1000.0 / Stopwatch.Frequency).ToString("F3") + ", explanation chars=" + maximumExplanation + ", proof steps=" + maximumProofSteps);
    }
}
