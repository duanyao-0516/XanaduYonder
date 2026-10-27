using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using XanaduYonder.Core.Sheep;

internal sealed class PlayerOutcome
{
    public uint Seed { get; set; }
    public string Difficulty { get; set; }
    public string Policy { get; set; }
    // Omitted for the original generous contract, preserving baseline JSON.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Observation { get; set; }
    public bool Tools { get; set; }
    public bool Won { get; set; }
    public int Picks { get; set; }
    public int Cleared { get; set; }
    public int Layout { get; set; }
    public int PeakTray { get; set; }
    // Order: RemoveThree, Undo, Shuffle, Revive.
    public int[] ToolUses { get; set; } = new int[4];
    public int[] TrayHistogram { get; set; } = new int[8];
    public int InitialExposed { get; set; }
    public int InitialTripleKinds { get; set; }
    public int MatchPicks { get; set; }
    public double MeanExposed { get; set; }
    public int NoVisibleTripleSteps { get; set; }
}

// An independent, deterministic player. Generation is outside this class; this
// player never receives or reads the generator's witness. Both policies choose
// exclusively from observations whose hidden blind-stack kinds are masked.
internal static class VisiblePlayer
{
    private sealed class Geometry
    {
        internal readonly int[][] Above;
        internal readonly SheepPosition[] Positions;

        internal Geometry(SheepPosition[] positions)
        {
            Positions = positions;
            Above = new int[positions.Length][];
            for (int id = 0; id < positions.Length; id++)
            {
                var blockers = new List<int>();
                for (int other = 0; other < positions.Length; other++)
                {
                    if (positions[other].Layer > positions[id].Layer &&
                        Math.Abs(positions[other].X - positions[id].X) < 2 &&
                        Math.Abs(positions[other].Y - positions[id].Y) < 2)
                        blockers.Add(other);
                }
                Above[id] = blockers.ToArray();
            }
        }
    }

    private sealed class Observation
    {
        internal int[] Kinds;
        internal bool[] Board, Reserve;
        internal int[] Tray;
        internal int TraySize;
        internal Geometry Geometry;

        internal Observation Copy()
        {
            return new Observation {
                Kinds = Kinds, Board = (bool[])Board.Clone(), Reserve = (bool[])Reserve.Clone(),
                Tray = (int[])Tray.Clone(), TraySize = TraySize, Geometry = Geometry
            };
        }

        internal List<int> Legal()
        {
            var ids = new List<int>();
            for (int id = 0; id < Kinds.Length; id++)
            {
                if (Kinds[id] >= 0 && (Reserve[id] ||
                    (Board[id] && Geometry.Above[id].All(blocker => !Board[blocker]))))
                    ids.Add(id);
            }
            return ids;
        }

        internal int Pick(int id)
        {
            Board[id] = false;
            Reserve[id] = false;
            int kind = Kinds[id];
            Tray[kind]++;
            TraySize++;
            if (Tray[kind] != 3) return 0;
            Tray[kind] = 0;
            TraySize -= 3;
            return 3;
        }
    }

    // This is the only boundary that reads actual card kinds. The benchmark
    // deliberately permits knowledge of every non-blind icon, including cards
    // that may be fully occluded by the rendered UI. Only the top blind icon
    // is known. The optional exposed contract instead masks every covered
    // board icon, including ordinary cards. Neither contract models pixels.
    // Hypothetical picks never unmask unknown icons or infer their kinds
    // from kind quotas. Removed cards carry no kind information either.
    private static Observation Observe(SheepSnapshot snapshot, Geometry geometry, string observation)
    {
        var observed = new Observation {
            Kinds = Enumerable.Repeat(-1, snapshot.Kinds.Length).ToArray(),
            Board = new bool[snapshot.Kinds.Length], Reserve = new bool[snapshot.Kinds.Length],
            Tray = new int[16], TraySize = snapshot.Tray.Length, Geometry = geometry
        };
        for (int id = 0; id < snapshot.Kinds.Length; id++)
        {
            observed.Board[id] = snapshot.Locations[id] == SheepLocation.Board;
            observed.Reserve[id] = snapshot.Locations[id] == SheepLocation.Reserve;
            bool visible = (observation == "generous" && geometry.Positions[id].BlindStack == 0) || !observed.Board[id] ||
                geometry.Above[id].All(blocker => snapshot.Locations[blocker] != SheepLocation.Board);
            if (visible && snapshot.Locations[id] != SheepLocation.Removed)
                observed.Kinds[id] = snapshot.Kinds[id];
        }
        foreach (int id in snapshot.Tray) observed.Tray[observed.Kinds[id]]++;
        return observed;
    }

    private static double Score(Observation observed, int id, List<int> legal)
    {
        int kind = observed.Kinds[id], inTray = observed.Tray[kind];
        if (observed.TraySize == 6 && inTray != 2) return -1e8;
        int same = legal.Count(other => observed.Kinds[other] == kind);
        var next = observed.Copy();
        int cleared = next.Pick(id);
        var nextLegal = next.Legal();
        var nextCounts = new int[16];
        foreach (int other in nextLegal) nextCounts[next.Kinds[other]]++;

        // Immediate completion, exposed same-kind clusters, existing tray
        // matches, and directly revealed known matches. The smallest ID breaks
        // equal scores deterministically; the witness order is never consulted.
        double score = cleared * 10000 + (same + inTray >= 3 ? 3000 : 0) +
            inTray * 160 + Math.Min(2, same - 1) * 70;
        if (next.Tray[kind] > 0 && nextCounts[kind] + next.Tray[kind] >= 3) score += 900;
        foreach (int other in nextLegal)
        {
            if (!legal.Contains(other)) score += 22 + next.Tray[next.Kinds[other]] * 35;
        }
        score -= next.TraySize * 12;
        score += nextLegal.Count * 0.2;
        if (observed.Geometry.Positions[id].BlindStack > 0) score += 4;
        if (observed.Reserve[id]) score += 3;
        return score;
    }

    private static int Choose(Observation observed, string policy, HashSet<int> banned)
    {
        var all = observed.Legal();
        var legal = all.Where(id => !banned.Contains(id)).ToList();
        if (legal.Count == 0) legal = all;
        var ranked = legal.OrderByDescending(id => Score(observed, id, all)).ThenBy(id => id).ToList();
        if (ranked.Count == 0) return -1;
        if (policy == "greedy" || observed.Tray[observed.Kinds[ranked[0]]] == 2) return ranked[0];

        // Optional stronger policies: at most three picks, with beam width
        // eight (visible3) or twelve (visible3wide). Unknown newly revealed
        // cards cannot be selected during either search.
        int beamWidth = policy == "visible3wide" ? 12 : 8;
        double best = double.NegativeInfinity;
        int chosen = ranked[0];
        foreach (int id in ranked.Take(beamWidth))
        {
            var next = observed.Copy();
            int cleared = next.Pick(id);
            if (next.TraySize >= 7) continue;
            double value = cleared * 10000 + Search(next, 2, beamWidth) + Score(observed, id, all) * 0.001;
            if (value <= best) continue;
            best = value;
            chosen = id;
        }
        return chosen;
    }

    private static double Search(Observation observed, int depth, int beamWidth)
    {
        var legal = observed.Legal();
        double best = -observed.TraySize * 40 + legal.Count * 0.5;
        if (depth == 0 || legal.Count == 0) return best;
        foreach (int id in legal.OrderByDescending(id => Score(observed, id, legal)).ThenBy(id => id).Take(beamWidth))
        {
            var next = observed.Copy();
            int cleared = next.Pick(id);
            if (next.TraySize >= 7) continue;
            best = Math.Max(best, cleared * 10000 - 1 + Search(next, depth - 1, beamWidth));
        }
        return best;
    }

    internal static void CheckVisibilityBoundary(SheepSnapshot initial, string observation = "generous")
    {
        var geometry = new Geometry(SheepBoardLayout.GetPositions(initial.Level, initial.LayoutId));
        var first = Observe(initial, geometry, observation);
        var changed = initial.Copy();
        var hidden = new List<int>();
        for (int id = 0; id < initial.Kinds.Length; id++)
        {
            bool isHidden = (observation == "exposed" || geometry.Positions[id].BlindStack > 0) &&
                initial.Locations[id] == SheepLocation.Board &&
                geometry.Above[id].Any(blocker => initial.Locations[blocker] == SheepLocation.Board);
            if (isHidden)
            {
                if (first.Kinds[id] != -1) throw new Exception("Hidden icon leaked into observation.");
                hidden.Add(id);
            }
            else if (initial.Locations[id] != SheepLocation.Removed && first.Kinds[id] != initial.Kinds[id])
                throw new Exception("A visible icon is missing from observation.");
        }
        // Permute only hidden icons. The observed arrays and all player decisions must
        // remain identical, while the real kind multiplicities remain valid.
        for (int index = 0; index < hidden.Count; index++)
            changed.Kinds[hidden[index]] = initial.Kinds[hidden[(index + 1) % hidden.Count]];
        var second = Observe(changed, geometry, observation);
        if (!first.Kinds.SequenceEqual(second.Kinds)) throw new Exception("Hidden permutation changed observation.");
        foreach (string policy in new[] { "greedy", "visible3", "visible3wide" })
        {
            if (Choose(first, policy, new HashSet<int>()) != Choose(second, policy, new HashSet<int>()))
                throw new Exception("Hidden permutation changed a player decision.");
        }
        // Check every one-step hypothetical reveal, ordinary and blind.
        foreach (int pick in first.Legal())
        {
            var hypothetical = first.Copy();
            hypothetical.Pick(pick);
            foreach (int id in hidden)
                if (hypothetical.Kinds[id] != -1) throw new Exception("Hypothetical pick revealed a hidden icon.");
        }
    }

    internal static PlayerOutcome Run(SheepSnapshot initial, bool useTools, string policy, string observation = "generous")
    {
        if (policy != "greedy" && policy != "visible3" && policy != "visible3wide") throw new ArgumentException("Unknown player policy.");
        if (observation != "generous" && observation != "exposed") throw new ArgumentException("Unknown observation contract.");
        var game = new SheepGame(initial);
        var geometry = new Geometry(SheepBoardLayout.GetPositions(initial.Level, initial.LayoutId));
        var result = new PlayerOutcome {
            Difficulty = initial.Difficulty.ToString(), Policy = policy, Tools = useTools, Layout = initial.LayoutId,
            Observation = observation == "generous" ? null : observation
        };
        var rejected = new Dictionary<string, HashSet<int>>();
        string previousKey = "";
        int previousId = -1, previousChoices = 0;
        double exposure = 0;

        void RejectPrevious()
        {
            if (!game.UseTool(SheepTool.Undo)) throw new Exception("Undo failed after CanUseTool.");
            result.ToolUses[1]++;
            if (!rejected.ContainsKey(previousKey)) rejected[previousKey] = new HashSet<int>();
            rejected[previousKey].Add(previousId);
        }

        for (int turns = 0; turns < 800; turns++)
        {
            if (game.Phase == SheepPhase.Won) { result.Won = true; break; }
            // CanPick rejects every ID after loss. Recovery must precede any
            // selection or legality query; a lost snapshot is never searched.
            if (game.Phase == SheepPhase.Lost)
            {
                if (!useTools) break;
                if (previousChoices > 1 && game.CanUseTool(SheepTool.Undo)) { RejectPrevious(); continue; }
                if (game.CanUseTool(SheepTool.Revive))
                {
                    game.UseTool(SheepTool.Revive);
                    result.ToolUses[3]++;
                    continue;
                }
                break;
            }

            var snapshot = game.GetSnapshot();
            string key = string.Concat(snapshot.Locations.Select(location => (char)(65 + (int)location)));
            var banned = rejected.TryGetValue(key, out var choices) ? choices : new HashSet<int>();
            var observed = Observe(snapshot, geometry, observation);
            var legal = observed.Legal();
            foreach (int id in legal)
                if (!game.CanPick(id)) throw new Exception("Observed legality disagrees with core.");
            var exposedCounts = new int[16];
            foreach (int id in legal) exposedCounts[observed.Kinds[id]]++;
            if (result.Picks == 0)
            {
                result.InitialExposed = legal.Count;
                result.InitialTripleKinds = exposedCounts.Count(count => count >= 3);
            }
            int pick = Choose(observed, policy, banned);
            if (pick < 0) throw new Exception("A playing game has no visible legal move.");
            bool immediate = observed.Tray[observed.Kinds[pick]] == 2;

            // Bounded rescue policy, using default inventory: at six slots with
            // no immediate completion, undo the preceding non-clearing choice
            // and try another ID. Then remove, shuffle, or pick and revive.
            // This is a reproducible heuristic, not optimal tool planning.
            if (useTools && snapshot.Tray.Length == 6 && !immediate)
            {
                if (previousChoices > 1 && game.CanUseTool(SheepTool.Undo)) { RejectPrevious(); continue; }
                if (game.CanUseTool(SheepTool.RemoveThree))
                {
                    game.UseTool(SheepTool.RemoveThree);
                    result.ToolUses[0]++;
                    continue;
                }
                if (game.CanUseTool(SheepTool.Shuffle))
                {
                    game.UseTool(SheepTool.Shuffle);
                    result.ToolUses[2]++;
                    rejected.Clear();
                    continue;
                }
            }
            if (!game.CanPick(pick)) throw new Exception("Player selected an illegal move.");
            previousKey = key;
            previousId = pick;
            previousChoices = legal.Count;
            var move = game.Pick(pick);
            if (!move.Changed) throw new Exception("Core rejected a legal pick.");
            result.Picks++;
            if (move.MatchedIds.Length > 0) result.MatchPicks++;
            int tray = game.GetSnapshot().Tray.Length;
            result.TrayHistogram[tray]++;
            result.PeakTray = Math.Max(result.PeakTray, tray);
            exposure += legal.Count;
            if (!exposedCounts.Where((count, kind) => count + observed.Tray[kind] >= 3).Any())
                result.NoVisibleTripleSteps++;
        }
        if (game.Phase == SheepPhase.Playing) throw new Exception("Player exceeded bounded action count.");
        result.Won = game.Phase == SheepPhase.Won;
        result.Cleared = game.ClearedCount;
        result.MeanExposed = exposure / Math.Max(1, result.Picks);
        return result;
    }
}
