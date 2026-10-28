using System;
using System.Diagnostics;
using System.Threading;

namespace XanaduYonder.Core.Gomoku
{
    /// <summary>Per-search limits; caller values are copied before searching. Milliseconds are targets, not device guarantees.</summary>
    public sealed class GomokuSearchOptions
    {
        public int TimeLimitMilliseconds { get; set; } = 450;
        public long MaxNodes { get; set; } = 60000;
        public int MaxDepth { get; set; } = 4;
        public int CandidateLimit { get; set; } = 12;
        public int TranspositionCapacity { get; set; } = 16384;
        public int VcfMaxDepth { get; set; } = 7;

        internal static GomokuSearchOptions Defaults(GomokuDifficulty difficulty)
        {
            if (difficulty == GomokuDifficulty.Easy) return new GomokuSearchOptions { TimeLimitMilliseconds = 100, MaxNodes = 12000, MaxDepth = 2, CandidateLimit = 8, TranspositionCapacity = 4096, VcfMaxDepth = 0 };
            if (difficulty == GomokuDifficulty.Hard) return new GomokuSearchOptions { TimeLimitMilliseconds = 1200, MaxNodes = 250000, MaxDepth = 8, CandidateLimit = 16, TranspositionCapacity = 32768 };
            return new GomokuSearchOptions();
        }

        internal GomokuSearchOptions Snapshot()
        {
            var value = (GomokuSearchOptions)MemberwiseClone();
            if (value.TimeLimitMilliseconds < 0 || value.MaxNodes < 0 || value.MaxDepth < 1 || value.MaxDepth > 12 ||
                value.CandidateLimit < 1 || value.CandidateLimit > 64 || value.TranspositionCapacity < 0 || value.TranspositionCapacity > 262144 ||
                value.VcfMaxDepth < 0 || value.VcfMaxDepth > 12)
                throw new ArgumentOutOfRangeException(nameof(GomokuSearchOptions), "Search limits are outside their supported bounds.");
            return value;
        }
    }

    public sealed class GomokuAiResult
    {
        public int Index { get; internal set; }
        /// <summary>Last fully completed negamax depth; zero includes tactical proofs and pre-search fallback.</summary>
        public int CompletedDepth { get; internal set; }
        public long Nodes { get; internal set; }
        public long ElapsedMilliseconds { get; internal set; }
    }

    /// <summary>Original offline bounded Gomoku search. Candidate pruning and finite VCF depth do not establish completeness.</summary>
    public static class GomokuAi
    {
        public static GomokuAiResult FindMove(GomokuGame game, GomokuDifficulty difficulty, CancellationToken cancellation, GomokuSearchOptions options = null)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (difficulty < GomokuDifficulty.Easy || difficulty > GomokuDifficulty.Hard) throw new ArgumentOutOfRangeException(nameof(difficulty));
            cancellation.ThrowIfCancellationRequested();
            var limits = (options ?? GomokuSearchOptions.Defaults(difficulty)).Snapshot();
            var search = new Search(game, limits, cancellation);
            return search.Run(difficulty);
        }

        private sealed class Search
        {
            private const int Size = 15, Count = 225, Mate = 100000000, Infinity = 110000000;
            private readonly int[] board = new int[Count];
            private readonly Candidate[][] buffers = new Candidate[32][];
            private readonly Entry[] table;
            private readonly GomokuSearchOptions limits;
            private readonly CancellationToken cancellation;
            private readonly Stopwatch clock = Stopwatch.StartNew();
            private readonly int rootStone;
            private readonly bool terminal;
            private int occupied;
            private ulong hash;
            private long nodes, vcfNodeEnd, vcfTimeEnd;
            private bool proving;
            private static readonly ulong[,] Keys = CreateKeys();
            private const ulong WhiteTurnKey = 0xD6E8FEB86659FD93UL;

            internal Search(GomokuGame game, GomokuSearchOptions limits, CancellationToken cancellation)
            {
                this.limits = limits; this.cancellation = cancellation;
                rootStone = (int)game.ToPlay; occupied = game.MoveCount; terminal = game.State != GomokuState.Playing;
                for (int i = 0; i < Count; i++) { board[i] = (int)game.GetCell(i); if (board[i] != 0) hash ^= Keys[i, board[i] - 1]; }
                for (int i = 0; i < buffers.Length; i++) buffers[i] = new Candidate[Count];
                table = new Entry[limits.TranspositionCapacity];
            }

            internal GomokuAiResult Run(GomokuDifficulty difficulty)
            {
                if (terminal) return Result(-1, 0);
                if (occupied == 0) return Result(112, 0);
                // Full-board tactical scan is deliberately outside optional search budgets: no branch cap can hide a win or block.
                int firstWin, firstBlock;
                if (WinningMoves(rootStone, out firstWin) > 0) return Result(firstWin, 0);
                int blocks = WinningMoves(3 - rootStone, out firstBlock);
                int fallback = blocks > 0 ? firstBlock : NearestEmpty();
                if (Expired()) return Result(fallback, 0);
                int count = Generate(rootStone, 0, -1, false);
                if (count > 0) fallback = buffers[0][0].Index;
                int completed = 0;
                if (difficulty == GomokuDifficulty.Hard && limits.VcfMaxDepth > 0 && blocks < 2)
                {
                    // VCF gets at most one quarter of the shared budget, leaving room for positional search.
                    vcfNodeEnd = Math.Min(limits.MaxNodes, Math.Max(1, limits.MaxNodes / 4));
                    vcfTimeEnd = limits.TimeLimitMilliseconds / 4;
                    proving = true;
                    try { int forced; if (ProveFour(rootStone, limits.VcfMaxDepth, 0, out forced)) return Result(forced, 0); }
                    catch (BudgetReached) { }
                    finally { proving = false; }
                }
                for (int depth = 1; depth <= limits.MaxDepth; depth++)
                {
                    try
                    {
                        CheckBudget();
                        int best, score = Root(depth, fallback, out best);
                        fallback = best; completed = depth;
                        if (score >= Mate - 64 || score <= -Mate + 64) break;
                    }
                    catch (BudgetReached) { break; }
                }
                return Result(fallback, completed);
            }

            private GomokuAiResult Result(int index, int depth)
            {
                cancellation.ThrowIfCancellationRequested();
                return new GomokuAiResult { Index = index, CompletedDepth = depth, Nodes = nodes, ElapsedMilliseconds = clock.ElapsedMilliseconds };
            }

            private int Root(int depth, int previous, out int best)
            {
                Visit();
                int count = Generate(rootStone, 0, previous, true), alpha = -Infinity;
                best = buffers[0][0].Index;
                for (int i = 0; i < count; i++)
                {
                    CheckBudget(); int index = buffers[0][i].Index; Put(index, rootStone); int score;
                    try { score = IsWin(index, rootStone) ? Mate - 1 : -Negamax(depth - 1, -Infinity, -alpha, 3 - rootStone, 1); }
                    finally { Remove(index, rootStone); }
                    if (score > alpha) { alpha = score; best = index; }
                }
                return alpha;
            }

            private int Negamax(int depth, int alpha, int beta, int stone, int ply)
            {
                Visit();
                if (occupied == Count) return 0;
                int first;
                if (WinningMoves(stone, out first) > 0) return Mate - ply - 1;
                int threats = WinningMoves(3 - stone, out first);
                if (threats > 1) return -Mate + ply + 2;
                if (depth == 0 && threats == 0) return Evaluate(stone);
                // A forced single defense extends the leaf, bounded by 24 plies and the shared time/node limits.
                if (ply >= 24) return Evaluate(stone);
                int originalAlpha = alpha, originalBeta = beta, preferred = -1;
                ulong key = hash ^ (stone == 2 ? WhiteTurnKey : 0);
                int slot = table.Length == 0 ? -1 : (int)(key % (ulong)table.Length);
                if (slot >= 0)
                {
                    Entry hit = table[slot];
                    if (hit.Used && hit.Key == key)
                    {
                        preferred = hit.Move;
                        if (hit.Depth >= depth)
                        {
                            int score = FromTable(hit.Score, ply);
                            if (hit.Bound == 0) return score;
                            if (hit.Bound == 1) alpha = Math.Max(alpha, score); else beta = Math.Min(beta, score);
                            if (alpha >= beta) return score;
                        }
                    }
                }
                int count = Generate(stone, ply, preferred, true), best = -Infinity, bestMove = -1;
                for (int i = 0; i < count; i++)
                {
                    int index = buffers[ply][i].Index; Put(index, stone); int score;
                    try { score = IsWin(index, stone) ? Mate - ply - 1 : -Negamax(Math.Max(0, depth - 1), -beta, -alpha, 3 - stone, ply + 1); }
                    finally { Remove(index, stone); }
                    if (score > best) { best = score; bestMove = index; }
                    alpha = Math.Max(alpha, score); if (alpha >= beta) break;
                }
                if (slot >= 0) table[slot] = new Entry { Used = true, Key = key, Depth = depth, Score = ToTable(best, ply), Move = bestMove, Bound = best <= originalAlpha ? 2 : best >= originalBeta ? 1 : 0 };
                return best;
            }

            /// <summary>Sound, bounded continuous-four proof: every defender reply is either forced or loses immediately.</summary>
            private bool ProveFour(int attacker, int remaining, int ply, out int move)
            {
                Visit(); move = -1;
                if (remaining == 0 || occupied == Count) return false;
                int direct;
                if (WinningMoves(attacker, out direct) > 0) { move = direct; return true; }
                int mandatory, opponentWins = WinningMoves(3 - attacker, out mandatory);
                if (opponentWins > 1) return false;
                int count = Generate(attacker, ply, -1, false);
                for (int i = 0; i < count; i++)
                {
                    CheckBudget(); int index = buffers[ply][i].Index;
                    if (opponentWins == 1 && index != mandatory) continue;
                    Put(index, attacker);
                    bool proven = false;
                    try
                    {
                        int counter, forced;
                        // The defender can always choose an immediate counter-win instead of answering a four.
                        if (WinningMoves(3 - attacker, out counter) > 0) continue;
                        int wins = WinningMoves(attacker, out forced);
                        if (wins >= 2) proven = true;
                        else if (wins == 1 && remaining > 1)
                        {
                            Put(forced, 3 - attacker);
                            try { int continuation; proven = ProveFour(attacker, remaining - 1, ply + 1, out continuation); }
                            finally { Remove(forced, 3 - attacker); }
                        }
                    }
                    finally { Remove(index, attacker); }
                    if (proven) { move = index; return true; }
                }
                return false;
            }

            private int Generate(int stone, int ply, int preferred, bool truncate)
            {
                Candidate[] list = buffers[ply]; int count = 0, first;
                int wins = WinningMoves(stone, out first), threats = wins == 0 ? WinningMoves(3 - stone, out first) : 0;
                for (int i = 0; i < Count; i++)
                {
                    if ((i & 31) == 0) { cancellation.ThrowIfCancellationRequested(); if (truncate) CheckBudget(); }
                    if (board[i] != 0) continue;
                    bool urgent = wins > 0 ? WouldWin(i, stone) : threats > 0 && WouldWin(i, 3 - stone);
                    if ((wins > 0 || threats > 0) && !urgent) continue;
                    if (!urgent && occupied > 0 && !NearStone(i)) continue;
                    int attack = Potential(i, stone), defense = Potential(i, 3 - stone);
                    int score = attack + defense * 11 / 10 + Center(i);
                    if (i == preferred) score += 12000000;
                    // Stable insertion order gives reproducible node-bounded searches, with no per-node allocations.
                    int j = count;
                    while (j > 0 && score > list[j - 1].Score) { list[j] = list[j - 1]; j--; }
                    list[j] = new Candidate { Index = i, Score = score }; count++;
                }
                // Urgent moves are never cut by the positional candidate cap.
                return truncate && wins == 0 && threats == 0 ? Math.Min(count, limits.CandidateLimit) : count;
            }

            private int WinningMoves(int stone, out int first)
            {
                int count = 0, bestCenter = -1; first = -1;
                for (int i = 0; i < Count; i++)
                {
                    if ((i & 31) == 0) cancellation.ThrowIfCancellationRequested();
                    if (board[i] != 0 || !WouldWin(i, stone)) continue;
                    count++; int central = Center(i);
                    if (central > bestCenter) { first = i; bestCenter = central; }
                }
                return count;
            }

            private bool WouldWin(int index, int stone)
            {
                int r = index / Size, c = index % Size;
                for (int d = 0; d < 4; d++)
                {
                    int dr = d == 0 ? 0 : 1, dc = d == 1 ? 0 : d == 3 ? -1 : 1;
                    if (Run(r, c, dr, dc, stone) + Run(r, c, -dr, -dc, stone) >= 4) return true;
                }
                return false;
            }
            private bool IsWin(int index, int stone) { return WouldWin(index, stone); }
            private int Run(int r, int c, int dr, int dc, int stone)
            {
                int n = 0; r += dr; c += dc;
                while (r >= 0 && r < Size && c >= 0 && c < Size && board[r * Size + c] == stone) { n++; r += dr; c += dc; }
                return n;
            }

            // Evaluate a proposed move by windows containing it. Distinct winning holes distinguish open from closed/jump fours.
            private int Potential(int index, int stone)
            {
                int score = 0, fours = 0, threes = 0;
                int r = index / Size, c = index % Size;
                for (int d = 0; d < 4; d++)
                {
                    int dr = d == 0 ? 0 : 1, dc = d == 1 ? 0 : d == 3 ? -1 : 1;
                    int holes = 0, best = 0;
                    for (int start = -4; start <= 0; start++)
                    {
                        int own = 0, hole = 0; bool blocked = false;
                        for (int k = start; k < start + 5; k++)
                        {
                            int cell = At(r + k * dr, c + k * dc);
                            if (k == 0) cell = stone;
                            if (cell < 0 || (cell != 0 && cell != stone)) { blocked = true; break; }
                            if (cell == stone) own++; else hole = k + 4;
                        }
                        if (blocked) continue;
                        if (own == 5) return 5000000;
                        if (own == 4) holes |= 1 << hole;
                        best = Math.Max(best, own == 3 ? 700 : own == 2 ? 70 : 4);
                    }
                    int holeCount = BitCount(holes);
                    if (holeCount >= 2) { score += 500000; fours += 2; continue; }
                    if (holeCount == 1) { score += 60000; fours++; continue; }
                    // Six-cell open windows also recognize jump threes: .XX.X. and .X.XX.
                    bool openThree = false;
                    for (int start = -4; start <= 0; start++)
                    {
                        if (At(r + start * dr, c + start * dc) != 0 || At(r + (start + 5) * dr, c + (start + 5) * dc) != 0) continue;
                        int own = 0; bool blocked = false;
                        for (int k = start + 1; k < start + 5; k++)
                        {
                            int cell = k == 0 ? stone : At(r + k * dr, c + k * dc);
                            if (cell < 0 || (cell != 0 && cell != stone)) { blocked = true; break; }
                            if (cell == stone) own++;
                        }
                        if (!blocked && own == 3) openThree = true;
                        if (!blocked && own == 2) best = Math.Max(best, 250);
                    }
                    if (openThree) { score += 6000; threes++; } else score += best;
                }
                if (fours >= 2) score += 700000;
                else if (fours == 1 && threes > 0) score += 70000;
                else if (threes >= 2) score += 20000;
                return score;
            }

            private int Evaluate(int stone)
            {
                int mine = 0, theirs = 0, mineBest = 0, theirsBest = 0;
                for (int i = 0; i < Count; i++)
                {
                    if ((i & 31) == 0) CheckBudget();
                    if (board[i] != 0 || !NearStone(i)) continue;
                    int attack = Potential(i, stone), defense = Potential(i, 3 - stone);
                    mine += attack; theirs += defense; mineBest = Math.Max(mineBest, attack); theirsBest = Math.Max(theirsBest, defense);
                }
                // Side to move has initiative, while multiple opponent threats carry a substantial defensive cost.
                return Math.Max(-Mate / 2, Math.Min(Mate / 2, mine / 8 + mineBest - theirs / 8 - theirsBest * 11 / 10));
            }

            private int At(int row, int col) { return row < 0 || row >= Size || col < 0 || col >= Size ? -1 : board[row * Size + col]; }
            private bool NearStone(int index)
            {
                int r = index / Size, c = index % Size;
                for (int y = Math.Max(0, r - 2); y <= Math.Min(14, r + 2); y++)
                    for (int x = Math.Max(0, c - 2); x <= Math.Min(14, c + 2); x++) if (board[y * Size + x] != 0) return true;
                return false;
            }
            private int NearestEmpty()
            {
                int best = -1, score = -1;
                for (int i = 0; i < Count; i++) if (board[i] == 0 && Center(i) > score) { best = i; score = Center(i); }
                return best;
            }
            private static int Center(int index) { return 28 - Math.Abs(index / Size - 7) - Math.Abs(index % Size - 7); }
            private static int BitCount(int value) { int n = 0; while (value != 0) { value &= value - 1; n++; } return n; }
            private void Put(int index, int stone) { board[index] = stone; occupied++; hash ^= Keys[index, stone - 1]; }
            private void Remove(int index, int stone) { board[index] = 0; occupied--; hash ^= Keys[index, stone - 1]; }
            private void Visit() { CheckBudget(); nodes++; }
            private bool Expired() { return nodes >= limits.MaxNodes || clock.ElapsedMilliseconds >= limits.TimeLimitMilliseconds; }
            private void CheckBudget()
            {
                cancellation.ThrowIfCancellationRequested();
                if (Expired() || (proving && (nodes >= vcfNodeEnd || clock.ElapsedMilliseconds >= vcfTimeEnd))) throw new BudgetReached();
            }
            private static int ToTable(int score, int ply) { return score > Mate - 64 ? score + ply : score < -Mate + 64 ? score - ply : score; }
            private static int FromTable(int score, int ply) { return score > Mate - 64 ? score - ply : score < -Mate + 64 ? score + ply : score; }
            private static ulong[,] CreateKeys()
            {
                var result = new ulong[Count, 2]; ulong state = 0xA0761D6478BD642FUL;
                for (int i = 0; i < Count; i++) for (int s = 0; s < 2; s++)
                {
                    state ^= state >> 12; state ^= state << 25; state ^= state >> 27; result[i, s] = unchecked(state * 2685821657736338717UL);
                }
                return result;
            }
            private struct Candidate { internal int Index, Score; }
            private struct Entry { internal bool Used; internal ulong Key; internal int Depth, Score, Move, Bound; }
            private sealed class BudgetReached : Exception { }
        }
    }
}
