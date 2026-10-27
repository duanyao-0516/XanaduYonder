using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Sheep
{
    public static class SheepLevelGenerator
    {
        private const int MaxGenerationAttempts = 8;

        public static GeneratedSheepBoard Generate(int level, uint seed, int previousLayoutId = -1)
        {
            if (level < 1 || level > 2) throw new ArgumentOutOfRangeException("level");
            return Generate(level == 1 ? SheepMode.Tutorial : SheepMode.Challenge,
                SheepDifficulty.Normal, seed, previousLayoutId: previousLayoutId);
        }

        public static GeneratedSheepBoard Generate(SheepMode mode, SheepDifficulty difficulty, uint seed,
            int removeLimit = 2, int undoLimit = 3, int shuffleLimit = 1, int previousLayoutId = -1)
        {
            if (mode < SheepMode.Challenge || mode > SheepMode.Tutorial) throw new ArgumentOutOfRangeException("mode");
            if (difficulty < SheepDifficulty.Easy || difficulty > SheepDifficulty.Extreme) throw new ArgumentOutOfRangeException("difficulty");
            if (removeLimit < 0 || removeLimit > 99) throw new ArgumentOutOfRangeException("removeLimit");
            if (undoLimit < 0 || undoLimit > 99) throw new ArgumentOutOfRangeException("undoLimit");
            if (shuffleLimit < 0 || shuffleLimit > 99) throw new ArgumentOutOfRangeException("shuffleLimit");
            if (mode != SheepMode.Challenge) difficulty = SheepDifficulty.Normal;
            int level = mode == SheepMode.Tutorial ? 1 : 2;
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
            for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
            {
                int[] solution = RemovalOrder(positions, ref random);
                int kindCount = GetKindCount(mode, difficulty);
                int profile = mode == SheepMode.Tutorial ? 0 : (int)difficulty;
                int[] kinds = AssignKinds(solution, kindCount, profile, ref random);
                var snapshot = new SheepSnapshot {
                    Level = level, LayoutId = layoutId, Mode = mode, Difficulty = difficulty,
                    Kinds = kinds, Locations = new SheepLocation[positions.Length],
                    Tray = new int[0], Reserve = new int[0], RandomState = random,
                    RemoveLimit = removeLimit, RemoveRemaining = removeLimit,
                    UndoLimit = undoLimit, UndoRemaining = undoLimit,
                    ShuffleLimit = shuffleLimit, ShuffleRemaining = shuffleLimit
                };
                // The acceptance boundary is the game itself: geometry, grouping and
                // the seven-slot loss check must all agree with this witness.
                if (VerifyWitness(snapshot, solution)) return new GeneratedSheepBoard(snapshot, solution);
            }
            throw new InvalidOperationException("Unable to generate a verified Sheep board within the candidate limit.");
        }

        public static int GetKindCount(SheepMode mode, SheepDifficulty difficulty)
        {
            if (mode == SheepMode.Tutorial) return 3;
            if (mode == SheepMode.Endless) return 12;
            return difficulty == SheepDifficulty.Easy ? 8 : difficulty == SheepDifficulty.Normal ? 12 : 16;
        }

        private static int[] AssignKinds(int[] solution, int kindCount, int profile, ref uint random)
        {
            var kinds = new int[solution.Length];
            var unopenedGroups = new int[kindCount];
            var progress = new int[kindCount];
            for (int i = 0; i < kindCount; i++) unopenedGroups[i] = solution.Length / kindCount / 3;
            int activeGroups = 0, trayCount = 0;
            int activeLimit = profile + 2, targetTrayCount = profile + 3;
            var candidates = new List<int>(kindCount);
            foreach (int id in solution)
            {
                candidates.Clear();
                // Higher profiles keep more kinds open and delay completing a triple.
                // Never put a seventh unmatched card in the tray. At six cards and
                // at most five open groups, at least one pair must be available.
                if (trayCount >= targetTrayCount)
                    for (int kind = 0; kind < kindCount; kind++) if (progress[kind] == 2) candidates.Add(kind);
                if (candidates.Count == 0 && trayCount < 6)
                {
                    if (activeGroups < activeLimit)
                        for (int kind = 0; kind < kindCount; kind++)
                            if (progress[kind] == 0 && unopenedGroups[kind] > 0) candidates.Add(kind);
                    if (candidates.Count == 0)
                        for (int kind = 0; kind < kindCount; kind++) if (progress[kind] == 1) candidates.Add(kind);
                }
                if (candidates.Count == 0)
                    for (int kind = 0; kind < kindCount; kind++) if (progress[kind] == 2) candidates.Add(kind);
                int selected = candidates[SheepRandom.Index(ref random, candidates.Count)];
                if (progress[selected] == 0) { unopenedGroups[selected]--; activeGroups++; }
                kinds[id] = selected;
                progress[selected]++; trayCount++;
                if (progress[selected] == 3) { progress[selected] = 0; activeGroups--; trayCount -= 3; }
            }
            return kinds;
        }

        private static bool VerifyWitness(SheepSnapshot snapshot, int[] solution)
        {
            var game = new SheepGame(snapshot);
            foreach (int id in solution)
                if (!game.Pick(id).Changed || game.Phase == SheepPhase.Lost) return false;
            return game.Phase == SheepPhase.Won;
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
