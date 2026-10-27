using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using XanaduYonder.Core.Sheep;

internal static class RaterChecks
{
    internal static void Run()
    {
        var watch = Stopwatch.StartNew();
        var boards = new List<SheepSnapshot>();
        var layouts = new HashSet<int>();
        for (uint seed = 1; layouts.Count < SheepBoardLayout.ChallengeLayoutCount - SheepBoardLayout.NewLayoutStart && seed <= 1000; seed++)
        {
            SheepSnapshot board = SheepLevelGenerator.Generate(SheepMode.Challenge, SheepDifficulty.Hard, seed).Snapshot;
            if (layouts.Add(board.LayoutId)) boards.Add(board);
        }
        Require(layouts.Count == SheepBoardLayout.ChallengeLayoutCount - SheepBoardLayout.NewLayoutStart,
            "Hard samples must cover every current challenge layout.");
        boards.Add(SheepLevelGenerator.Generate(SheepMode.Challenge, SheepDifficulty.Easy, 317).Snapshot);
        boards.Add(SheepLevelGenerator.Generate(SheepMode.Challenge, SheepDifficulty.Normal, 719).Snapshot);
        boards.Add(SheepLevelGenerator.Generate(SheepMode.Tutorial, SheepDifficulty.Normal, 123).Snapshot);
        boards.Add(SheepLevelGenerator.Generate(SheepMode.Endless, SheepDifficulty.Normal, 523).Snapshot);
        var serialization = new JsonSerializerOptions { IncludeFields = true };
        int comparisons = 0;
        var toolTotals = new int[4];
        foreach (SheepSnapshot board in boards)
        {
            string before = JsonSerializer.Serialize(board, serialization);
            SheepPosition[] positions = SheepBoardLayout.GetPositions(board.Level, board.LayoutId);
            foreach (bool exposedOnly in new[] { false, true })
            foreach (int width in new[] { 0, 8 })
            {
                string policy = width == 0 ? "greedy" : "visible3";
                string observation = exposedOnly ? "exposed" : "generous";
                PlayerOutcome independent = VisiblePlayer.Run(board, true, policy, observation);
                int first = SheepDifficultyRater.ClearedWithTools(board, positions, width, exposedOnly);
                int second = SheepDifficultyRater.ClearedWithTools(board, positions, width, exposedOnly);
                string description = $"layout {board.LayoutId}, {board.Mode}/{board.Difficulty}, {policy}/{observation}";
                Require(first == independent.Cleared, description + " rater/player cleared count mismatch: " + first + "/" + independent.Cleared);
                Require(first == second, description + " rating must repeat deterministically.");
                Require(before == JsonSerializer.Serialize(board, serialization), description + " rating mutated its input snapshot.");
                foreach (int quota in new[] { 0, 99 })
                {
                    SheepSnapshot differentQuota = board.Copy();
                    differentQuota.RemoveLimit = differentQuota.RemoveRemaining = quota;
                    differentQuota.UndoLimit = differentQuota.UndoRemaining = quota;
                    differentQuota.ShuffleLimit = differentQuota.ShuffleRemaining = quota;
                    string quotaBefore = JsonSerializer.Serialize(differentQuota, serialization);
                    Require(SheepDifficultyRater.ClearedWithTools(differentQuota, positions, width, exposedOnly) == first,
                        description + " rating must use fixed standard tools instead of selected quota " + quota + ".");
                    Require(quotaBefore == JsonSerializer.Serialize(differentQuota, serialization),
                        description + " rating mutated the selected quotas.");
                }
                for (int tool = 0; tool < toolTotals.Length; tool++) toolTotals[tool] += independent.ToolUses[tool];
                comparisons++;
            }
        }
        for (int tool = 0; tool < toolTotals.Length; tool++)
            Require(toolTotals[tool] > 0, "Parity samples must exercise tool " + (SheepTool)tool + ".");
        Console.WriteLine($"PASS rater parity: {boards.Count} boards, {comparisons} independent comparisons; all {layouts.Count} current challenge layouts; both observation policies; greedy and three-step beam 8.");
        Console.WriteLine("PASS deterministic, input-preserving, quota-independent scoring; tool coverage Remove/Undo/Shuffle/Revive=" + string.Join("/", toolTotals));
        Console.WriteLine($"Elapsed {watch.Elapsed.TotalSeconds:F2}s.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
