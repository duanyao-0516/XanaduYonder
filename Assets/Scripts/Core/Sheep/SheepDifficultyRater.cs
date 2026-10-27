using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Sheep
{
    // A bounded simulated player used to rank generation candidates. The generous
    // policy knows ordinary covered icons; exposedOnly masks every covered card.
    // Geometry is precomputed for scoring; every move and tool uses the real game.
    internal static class SheepDifficultyRater
    {
        private const int MaxTurns = 800;

        internal static int ClearedWithTools(SheepSnapshot snapshot, SheepPosition[] positions, int lookaheadWidth = 0, bool exposedOnly = false)
        {
            var standard = snapshot.Copy();
            standard.RemoveLimit = standard.RemoveRemaining = 2;
            standard.UndoLimit = standard.UndoRemaining = 3;
            standard.ShuffleLimit = standard.ShuffleRemaining = 1;
            standard.ReviveUsed = false;
            var game = new SheepGame(standard);
            var below = GetCoveredCards(positions);
            var rejected = new Dictionary<string, HashSet<int>>();
            string previousKey = string.Empty;
            int previousId = -1, previousChoices = 0;
            int[] blockers = new int[positions.Length], counts = new int[16], exposed = new int[16];
            var legal = new List<int>(positions.Length);
            var planner = lookaheadWidth > 0 ? new Lookahead(positions, below, lookaheadWidth, exposedOnly) : null;

            for (int turn = 0; turn < MaxTurns && game.Phase != SheepPhase.Won; turn++)
            {
                if (game.Phase == SheepPhase.Lost)
                {
                    if (previousChoices > 1 && game.CanUseTool(SheepTool.Undo))
                    {
                        game.UseTool(SheepTool.Undo);
                        Reject(rejected, previousKey, previousId);
                        continue;
                    }
                    if (game.CanUseTool(SheepTool.Revive))
                    {
                        game.UseTool(SheepTool.Revive);
                        continue;
                    }
                    break;
                }

                SheepSnapshot state = game.GetSnapshot();
                string key = StateKey(state.Locations);
                HashSet<int> banned;
                rejected.TryGetValue(key, out banned);
                Array.Clear(blockers, 0, blockers.Length);
                Array.Clear(counts, 0, counts.Length);
                Array.Clear(exposed, 0, exposed.Length);
                legal.Clear();
                foreach (int id in state.Tray) counts[state.Kinds[id]]++;
                for (int id = 0; id < positions.Length; id++)
                    if (state.Locations[id] == SheepLocation.Board)
                        foreach (int child in below[id])
                            if (state.Locations[child] == SheepLocation.Board) blockers[child]++;
                for (int id = 0; id < positions.Length; id++)
                    if (state.Locations[id] == SheepLocation.Reserve ||
                        (state.Locations[id] == SheepLocation.Board && blockers[id] == 0))
                    {
                        legal.Add(id);
                        exposed[state.Kinds[id]]++;
                    }
                if (legal.Count == 0) break;
                bool hasAlternative = false;
                foreach (int id in legal) if (banned == null || !banned.Contains(id)) hasAlternative = true;
                int selected = -1;
                double best = double.NegativeInfinity;
                foreach (int id in legal)
                {
                    if (hasAlternative && banned != null && banned.Contains(id)) continue;
                    double score = Score(state, positions, below, blockers, counts, exposed, legal.Count, id, exposedOnly);
                    // Iterating IDs in ascending order gives a deterministic tie break.
                    if (score > best) { best = score; selected = id; }
                }

                if (planner != null)
                    selected = planner.Choose(state, blockers, counts, banned, hasAlternative);

                if (state.Tray.Length == 6 && counts[state.Kinds[selected]] != 2)
                {
                    if (previousChoices > 1 && game.CanUseTool(SheepTool.Undo))
                    {
                        game.UseTool(SheepTool.Undo);
                        Reject(rejected, previousKey, previousId);
                        continue;
                    }
                    if (game.CanUseTool(SheepTool.RemoveThree))
                    {
                        game.UseTool(SheepTool.RemoveThree);
                        if (banned != null) banned.Clear();
                        continue;
                    }
                    if (game.CanUseTool(SheepTool.Shuffle))
                    {
                        game.UseTool(SheepTool.Shuffle);
                        rejected.Clear();
                        continue;
                    }
                }
                previousKey = key;
                previousId = selected;
                previousChoices = legal.Count;
                if (!game.Pick(selected).Changed)
                    throw new InvalidOperationException("Difficulty rater selected a blocked card.");
            }
            return game.ClearedCount;
        }

        private static double Score(SheepSnapshot state, SheepPosition[] positions, List<int>[] below,
            int[] blockers, int[] counts, int[] exposed, int legalCount, int id, bool exposedOnly)
        {
            int kind = state.Kinds[id], inTray = counts[kind];
            if (state.Tray.Length == 6 && inTray != 2) return -1e8;
            int nextKindCount = (inTray + 1) % 3;
            int nextTraySize = state.Tray.Length + 1 - (inTray == 2 ? 3 : 0);
            double score = (inTray == 2 ? 30000 : 0) + (exposed[kind] + inTray >= 3 ? 3000 : 0) +
                inTray * 160 + Math.Min(2, exposed[kind] - 1) * 70;
            int nextSameKind = exposed[kind] - 1, newlyVisible = 0;
            if (!exposedOnly && state.Locations[id] == SheepLocation.Board)
                foreach (int child in below[id])
                    if (state.Locations[child] == SheepLocation.Board && blockers[child] == 1 &&
                        positions[child].BlindStack == 0)
                    {
                        // This generous information model includes ordinary covered
                        // icons, even fully occluded ones. Blind icons remain unknown.
                        int childKind = state.Kinds[child];
                        newlyVisible++;
                        if (childKind == kind) nextSameKind++;
                        score += 22 + (childKind == kind ? nextKindCount : counts[childKind]) * 35;
                    }
            if (nextKindCount > 0 && nextSameKind + nextKindCount >= 3) score += 900;
            score -= nextTraySize * 12;
            score += (legalCount - 1 + newlyVisible) * 0.2;
            if (positions[id].BlindStack > 0) score += 4;
            if (state.Locations[id] == SheepLocation.Reserve) score += 3;
            return score;
        }

        // This search never changes the game. It restores its small working arrays
        // after each hypothetical pick and keeps unrevealed blind icons unknown.
        private sealed class Lookahead
        {
            private readonly SheepPosition[] positions;
            private readonly List<int>[] below;
            private readonly int width;
            private readonly bool exposedOnly;
            private readonly int[] kinds, locations, blockers, counts;
            private readonly int[][] legal, exposed, ranked;
            private readonly double[][] rankedScores;
            private int traySize;

            internal Lookahead(SheepPosition[] positions, List<int>[] below, int width, bool exposedOnly)
            {
                this.positions = positions; this.below = below; this.exposedOnly = exposedOnly;
                this.width = Math.Min(width, positions.Length);
                kinds = new int[positions.Length]; locations = new int[positions.Length];
                blockers = new int[positions.Length]; counts = new int[16];
                legal = new int[4][]; exposed = new int[4][]; ranked = new int[4][];
                rankedScores = new double[4][];
                for (int depth = 0; depth < 4; depth++)
                {
                    legal[depth] = new int[positions.Length]; exposed[depth] = new int[16];
                    ranked[depth] = new int[this.width]; rankedScores[depth] = new double[this.width];
                }
            }

            internal int Choose(SheepSnapshot state, int[] currentBlockers, int[] currentCounts,
                HashSet<int> banned, bool hasAlternative)
            {
                Array.Copy(currentBlockers, blockers, blockers.Length);
                Array.Copy(currentCounts, counts, counts.Length);
                traySize = state.Tray.Length;
                for (int id = 0; id < kinds.Length; id++)
                {
                    locations[id] = (int)state.Locations[id];
                    kinds[id] = state.Locations[id] == SheepLocation.Board &&
                        (exposedOnly || positions[id].BlindStack > 0) && blockers[id] > 0 ? -1 : state.Kinds[id];
                }
                int count = FindLegal(3);
                int rankedCount = Rank(3, count, banned, hasAlternative);
                int chosen = ranked[3][0];
                if (counts[kinds[chosen]] == 2) return chosen;
                double best = double.NegativeInfinity;
                for (int index = 0; index < rankedCount; index++)
                {
                    int id = ranked[3][index], oldLocation = locations[id], oldCount = counts[kinds[id]];
                    int cleared = Pick(id);
                    if (traySize < 7)
                    {
                        double score = cleared * 10000 + Search(2) + rankedScores[3][index] * 0.001;
                        if (score > best) { best = score; chosen = id; }
                    }
                    Restore(id, oldLocation, oldCount, cleared);
                }
                return chosen;
            }

            private double Search(int depth)
            {
                int count = FindLegal(depth);
                double best = -traySize * 40 + count * 0.5;
                if (depth == 0 || count == 0) return best;
                int rankedCount = Rank(depth, count, null, false);
                for (int index = 0; index < rankedCount; index++)
                {
                    int id = ranked[depth][index], oldLocation = locations[id], oldCount = counts[kinds[id]];
                    int cleared = Pick(id);
                    if (traySize < 7) best = Math.Max(best, cleared * 10000 - 1 + Search(depth - 1));
                    Restore(id, oldLocation, oldCount, cleared);
                }
                return best;
            }

            private int FindLegal(int depth)
            {
                int count = 0;
                Array.Clear(exposed[depth], 0, 16);
                for (int id = 0; id < kinds.Length; id++)
                    if (kinds[id] >= 0 && (locations[id] == (int)SheepLocation.Reserve ||
                        (locations[id] == (int)SheepLocation.Board && blockers[id] == 0)))
                    {
                        legal[depth][count++] = id; exposed[depth][kinds[id]]++;
                    }
                return count;
            }

            private int Rank(int depth, int count, HashSet<int> banned, bool hasAlternative)
            {
                int kept = 0;
                for (int index = 0; index < count; index++)
                {
                    int id = legal[depth][index];
                    if (hasAlternative && banned != null && banned.Contains(id)) continue;
                    double score = Score(id, exposed[depth], count);
                    int place = kept;
                    while (place > 0 && score > rankedScores[depth][place - 1]) place--;
                    if (place >= width) continue;
                    for (int move = Math.Min(kept, width - 1); move > place; move--)
                    {
                        ranked[depth][move] = ranked[depth][move - 1];
                        rankedScores[depth][move] = rankedScores[depth][move - 1];
                    }
                    ranked[depth][place] = id; rankedScores[depth][place] = score;
                    if (kept < width) kept++;
                }
                return kept;
            }

            private double Score(int id, int[] currentExposed, int legalCount)
            {
                int kind = kinds[id], inTray = counts[kind];
                if (traySize == 6 && inTray != 2) return -1e8;
                int nextKind = (inTray + 1) % 3, nextSize = traySize + 1 - (inTray == 2 ? 3 : 0);
                double score = (inTray == 2 ? 30000 : 0) + (currentExposed[kind] + inTray >= 3 ? 3000 : 0) +
                    inTray * 160 + Math.Min(2, currentExposed[kind] - 1) * 70;
                int nextSame = currentExposed[kind] - 1, newlyVisible = 0;
                if (locations[id] == (int)SheepLocation.Board)
                    foreach (int child in below[id])
                        if (locations[child] == (int)SheepLocation.Board && blockers[child] == 1 && kinds[child] >= 0)
                        {
                            int childKind = kinds[child]; newlyVisible++;
                            if (childKind == kind) nextSame++;
                            score += 22 + (childKind == kind ? nextKind : counts[childKind]) * 35;
                        }
                if (nextKind > 0 && nextSame + nextKind >= 3) score += 900;
                score -= nextSize * 12; score += (legalCount - 1 + newlyVisible) * 0.2;
                if (positions[id].BlindStack > 0) score += 4;
                if (locations[id] == (int)SheepLocation.Reserve) score += 3;
                return score;
            }

            private int Pick(int id)
            {
                if (locations[id] == (int)SheepLocation.Board)
                    foreach (int child in below[id]) blockers[child]--;
                locations[id] = (int)SheepLocation.Tray;
                int kind = kinds[id]; counts[kind]++; traySize++;
                if (counts[kind] != 3) return 0;
                counts[kind] = 0; traySize -= 3; return 3;
            }

            private void Restore(int id, int oldLocation, int oldCount, int cleared)
            {
                locations[id] = oldLocation;
                if (oldLocation == (int)SheepLocation.Board)
                    foreach (int child in below[id]) blockers[child]++;
                counts[kinds[id]] = oldCount; traySize += cleared - 1;
            }
        }

        private static List<int>[] GetCoveredCards(SheepPosition[] positions)
        {
            var below = new List<int>[positions.Length];
            for (int id = 0; id < positions.Length; id++)
            {
                below[id] = new List<int>();
                for (int child = 0; child < positions.Length; child++)
                    if (positions[id].Layer > positions[child].Layer &&
                        SheepBoardLayout.Overlaps(positions[id], positions[child])) below[id].Add(child);
            }
            return below;
        }

        private static string StateKey(SheepLocation[] locations)
        {
            var key = new char[locations.Length];
            for (int id = 0; id < locations.Length; id++) key[id] = (char)('A' + (int)locations[id]);
            return new string(key);
        }

        private static void Reject(Dictionary<string, HashSet<int>> rejected, string key, int id)
        {
            HashSet<int> ids;
            if (!rejected.TryGetValue(key, out ids)) { ids = new HashSet<int>(); rejected.Add(key, ids); }
            ids.Add(id);
        }
    }
}
