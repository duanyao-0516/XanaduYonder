using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace XanaduYonder.Core.BoardGames
{
    public sealed class ChineseCheckersGame : IBoardPosition
    {
        private const int Size = 121;
        private const int Swap = 1;
        private static readonly int[] RowWidths = { 1, 2, 3, 4, 13, 12, 11, 10, 9, 10, 11, 12, 13, 4, 3, 2, 1 };
        private static readonly int[] DeltaRow = { 0, 1, 1, 0, -1, -1 };
        private static readonly int[] DeltaX = { 2, 1, -1, -2, -1, 1 };
        private static readonly Hole[] Holes = BuildHoles();
        private static readonly int[,] Neighbors = BuildNeighbors(1);
        private static readonly int[,] Jumps = BuildNeighbors(2);
        private static readonly int[][] Camps = Enumerable.Range(0, 6)
            .Select(camp => Enumerable.Range(0, Size).Where(point => Holes[point].Camp == camp).ToArray()).ToArray();
        private static readonly double[,] DistanceToGoalTip = BuildDistances();

        private readonly int[] seatCamps;
        private readonly int[] occupancy;
        private int passes;
        public int CurrentSide { get; private set; }
        public int Winner { get; private set; } = BoardResult.Ongoing;
        public int PointCount => Size;
        public int Seats => seatCamps.Length;
        public string Status => Winner == BoardResult.Draw ? "各席均无合法走法，和棋" :
            Winner >= 0 ? "已占满目标营" : passes > 0 ? "没有落点的席位已跳过" : "";

        public ChineseCheckersGame(int seats)
        {
            switch (seats)
            {
                case 2: seatCamps = new[] { 0, 3 }; break;
                case 3: seatCamps = new[] { 0, 2, 4 }; break;
                case 4: seatCamps = new[] { 0, 1, 3, 4 }; break;
                case 6: seatCamps = new[] { 0, 1, 2, 3, 4, 5 }; break;
                default: throw new ArgumentOutOfRangeException(nameof(seats));
            }
            occupancy = new int[Size];
            for (int point = 0; point < Size; point++) occupancy[point] = Array.IndexOf(seatCamps, Holes[point].Camp);
        }

        private ChineseCheckersGame(ChineseCheckersGame other)
        {
            seatCamps = other.seatCamps;
            occupancy = (int[])other.occupancy.Clone();
            CurrentSide = other.CurrentSide;
            Winner = other.Winner;
            passes = other.passes;
        }

        public static ChineseCheckersGame CreateTutorial()
        {
            var game = new ChineseCheckersGame(6);
            game.Relocate(PointAt(14, 0), PointAt(12, 0));
            game.Relocate(PointAt(12, -6), PointAt(10, 2));
            game.Relocate(PointAt(4, -6), PointAt(8, 2));
            return game;
        }

        public static ChineseCheckersGame CreateTutorialCampCrossing()
        {
            var game = new ChineseCheckersGame(6);
            game.Relocate(PointAt(13, -3), PointAt(4, -4));
            game.Relocate(PointAt(4, -8), PointAt(8, 0));
            return game;
        }

        public static ChineseCheckersGame CreateTutorialGoal(bool blockedEntry, bool leaveAnotherHoleEmpty = false)
        {
            var game = new ChineseCheckersGame(2);
            var home = Camps[game.SeatCamp(0)];
            var goal = Camps[game.GoalCamp(0)];
            int entrance = PointAt(3, -3);
            for (int i = 0; i < goal.Length; i++)
            {
                bool entry = goal[i] == entrance;
                game.occupancy[goal[i]] = entry ? (blockedEntry ? 1 : -1) : 0;
                game.occupancy[home[i]] = entry && blockedEntry ? -1 : 1;
            }
            game.occupancy[PointAt(4, -4)] = 0;
            if (leaveAnotherHoleEmpty) game.Relocate(PointAt(0, 0), PointAt(8, 0));
            return game;
        }

        private void Relocate(int from, int to) { occupancy[to] = occupancy[from]; occupancy[from] = -1; }
        public int SeatCamp(int seat) => seatCamps[seat];
        public int GoalCamp(int seat) => (seatCamps[seat] + 3) % 6;
        public static int RowAt(int point) => Holes[point].Row;
        public static int DoubledXAt(int point) => Holes[point].X;
        public static int CampAt(int point) => Holes[point].Camp;

        public static int PointAt(int row, int doubledX)
        {
            if (row < 0 || row >= RowWidths.Length) return -1;
            int column = doubledX + RowWidths[row] - 1;
            if (column < 0 || column >= 2 * RowWidths[row] || (column & 1) != 0) return -1;
            int point = column / 2;
            for (int i = 0; i < row; i++) point += RowWidths[i];
            return point;
        }

        public BoardPiece PieceAt(int point, int viewer) =>
            IsPoint(point) && occupancy[point] >= 0 ? new BoardPiece(occupancy[point], 1) : default;

        public IBoardPosition Copy() => new ChineseCheckersGame(this);

        public IReadOnlyList<BoardMove> LegalMoves()
        {
            if (Winner != BoardResult.Ongoing) return Array.Empty<BoardMove>();
            var moves = FindMoves(CurrentSide);
            if (moves.Count == 0) moves.Add(BoardMove.Pass);
            return moves;
        }

        public IReadOnlyList<int> Continuations(int origin, IReadOnlyList<int> path)
        {
            if (Winner != BoardResult.Ongoing || !IsPoint(origin) || occupancy[origin] != CurrentSide) return Array.Empty<int>();
            int current = origin;
            var seen = new HashSet<int> { origin };
            foreach (int destination in path)
            {
                if (!seen.Add(destination) || !CanHop(origin, current, destination, CurrentSide, false)) return Array.Empty<int>();
                current = destination;
            }
            var result = new List<int>(6);
            for (int dir = 0; dir < 6; dir++)
            {
                int destination = Jumps[current, dir];
                if (destination >= 0 && !seen.Contains(destination) &&
                    CanHop(origin, current, destination, CurrentSide, CanSwap(destination, CurrentSide)))
                    result.Add(destination);
            }
            return result;
        }

        public bool CanFinish(int origin, IReadOnlyList<int> path)
        {
            if (path.Count == 0) return false;
            int end = path[path.Count - 1];
            return IsLegal(new BoardMove(origin, end, CanSwap(end, CurrentSide) ? Swap : 0, path.ToArray()));
        }

        public BoardMove CompleteTurn(int origin, IReadOnlyList<int> path)
        {
            if (!CanFinish(origin, path)) return BoardMove.None;
            int end = path[path.Count - 1];
            return new BoardMove(origin, end, CanSwap(end, CurrentSide) ? Swap : 0, path.ToArray());
        }

        public bool TryPlay(BoardMove move)
        {
            if (Winner != BoardResult.Ongoing) return false;
            if (move == BoardMove.Pass)
            {
                if (FindMoves(CurrentSide).Count != 0) return false;
                if (++passes >= Seats) Winner = BoardResult.Draw;
                else CurrentSide = (CurrentSide + 1) % Seats;
                return true;
            }
            if (!IsLegal(move)) return false;
            ApplyMove(move);
            return true;
        }

        private void ApplyMove(BoardMove move)
        {
            int previous = occupancy[move.To];
            occupancy[move.To] = CurrentSide;
            occupancy[move.From] = move.Choice == Swap ? previous : -1;
            passes = 0;
            if (Camps[GoalCamp(CurrentSide)].All(point => occupancy[point] == CurrentSide)) Winner = CurrentSide;
            else CurrentSide = (CurrentSide + 1) % Seats;
        }

        private bool IsLegal(BoardMove move)
        {
            if (Winner != BoardResult.Ongoing || !IsPoint(move.From) || !IsPoint(move.To) || move.From == move.To ||
                occupancy[move.From] != CurrentSide || !CanStop(move.To, CurrentSide) ||
                (move.Choice != 0 && move.Choice != Swap)) return false;
            bool swap = move.Choice == Swap;
            if (swap != CanSwap(move.To, CurrentSide) || (!swap && occupancy[move.To] >= 0)) return false;
            int length = Math.Max(move.PathLength, 1);
            if (move.PathLength > 0 && move.PathAt(length - 1) != move.To) return false;
            if (length == 1)
                for (int d = 0; d < 6; d++)
                    if (Neighbors[move.From, d] == move.To && CanStayInGoal(move.From, move.To, CurrentSide)) return true;

            if (length >= Size) return false;
            var visited = new HashSet<int> { move.From };
            int current = move.From;
            for (int i = 0; i < length; i++)
            {
                int next = move.PathLength == 0 ? move.To : move.PathAt(i);
                if (!visited.Add(next) || !CanHop(move.From, current, next, CurrentSide, swap && i + 1 == length)) return false;
                current = next;
            }
            return true;
        }

        private List<BoardMove> FindMoves(int seat)
        {
            var moves = new List<BoardMove>();
            var parents = new int[Size];
            var queue = new int[Size];
            for (int from = 0; from < Size; from++)
            {
                if (occupancy[from] != seat) continue;
                for (int i = 0; i < Size; i++) parents[i] = -2;
                for (int d = 0; d < 6; d++)
                {
                    int to = Neighbors[from, d];
                    if (!IsPoint(to) || !CanStayInGoal(from, to, seat) || !CanStop(to, seat)) continue;
                    if (occupancy[to] < 0) moves.Add(new BoardMove(from, to));
                    else if (CanSwap(to, seat)) moves.Add(new BoardMove(from, to, Swap));
                }
                int head = 0, tail = 1;
                queue[0] = from;
                parents[from] = -1;
                while (head < tail)
                {
                    int current = queue[head++];
                    for (int d = 0; d < 6; d++)
                    {
                        int to = Jumps[current, d], bridge = Neighbors[current, d];
                        if (!IsPoint(to) || parents[to] != -2 || occupancy[bridge] < 0 || bridge == from ||
                            !CanStayInGoal(current, to, seat)) continue;
                        bool swap = occupancy[to] >= 0;
                        if (swap && !CanSwap(to, seat)) continue;
                        parents[to] = current;
                        if (!swap) queue[tail++] = to;
                        if (!CanStop(to, seat)) continue;
                        var route = new List<int>();
                        for (int hop = to; hop != from; hop = parents[hop]) route.Add(hop);
                        route.Reverse();
                        moves.Add(new BoardMove(from, to, swap ? Swap : 0, route.ToArray()));
                    }
                }
            }
            return moves;
        }

        private bool CanHop(int origin, int current, int destination, int seat, bool swap)
        {
            if (!IsPoint(destination) || (occupancy[destination] >= 0 && !swap) || !CanStayInGoal(current, destination, seat)) return false;
            for (int d = 0; d < 6; d++)
            {
                int bridge = Neighbors[current, d];
                if (Jumps[current, d] == destination && bridge != origin && occupancy[bridge] >= 0) return true;
            }
            return false;
        }

        private bool CanStayInGoal(int from, int to, int seat) => Holes[from].Camp != GoalCamp(seat) || Holes[to].Camp == GoalCamp(seat);
        private bool CanStop(int point, int seat) => Holes[point].Camp < 0 || Holes[point].Camp == seatCamps[seat] || Holes[point].Camp == GoalCamp(seat);

        // App profile: in a full target camp, a legal final landing may exchange a blocking original resident.
        private bool CanSwap(int point, int seat)
        {
            if (!IsPoint(point) || Holes[point].Camp != GoalCamp(seat) || occupancy[point] < 0 ||
                occupancy[point] == seat || seatCamps[occupancy[point]] != Holes[point].Camp) return false;
            bool hasOwn = false;
            foreach (int hole in Camps[GoalCamp(seat)])
            {
                int owner = occupancy[hole];
                if (owner < 0 || (owner != seat && seatCamps[owner] != Holes[hole].Camp)) return false;
                if (owner == seat) hasOwn = true;
            }
            return hasOwn;
        }

        public BoardSearch CaptureSearch() => CaptureSearch(false);

        public BoardSearch CaptureSingleTapHintSearch() => CaptureSearch(true);

        private BoardSearch CaptureSearch(bool singleTapHint)
        {
            var snapshot = new ChineseCheckersGame(this);
            return (difficulty, seed, cancellation) => snapshot.FindAiMove(difficulty, seed, cancellation, singleTapHint);
        }

        private BoardMove FindAiMove(BoardDifficulty difficulty, int seed, CancellationToken cancellation, bool singleTapHint)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Winner != BoardResult.Ongoing) return BoardMove.None;
            int seat = CurrentSide;
            var moves = FindMoves(seat);
            if (singleTapHint) moves.RemoveAll(move => StopsBeforeEnd(move, seat));
            if (moves.Count == 0) return BoardMove.Pass;
            var clock = Stopwatch.StartNew();
            var random = new Random(seed);
            var ordered = moves.Select(move => new
            {
                Move = move,
                Score = ScoreMove(move, seat, difficulty != BoardDifficulty.Easy) + random.NextDouble() * .08,
            }).OrderByDescending(entry => entry.Score).ToArray();
            cancellation.ThrowIfCancellationRequested();
            if (difficulty != BoardDifficulty.Hard) return ordered[0].Move;

            BoardMove best = ordered[0].Move;
            double bestScore = double.NegativeInfinity;
            foreach (var candidate in ordered.Take(6))
            {
                var projected = new ChineseCheckersGame(this);
                projected.ApplyMove(candidate.Move);
                if (projected.Winner == seat) return candidate.Move;
                double threat = 0;
                while (projected.Winner == BoardResult.Ongoing && projected.CurrentSide != seat)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (clock.ElapsedMilliseconds >= 210) return best;
                    int opponent = projected.CurrentSide;
                    var replies = projected.FindMoves(opponent);
                    if (replies.Count == 0) { projected.TryPlay(BoardMove.Pass); continue; }
                    BoardMove reply = replies[0];
                    double replyScore = double.NegativeInfinity;
                    foreach (var next in replies)
                    {
                        double value = projected.ScoreMove(next, opponent, true);
                        if (value > replyScore) { reply = next; replyScore = value; }
                    }
                    projected.ApplyMove(reply);
                    threat = Math.Max(threat, Math.Max(replyScore, 0));
                }
                if (projected.Winner >= 0) continue;
                double nextScore = 0;
                foreach (var next in projected.FindMoves(seat)) nextScore = Math.Max(nextScore, projected.ScoreMove(next, seat, true));
                double score = candidate.Score + .45 * nextScore - .25 * threat;
                if (score > bestScore) { best = candidate.Move; bestScore = score; }
            }
            return best;
        }

        private bool StopsBeforeEnd(BoardMove move, int seat)
        {
            // All intermediate landings in a legal route are empty. Single-tap play commits at the first permitted stop.
            for (int hop = 0; hop + 1 < move.PathLength; hop++)
                if (CanStop(move.PathAt(hop), seat)) return true;
            return false;
        }

        private double ScoreMove(BoardMove move, int seat, bool team)
        {
            int goal = GoalCamp(seat);
            double progress = DistanceToGoalTip[goal, move.From] - DistanceToGoalTip[goal, move.To];
            if (Holes[move.To].Camp == goal && Holes[move.From].Camp != goal &&
                Camps[goal].Count(point => occupancy[point] == seat) == 9) return 10000;
            if (!team) return progress - .24 * Math.Max(0, move.PathLength - 1);
            if (Holes[move.To].Camp == goal && Holes[move.From].Camp != goal) progress += 1.45;
            progress += .07 * DistanceToGoalTip[goal, move.From];
            int onward = 0;
            for (int d = 0; d < 6; d++)
            {
                int end = Jumps[move.To, d], bridge = Neighbors[move.To, d];
                if (end >= 0 && occupancy[end] < 0 && occupancy[bridge] >= 0 && bridge != move.From &&
                    DistanceToGoalTip[goal, end] < DistanceToGoalTip[goal, move.To]) onward++;
            }
            return progress + .32 * onward;
        }

        private static bool IsPoint(int point) => (uint)point < Size;

        private static Hole[] BuildHoles()
        {
            var holes = new List<Hole>(Size);
            for (int row = 0; row < RowWidths.Length; row++)
            for (int x = 1 - RowWidths[row]; x < RowWidths[row]; x += 2)
            {
                int camp = -1;
                if (row >= 13) camp = 0;
                else if (row <= 3) camp = 3;
                else if (row <= 7 && x < -(row + 1)) camp = 2;
                else if (row <= 7 && x > row + 1) camp = 4;
                else if (row >= 9 && x < row - 17) camp = 1;
                else if (row >= 9 && x > 17 - row) camp = 5;
                holes.Add(new Hole(row, x, camp));
            }
            return holes.ToArray();
        }

        private static int[,] BuildNeighbors(int steps)
        {
            var results = new int[Size, 6];
            for (int point = 0; point < Size; point++)
            for (int direction = 0; direction < 6; direction++)
                results[point, direction] = PointAt(Holes[point].Row + steps * DeltaRow[direction], Holes[point].X + steps * DeltaX[direction]);
            return results;
        }

        private static double[,] BuildDistances()
        {
            var distances = new double[6, Size];
            for (int camp = 0; camp < 6; camp++)
            {
                // Filling the deepest holes first leaves the camp entrance free for incoming pieces.
                int tip = Camps[camp].OrderByDescending(point => SquaredDistance(Holes[point].Row - 8, Holes[point].X)).First();
                for (int point = 0; point < Size; point++)
                {
                    int rowOffset = Holes[point].Row - Holes[tip].Row;
                    int xOffset = Holes[point].X - Holes[tip].X;
                    distances[camp, point] = .5 * Math.Sqrt(SquaredDistance(rowOffset, xOffset));
                }
            }
            return distances;
        }

        private static int SquaredDistance(int rowOffset, int xOffset) => 3 * rowOffset * rowOffset + xOffset * xOffset;

        private readonly struct Hole
        {
            public readonly int Row, X, Camp;
            public Hole(int row, int x, int camp) { Row = row; X = x; Camp = camp; }
        }
    }
}
