using System;

namespace XanaduYonder.Core.BoardGames
{
    internal static class ArmyBeliefs
    {
        private const int FitRounds = 28;
        private static readonly int[] RankTokens = BuildRankTokens();

        internal static double[,] Estimate(int[] domains)
        {
            var probabilities = new double[domains.Length, ArmyBoard.HighestKind + 1];
            for (int slot = 0; slot < domains.Length; slot++)
                for (int rank = 1; rank <= ArmyBoard.HighestKind; rank++)
                    if ((domains[slot] & (1 << rank)) != 0) probabilities[slot, rank] = Prior(slot, rank, domains);

            // Fit public slot constraints against the original inventory. Captured pieces
            // are still included because uncertainty about their ranks affects survivors.
            for (int round = 0; round < FitRounds; round++)
            {
                for (int rank = 1; rank <= ArmyBoard.HighestKind; rank++)
                {
                    double total = 0;
                    for (int slot = 0; slot < domains.Length; slot++) total += probabilities[slot, rank];
                    if (total <= 0) continue;
                    double scale = ArmyBoard.Inventory[rank] / total;
                    for (int slot = 0; slot < domains.Length; slot++) probabilities[slot, rank] *= scale;
                }

                for (int slot = 0; slot < domains.Length; slot++)
                {
                    double total = 0;
                    for (int rank = 1; rank <= ArmyBoard.HighestKind; rank++) total += probabilities[slot, rank];
                    if (total <= 0) continue;
                    for (int rank = 1; rank <= ArmyBoard.HighestKind; rank++) probabilities[slot, rank] /= total;
                }
            }
            return probabilities;
        }

        private static double Prior(int slot, int rank, int[] domains)
        {
            int row = slot / ArmyBoard.Columns;
            int col = slot % ArmyBoard.Columns;
            bool headquarters = row == 5 && (col == 1 || col == 3);
            if (rank == (int)ArmyPieceKind.Mine)
            {
                int leftFlag = 5 * ArmyBoard.Columns + 1;
                int rightFlag = 5 * ArmyBoard.Columns + 3;
                int flagBit = ArmyBoard.KindBit(ArmyPieceKind.Flag);
                int leftDistance = (domains[leftFlag] & flagBit) != 0 ? Math.Abs(col - 1) : ArmyBoard.Columns;
                int rightDistance = (domains[rightFlag] & flagBit) != 0 ? Math.Abs(col - 3) : ArmyBoard.Columns;
                return Math.Min(leftDistance, rightDistance) + 5 - row <= 1 ? 1.7 : 1;
            }
            if (rank == (int)ArmyPieceKind.Bomb && (row == 1 || row == 2)) return 1.35;
            if (headquarters && rank <= (int)ArmyPieceKind.Company) return 1.4;
            if (headquarters && rank >= (int)ArmyPieceKind.Division && rank <= (int)ArmyPieceKind.Marshal) return 0.45;
            return 1;
        }

        internal static int[] Sample(int[] domains, double[,] weights, Random random)
        {
            var preferences = new int[domains.Length][];
            var slots = new int[RankTokens.Length];
            var slotPriority = new double[RankTokens.Length];
            int usedSlots = 0;

            for (int slot = 0; slot < domains.Length; slot++)
            {
                if (domains[slot] == 0) continue;
                if (usedSlots == slots.Length) return null;
                int[] tokens = new int[RankTokens.Length];
                double[] priorities = new double[RankTokens.Length];
                int count = 0;
                for (int token = 0; token < RankTokens.Length; token++)
                {
                    int rank = RankTokens[token];
                    if ((domains[slot] & (1 << rank)) == 0) continue;
                    tokens[count] = token;
                    double weight = Math.Max(0.000001, weights[slot, rank] / ArmyBoard.Inventory[rank]);
                    priorities[count++] = -Math.Log(1 - random.NextDouble()) / weight;
                }
                Array.Sort(priorities, tokens, 0, count);
                Array.Resize(ref tokens, count);
                preferences[slot] = tokens;
                slots[usedSlots] = slot;
                slotPriority[usedSlots++] = count + random.NextDouble();
            }

            if (usedSlots != slots.Length) return null;
            Array.Sort(slotPriority, slots);

            // An augmenting path can move already assigned ranks to other legal slots.
            // Unlike greedy placement, this retains the exact inventory for every draw.
            var slotByToken = new int[RankTokens.Length];
            for (int token = 0; token < slotByToken.Length; token++) slotByToken[token] = -1;
            foreach (int slot in slots)
            {
                int visited = 0;
                if (!AssignRank(slot, preferences, slotByToken, ref visited)) return null;
            }

            var sampled = new int[domains.Length];
            for (int token = 0; token < RankTokens.Length; token++) sampled[slotByToken[token]] = RankTokens[token];
            return sampled;
        }

        private static bool AssignRank(int slot, int[][] preferences, int[] slotByToken, ref int visited)
        {
            foreach (int token in preferences[slot])
            {
                int bit = 1 << token;
                if ((visited & bit) != 0) continue;
                visited |= bit;
                if (slotByToken[token] >= 0 && !AssignRank(slotByToken[token], preferences, slotByToken, ref visited)) continue;
                slotByToken[token] = slot;
                return true;
            }
            return false;
        }

        private static int[] BuildRankTokens()
        {
            var pieces = new int[ArmyBoard.PiecesPerSide];
            int token = 0;
            for (int rank = 1; rank <= ArmyBoard.HighestKind; rank++)
                for (int count = 0; count < ArmyBoard.Inventory[rank]; count++) pieces[token++] = rank;
            return pieces;
        }
    }
}
