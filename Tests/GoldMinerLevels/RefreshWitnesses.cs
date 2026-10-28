using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.GoldMiner;

partial class Program
{
    // Rebalance existing layouts without drawing new geometry. Every accepted policy
    // trace must satisfy production ReplayWitness, including the full-shop income bound.
    static void RefreshWitnesses()
    {
        var shards = new JArray(); var all = Stopwatch.StartNew();
        var profiles = new[] { SimulationPolicy.Focused,
            new SimulationPolicy.Profile("exact-100ms", 6, 0, 1),
            new SimulationPolicy.Profile("exact-50ms", 3, 0, 1),
            new SimulationPolicy.Profile("focused-50ms", 3, 1, .95),
            new SimulationPolicy.Profile("coarse", 18, 4, .7),
            new SimulationPolicy.Profile("focus-200ms", 12, 2, .9) };
        foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
        for (int level = 1; level <= 10; level++)
        {
            string file = GoldMinerRules.ModeId(mode) + "-" + level.ToString("00", CultureInfo.InvariantCulture) + ".json";
            string path = Path.Combine(DirectoryPath, file); var shard = JObject.Parse(File.ReadAllText(path));
            var statistics = new JArray();
            foreach (JObject token in shard["layouts"])
            {
                var timer = Stopwatch.StartNew(); var layout = DecodeLayout(token);
                string originalEntities = token["entities"].ToString(Formatting.None);
                SimulationPolicy.Result accepted = null; string profileName = null; int acceptedSeed = 0, attempts = 0;
                foreach (var profile in profiles)
                {
                    for (int sample = 0; sample < 80; sample++)
                    {
                        int seed = sample + 7919 * level + (int)mode * 987; attempts++;
                        var result = SimulationPolicy.Run(mode, level, layout, profile, seed, true);
                        if (result.Witness.ExpectedIncome < result.Witness.RequiredIncome) continue;
                        layout.Witness = result.Witness; string reason;
                        Check(GoldMinerPlanner.ReplayWitness(mode, level, layout, out reason), layout.Id + ": " + reason);
                        accepted = result; profileName = profile.Name; acceptedSeed = seed; break;
                    }
                    if (accepted != null) break;
                }
                if (accepted == null)
                {
                    int expanded; layout.Witness = GoldMinerPlanner.FindWitness(mode, level, layout, out expanded);
                    if (layout.Witness == null) throw new InvalidOperationException("Existing geometry needs further bounded search: " + layout.Id);
                    string reason; Check(GoldMinerPlanner.ReplayWitness(mode, level, layout, out reason), reason);
                    profileName = "production-beam-search";
                }
                token["witness"] = EncodeLayout(layout)["witness"];
                Check(token["entities"].ToString(Formatting.None) == originalEntities, "Reproof must not alter geometry.");
                statistics.Add(new JObject { ["id"] = layout.Id, ["variantSeed"] = layout.VariantSeed,
                    ["recording"] = "existing-geometry-reproof", ["geometryChanged"] = false,
                    ["policyAttempts"] = attempts, ["acceptedPolicy"] = profileName, ["acceptedSeed"] = acceptedSeed,
                    ["requiredIncome"] = layout.Witness.RequiredIncome, ["witnessIncome"] = layout.Witness.ExpectedIncome,
                    ["firstRequiredTick"] = accepted == null ? JValue.CreateNull() : new JValue(accepted.FirstRequiredTick),
                    ["planningAndReplayElapsedSeconds"] = timer.Elapsed.TotalSeconds });
                Console.WriteLine(layout.Id + " income=" + layout.Witness.ExpectedIncome + "/" + layout.Witness.RequiredIncome +
                    " end=" + layout.Witness.EndTick + " policy=" + profileName + " attempts=" + attempts);
            }
            shard["rulesVersion"] = GoldMinerRules.Version;
            string text = shard.ToString(Formatting.None) + "\n"; File.WriteAllText(path, text, new UTF8Encoding(false, true));
            LevelReports.WriteGenerationShard(file, text, statistics);
            shards.Add(new JObject { ["mode"] = Token(mode), ["levelIndex"] = level, ["file"] = file, ["sha256"] = Hash(text) });
        }
        File.WriteAllText(Path.Combine(DirectoryPath, "catalog.json"), new JObject { ["schemaVersion"] = 1,
            ["rulesVersion"] = GoldMinerRules.Version, ["catalogVersion"] = 1, ["shards"] = shards }.ToString(Formatting.None) + "\n", new UTF8Encoding(false, true));
        Console.WriteLine("Reproved all 240 original geometries in " + all.Elapsed);
    }
}
