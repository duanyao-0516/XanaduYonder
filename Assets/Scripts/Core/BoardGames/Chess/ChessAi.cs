using System;
using System.Collections.Generic;
using System.Linq;

namespace XanaduYonder.Core.BoardGames
{
    public sealed partial class ChessGame
    {
        private static readonly int[] MaterialValue = { 0, 100, 320, 335, 500, 930, 0 };
        private static readonly int[] PhaseWeight = { 0, 0, 1, 1, 2, 4, 0 };
        private static readonly int[] PawnAdvance = { 0, 0, 8, 18, 35, 65, 115, 0 };
        private const int FullPhase = 24;

        public BoardSearch CaptureSearch() => CaptureSearch(true);

        public BoardSearch CaptureHintSearch() => CaptureSearch(false);

        private BoardSearch CaptureSearch(bool includeDrawClaim)
        {
            var snapshot = new ChessGame(this);
            if (!includeDrawClaim && snapshot.legalMoves.Count > 0 && snapshot.legalMoves[snapshot.legalMoves.Count - 1] == BoardMove.ClaimDraw)
                snapshot.legalMoves = snapshot.legalMoves.Take(snapshot.legalMoves.Count - 1).ToArray();
            return (difficulty, seed, cancellation) =>
                BoardNegamax.Find(new SearchPosition(new ChessGame(snapshot)), difficulty, seed, cancellation);
        }

        private sealed class SearchPosition : IClassicSearchPosition
        {
            private readonly ChessGame game;
            private readonly Stack<Frame> history = new Stack<Frame>();

            internal SearchPosition(ChessGame game) { this.game = game; }

            public int CurrentSide => game.CurrentSide;
            public int Winner => game.Winner;
            public bool InCheck => game.InCheck(CurrentSide);
            public int NoMoveScore => InCheck ? -BoardNegamax.MateValue : 0;
            public ulong SearchKey
            {
                get
                {
                    ulong historyKey = game.repetitionSignature;
                    return game.currentKey.Signature(game.halfmoves) ^ ((historyKey << 17) | (historyKey >> 47));
                }
            }

            public int Evaluate()
            {
                int score = game.EvaluatePosition();
                var moves = game.legalMoves;
                if (moves.Count > 0 && moves[moves.Count - 1] == BoardMove.ClaimDraw)
                    score = Math.Max(0, score);
                return score;
            }

            public IReadOnlyList<BoardMove> SearchMoves(bool tacticalOnly)
            {
                if (!tacticalOnly) return game.legalMoves;
                var tactical = new List<BoardMove>();
                foreach (BoardMove move in game.legalMoves)
                    if (move == BoardMove.ClaimDraw || move.Choice != 0 || game.CapturedKind(move) != 0)
                        tactical.Add(move);
                return tactical;
            }

            public int Priority(BoardMove move)
            {
                if (move == BoardMove.ClaimDraw) return 1;
                int moving = Math.Abs(game.board[move.From]);
                int capture = game.CapturedKind(move);
                int value = move.Choice == 0 ? 0 : 3000 + MaterialValue[move.Choice];
                if (capture != 0) value += 1500 + MaterialValue[capture] * 8 - MaterialValue[moving];
                else if (moving == King && Math.Abs(move.To - move.From) == 2) value += 80;
                return value + Center(move.To / 8, move.To % 8) - Center(move.From / 8, move.From % 8);
            }

            public void Push(BoardMove move)
            {
                var frame = new Frame
                {
                    Move = move, PreviousMoves = game.legalMoves, PreviousStatus = game.Status, PreviousWinner = game.Winner
                };
                if (move == BoardMove.ClaimDraw) game.End(BoardResult.Draw, game.FindDrawClaim(game.legalMoves));
                else
                {
                    frame.Undo = game.Push(move);
                    game.RefreshStatus();
                }
                history.Push(frame);
            }

            public void Pop()
            {
                Frame frame = history.Pop();
                if (frame.Move != BoardMove.ClaimDraw) game.Pop(frame.Move, frame.Undo);
                game.legalMoves = frame.PreviousMoves;
                game.Status = frame.PreviousStatus;
                game.Winner = frame.PreviousWinner;
            }

            private struct Frame
            {
                internal BoardMove Move;
                internal UndoState Undo;
                internal IReadOnlyList<BoardMove> PreviousMoves;
                internal string PreviousStatus;
                internal int PreviousWinner;
            }
        }

        private int EvaluatePosition()
        {
            int phase = 0;
            int whitePawns = 0, blackPawns = 0;
            int whiteBishops = 0, blackBishops = 0;
            for (int square = 0; square < board.Length; square++)
            {
                int piece = board[square];
                phase += PhaseWeight[Math.Abs(piece)];
                int fileBit = 1 << (square % 8);
                if (piece == Pawn) whitePawns |= fileBit;
                if (piece == -Pawn) blackPawns |= fileBit;
                if (piece == Bishop) whiteBishops++;
                if (piece == -Bishop) blackBishops++;
            }
            phase = Math.Min(phase, FullPhase);
            int score = (whiteBishops >= 2 ? 30 : 0) - (blackBishops >= 2 ? 30 : 0);
            for (int square = 0; square < board.Length; square++)
            {
                int piece = board[square];
                if (piece == 0) continue;
                int side = SideOf(piece);
                int kind = Math.Abs(piece);
                int friendlyFiles = side == White ? whitePawns : blackPawns;
                int enemyFiles = side == White ? blackPawns : whitePawns;
                int pieceScore = MaterialValue[kind] + PiecePlacement(square, kind, side, friendlyFiles, enemyFiles, phase);
                score += side == White ? pieceScore : -pieceScore;
            }
            return (CurrentSide == White ? score : -score) + 7;
        }

        private int PiecePlacement(int square, int kind, int side, int friendlyFiles, int enemyFiles, int phase)
        {
            int row = square / 8;
            int col = square % 8;
            int advance = side == White ? row : 7 - row;
            int center = Center(row, col);
            int file = 1 << col;
            switch (kind)
            {
                case Pawn:
                    int pawnScore = advance * 3 + center * 2;
                    if ((friendlyFiles & ((file << 1) | (file >> 1))) == 0) pawnScore -= 12;
                    if (HasPawnAhead(row, col, side, side, 0)) pawnScore -= 13;
                    if (!HasPawnAhead(row, col, side, 1 - side, 1))
                        pawnScore += Blend(PawnAdvance[advance] / 2, PawnAdvance[advance], phase);
                    return pawnScore;
                case Knight:
                    return center * 5 - (advance == 0 ? 10 : 0);
                case Bishop:
                    return center * 3 - (advance == 0 ? 12 : 0);
                case Rook:
                    int openFile = (friendlyFiles & file) != 0 ? 0 : (enemyFiles & file) == 0 ? 23 : 13;
                    return openFile + (advance == 6 ? 24 : 0);
                case Queen:
                    return Blend(center - (advance > 2 ? 10 : 0), center * 3, phase);
                case King:
                    int openingKing = -center * 4 + KingShield(row, col, side) * 11;
                    if (advance == 0 && (col == 2 || col == 6)) openingKing += 27;
                    return Blend(openingKing, center * 6, phase);
                default:
                    return 0;
            }
        }

        private bool HasPawnAhead(int row, int col, int forwardSide, int pawnSide, int radius)
        {
            int pawn = Encode(pawnSide, Pawn);
            int step = PawnStep(forwardSide) / 8;
            int first = Math.Max(0, col - radius);
            int last = Math.Min(7, col + radius);
            for (int rank = row + step; (uint)rank < 8; rank += step)
                for (int file = first; file <= last; file++)
                    if (board[rank * 8 + file] == pawn) return true;
            return false;
        }

        private int KingShield(int row, int col, int side)
        {
            int next = row + PawnStep(side) / 8;
            int count = 0;
            for (int file = col - 1; file <= col + 1; file++)
                if (Inside(next, file) && board[next * 8 + file] == Encode(side, Pawn))
                    count++;
            return count;
        }

        private static int Blend(int opening, int endgame, int phase) =>
            (opening * phase + endgame * (FullPhase - phase)) / FullPhase;

        private static int Center(int row, int col) => 14 - Math.Abs(2 * row - 7) - Math.Abs(2 * col - 7);
    }
}
