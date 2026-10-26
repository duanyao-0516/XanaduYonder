using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Sheep
{
    public static class SheepLevelGenerator
    {
        public static GeneratedSheepBoard Generate(int level, uint seed, int previousLayoutId = -1)
        {
            if (level < 1 || level > 2) throw new ArgumentOutOfRangeException("level");
            uint random = seed == 0 ? 0x6D2B79F5u : seed;
            // Mix small consecutive seeds before selecting a layout.
            SheepRandom.Index(ref random, int.MaxValue);
            int layoutId;
            if (level == 1) layoutId = SheepRandom.Index(ref random, SheepBoardLayout.NewLayoutStart);
            else
            {
                int count = SheepBoardLayout.ChallengeLayoutCount - SheepBoardLayout.NewLayoutStart;
                bool excludePrevious = previousLayoutId >= SheepBoardLayout.NewLayoutStart &&
                    previousLayoutId < SheepBoardLayout.ChallengeLayoutCount;
                int choice = SheepRandom.Index(ref random, count - (excludePrevious ? 1 : 0));
                layoutId = SheepBoardLayout.NewLayoutStart + choice;
                if (excludePrevious && layoutId >= previousLayoutId) layoutId++;
            }
            SheepPosition[] positions = SheepBoardLayout.GetPositions(level, layoutId);
            int[] solution = RemovalOrder(positions, ref random);
            var kinds = new int[positions.Length];
            int kindCount = level == 1 ? 3 : 16;
            var unopenedGroups = new int[kindCount];
            var progress = new int[kindCount];
            for (int i = 0; i < kindCount; i++) unopenedGroups[i] = level == 1 ? 2 : 3;
            int activeGroups = 0;
            var candidates = new List<int>(kindCount);
            foreach (int id in solution)
            {
                candidates.Clear();
                for (int kind = 0; kind < kindCount; kind++)
                    if (progress[kind] > 0 || (activeGroups < 3 && unopenedGroups[kind] > 0)) candidates.Add(kind);
                int selected = candidates[SheepRandom.Index(ref random, candidates.Count)];
                if (progress[selected] == 0) { unopenedGroups[selected]--; activeGroups++; }
                kinds[id] = selected;
                progress[selected]++;
                if (progress[selected] == 3) { progress[selected] = 0; activeGroups--; }
            }

            // At most three distinct unfinished triples can exist along this witness.
            // Each contributes at most two settled tray cards. A seventh card completes
            // a triple, and matching happens before the capacity check in SheepGame.
            var snapshot = new SheepSnapshot {
                Level = level, LayoutId = layoutId, Kinds = kinds,
                Locations = new SheepLocation[positions.Length], Tray = new int[0], Reserve = new int[0],
                RandomState = random
            };
            return new GeneratedSheepBoard(snapshot, solution);
        }

        private static int[] RemovalOrder(SheepPosition[] positions, ref uint random)
        {
            var blockers = new int[positions.Length];
            var removed = new bool[positions.Length];
            var order = new int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            for (int j = 0; j < positions.Length; j++)
                if (positions[j].Layer > positions[i].Layer && SheepBoardLayout.Overlaps(positions[i], positions[j])) blockers[i]++;
            var candidates = new List<int>(positions.Length);
            for (int step = 0; step < order.Length; step++)
            {
                candidates.Clear();
                for (int i = 0; i < positions.Length; i++) if (!removed[i] && blockers[i] == 0) candidates.Add(i);
                int selected = candidates[SheepRandom.Index(ref random, candidates.Count)];
                removed[selected] = true;
                order[step] = selected;
                for (int i = 0; i < positions.Length; i++)
                    if (!removed[i] && positions[selected].Layer > positions[i].Layer && SheepBoardLayout.Overlaps(positions[i], positions[selected])) blockers[i]--;
            }
            return order;
        }
    }

    internal static class SheepRandom
    {
        internal static int Index(ref uint state, int count)
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return (int)(value % (uint)count);
        }
    }
}
