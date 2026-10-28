using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.GoldMiner;

internal static class LevelReports
{
    internal static readonly string DirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "artifacts/validation/gold-miner");
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    internal static string Hash(string text)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
    }
    internal static void Write(string relativePath, JObject value)
    {
        string path = Path.Combine(DirectoryPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, value.ToString(Formatting.Indented) + "\n", Utf8);
    }
    internal static JObject Attempt(GoldMinerGenerationAttempt attempt)
    {
        return new JObject {
            ["candidateIndex"] = attempt.CandidateIndex, ["seed"] = attempt.Seed,
            ["expandedStates"] = attempt.ExpandedStates, ["failureReason"] = attempt.FailureReason,
            ["planningElapsedSeconds"] = attempt.PlanningElapsedSeconds
        };
    }
    internal static void WriteGenerationShard(string shardFile, string shardText, JArray entries)
    {
        Write("generation/" + shardFile, new JObject {
            ["schemaVersion"] = 1, ["recordedAtUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ["shardFile"] = shardFile, ["shardSha256"] = shardText == null ? JValue.CreateNull() : new JValue(Hash(shardText)), ["layouts"] = entries
        });
    }
    internal static Dictionary<string, JObject> ReadGenerationShard(string shardFile, string shardHash)
    {
        var result = new Dictionary<string, JObject>(StringComparer.Ordinal);
        string path = Path.Combine(DirectoryPath, "generation", shardFile);
        if (!File.Exists(path)) return result;
        var report = JObject.Parse(File.ReadAllText(path, Utf8));
        if ((string)report["shardSha256"] != shardHash) return result;
        foreach (JObject entry in report["layouts"]) result.Add((string)entry["id"], entry);
        return result;
    }
    internal static void ArchiveGenerationLogs(string resourceDirectory, string[] logPaths)
    {
        var layouts = new Dictionary<string, JToken>(StringComparer.Ordinal);
        var shards = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in Directory.GetFiles(resourceDirectory, "*.json"))
        {
            if (Path.GetFileName(path) == "catalog.json") continue;
            string text = File.ReadAllText(path, Utf8);
            shards.Add(Path.GetFileName(path), text);
            foreach (var layout in JObject.Parse(text)["layouts"]) layouts.Add((string)layout["id"], layout);
        }
        var records = new Dictionary<string, JObject>(StringComparer.Ordinal);
        var sources = new JArray();
        var linePattern = new Regex(@"^(?<id>(solo|coop)-\d{2}-(balanced|left|right)-[0-3]) income=(?<income>\d+)/(?<required>\d+) inputs=(?<inputs>\d+) candidates=(?<candidates>\d+) expanded=(?<expanded>\d+) seconds=(?<seconds>\d+\.\d+)$", RegexOptions.CultureInvariant);
        foreach (string source in logPaths)
        {
            string text = File.ReadAllText(source, Utf8), relative = "generation-logs/" + Path.GetFileName(source);
            string archive = Path.Combine(DirectoryPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(archive));
            File.Copy(source, archive, true);
            sources.Add(new JObject { ["file"] = relative, ["sha256"] = Hash(text) });
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var match = linePattern.Match(lines[i].TrimEnd('\r'));
                if (!match.Success) continue;
                string id = match.Groups["id"].Value;
                JToken layout;
                if (!layouts.TryGetValue(id, out layout)) throw new InvalidDataException("Unknown logged layout " + id);
                long income = long.Parse(match.Groups["income"].Value, CultureInfo.InvariantCulture);
                long required = long.Parse(match.Groups["required"].Value, CultureInfo.InvariantCulture);
                int inputs = int.Parse(match.Groups["inputs"].Value, CultureInfo.InvariantCulture);
                if (income != (long)layout["witness"]["expectedIncome"] || required != (long)layout["witness"]["requiredIncome"] || inputs != ((JArray)layout["witness"]["inputs"]).Count)
                    throw new InvalidDataException("Archived statistics do not match shipped witness " + id);
                int candidates = int.Parse(match.Groups["candidates"].Value, CultureInfo.InvariantCulture);
                records.Add(id, new JObject {
                    ["id"] = id, ["variantSeed"] = layout["variantSeed"], ["candidateCount"] = candidates,
                    ["expandedStates"] = int.Parse(match.Groups["expanded"].Value, CultureInfo.InvariantCulture),
                    ["failureReason"] = JValue.CreateNull(),
                    ["candidateFailures"] = candidates == 1 ? (JToken)new JArray() : JValue.CreateNull(),
                    ["planningElapsedSeconds"] = JValue.CreateNull(),
                    ["generationAndReplayElapsedSeconds"] = double.Parse(match.Groups["seconds"].Value, CultureInfo.InvariantCulture),
                    ["durationScope"] = "original console timer includes generation and witness replay; planner-only timing was not recorded",
                    ["witnessIncome"] = income, ["requiredIncome"] = required, ["recording"] = "archived-console-log",
                    ["sourceLog"] = relative, ["sourceLine"] = i + 1,
                    ["missingDetails"] = "Individual candidate rejection reasons and planner-only durations were not recorded."
                });
            }
        }
        foreach (var shard in shards)
        {
            var entries = new JArray();
            foreach (var layout in JObject.Parse(shard.Value)["layouts"])
            {
                JObject record;
                if (records.TryGetValue((string)layout["id"], out record)) entries.Add(record);
            }
            WriteGenerationShard(shard.Key, shard.Value, entries);
        }
        Write("generation-archive.json", new JObject {
            ["schemaVersion"] = 1, ["archivedAtUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ["recordedLayoutCount"] = records.Count, ["missingLayoutCount"] = layouts.Count - records.Count,
            ["sources"] = sources, ["note"] = "Missing timings/counts are not inferred or replaced by replay timings."
        });
        Console.WriteLine("Archived real generation logs: " + records.Count + " layouts; " + (layouts.Count - records.Count) + " layouts have no recorded generation statistics.");
    }
}
