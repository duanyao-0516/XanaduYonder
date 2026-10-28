using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Sheep
{
    // Construct a legal route through dependent piles, then reduce alternative
    // easy matches by recoloring whole triples. No card is changed during play.
    internal static class SheepChallengeBuilder
    {
        private const int KindCount = 16;
        private const int ColoringPasses = 2000;

        internal static int[] Build(SheepPosition[] positions, ref uint random, out int[] solution)
        {
            int[] removedAt, exposedAt;
            solution = RemovalOrder(positions, ref random, out removedAt, out exposedAt);
            int[] groups = AssignGroups(solution, exposedAt, ref random);
            return ColorGroups(groups, removedAt, exposedAt, ref random);
        }

        internal static int[] RemovalOrder(SheepPosition[] positions, ref uint random,
            out int[] removedAt, out int[] exposedAt)
        {
            int count = positions.Length;
            var children = new List<int>[count];
            var blockers = new int[count];
            for (int id = 0; id < count; id++) children[id] = new List<int>();
            for (int below = 0; below < count; below++)
            for (int above = 0; above < count; above++)
                if (positions[above].Layer > positions[below].Layer &&
                    SheepBoardLayout.Overlaps(positions[below], positions[above]))
                {
                    blockers[below]++;
                    children[above].Add(below);
                }

            var solution = new int[count];
            var removed = new bool[count];
            removedAt = new int[count];
            exposedAt = new int[count];
            int last = -1;
            for (int step = 0; step < count; step++)
            {
                int selected = -1;
                double best = double.NegativeInfinity;
                for (int id = 0; id < count; id++)
                {
                    if (removed[id] || blockers[id] != 0) continue;
                    double score = Noise(ref random);
                    if (last >= 0)
                    {
                        int distance = Math.Abs(positions[id].X - positions[last].X) +
                            Math.Abs(positions[id].Y - positions[last].Y);
                        score += 12.0 / (1 + distance);
                        if (exposedAt[id] == step) score += 8;
                        score += Math.Min(3, children[id].Count) * 0.15;
                    }
                    if (score > best) { best = score; selected = id; }
                }
                if (selected < 0) throw new InvalidOperationException("Sheep layout contains a dependency cycle.");
                solution[step] = selected;
                removedAt[selected] = step;
                removed[selected] = true;
                last = selected;
                foreach (int child in children[selected])
                    if (--blockers[child] == 0) exposedAt[child] = step + 1;
            }

            return solution;
        }

        private static int[] AssignGroups(int[] order, int[] exposedAt, ref uint random)
        {
            var groups = new int[order.Length];
            var groupFor = new int[KindCount];
            var progress = new int[KindCount];
            var unopened = new int[KindCount];
            var firstAt = new int[KindCount];
            var pairedAt = new int[KindCount];
            var kindAt = new int[order.Length];
            for (int kind = 0; kind < KindCount; kind++) unopened[kind] = order.Length / KindCount / 3;
            var candidates = new List<int>(KindCount);
            int tray = 0, active = 0, nextGroup = 0;
            for (int step = 0; step < order.Length; step++)
            {
                candidates.Clear();
                if (tray == 6)
                {
                    for (int kind = 0; kind < KindCount; kind++)
                        if (progress[kind] == 2) candidates.Add(kind);
                }
                else
                {
                    for (int kind = 0; kind < KindCount; kind++)
                        if (progress[kind] == 1 && (step - firstAt[kind] >= 2 || tray == 5)) candidates.Add(kind);
                    if (active < 4 && (tray < 5 || candidates.Count == 0))
                        for (int kind = 0; kind < KindCount; kind++)
                            if (progress[kind] == 0 && unopened[kind] > 0) candidates.Add(kind);
                    if (candidates.Count == 0)
                        for (int kind = 0; kind < KindCount; kind++)
                            if (progress[kind] > 0) candidates.Add(kind);
                }

                int selected = -1;
                double best = double.NegativeInfinity;
                foreach (int kind in candidates)
                {
                    double score = Noise(ref random);
                    if (progress[kind] == 2) score += (step - pairedAt[kind]) * 10;
                    if (progress[kind] == 1) score += (step - firstAt[kind]) * 0.3;
                    if (step > 0 && kindAt[step - 1] == kind) score -= 100;
                    for (int prior = exposedAt[order[step]]; prior < step; prior++)
                        if (kindAt[prior] == kind) score -= 20;
                    if (score > best) { best = score; selected = kind; }
                }
                if (selected < 0) throw new InvalidOperationException("Sheep triple schedule cannot make progress.");
                kindAt[step] = selected;
                if (progress[selected] == 0)
                {
                    unopened[selected]--;
                    active++;
                    firstAt[selected] = step;
                    groupFor[selected] = nextGroup++;
                }
                groups[order[step]] = groupFor[selected];
                if (progress[selected] == 1) pairedAt[selected] = step;
                progress[selected]++;
                tray++;
                if (progress[selected] == 3) { progress[selected] = 0; tray -= 3; active--; }
            }
            return groups;
        }

        private static int[] ColorGroups(int[] groups, int[] removedAt, int[] exposedAt, ref uint random)
        {
            int groupCount = groups.Length / 3;
            var weights = new double[groupCount, groupCount];
            for (int a = 0; a < groups.Length; a++)
            for (int b = a + 1; b < groups.Length; b++)
            {
                int overlap = Math.Min(removedAt[a], removedAt[b]) - Math.Max(exposedAt[a], exposedAt[b]) + 1;
                if (overlap <= 0 || groups[a] == groups[b]) continue;
                double weight = 1 + Math.Min(12, overlap) * 0.1;
                if (exposedAt[a] == 0 && exposedAt[b] == 0) weight += 20;
                weights[groups[a], groups[b]] += weight;
                weights[groups[b], groups[a]] += weight;
            }

            var colors = new int[groupCount];
            for (int group = 0; group < groupCount; group++) colors[group] = group / (groupCount / KindCount);
            for (int group = groupCount - 1; group > 0; group--)
                Swap(colors, group, SheepRandom.Index(ref random, group + 1));
            // Swaps retain exactly three triples per kind. Recoloring a whole
            // triple cannot increase occupancy along the original legal route:
            // combining groups of the same kind only clears some cards earlier.
            for (int iteration = 0; iteration < ColoringPasses; iteration++)
            {
                int a = SheepRandom.Index(ref random, groupCount), b = SheepRandom.Index(ref random, groupCount);
                if (colors[a] == colors[b]) continue;
                double delta = 0;
                for (int group = 0; group < groupCount; group++)
                {
                    if (group == a || group == b) continue;
                    if (colors[group] == colors[a]) delta += weights[b, group] - weights[a, group];
                    if (colors[group] == colors[b]) delta += weights[a, group] - weights[b, group];
                }
                if (delta < 0 || (delta == 0 && SheepRandom.Index(ref random, 10) == 0)) Swap(colors, a, b);
            }
            var kinds = new int[groups.Length];
            for (int id = 0; id < kinds.Length; id++) kinds[id] = colors[groups[id]];
            return kinds;
        }

        private static void Swap(int[] values, int a, int b)
        {
            int value = values[a]; values[a] = values[b]; values[b] = value;
        }

        private static double Noise(ref uint random) { return SheepRandom.Index(ref random, 10000) / 10000.0; }
    }
}
