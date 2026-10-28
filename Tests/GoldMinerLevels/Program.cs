using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.GoldMiner;
partial class Program
{
    static readonly string DirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets/Resources/GoldMiner/Levels");
    static int assertions;
    static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "--refresh-witnesses") { RefreshWitnesses(); return 0; }
            if (args.Length > 0 && args[0] == "--archive-generation-logs") { LevelReports.ArchiveGenerationLogs(DirectoryPath, args.Skip(1).ToArray()); return 0; }
            if (args.Length > 0 && (args[0] == "--generate" || args[0] == "--regenerate")) { Generate(args); return 0; }
            Check(File.Exists(Path.Combine(DirectoryPath, "catalog.json")), "the shipped catalog must exist and contain all 240 proven layouts");
            Verify();
            VerifyFullGeneratorReproducibility();
            Check(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "artifacts/validation/gold-miner/levels.json")), "verification must write its real 240-layout report");
            Check(typeof(GoldMinerLevelGenerator).GetMethod("Generate").GetParameters().Any(p => p.Name == "expansionBudget"), "offline generator must expose bounded search budget for real candidate exhaustion");
            Console.WriteLine("PASS GoldMiner levels: " + assertions + " checks, 240 real Core witnesses, 30 full-generator reproducibility pairs."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("FAIL: " + e); return 1; }
    }
    static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    static string Hash(string text) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(new UTF8Encoding(false, true).GetBytes(text))).Replace("-", "").ToLowerInvariant(); }
    static string Token(object value) { string s = value.ToString(); return char.ToLowerInvariant(s[0]) + s.Substring(1); }
    static JObject EncodeEntity(GoldMinerEntity e)
    {
        return new JObject { ["id"] = e.Id, ["kind"] = Token(e.Kind), ["status"] = "available", ["x"] = e.X, ["y"] = e.Y, ["bagValue"] = e.BagValue == null ? JValue.CreateNull() : new JValue(e.BagValue.Value), ["ownerPlayerId"] = JValue.CreateNull(), ["moveDirection"] = e.MoveDirection };
    }
    static JObject EncodeLayout(GoldMinerLayout layout)
    {
        var inputs = new JArray(); foreach (var input in layout.Witness.Inputs) inputs.Add(new JObject { ["tick"] = input.Tick, ["playerId"] = input.PlayerId, ["action"] = input.Action });
        return new JObject { ["id"] = layout.Id, ["patternId"] = layout.PatternId, ["variantSeed"] = layout.VariantSeed, ["entities"] = new JArray(layout.Entities.Select(EncodeEntity)), ["witness"] = new JObject { ["requiredIncome"] = layout.Witness.RequiredIncome, ["inputs"] = inputs, ["expectedIncome"] = layout.Witness.ExpectedIncome, ["endTick"] = layout.Witness.EndTick } };
    }
    static GoldMinerLayout DecodeLayout(JToken json)
    {
        var entities = new List<GoldMinerEntity>();
        foreach (var e in json["entities"]) entities.Add(new GoldMinerEntity { Id = (int)e["id"], Kind = (GoldMinerKind)Enum.Parse(typeof(GoldMinerKind), (string)e["kind"], true), Status = GoldMinerEntityStatus.Available, X = (double)e["x"], Y = (double)e["y"], BagValue = e["bagValue"].Type == JTokenType.Null ? (long?)null : (long)e["bagValue"], MoveDirection = (int)e["moveDirection"] });
        var inputs = new List<GoldMinerWitnessInput>(); foreach (var x in json["witness"]["inputs"]) inputs.Add(new GoldMinerWitnessInput { Tick = (int)x["tick"], PlayerId = (int)x["playerId"], Action = (string)x["action"] });
        return new GoldMinerLayout { Id = (string)json["id"], PatternId = (string)json["patternId"], VariantSeed = (uint)json["variantSeed"], Entities = entities.ToArray(), Witness = new GoldMinerWitness { RequiredIncome = (long)json["witness"]["requiredIncome"], ExpectedIncome = (long)json["witness"]["expectedIncome"], EndTick = (int)json["witness"]["endTick"], Inputs = inputs.ToArray() } };
    }
    static void Generate(string[] args)
    {
        Directory.CreateDirectory(DirectoryPath);
        var all = Stopwatch.StartNew();
        foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop }) for (int level = 1; level <= 10; level++)
        {
            string file = GoldMinerRules.ModeId(mode) + "-" + level.ToString("00", CultureInfo.InvariantCulture) + ".json";
            if (args.Length > 1 && args[1] != file && args[1] != "all") continue;
            string path = Path.Combine(DirectoryPath, file);
            if (File.Exists(path) && args[0] != "--regenerate") { Console.WriteLine("Keep existing " + file); continue; }
            var layouts = new JArray(); var statistics = new JArray();
            foreach (string pattern in GoldMinerLevelGenerator.Patterns) for (int variant = 0; variant < 4; variant++)
            {
                uint seed = (uint)(0x7155A123 + ((int)mode + 1) * 100000 + level * 1000 + Array.IndexOf(GoldMinerLevelGenerator.Patterns, pattern) * 100 + variant);
                int candidates = 0, expanded = 0; var elapsed = Stopwatch.StartNew();
                var attempts = new List<GoldMinerGenerationAttempt>();
                GoldMinerLayout layout;
                try
                {
                    layout = GoldMinerLevelGenerator.Generate(mode, level, pattern, variant, seed, out candidates, out expanded,
                        GoldMinerPlanner.ExpansionBudget, attempts.Add);
                }
                catch (InvalidOperationException exception)
                {
                    statistics.Add(new JObject {
                        ["id"] = GoldMinerLevelGenerator.Identity(mode, level, pattern, variant), ["initialGenerationSeed"] = seed,
                        ["candidateCount"] = candidates, ["expandedStates"] = expanded, ["failureReason"] = exception.Message,
                        ["planningElapsedSeconds"] = attempts.Sum(a => a.PlanningElapsedSeconds),
                        ["generationElapsedSeconds"] = elapsed.Elapsed.TotalSeconds, ["witnessIncome"] = JValue.CreateNull(),
                        ["attempts"] = new JArray(attempts.Select(LevelReports.Attempt)), ["recording"] = "live-generator"
                    });
                    LevelReports.WriteGenerationShard(file, null, statistics);
                    throw;
                }
                double generationSeconds = elapsed.Elapsed.TotalSeconds;
                string reason; Check(GoldMinerPlanner.ReplayWitness(mode, level, layout, out reason), layout.Id + ": " + reason);
                statistics.Add(new JObject {
                    ["id"] = layout.Id, ["variantSeed"] = layout.VariantSeed, ["initialGenerationSeed"] = seed,
                    ["candidateCount"] = candidates, ["expandedStates"] = expanded, ["failureReason"] = JValue.CreateNull(),
                    ["planningElapsedSeconds"] = attempts.Sum(a => a.PlanningElapsedSeconds),
                    ["generationElapsedSeconds"] = generationSeconds, ["generationAndReplayElapsedSeconds"] = elapsed.Elapsed.TotalSeconds,
                    ["witnessIncome"] = layout.Witness.ExpectedIncome, ["requiredIncome"] = layout.Witness.RequiredIncome,
                    ["attempts"] = new JArray(attempts.Select(LevelReports.Attempt)), ["recording"] = "live-generator"
                });
                LevelReports.WriteGenerationShard(file, null, statistics);
                layouts.Add(EncodeLayout(layout));
                Console.WriteLine(layout.Id + " income=" + layout.Witness.ExpectedIncome + "/" + layout.Witness.RequiredIncome + " inputs=" + layout.Witness.Inputs.Length + " candidates=" + candidates + " expanded=" + expanded + " seconds=" + elapsed.Elapsed.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture));
            }
            var shard = new JObject { ["schemaVersion"] = 1, ["rulesVersion"] = GoldMinerRules.Version, ["catalogVersion"] = 1, ["mode"] = Token(mode), ["levelIndex"] = level, ["layouts"] = new JArray(layouts.OrderBy(x => (string)x["id"], StringComparer.Ordinal)) };
            string shardText = shard.ToString(Formatting.None) + "\n";
            File.WriteAllText(path, shardText, new UTF8Encoding(false, true));
            LevelReports.WriteGenerationShard(file, shardText, statistics);
        }
        var shards = new JArray();
        foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop }) for (int level = 1; level <= 10; level++)
        {
            string file = GoldMinerRules.ModeId(mode) + "-" + level.ToString("00", CultureInfo.InvariantCulture) + ".json", path = Path.Combine(DirectoryPath, file);
            if (!File.Exists(path)) continue;
            shards.Add(new JObject { ["mode"] = Token(mode), ["levelIndex"] = level, ["file"] = file, ["sha256"] = Hash(File.ReadAllText(path, new UTF8Encoding(false, true))) });
        }
        if (shards.Count == 20) File.WriteAllText(Path.Combine(DirectoryPath, "catalog.json"), new JObject { ["schemaVersion"] = 1, ["rulesVersion"] = GoldMinerRules.Version, ["catalogVersion"] = 1, ["shards"] = shards }.ToString(Formatting.None) + "\n", new UTF8Encoding(false, true));
        Console.WriteLine("Generation elapsed " + all.Elapsed + ", complete shards=" + shards.Count);
    }
    static void Verify()
    {
        var verificationTimer = Stopwatch.StartNew();
        string startedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var rows = new JArray(); bool passed = false; JObject exhaustion = null;
        string catalogText = File.ReadAllText(Path.Combine(DirectoryPath, "catalog.json"));
        try
        {
        var catalog = JObject.Parse(File.ReadAllText(Path.Combine(DirectoryPath, "catalog.json"))); Check(((JArray)catalog["shards"]).Count == 20, "20 shards");
        Check((int)catalog["rulesVersion"] == GoldMinerRules.Version, "catalog witnesses must match the active balance version");
        int total = 0; var allIds = new HashSet<string>(); var files = new HashSet<string>();
        foreach (var shard in catalog["shards"])
        {
            Check(files.Add((string)shard["file"]), "distinct shard files");
            string text = File.ReadAllText(Path.Combine(DirectoryPath, (string)shard["file"]), new UTF8Encoding(false, true));
            Check(Encoding.UTF8.GetByteCount(text) <= 512 * 1024, "shard size"); Check(Hash(text) == (string)shard["sha256"], "shard SHA-256");
            var generation = LevelReports.ReadGenerationShard((string)shard["file"], Hash(text));
            var json = JObject.Parse(text); var mode = (GoldMinerMode)Enum.Parse(typeof(GoldMinerMode), (string)json["mode"], true); int level = (int)json["levelIndex"];
            Check((int)json["rulesVersion"] == GoldMinerRules.Version, "shard witnesses must match the active balance version");
            var ids = new List<string>(); var patterns = new Dictionary<string, int>();
            foreach (var token in json["layouts"])
            {
                var layout = DecodeLayout(token); total++; Check(allIds.Add(layout.Id), "globally unique layout identity"); ids.Add(layout.Id); patterns[layout.PatternId] = patterns.ContainsKey(layout.PatternId) ? patterns[layout.PatternId] + 1 : 1;
                var baseline = GoldMinerLevelGenerator.CreateProofRun(mode, level, layout);
                Check(baseline.Current.ActiveBuffs.IsEmpty && baseline.Current.Players.All(p => p.Bombs == 0), "proof has no buffs or bombs");
                Check(baseline.Current.Entities.Where(e => e.Kind == GoldMinerKind.MysteryBag).All(e => e.BagValue == 100), "proof uses only bag baseline");
                Check(baseline.Current.RemainingTicks == 3600 && baseline.Current.SimulationTick == 0, "proof starts at full rule time");
                string reason; Check(GoldMinerLevelGenerator.ValidateGeometry(mode, level, layout, out reason), layout.Id + " geometry: " + reason);
                var replayTimer = Stopwatch.StartNew();
                bool replayPassed = GoldMinerPlanner.ReplayWitness(mode, level, layout, out reason);
                replayTimer.Stop();
                JObject recorded;
                generation.TryGetValue(layout.Id, out recorded);
                if (recorded != null && (uint?)recorded["variantSeed"] != layout.VariantSeed) recorded = null;
                rows.Add(new JObject {
                    ["id"] = layout.Id, ["variantSeed"] = layout.VariantSeed, ["shardSha256"] = Hash(text),
                    ["requiredIncome"] = layout.Witness.RequiredIncome, ["witnessIncome"] = layout.Witness.ExpectedIncome,
                    ["endTick"] = layout.Witness.EndTick, ["inputCount"] = layout.Witness.Inputs.Length,
                    ["replayPassed"] = replayPassed, ["replayFailureReason"] = reason, ["replayElapsedSeconds"] = replayTimer.Elapsed.TotalSeconds,
                    ["generation"] = recorded == null ? (JToken)JValue.CreateNull() : recorded.DeepClone(),
                    ["generationStatisticsStatus"] = recorded == null ? "not-recorded" : "recorded"
                });
                Check(replayPassed, layout.Id + " witness: " + reason);
                var repeated = GoldMinerLevelGenerator.CreateCandidate(mode, level, layout.PatternId, int.Parse(layout.Id.Substring(layout.Id.Length - 1), CultureInfo.InvariantCulture), layout.VariantSeed);
                Check(repeated != null, "saved candidate seed regenerates");
                for (int i = 0; i < layout.Entities.Length; i++) Check(layout.Entities[i].X == repeated.Entities[i].X && layout.Entities[i].Y == repeated.Entities[i].Y && layout.Entities[i].Kind == repeated.Entities[i].Kind, "deterministic candidate coordinates");
                int expanded; Check(GoldMinerPlanner.FindWitness(mode, level, layout, 0, out expanded) == null && expanded == 0, "zero search budget produces no proof");
                if (total == 1)
                {
                    long income = layout.Witness.ExpectedIncome; layout.Witness.ExpectedIncome++;
                    Check(!GoldMinerPlanner.ReplayWitness(mode, level, layout, out reason), "reject fabricated expected income"); layout.Witness.ExpectedIncome = income;
                    string action = layout.Witness.Inputs[0].Action; layout.Witness.Inputs[0].Action = "dynamite";
                    Check(!GoldMinerPlanner.ReplayWitness(mode, level, layout, out reason), "baseline cannot use bombs"); layout.Witness.Inputs[0].Action = action;
                    int end = layout.Witness.EndTick; layout.Witness.EndTick--;
                    Check(!GoldMinerPlanner.ReplayWitness(mode, level, layout, out reason), "reject fabricated completion time"); layout.Witness.EndTick = end;
                    var copy = layout.Entities[0].Copy(); layout.Entities[0].X = -1;
                    Check(!GoldMinerLevelGenerator.ValidateGeometry(mode, level, layout, out reason), "reject out-of-bounds geometry"); layout.Entities[0] = copy;
                    copy = layout.Entities[1].Copy(); layout.Entities[1].X = layout.Entities[0].X; layout.Entities[1].Y = layout.Entities[0].Y;
                    Check(!GoldMinerLevelGenerator.ValidateGeometry(mode, level, layout, out reason), "reject overlap"); layout.Entities[1] = copy;
                    var first = layout.Witness.Inputs[0]; int tick = first.Tick; first.Tick = GoldMinerRules.LevelTicks;
                    Check(!GoldMinerPlanner.ReplayWitness(mode, level, layout, out reason), "no launch after deadline"); first.Tick = tick;
                    var run = GoldMinerLevelGenerator.CreateProofRun(mode, level, layout); var game = new GoldMinerGame(run); game.StartLevel(); game.TryLaunch(0, 1); game.Step();
                    Check(run.Current.Phase == GoldMinerPhase.Ready && run.Current.SimulationTick == 0 && run.Checkpoint.InitialEntities[0].X == layout.Entities[0].X, "simulation cannot mutate source checkpoint");
                }
                if (level == 10)
                {
                    var proof = GoldMinerPlanner.FindWitness(mode, level, layout, 1, out expanded);
                    Check(proof == null && expanded == 1, "bounded incomplete search must fail rather than invent proof");
                }
            }
            Check(ids.Count == 12 && ids.Distinct().Count() == 12 && ids.SequenceEqual(ids.OrderBy(x => x, StringComparer.Ordinal)), "12 sorted stable ids");
            foreach (string p in GoldMinerLevelGenerator.Patterns) Check(patterns.ContainsKey(p) && patterns[p] == 4, "4 per pattern");
        }
        Check(total == 240, "240 layouts");
        exhaustion = CandidateExhaustion();
        Check(Hash(File.ReadAllText(Path.Combine(DirectoryPath, "catalog.json"))) == Hash(catalogText), "verification never alters catalog bytes");
        passed = true;
        }
        finally
        {
            verificationTimer.Stop();
            LevelReports.Write("levels.json", new JObject {
                ["schemaVersion"] = 1, ["startedAtUtc"] = startedAt,
                ["finishedAtUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["catalogSha256"] = Hash(catalogText), ["passed"] = passed, ["layoutCount"] = rows.Count,
                ["passedWitnessCount"] = rows.Count(r => (bool)r["replayPassed"]),
                ["generationStatisticsCount"] = rows.Count(r => r["generation"].Type != JTokenType.Null),
                ["verificationElapsedSeconds"] = verificationTimer.Elapsed.TotalSeconds,
                ["candidateExhaustion"] = exhaustion, ["layouts"] = rows
            });
        }
    }
    static void VerifyFullGeneratorReproducibility()
    {
        var timer = Stopwatch.StartNew(); var rows = new JArray(); bool passed = false;
        uint[] seeds = { 1U, 7U, 42U, 20261027U, uint.MaxValue };
        try
        {
            foreach (uint seed in seeds)
            foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
            foreach (string pattern in GoldMinerLevelGenerator.Patterns)
            {
                int firstCandidates, firstExpanded, secondCandidates, secondExpanded;
                var firstTime = Stopwatch.StartNew();
                var first = GoldMinerLevelGenerator.Generate(mode, 1, pattern, 0, seed, out firstCandidates, out firstExpanded);
                firstTime.Stop();
                var secondTime = Stopwatch.StartNew();
                var second = GoldMinerLevelGenerator.Generate(mode, 1, pattern, 0, seed, out secondCandidates, out secondExpanded);
                secondTime.Stop();
                string firstJson = JsonConvert.SerializeObject(first), secondJson = JsonConvert.SerializeObject(second);
                bool identical = firstJson == secondJson && firstCandidates == secondCandidates && firstExpanded == secondExpanded;
                string firstReason, secondReason;
                bool firstReplay = GoldMinerPlanner.ReplayWitness(mode, 1, first, out firstReason);
                bool secondReplay = GoldMinerPlanner.ReplayWitness(mode, 1, second, out secondReason);
                rows.Add(new JObject {
                    ["seed"] = seed, ["mode"] = GoldMinerRules.ModeId(mode), ["levelIndex"] = 1, ["patternId"] = pattern, ["variant"] = 0,
                    ["firstCandidateCount"] = firstCandidates, ["secondCandidateCount"] = secondCandidates,
                    ["firstExpandedStates"] = firstExpanded, ["secondExpandedStates"] = secondExpanded,
                    ["firstElapsedSeconds"] = firstTime.Elapsed.TotalSeconds, ["secondElapsedSeconds"] = secondTime.Elapsed.TotalSeconds,
                    ["firstCompleteLayoutSha256"] = Hash(firstJson), ["secondCompleteLayoutSha256"] = Hash(secondJson),
                    ["identical"] = identical, ["firstReplayPassed"] = firstReplay, ["secondReplayPassed"] = secondReplay,
                    ["firstReplayFailureReason"] = firstReason, ["secondReplayFailureReason"] = secondReason,
                    ["witnessIncome"] = first.Witness.ExpectedIncome, ["witnessEndTick"] = first.Witness.EndTick,
                    ["witnessInputCount"] = first.Witness.Inputs.Length
                });
                Check(identical, "full Generate must reproduce candidate count, search expansion, coordinates, income and all witness commands");
                Check(firstReplay && secondReplay, "both freshly generated witnesses must pass real Core replay: " + firstReason + secondReason);
                Check(firstCandidates >= 1 && firstCandidates <= GoldMinerLevelGenerator.CandidateBudget, "repeated full generator stays within the candidate bound");
            }
            Check(rows.Count == 30, "five explicit seeds cover both modes and all three first-level patterns");
            passed = true;
        }
        finally
        {
            timer.Stop();
            LevelReports.Write("generation-reproducibility.json", new JObject {
                ["schemaVersion"] = 1, ["passed"] = passed, ["recordedAtUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["scope"] = "Five specified seeds x Solo/Coop x level 1 x balanced/left/right, variant 0. Each pair calls full production Generate twice and replays both resulting witnesses. Levels 2-10 are covered by shipped-catalog replay, not repeated full generation in this test.",
                ["comparison"] = "Exact JsonConvert serialization of the complete GoldMinerLayout, including variant seed, every entity and the entire witness, plus candidate and expansion counts.",
                ["pairCount"] = rows.Count, ["elapsedSeconds"] = timer.Elapsed.TotalSeconds, ["pairs"] = rows
            });
        }
    }
    static JObject CandidateExhaustion()
    {
        int candidates = 0, expanded = 0; uint initialSeed = 1271;
        var attempts = new List<GoldMinerGenerationAttempt>(); GoldMinerLayout returned = null;
        string failure = null; var timer = Stopwatch.StartNew();
        try
        {
            returned = GoldMinerLevelGenerator.Generate(GoldMinerMode.Solo, 1, "balanced", 0, initialSeed,
                out candidates, out expanded, 0, attempts.Add);
        }
        catch (InvalidOperationException exception) { failure = exception.Message; }
        timer.Stop();
        Check(failure != null && failure.Contains("32 candidates"), "32 rejected candidates must throw a clear generation failure");
        Check(returned == null && candidates == 32 && attempts.Count == 32 && expanded == 0, "candidate exhaustion cannot publish an unproven layout");
        Check(attempts.Any(a => a.FailureReason == "search-expansion-budget-exhausted"), "exhaustion calls the real bounded planner for valid candidates");
        uint seed = initialSeed;
        for (int i = 0; i < attempts.Count; i++)
        {
            Check(attempts[i].CandidateIndex == i + 1 && attempts[i].Seed == GoldMinerRules.NextUInt(ref seed) && !string.IsNullOrEmpty(attempts[i].FailureReason), "each actual candidate has its deterministic seed and failure reason");
        }
        return new JObject {
            ["passed"] = true, ["candidateCount"] = candidates, ["expansionBudget"] = 0,
            ["expandedStates"] = expanded, ["returnedLayout"] = JValue.CreateNull(),
            ["failureReason"] = failure, ["elapsedSeconds"] = timer.Elapsed.TotalSeconds,
            ["attempts"] = new JArray(attempts.Select(LevelReports.Attempt))
        };
    }
}
