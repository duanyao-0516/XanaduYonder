using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using XanaduYonder.Core.Idiom;

internal static class CatalogChecks
{
    internal static void Run(string path)
    {
        var commonPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), "../../Data/Idioms/common-idioms.txt"));
        var common = new HashSet<string>(File.ReadAllLines(commonPath).Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith("#")), StringComparer.Ordinal);
        var load = Stopwatch.StartNew(); var entries = new List<IdiomEntry>();
        using (var document = JsonDocument.Parse(File.ReadAllBytes(path)))
        foreach (var row in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            string word = row.GetProperty("word").GetString();
            entries.Add(new IdiomEntry(row.GetProperty("id").GetString(), word,
                row.GetProperty("pinyin").GetProperty("display").GetString(), row.GetProperty("firstKey").GetString(),
                row.GetProperty("lastKey").GetString(), row.GetProperty("senseId").GetString(),
                row.GetProperty("detailFile").GetString(), common.Contains(word),
                row.GetProperty("readings").EnumerateArray().Select(r => new IdiomSoundReading(r.GetProperty("firstKey").GetString(), r.GetProperty("lastKey").GetString()))));
        }
        var lex = new IdiomLexicon(entries, "full-catalog-test"); load.Stop();
        Require(lex.Count == 45776, "Every source four-Han word must be in the current game dictionary.");
        foreach (string word in new[] { "当仁不让", "明知故问", "问心无愧" })
        { IdiomEntry entry; Require(lex.TryGetWord(word, out entry), "Missing restored source word: " + word); }
        Require(lex.Entries.Count(e => e.SoundReadings.Count == 0) == 102, "Unparsed readings stay available in character mode.");
        Console.WriteLine("CATALOG entries=" + lex.Count + " common=" + entries.Count(e => e.Common) + " load_ms=" + load.Elapsed.TotalMilliseconds.ToString("F2"));
        var starts = new List<double>(); var submissions = new List<double>(); var restores = new List<double>();
        int longest = 0, totalMessages = 0;
        foreach (IdiomMatchKind kind in new[] { IdiomMatchKind.Computer, IdiomMatchKind.Local })
        foreach (IdiomMode mode in new[] { IdiomMode.SameCharacter, IdiomMode.SameSound })
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in lex.Entries)
                foreach (string key in Heads(entry, mode))
                { int n; totals.TryGetValue(key, out n); totals[key] = n + 1; }
            for (uint seed = 1; seed <= (kind == IdiomMatchKind.Computer ? 8 : 2); seed++)
            {
                var profile = new IdiomLearningProfile(lex);
                var game = new IdiomGame(lex, mode, seed, kind, profile, () => 1800000000L);
                var clock = Stopwatch.StartNew(); game.Start(IdiomActor.App);
                clock.Stop(); starts.Add(clock.Elapsed.TotalMilliseconds);
                var used = new HashSet<string>(StringComparer.Ordinal); var usedFirst = new Dictionary<string, int>(StringComparer.Ordinal);
                int accounted = 0;
                while (!game.IsEnded && game.History.Count < 1000)
                {
                    while (accounted < game.History.Count)
                    {
                        IdiomEntry accepted; lex.TryGetById(game.History[accounted++], out accepted);
                        Require(used.Add(accepted.Id), "A source word appeared twice in the game.");
                        foreach (string key in Heads(accepted, mode))
                        {
                        int n; usedFirst.TryGetValue(key, out n); usedFirst[key] = n + 1;
                        }
                    }
                    var candidates = lex.Following(mode, game.LastEntry, game.CurrentSoundKeys);
                    IdiomEntry selected = null; int score = -1;
                    foreach (var entry in candidates)
                    {
                        if (used.Contains(entry.Id)) continue;
                        var tails = mode == IdiomMode.SameCharacter ? (IReadOnlyList<string>)new[] { entry.LastCharacter }
                            : IdiomGame.TailKeys(entry, game.LastEntry == null ? null : game.CurrentSoundKeys);
                        int remaining = 0;
                        foreach (string next in tails)
                        {
                            int total, spent; totals.TryGetValue(next, out total); usedFirst.TryGetValue(next, out spent);
                            remaining += total - spent;
                        }
                        if (remaining > score) { score = remaining; selected = entry; }
                    }
                    Require(selected != null, "A live player turn must have a legal move.");
                    clock.Restart(); Require(game.Submit(selected.Word).Success, "Selected legal player word was rejected.");
                    clock.Stop(); submissions.Add(clock.Elapsed.TotalMilliseconds);
                    if (game.History.Count % 100 <= 1 || game.IsEnded)
                    {
                        var snapshot = game.GetSnapshot(); clock.Restart(); var restored = new IdiomGame(lex, snapshot, profile, () => 1800000000L); clock.Stop();
                        restores.Add(clock.Elapsed.TotalMilliseconds);
                        Require(restored.History.SequenceEqual(game.History) && restored.GetSnapshot().RandomState == snapshot.RandomState &&
                                restored.EndReason == game.EndReason && restored.PlayerCount == game.PlayerCount, "Long snapshot restores exactly.");
                        game = restored;
                    }
                }
                Require(game.History.Distinct().Count() == game.History.Count, "No repeated ids including last APP reply.");
                Require(game.EndReason != IdiomEndReason.RecommendationExhausted, "New games cannot end because a preferred pool is empty.");
                new IdiomGame(lex, game.GetSnapshot(), profile, () => 1800000000L);
                longest = Math.Max(longest, game.History.Count); totalMessages += game.History.Count;
                Console.WriteLine("CATALOG run kind=" + kind + " mode=" + mode + " seed=" + seed + " messages=" + game.History.Count + " end=" + game.EndReason);
            }
        }
        Require(longest >= 1000, "A local full-dictionary game must still exercise a 1,000-message history.");
        foreach (IdiomMode mode in new[] { IdiomMode.SameCharacter, IdiomMode.SameSound })
        {
            var outputs = new HashSet<string>();
            if (mode == IdiomMode.SameCharacter)
            {
                var emptyProfile = new IdiomLearningProfile(lex);
                var probe = new IdiomGame(lex, mode, 17, IdiomMatchKind.Computer, emptyProfile, () => 1800000000L);
                probe.Start(IdiomActor.Player); probe.Submit("一望无际");
                Require(probe.History.Count == 2 && probe.LastEntry.FirstCharacter == "际", "Rare 际 words must be selectable without personal familiarity.");
                new IdiomGame(lex, probe.GetSnapshot(), emptyProfile, () => 1800000000L);
            }
            for (uint seed = 1; seed <= 40; seed++)
            {
                var profile = new IdiomLearningProfile(lex);
                // Personal familiarity makes these rare words eligible without relabeling the public vocabulary.
                if (mode == IdiomMode.SameCharacter)
                    foreach (string word in new[] { "际会风云", "际地蟠天", "际遇风云" })
                    { IdiomEntry entry; Require(lex.TryGetWord(word, out entry), "Expected catalog example."); profile.Remember(entry.Id); }
                var game = new IdiomGame(lex, mode, seed, IdiomMatchKind.Computer, profile, () => 1800000000L);
                game.Start(IdiomActor.Player); Require(game.Submit("一望无际").Success, "Real-catalog example is accepted.");
                if (game.History.Count > 1)
                {
                    outputs.Add(game.LastEntry.Word);
                }
                new IdiomGame(lex, game.GetSnapshot(), profile, () => 1800000000L);
            }
            Require(outputs.Count > 1, "A single high score must not monopolize the real-catalog example.");
            Console.WriteLine("CATALOG 一望无际 mode=" + mode + " distinct_replies=" + outputs.Count + " [" + string.Join(",", outputs) + "]");
        }
        for (uint seed = 1; seed <= 40; seed++)
        {
            var probe = new IdiomGame(lex, IdiomMode.SameCharacter, seed);
            probe.Start(IdiomActor.Player); Require(probe.Submit("明知故问").Success && probe.History.Count == 2, "问 words must remain selectable.");
            var local = new IdiomGame(lex, IdiomMode.SameCharacter, seed, IdiomMatchKind.Local);
            local.Start(IdiomActor.Player);
            foreach (string word in new[] { "明知故问", "问心无愧", "愧不敢当", "当仁不让" }) Require(local.Submit(word).Success, "Restored real source chain must be playable: " + word);
            new IdiomGame(lex, local.GetSnapshot());
        }
        Print("start", starts); Print("submit", submissions); Print("restore", restores);
        Console.WriteLine("CATALOG total_messages=" + totalMessages + " longest=" + longest + " (desktop .NET timings, not device timings)");
    }

    private static IEnumerable<string> Heads(IdiomEntry entry, IdiomMode mode)
    {
        return mode == IdiomMode.SameCharacter ? new[] { entry.FirstCharacter } : entry.SoundReadings.Select(r => r.FirstKey).Distinct();
    }

    private static void Print(string name, List<double> values)
    {
        values.Sort();
        Console.WriteLine("CATALOG " + name + " samples=" + values.Count + " median_ms=" + values[values.Count / 2].ToString("F3") +
                          " max_ms=" + values[values.Count - 1].ToString("F3"));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
