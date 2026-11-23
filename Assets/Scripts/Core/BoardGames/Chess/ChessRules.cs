using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.BoardGames
{
    public sealed partial class ChessGame
    {
        private static readonly (int row, int col)[] Directions =
        {
            (0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1)
        };
        private static readonly (int row, int col)[] KnightSteps =
        {
            (1, 2), (1, -2), (-1, 2), (-1, -2), (2, 1), (2, -1), (-2, 1), (-2, -1)
        };
        private static readonly int[] Promotions = { Queen, Rook, Bishop, Knight };

        private static int Encode(int side, int kind) => side == White ? kind : -kind;
        private static int SideOf(int piece) => piece > 0 ? White : Black;
        private static int PawnStep(int side) => side == White ? 8 : -8;
        private static int HomeKing(int side) => side == White ? 4 : 60;
        private static bool Inside(int row, int col) => (uint)row < 8 && (uint)col < 8;

        private bool InCheck(int side) => IsAttacked(kings[side], 1 - side);

        private bool IsAttacked(int square, int bySide)
        {
            int row = square / 8;
            int col = square % 8;
            int pawnRow = row - PawnStep(bySide) / 8;
            for (int offset = -1; offset <= 1; offset += 2)
                if (Inside(pawnRow, col + offset) && board[pawnRow * 8 + col + offset] == Encode(bySide, Pawn))
                    return true;

            foreach (var step in KnightSteps)
            {
                int nextRow = row + step.row;
                int nextCol = col + step.col;
                if (Inside(nextRow, nextCol) && board[nextRow * 8 + nextCol] == Encode(bySide, Knight))
                    return true;
            }
            for (int direction = 0; direction < Directions.Length; direction++)
            {
                var step = Directions[direction];
                int nextRow = row + step.row;
                int nextCol = col + step.col;
                int slide = Encode(bySide, direction < 4 ? Rook : Bishop);
                if (Inside(nextRow, nextCol) && board[nextRow * 8 + nextCol] == Encode(bySide, King))
                    return true;
                while (Inside(nextRow, nextCol))
                {
                    int piece = board[nextRow * 8 + nextCol];
                    if (piece == slide || piece == Encode(bySide, Queen)) return true;
                    if (piece != 0) break;
                    nextRow += step.row;
                    nextCol += step.col;
                }
            }
            return false;
        }

        private List<BoardMove> GenerateLegalMoves()
        {
            var moves = new List<BoardMove>(48);
            int side = CurrentSide;
            for (int square = 0; square < board.Length; square++)
            {
                int piece = board[square];
                if (piece == 0 || SideOf(piece) != side) continue;
                switch (Math.Abs(piece))
                {
                    case Pawn:
                        AddPawnMoves(moves, square, side);
                        break;
                    case Knight:
                        foreach (var step in KnightSteps) AddStep(moves, square, side, step);
                        break;
                    case Bishop:
                        AddSlides(moves, square, side, 4, 8);
                        break;
                    case Rook:
                        AddSlides(moves, square, side, 0, 4);
                        break;
                    case Queen:
                        AddSlides(moves, square, side, 0, 8);
                        break;
                    case King:
                        foreach (var step in Directions) AddStep(moves, square, side, step);
                        AddCastles(moves, square, side);
                        break;
                }
            }

            int count = 0;
            for (int i = 0; i < moves.Count; i++)
            {
                BoardMove move = moves[i];
                if (LeavesKingSafe(move)) moves[count++] = move;
            }
            moves.RemoveRange(count, moves.Count - count);
            return moves;
        }

        private void AddPawnMoves(List<BoardMove> moves, int from, int side)
        {
            int row = from / 8;
            int col = from % 8;
            int step = PawnStep(side);
            int next = from + step;
            if (board[next] == 0)
            {
                AddPawnMove(moves, from, next);
                int homeRow = side == White ? 1 : 6;
                if (row == homeRow && board[next + step] == 0)
                    moves.Add(new BoardMove(from, next + step));
            }
            for (int offset = -1; offset <= 1; offset += 2)
            {
                if ((uint)(col + offset) >= 8) continue;
                int target = next + offset;
                if (target == enPassant || IsEnemy(board[target], side))
                    AddPawnMove(moves, from, target);
            }
        }

        private static void AddPawnMove(List<BoardMove> moves, int from, int to)
        {
            if (to < 8 || to >= 56)
            {
                foreach (int kind in Promotions) moves.Add(new BoardMove(from, to, kind));
            }
            else moves.Add(new BoardMove(from, to));
        }

        private void AddStep(List<BoardMove> moves, int from, int side, (int row, int col) step)
        {
            int row = from / 8 + step.row;
            int col = from % 8 + step.col;
            if (!Inside(row, col)) return;
            int to = row * 8 + col;
            if (board[to] == 0 || IsEnemy(board[to], side))
                moves.Add(new BoardMove(from, to));
        }

        private void AddSlides(List<BoardMove> moves, int from, int side, int first, int last)
        {
            for (int direction = first; direction < last; direction++)
            {
                var step = Directions[direction];
                int row = from / 8 + step.row;
                int col = from % 8 + step.col;
                while (Inside(row, col))
                {
                    int to = row * 8 + col;
                    if (board[to] == 0 || IsEnemy(board[to], side))
                        moves.Add(new BoardMove(from, to));
                    if (board[to] != 0) break;
                    row += step.row;
                    col += step.col;
                }
            }
        }

        private static bool IsEnemy(int piece, int side)
        {
            return piece != 0 && SideOf(piece) != side && Math.Abs(piece) != King;
        }

        private void AddCastles(List<BoardMove> moves, int from, int side)
        {
            int shortRight = side == White ? WhiteShort : BlackShort;
            int longRight = side == White ? WhiteLong : BlackLong;
            if ((castleRights & (shortRight | longRight)) == 0 || InCheck(side)) return;
            if ((castleRights & shortRight) != 0 && board[from + 1] == 0 && board[from + 2] == 0
                && !IsAttacked(from + 1, 1 - side))
                moves.Add(new BoardMove(from, from + 2));
            if ((castleRights & longRight) != 0 && board[from - 1] == 0 && board[from - 2] == 0 && board[from - 3] == 0
                && !IsAttacked(from - 1, 1 - side))
                moves.Add(new BoardMove(from, from - 2));
        }

        private bool LeavesKingSafe(BoardMove move)
        {
            int side = CurrentSide;
            UndoState undo = Apply(move);
            bool safe = !InCheck(side);
            Revert(move, undo);
            return safe;
        }

        private int CapturedKind(BoardMove move)
        {
            if (move.To == enPassant && Math.Abs(board[move.From]) == Pawn) return Pawn;
            return Math.Abs(board[move.To]);
        }

        private UndoState Apply(BoardMove move)
        {
            int piece = board[move.From];
            int side = CurrentSide;
            int kind = Math.Abs(piece);
            int capture = move.To;
            if (kind == Pawn && move.To == enPassant) capture -= PawnStep(side);
            int captured = board[capture];
            var undo = new UndoState
            {
                Mover = piece, CaptureSquare = capture, Captured = captured,
                CastleRights = castleRights, EnPassant = enPassant, Halfmoves = halfmoves, PreviousKey = currentKey
            };
            board[move.From] = 0;
            board[capture] = 0;
            board[move.To] = move.Choice == 0 ? piece : Encode(side, move.Choice);

            if (kind == King)
            {
                kings[side] = move.To;
                castleRights &= ~(side == White ? WhiteShort | WhiteLong : BlackShort | BlackLong);
                if (Math.Abs(move.To - move.From) == 2)
                {
                    bool shortCastle = move.To > move.From;
                    int rookFrom = move.From + (shortCastle ? 3 : -4);
                    int rookTo = move.From + (shortCastle ? 1 : -1);
                    board[rookTo] = board[rookFrom];
                    board[rookFrom] = 0;
                    undo.RookFrom = rookFrom;
                    undo.RookTo = rookTo;
                    undo.Castled = true;
                }
            }
            if (kind == Rook) ClearRookRight(move.From);
            if (Math.Abs(captured) == Rook) ClearRookRight(capture);
            enPassant = kind == Pawn && Math.Abs(move.To - move.From) == 16 ? (move.From + move.To) / 2 : NoSquare;
            halfmoves = kind == Pawn || captured != 0 ? 0 : halfmoves + 1;
            CurrentSide = 1 - side;
            return undo;
        }

        private void Revert(BoardMove move, UndoState undo)
        {
            CurrentSide = 1 - CurrentSide;
            castleRights = undo.CastleRights;
            enPassant = undo.EnPassant;
            halfmoves = undo.Halfmoves;
            board[move.To] = 0;
            board[undo.CaptureSquare] = undo.Captured;
            board[move.From] = undo.Mover;
            if (Math.Abs(undo.Mover) == King) kings[CurrentSide] = move.From;
            if (undo.Castled)
            {
                board[undo.RookFrom] = board[undo.RookTo];
                board[undo.RookTo] = 0;
            }
        }

        private void ClearRookRight(int square)
        {
            switch (square)
            {
                case 0: castleRights &= ~WhiteLong; break;
                case 7: castleRights &= ~WhiteShort; break;
                case 56: castleRights &= ~BlackLong; break;
                case 63: castleRights &= ~BlackShort; break;
            }
        }

        private int EffectiveEnPassant()
        {
            if (enPassant == NoSquare) return NoSquare;
            int col = enPassant % 8;
            for (int offset = -1; offset <= 1; offset += 2)
            {
                if ((uint)(col + offset) >= 8) continue;
                int from = enPassant - PawnStep(CurrentSide) + offset;
                if (board[from] == Encode(CurrentSide, Pawn) && LeavesKingSafe(new BoardMove(from, enPassant)))
                    return enPassant;
            }
            return NoSquare;
        }

        private PositionKey Key()
        {
            ulong a = 0, b = 0, c = 0, d = 0;
            for (int i = 0; i < 16; i++)
            {
                int shift = i * 4;
                a |= (ulong)(board[i] + King) << shift;
                b |= (ulong)(board[16 + i] + King) << shift;
                c |= (ulong)(board[32 + i] + King) << shift;
                d |= (ulong)(board[48 + i] + King) << shift;
            }
            int flags = (EffectiveEnPassant() + 1) * 32 + castleRights * 2 + CurrentSide;
            return new PositionKey(a, b, c, d, flags);
        }

        private bool IsDeadPosition()
        {
            int knights = 0, bishops = 0, bishopColor = -1;
            bool differentBishopColors = false;
            bool hasPawns = false;
            for (int square = 0; square < board.Length; square++)
            {
                switch (Math.Abs(board[square]))
                {
                    case Pawn:
                        hasPawns = true;
                        break;
                    case Rook:
                    case Queen:
                        return false;
                    case Knight:
                        knights++;
                        break;
                    case Bishop:
                        bishops++;
                        int color = (square / 8 + square % 8) % 2;
                        if (bishopColor >= 0 && color != bishopColor) differentBishopColors = true;
                        bishopColor = color;
                        break;
                }
            }
            if (hasPawns) return knights == 0 && bishops == 0 && PawnsPermanentlyBlocked();
            if (knights > 0) return knights == 1 && bishops == 0;
            return !differentBishopColors;
        }

        private bool ForcedCapturesAreDead(List<BoardMove> moves)
        {
            foreach (BoardMove move in moves)
                if (CapturedKind(move) == 0) return false;

            foreach (BoardMove move in moves)
            {
                UndoState undo = Apply(move);
                bool dead = IsDeadPosition();
                Revert(move, undo);
                if (!dead) return false;
            }
            return true;
        }

        private bool PawnsPermanentlyBlocked()
        {
            if (EffectiveEnPassant() != NoSquare) return false;

            // If no pawn can move or be captured, its attack map stays fixed. Treating the
            // other king as absent gives each king more ways to reach an enemy pawn.
            ulong whiteAttacks = 0, blackAttacks = 0;
            for (int square = 0; square < board.Length; square++)
            {
                if (Math.Abs(board[square]) != Pawn) continue;
                int side = SideOf(board[square]);
                int front = square + PawnStep(side);
                if (Math.Abs(board[front]) != Pawn) return false;
                int col = square % 8;
                for (int offset = -1; offset <= 1; offset += 2)
                {
                    if ((uint)(col + offset) >= 8) continue;
                    int attack = front + offset;
                    if (board[attack] == Encode(1 - side, Pawn)) return false;
                    if (side == White) whiteAttacks |= 1UL << attack;
                    else blackAttacks |= 1UL << attack;
                }
            }
            return !KingCanReachEnemyPawn(White, blackAttacks) && !KingCanReachEnemyPawn(Black, whiteAttacks);
        }

        private bool KingCanReachEnemyPawn(int side, ulong hostileAttacks)
        {
            var queue = new int[64];
            queue[0] = kings[side];
            ulong seen = (1UL << kings[side]) | hostileAttacks;
            int count = 1;
            for (int i = 0; i < count; i++)
            {
                int square = queue[i];
                foreach (var step in Directions)
                {
                    int row = square / 8 + step.row;
                    int col = square % 8 + step.col;
                    if (!Inside(row, col)) continue;
                    int target = row * 8 + col;
                    ulong bit = 1UL << target;
                    if ((seen & bit) != 0) continue;
                    seen |= bit;

                    if (board[target] == Encode(1 - side, Pawn)) return true;
                    if (board[target] != Encode(side, Pawn)) queue[count++] = target;
                }
            }
            return false;
        }

        private struct UndoState
        {
            internal int Mover, CaptureSquare, Captured, CastleRights, EnPassant, Halfmoves, RookFrom, RookTo;
            internal bool Castled;
            internal PositionKey PreviousKey;
        }

        private readonly struct PositionKey : IEquatable<PositionKey>
        {
            private readonly ulong a, b, c, d;
            private readonly int flags;

            internal PositionKey(ulong a, ulong b, ulong c, ulong d, int flags)
            {
                this.a = a;
                this.b = b;
                this.c = c;
                this.d = d;
                this.flags = flags;
            }

            public bool Equals(PositionKey other) =>
                a == other.a && b == other.b && c == other.c && d == other.d && flags == other.flags;

            public override bool Equals(object other) => other is PositionKey position && Equals(position);
            public override int GetHashCode() => unchecked((int)Signature(0));

            internal ulong Signature(int count)
            {
                unchecked
                {
                    ulong hash = Mix(a + 0x9e3779b97f4a7c15UL);
                    hash = Mix(hash ^ b);
                    hash = Mix(hash ^ c);
                    hash = Mix(hash ^ d);
                    return Mix(hash ^ (uint)(flags + count * 4096));
                }
            }

            private static ulong Mix(ulong value)
            {
                unchecked
                {
                    value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
                    value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
                    return value ^ (value >> 31);
                }
            }
        }
    }
}
