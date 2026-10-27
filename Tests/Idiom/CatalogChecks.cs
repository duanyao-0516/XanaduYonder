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
                row.GetProperty("detailFile").GetString(), common.Contains(word)));
        }
        var lex = new IdiomLexicon(entries, "full-catalog-test"); load.Stop();
        Require(lex.Count > 40000, "Expected the complete current game dictionary.");
        Console.WriteLine("CATALOG entries=" + lex.Count + " common=" + entries.Count(e => e.Common) + " load_ms=" + load.Elapsed.TotalMilliseconds.ToString("F2"));
        var starts = new List<double>(); var submissions = new List<double>(); var restores = new List<double>();
        int longest = 0, totalMessages = 0;
        foreach (IdiomMatchKind kind in new[] { IdiomMatchKind.Computer, IdiomMatchKind.Local })
        foreach (IdiomMode mode in new[] { IdiomMode.SameCharacter, IdiomMode.SameSound })
        {
            var byFirst = lex.Entries.GroupBy(e => mode == IdiomMode.SameCharacter ? e.FirstCharacter : e.FirstKey)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            for (uint seed = 1; seed <= (kind == IdiomMatchKind.Computer ? 8 : 2); seed++)
            {
                var profile = new IdiomLearningProfile(lex);
                var game = new IdiomGame(lex, mode, seed, kind, profile, () => 1800000000L);
                var clock = Stopwatch.StartNew(); game.Start(IdiomActor.App);
                clock.Stop(); starts.Add(clock.Elapsed.TotalMilliseconds);
                if (kind == IdiomMatchKind.Computer) Require(game.LastEntry.Common, "Complete catalog openings must be curated common words.");
                var used = new HashSet<string>(StringComparer.Ordinal); var usedFirst = new Dictionary<string, int>(StringComparer.Ordinal);
                int accounted = 0;
                while (!game.IsEnded && game.History.Count < 1000)
                {
                    while (accounted < game.History.Count)
                    {
                        IdiomEntry accepted; lex.TryGetById(game.History[accounted++], out accepted);
                        Require(used.Add(accepted.Id), "A source word appeared twice in the game.");
                        if (kind == IdiomMatchKind.Computer && game.ActorAt(accounted - 1) == IdiomActor.App)
                            Require((accepted.Common || profile.IsFamiliar(accepted.Id)) && !profile.IsNotebook(accepted.Id), "Normal APP reply must remain in the ordinary pool.");
                        string key = mode == IdiomMode.SameCharacter ? accepted.FirstCharacter : accepted.FirstKey;
                        int n; usedFirst.TryGetValue(key, out n); usedFirst[key] = n + 1;
                    }
                    List<IdiomEntry> candidates;
                    if (game.LastEntry == null) candidates = entries;
                    else
                    {
                        string target = mode == IdiomMode.SameCharacter ? game.LastEntry.LastCharacter : game.LastEntry.LastKey;
                        Require(byFirst.TryGetValue(target, out candidates), "Live turn must have indexed moves.");
                    }
                    IdiomEntry selected = null; int score = -1;
                    foreach (var entry in candidates)
                    {
                        if (used.Contains(entry.Id)) continue;
                        string next = mode == IdiomMode.SameCharacter ? entry.LastCharacter : entry.LastKey;
                        List<IdiomEntry> future; int spent;
                        int remaining = (byFirst.TryGetValue(next, out future) ? future.Count : 0) - (usedFirst.TryGetValue(next, out spent) ? spent : 0);
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
                if (game.EndReason == IdiomEndReason.RecommendationExhausted) profile.Remember(lex.Entries[0].Id);
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
                Require(probe.EndReason == IdiomEndReason.RecommendationExhausted, "Rare 际 words must not be an automatic fallback for a new learner.");
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
                    Require(game.LastEntry.Common || profile.IsFamiliar(game.LastEntry.Id), "Example reply stays in the ordinary pool."); outputs.Add(game.LastEntry.Word);
                }
                new IdiomGame(lex, game.GetSnapshot(), profile, () => 1800000000L);
            }
            Require(outputs.Count > 1, "A single high score must not monopolize the real-catalog example.");
            Console.WriteLine("CATALOG 一望无际 mode=" + mode + " distinct_replies=" + outputs.Count + " [" + string.Join(",", outputs) + "]");
        }
        Print("start", starts); Print("submit", submissions); Print("restore", restores);
        Console.WriteLine("CATALOG total_messages=" + totalMessages + " longest=" + longest + " (desktop .NET timings, not device timings)");
    }

    private static void Print(string name, List<double> values)
    {
        values.Sort();
        Console.WriteLine("CATALOG " + name + " samples=" + values.Count + " median_ms=" + values[values.Count / 2].ToString("F3") +
                          " max_ms=" + values[values.Count - 1].ToString("F3"));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
