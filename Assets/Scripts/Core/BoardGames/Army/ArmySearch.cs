using System;
using System.Collections.Generic;
using System.Threading;

namespace XanaduYonder.Core.BoardGames
{
    internal sealed class ArmySearch
    {
        private const int SampleCount = 16;
        private const int CandidateCount = 10;
        private const int ReplyCount = 2;
        private const double WinScore = 70;
        private static readonly double[] Material = { 0, 3.5, 1.4, 2, 2.9, 3.6, 4.5, 5.6, 7.2, 9.8, 5.4, 3.1, 0 };

        private readonly int side;
        private readonly int[] visibleRanks;
        private readonly int[] sides;
        private readonly int[] slots;
        private readonly int[] enemyDomains;
        private readonly BoardMove previousMove;

        internal ArmySearch(int side, int[] visibleRanks, int[] sides, int[] slots, int[] enemyDomains, BoardMove previousMove)
        {
            this.side = side;
            this.visibleRanks = visibleRanks;
            this.sides = sides;
            this.slots = slots;
            this.enemyDomains = enemyDomains;
            this.previousMove = previousMove;
        }

        internal static BoardMove NoMove(BoardDifficulty difficulty, int seed, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            return BoardMove.None;
        }

        internal BoardMove Choose(BoardDifficulty difficulty, int seed, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var legal = ArmyBoard.LegalMoves(visibleRanks, sides, side);
            if (legal.Count == 0) return BoardMove.None;

            var random = new Random(seed);
            var probabilities = difficulty == BoardDifficulty.Easy ? null : ArmyBeliefs.Estimate(enemyDomains);
            double noise = difficulty == BoardDifficulty.Easy ? 1.15 : 0.07;
            var candidates = new List<ScoredMove>(legal.Count);
            foreach (var move in legal)
            {
                double score = ExpectedScore(move, probabilities) + RepeatPenalty(move);
                candidates.Add(new ScoredMove(move, score + noise * random.NextDouble()));
            }
            candidates.Sort(CompareScores);
            if (difficulty != BoardDifficulty.Hard) return candidates[0].Move;
            return ChooseWithSamples(candidates, probabilities, random, cancellation);
        }

        private BoardMove ChooseWithSamples(List<ScoredMove> rated, double[,] probabilities, Random random,
            CancellationToken cancellation)
        {
            int count = Math.Min(CandidateCount, rated.Count);
            var totals = new double[count];
            int completed = 0;
            var sampleSides = (int[])sides.Clone();
            var sampleRanks = (int[])visibleRanks.Clone();
            for (int sample = 0; sample < SampleCount; sample++)
            {
                cancellation.ThrowIfCancellationRequested();
                var layout = ArmyBeliefs.Sample(enemyDomains, probabilities, random);
                if (layout == null) break;

                completed++;
                for (int point = 0; point < sampleRanks.Length; point++)
                    if (sampleSides[point] == 1 - side) sampleRanks[point] = layout[slots[point]];

                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    totals[i] += LookAhead(rated[i].Move, sampleRanks, sampleSides) + RepeatPenalty(rated[i].Move);
                }
            }

            int best = 0;
            if (completed == 0) return rated[best].Move;

            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                double score = 0.84 * totals[i] / completed + 0.16 * rated[i].Score;
                if (score <= bestScore) continue;
                best = i;
                bestScore = score;
            }
            return rated[best].Move;
        }

        private double LookAhead(BoardMove move, int[] kinds, int[] owners)
        {
            var played = new SimulatedMove(move, kinds, owners);
            if (played.DefenderKind == (int)ArmyPieceKind.Flag) return WinScore;
            double immediate = ActualScore(move, kinds, side);
            played.Apply(kinds, owners);
            var replies = ArmyBoard.LegalMoves(kinds, owners, 1 - side);
            if (replies.Count == 0)
            {
                played.Restore(kinds, owners);
                return WinScore;
            }

            var scoredReplies = new List<ScoredMove>(replies.Count);
            foreach (var reply in replies) scoredReplies.Add(new ScoredMove(reply, ActualScore(reply, kinds, 1 - side)));
            scoredReplies.Sort(CompareScores);
            double worst = double.PositiveInfinity;
            for (int i = 0; i < Math.Min(ReplyCount, scoredReplies.Count); i++)
            {
                var reply = scoredReplies[i];
                var response = new SimulatedMove(reply.Move, kinds, owners);
                if (response.DefenderKind == (int)ArmyPieceKind.Flag)
                {
                    worst = -WinScore;
                    break;
                }

                response.Apply(kinds, owners);
                var next = ArmyBoard.LegalMoves(kinds, owners, side);
                double value = -WinScore;
                if (next.Count > 0)
                {
                    double bestFollowUp = double.NegativeInfinity;
                    foreach (var follow in next) bestFollowUp = Math.Max(bestFollowUp, ActualScore(follow, kinds, side));
                    value = immediate - 0.88 * reply.Score + 0.54 * bestFollowUp;
                }
                worst = Math.Min(worst, value);
                response.Restore(kinds, owners);
            }
            played.Restore(kinds, owners);
            return worst;
        }

        private double ExpectedScore(BoardMove move, double[,] probabilities)
        {
            int attacker = visibleRanks[move.From];
            int defender = visibleRanks[move.To];
            if (defender >= 0) return MoveScore(move, attacker, defender, side);

            int slot = slots[move.To];
            double expected = 0;
            if (probabilities != null)
            {
                for (int rank = 1; rank <= ArmyBoard.HighestKind; rank++)
                    expected += probabilities[slot, rank] * MoveScore(move, attacker, rank, side);
                return expected;
            }

            int domain = ArmyBoard.FormationDomain(slot);
            if (move.To != ArmyGame.FormationPoint(1 - side, slot))
                domain &= ArmyBoard.MovableKinds;
            double total = 0;
            for (int rank = 1; rank <= ArmyBoard.HighestKind; rank++)
            {
                if ((domain & (1 << rank)) == 0) continue;
                int weight = ArmyBoard.Inventory[rank];
                expected += weight * MoveScore(move, attacker, rank, side);
                total += weight;
            }
            return total == 0 ? 0 : expected / total;
        }

        private double RepeatPenalty(BoardMove move)
        {
            return move.From == previousMove.To && move.To == previousMove.From && sides[move.To] < 0 ? -0.9 : 0;
        }

        private static double ActualScore(BoardMove move, int[] kinds, int movingSide)
        {
            return MoveScore(move, kinds[move.From], kinds[move.To], movingSide);
        }

        private static double MoveScore(BoardMove move, int attacker, int defender, int movingSide)
        {
            if (defender == (int)ArmyPieceKind.Flag) return WinScore;
            var outcome = ArmyGame.CombatOutcome((ArmyPieceKind)attacker, (ArmyPieceKind)defender);
            double position = PositionDelta(move, attacker, movingSide);
            switch (outcome)
            {
                case ArmyCombatOutcome.AttackerSurvives: return Material[defender] + position + 0.55;
                case ArmyCombatOutcome.DefenderSurvives: return -Material[attacker] - 0.3;
                case ArmyCombatOutcome.BothRemoved: return Material[defender] - Material[attacker] + 0.16;
                default: return position;
            }
        }

        private static double PositionDelta(BoardMove move, int kind, int movingSide)
        {
            int advance = move.To / ArmyBoard.Columns - move.From / ArmyBoard.Columns;
            double score = (movingSide == 0 ? -advance : advance) * 0.18;
            if (ArmyBoard.IsCamp(move.To)) score += 0.37;
            if (ArmyBoard.IsCamp(move.From)) score -= 0.13;
            if (ArmyBoard.IsHeadquarters(move.To)) score -= 0.6 * Material[kind];
            return score;
        }

        private static int CompareScores(ScoredMove a, ScoredMove b)
        {
            int byScore = b.Score.CompareTo(a.Score);
            if (byScore != 0) return byScore;
            int bySource = a.Move.From.CompareTo(b.Move.From);
            return bySource != 0 ? bySource : a.Move.To.CompareTo(b.Move.To);
        }

        private readonly struct ScoredMove
        {
            internal readonly BoardMove Move;
            internal readonly double Score;

            internal ScoredMove(BoardMove move, double score)
            {
                Move = move;
                Score = score;
            }
        }

        private readonly struct SimulatedMove
        {
            private readonly int from;
            private readonly int to;
            private readonly int attackingSide;
            private readonly int defendingSide;
            private readonly int attackerKind;
            internal readonly int DefenderKind;

            internal SimulatedMove(BoardMove move, int[] kinds, int[] owners)
            {
                from = move.From;
                to = move.To;
                attackingSide = owners[from];
                defendingSide = owners[to];
                attackerKind = kinds[from];
                DefenderKind = kinds[to];
            }

            internal void Apply(int[] kinds, int[] owners)
            {
                kinds[from] = 0;
                owners[from] = -1;
                var outcome = ArmyGame.CombatOutcome((ArmyPieceKind)attackerKind, (ArmyPieceKind)DefenderKind);
                if (outcome == ArmyCombatOutcome.None || outcome == ArmyCombatOutcome.AttackerSurvives)
                {
                    kinds[to] = attackerKind;
                    owners[to] = attackingSide;
                }
                else if (outcome == ArmyCombatOutcome.BothRemoved)
                {
                    kinds[to] = 0;
                    owners[to] = -1;
                }
            }

            internal void Restore(int[] kinds, int[] owners)
            {
                kinds[from] = attackerKind;
                kinds[to] = DefenderKind;
                owners[from] = attackingSide;
                owners[to] = defendingSide;
            }
        }
    }
}
