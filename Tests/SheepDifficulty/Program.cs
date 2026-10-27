using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using XanaduYonder.Core.Sheep;

internal static class Program
{
    private sealed class Options
    {
        internal int Samples = 50;
        internal uint SeedStart = 1001;
        internal string Policy = "greedy", Tools = "default", Observation = "generous", Output;
        internal bool Benchmark;
    }

    private static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--check-rater")) { RaterChecks.Run(); return 0; }
            if (args.Contains("--help")) { Help(); return 0; }
            var options = Parse(args);
            var stopwatch = Stopwatch.StartNew();
            // The player boundary is checked independently of difficulty tuning.
            foreach (SheepDifficulty difficulty in Enum.GetValues<SheepDifficulty>())
            {
                var initial = SheepLevelGenerator.Generate(SheepMode.Challenge, difficulty, options.SeedStart).Snapshot;
                VisiblePlayer.CheckVisibilityBoundary(initial, options.Observation);
                if (initial.RemoveRemaining != 2 || initial.UndoRemaining != 3 || initial.ShuffleRemaining != 1 || initial.ReviveUsed)
                    throw new Exception("Difficulty benchmark requires default tool inventory.");
            }
            Console.WriteLine($"PASS {options.Observation} information boundary; hidden-kind permutations cannot change any player policy.");
            Console.WriteLine($"Seeds {options.SeedStart}..{options.SeedStart + options.Samples - 1}; " +
                "deterministic simulations, not measured human win rates.");

            var results = new List<PlayerOutcome>();
            foreach (string policy in options.Policy == "both" ? new[] { "greedy", "visible3" } : new[] { options.Policy })
            foreach (bool tools in options.Tools == "both" ? new[] { false, true } : new[] { options.Tools == "default" })
            foreach (SheepDifficulty difficulty in Enum.GetValues<SheepDifficulty>())
            {
                var batch = new List<PlayerOutcome>();
                for (int index = 0; index < options.Samples; index++)
                {
                    uint seed = options.SeedStart + (uint)index;
                    // Only the initial snapshot crosses into the player.
                    var initial = SheepLevelGenerator.Generate(SheepMode.Challenge, difficulty, seed).Snapshot;
                    var result = VisiblePlayer.Run(initial, tools, policy, options.Observation);
                    result.Seed = seed;
                    batch.Add(result);
                }
                results.AddRange(batch);
                Print(batch);
            }
            if (options.Output != null)
            {
                string path = Path.GetFullPath(options.Output);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine("Saved per-seed evidence: " + path);
            }
            bool passed = options.Benchmark || CheckGradient(results);
            Console.WriteLine($"Elapsed {stopwatch.Elapsed.TotalSeconds:F2}s. " +
                (options.Benchmark ? "Benchmark only; no calibration thresholds asserted." : passed ? "PASS difficulty calibration." : "FAIL difficulty calibration."));
            return passed ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + error.Message);
            return 1;
        }
    }

    private static void Print(List<PlayerOutcome> batch)
    {
        var first = batch[0];
        int[] picks = batch.Select(result => result.Picks).OrderBy(value => value).ToArray();
        int[] cleared = batch.Select(result => result.Cleared).OrderBy(value => value).ToArray();
        string Quantiles(int[] values) => $"{values[values.Length / 10]}/{values[values.Length / 2]}/{values[values.Length * 9 / 10]}";
        Console.WriteLine($"{first.Observation ?? "generous",-8} {first.Policy,-8} {(first.Tools ? "default" : "none"),-7} {first.Difficulty,-7} " +
            $"won {batch.Count(result => result.Won),3}/{batch.Count} " +
            $"picks p10/50/90={Quantiles(picks)} cleared={Quantiles(cleared)} " +
            $"initial exposed={batch.Average(result => result.InitialExposed):F1} " +
            $"initial triple kinds={batch.Average(result => result.InitialTripleKinds):F2} " +
            "tool totals=" + string.Join("/", Enumerable.Range(0, 4).Select(index => batch.Sum(result => result.ToolUses[index]))));
    }

    private static bool CheckGradient(List<PlayerOutcome> results)
    {
        var wins = new double[4];
        foreach (SheepDifficulty difficulty in Enum.GetValues<SheepDifficulty>())
        {
            var batch = results.Where(result => result.Policy == "greedy" && result.Observation == null && result.Tools && result.Difficulty == difficulty.ToString()).ToArray();
            if (batch.Length == 0) throw new Exception("Regression mode requires --policy greedy or both and --tools default or both; use --benchmark for other reports.");
            wins[(int)difficulty] = batch.Count(result => result.Won) / (double)batch.Length;
        }
        bool passed = true;
        void Require(bool condition, string description)
        {
            if (!condition) { Console.WriteLine("FAIL " + description); passed = false; }
        }
        // Fixed regression acceptance targets, independent of witness tray usage.
        // They describe this named heuristic, not an estimate of human success.
        Require(wins[0] >= 0.90, $"Easy default-tools completion must be >=90%; actual {wins[0]:P0}.");
        Require(wins[1] >= 0.50 && wins[1] <= 0.90, $"Normal default-tools completion must be 50..90%; actual {wins[1]:P0}.");
        Require(wins[2] <= 0.45, $"Hard default-tools completion must be <=45%; actual {wins[2]:P0}.");
        Require(wins[3] <= 0.15, $"Extreme default-tools completion must be <=15%; actual {wins[3]:P0}.");
        for (int index = 0; index < 3; index++)
            Require(wins[index] - wins[index + 1] >= 0.12 - 1e-10,
                $"Adjacent completion gap {(SheepDifficulty)index} -> {(SheepDifficulty)(index + 1)} must be >=12 percentage points; actual {(wins[index] - wins[index + 1]) * 100:F1}.");
        return passed;
    }

    private static Options Parse(string[] args)
    {
        var options = new Options();
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument == "--benchmark") { options.Benchmark = true; continue; }
            if (++index >= args.Length) throw new ArgumentException("Missing value for " + argument);
            string value = args[index];
            switch (argument)
            {
                case "--samples": options.Samples = int.Parse(value); break;
                case "--seed-start": options.SeedStart = uint.Parse(value); break;
                case "--policy": options.Policy = value; break;
                case "--tools": options.Tools = value; break;
                case "--observation": options.Observation = value; break;
                case "--output": options.Output = value; break;
                default: throw new ArgumentException("Unknown option " + argument);
            }
        }
        if (options.Samples < 1 || options.Samples > 5000) throw new ArgumentException("Samples must be 1..5000.");
        if ((ulong)options.SeedStart + (ulong)options.Samples - 1 > uint.MaxValue) throw new ArgumentException("Seed range overflows uint.");
        if (options.Policy != "greedy" && options.Policy != "visible3" && options.Policy != "visible3wide" && options.Policy != "both")
            throw new ArgumentException("Policy must be greedy, visible3, visible3wide, or both.");
        if (options.Tools != "none" && options.Tools != "default" && options.Tools != "both")
            throw new ArgumentException("Tools must be none, default, or both.");
        if (options.Observation != "generous" && options.Observation != "exposed")
            throw new ArgumentException("Observation must be generous or exposed.");
        if (!options.Benchmark && options.Observation != "generous")
            throw new ArgumentException("Exposed-only reports require --benchmark; fixed regression thresholds use generous observations.");
        return options;
    }

    private static void Help()
    {
        Console.WriteLine("Usage: scripts/test-sheep-difficulty.sh [options]");
        Console.WriteLine("  Default: fast regression, greedy + default tools, 50 seeds starting at 1001.");
        Console.WriteLine("  --check-rater            Check optimized rater against the independent player, separately from calibration.");
        Console.WriteLine("  --benchmark              Report only; skip calibration thresholds.");
        Console.WriteLine("  --samples N              Seeds per difficulty (1..5000, default 50).");
        Console.WriteLine("  --seed-start N           First seed (default 1001).");
        Console.WriteLine("  --policy greedy|visible3|visible3wide|both  Three-step beams are 8/12; both means greedy+visible3.");
        Console.WriteLine("  --tools none|default|both Default inventory is 2 removes, 3 undos, 1 shuffle, 1 revive.");
        Console.WriteLine("  --observation generous|exposed  Default allows ordinary covered icons; exposed only sees selectable cards.");
        Console.WriteLine("  --output PATH            Write complete per-seed JSON evidence.");
    }
}
