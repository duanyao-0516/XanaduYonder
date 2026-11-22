using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace XanaduYonder.Core.BoardGames
{
    public interface IClassicSearchPosition
    {
        int CurrentSide { get; }
        int Winner { get; }
        bool InCheck { get; }
        ulong SearchKey { get; }
        int Evaluate();
        IReadOnlyList<BoardMove> SearchMoves(bool tacticalOnly);
        int Priority(BoardMove move);
        void Push(BoardMove move);
        void Pop();
        int NoMoveScore { get; }
    }

    public static class BoardNegamax
    {
        public const int MateValue = 100000;
        private const int Infinity = 110000;

        public static BoardMove Find(IClassicSearchPosition position, BoardDifficulty difficulty, int seed, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (position.Winner != BoardResult.Ongoing) return BoardMove.None;
            var context = new Search(position, difficulty, cancellation);
            return context.Find(seed);
        }

        private sealed class Search
        {
            private enum Bound { Exact, Lower, Upper }
            private readonly struct Entry
            {
                public readonly BoardMove Move;
                public readonly int Depth, Score;
                public readonly Bound Bound;
                public Entry(BoardMove move, int depth, int score, Bound bound)
                { Move = move; Depth = depth; Score = score; Bound = bound; }
            }

            private readonly IClassicSearchPosition position;
            private readonly BoardDifficulty difficulty;
            private readonly CancellationToken cancellation;
            private readonly Stopwatch clock = Stopwatch.StartNew();
            private readonly Dictionary<ulong, Entry> table;
            private readonly int timeLimit, nodeLimit, depthLimit, quietDepth;
            private int nodes;
            private bool expired;

            public Search(IClassicSearchPosition position, BoardDifficulty difficulty, CancellationToken cancellation)
            {
                this.position = position;
                this.difficulty = difficulty;
                this.cancellation = cancellation;
                switch (difficulty)
                {
                    case BoardDifficulty.Easy: timeLimit = 110; nodeLimit = 7000; depthLimit = 2; quietDepth = 1; break;
                    case BoardDifficulty.Normal: timeLimit = 350; nodeLimit = 45000; depthLimit = 4; quietDepth = 3; break;
                    default: timeLimit = 850; nodeLimit = 165000; depthLimit = 8; quietDepth = 5; break;
                }
                if (difficulty != BoardDifficulty.Easy) table = new Dictionary<ulong, Entry>();
            }

            public BoardMove Find(int seed)
            {
                var root = Ordered(position.SearchMoves(false), BoardMove.None);
                if (root.Length == 0) return BoardMove.None;
                BoardMove best = root[0];
                var alternatives = new List<(BoardMove Move, int Score)>();
                bool easy = difficulty == BoardDifficulty.Easy;
                for (int depth = 1; depth <= depthLimit; depth++)
                {
                    if (ShouldStop()) break;
                    var iteration = new List<(BoardMove Move, int Score)>(root.Length);
                    int bestScore = -Infinity;
                    foreach (BoardMove move in Ordered(root, best))
                    {
                        position.Push(move);
                        int score;
                        try { score = -Evaluate(depth - 1, 1, -Infinity, easy ? Infinity : -bestScore); }
                        finally { position.Pop(); }
                        if (expired) break;
                        iteration.Add((move, score));
                        bestScore = Math.Max(bestScore, score);
                    }
                    if (expired || iteration.Count == 0) break;
                    alternatives = iteration.OrderByDescending(item => item.Score).ToList();
                    best = alternatives[0].Move;
                    if (bestScore >= MateValue - 32) return best;
                }
                cancellation.ThrowIfCancellationRequested();
                if (easy && alternatives.Count > 1)
                {
                    int threshold = alternatives[0].Score - 65;
                    var acceptable = alternatives.Take(4).Where(item => item.Score >= threshold).ToArray();
                    return acceptable[new Random(seed).Next(acceptable.Length)].Move;
                }
                return best;
            }

            private int Evaluate(int depth, int ply, int alpha, int beta)
            {
                if (ShouldStop()) return 0;
                int winner = position.Winner;
                if (winner != BoardResult.Ongoing)
                    return winner == BoardResult.Draw ? 0 : winner == position.CurrentSide ? MateValue - ply : -MateValue + ply;
                if (depth <= 0) return Quiescence(quietDepth, ply, alpha, beta);

                int initialAlpha = alpha;
                ulong key = position.SearchKey;
                BoardMove preferred = BoardMove.None;
                if (table != null && table.TryGetValue(key, out Entry cached))
                {
                    preferred = cached.Move;
                    if (cached.Depth >= depth)
                    {
                        int score = Normalize(cached.Score, -ply);
                        if (cached.Bound == Bound.Exact || (cached.Bound == Bound.Lower && score >= beta) ||
                            (cached.Bound == Bound.Upper && score <= alpha)) return score;
                    }
                }
                var moves = position.SearchMoves(false);
                if (moves.Count == 0) return Normalize(position.NoMoveScore, -ply);
                int best = -Infinity;
                BoardMove bestMove = BoardMove.None;
                foreach (BoardMove move in Ordered(moves, preferred))
                {
                    position.Push(move);
                    int score;
                    try { score = -Evaluate(depth - 1, ply + 1, -beta, -alpha); }
                    finally { position.Pop(); }
                    if (expired) return 0;
                    if (score > best) { best = score; bestMove = move; }
                    alpha = Math.Max(alpha, score);
                    if (alpha >= beta) break;
                }
                if (table != null && table.Count < 32768)
                {
                    Bound bound = best <= initialAlpha ? Bound.Upper : best >= beta ? Bound.Lower : Bound.Exact;
                    table[key] = new Entry(bestMove, depth, Normalize(best, ply), bound);
                }
                return best;
            }

            private int Quiescence(int depth, int ply, int alpha, int beta)
            {
                if (ShouldStop()) return 0;
                int winner = position.Winner;
                if (winner != BoardResult.Ongoing)
                    return winner == BoardResult.Draw ? 0 : winner == position.CurrentSide ? MateValue - ply : -MateValue + ply;
                bool check = position.InCheck;
                var moves = position.SearchMoves(!check);
                if (moves.Count == 0)
                {
                    if (check || position.SearchMoves(false).Count == 0) return Normalize(position.NoMoveScore, -ply);
                    return position.Evaluate();
                }
                if (depth <= -2) return position.Evaluate();
                int best = -Infinity;
                if (!check)
                {
                    best = position.Evaluate();
                    if (best >= beta || depth <= 0) return best;
                    alpha = Math.Max(alpha, best);
                }
                foreach (BoardMove move in Ordered(moves, BoardMove.None))
                {
                    position.Push(move);
                    int score;
                    try { score = -Quiescence(depth - 1, ply + 1, -beta, -alpha); }
                    finally { position.Pop(); }
                    if (expired) return 0;
                    best = Math.Max(best, score);
                    alpha = Math.Max(alpha, score);
                    if (alpha >= beta) break;
                }
                return best;
            }

            private BoardMove[] Ordered(IReadOnlyList<BoardMove> moves, BoardMove preferred) =>
                moves.OrderByDescending(move => move == preferred ? int.MaxValue : position.Priority(move)).ToArray();

            private bool ShouldStop()
            {
                cancellation.ThrowIfCancellationRequested();
                if (++nodes > nodeLimit || clock.ElapsedMilliseconds >= timeLimit) expired = true;
                return expired;
            }

            private static int Normalize(int score, int ply) =>
                Math.Abs(score) >= MateValue - 1000 ? score + Math.Sign(score) * ply : score;
        }
    }
}
