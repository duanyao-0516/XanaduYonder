using System;
using System.Collections.Generic;
using System.Globalization;

namespace XanaduYonder.Core.BoardGames
{
    public sealed partial class ChessGame : IBoardPosition
    {
        public const int White = 0;
        public const int Black = 1;
        public const int Pawn = 1;
        public const int Knight = 2;
        public const int Bishop = 3;
        public const int Rook = 4;
        public const int Queen = 5;
        public const int King = 6;

        private const int WhiteShort = 1;
        private const int WhiteLong = 2;
        private const int BlackShort = 4;
        private const int BlackLong = 8;
        private const int NoSquare = -1;
        private const int ClaimHalfmoves = 100;
        private const int AutomaticHalfmoves = 150;
        private const string StartingFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        private const string RepetitionClaim = "和棋 · 申请三次重复";
        private const string MoveCountClaim = "和棋 · 申请五十回合";

        // White pieces are positive, black pieces negative, and empty squares zero.
        private readonly int[] board = new int[64];
        private readonly int[] kings = { NoSquare, NoSquare };
        private readonly Dictionary<PositionKey, int> repetitions = new Dictionary<PositionKey, int>();
        private int castleRights;
        private int enPassant = NoSquare;
        private int halfmoves;
        private int repeatedPositions;
        private ulong repetitionSignature;
        private PositionKey currentKey;
        private IReadOnlyList<BoardMove> legalMoves;

        public int CurrentSide { get; private set; }
        public int Winner { get; private set; } = BoardResult.Ongoing;
        public int PointCount => 64;
        public string Status { get; private set; }

        public ChessGame() : this(StartingFen) { }

        private ChessGame(string fen)
        {
            LoadFen(fen);
            currentKey = Key();
            AddOccurrence(currentKey);
            RefreshStatus();
        }

        private ChessGame(ChessGame source)
        {
            Array.Copy(source.board, board, board.Length);
            Array.Copy(source.kings, kings, kings.Length);
            foreach (KeyValuePair<PositionKey, int> entry in source.repetitions)
                repetitions.Add(entry.Key, entry.Value);
            castleRights = source.castleRights;
            enPassant = source.enPassant;
            halfmoves = source.halfmoves;
            repeatedPositions = source.repeatedPositions;
            repetitionSignature = source.repetitionSignature;
            currentKey = source.currentKey;
            CurrentSide = source.CurrentSide;
            Winner = source.Winner;
            Status = source.Status;
            legalMoves = source.legalMoves;
        }

        public static ChessGame FromFen(string fen) => new ChessGame(fen);
        public IBoardPosition Copy() => new ChessGame(this);

        public BoardPiece PieceAt(int point, int viewer)
        {
            int piece = board[point];
            return piece == 0 ? default : new BoardPiece(SideOf(piece), Math.Abs(piece));
        }

        public IReadOnlyList<BoardMove> LegalMoves() => legalMoves;

        public bool TryPlay(BoardMove move)
        {
            if (Winner != BoardResult.Ongoing) return false;
            if (move == BoardMove.ClaimDraw)
            {
                string claim = FindDrawClaim(legalMoves);
                if (claim == null) return false;
                End(BoardResult.Draw, claim);
                return true;
            }
            if (!ContainsMove(legalMoves, move)) return false;
            Push(move);
            RefreshStatus();
            return true;
        }

        public bool CanClaimDraw(BoardMove intendedMove)
        {
            return Winner == BoardResult.Ongoing && ContainsMove(legalMoves, intendedMove)
                && intendedMove.From >= 0 && IntendedDrawClaim(intendedMove) != null;
        }

        private static bool ContainsMove(IReadOnlyList<BoardMove> moves, BoardMove move)
        {
            foreach (BoardMove legal in moves)
                if (move == legal) return true;
            return false;
        }

        private void RefreshStatus()
        {
            var moves = GenerateLegalMoves();
            if (moves.Count == 0)
            {
                if (InCheck(CurrentSide)) End(1 - CurrentSide, "将死");
                else End(BoardResult.Draw, "和棋 · 逼和");
                return;
            }
            if (IsDeadPosition() || ForcedCapturesAreDead(moves)) { End(BoardResult.Draw, "和棋 · 死局"); return; }
            if (Occurrences(currentKey) >= 5) { End(BoardResult.Draw, "和棋 · 五次重复"); return; }
            if (halfmoves >= AutomaticHalfmoves) { End(BoardResult.Draw, "和棋 · 七十五回合"); return; }
            if (FindDrawClaim(moves) != null) moves.Add(BoardMove.ClaimDraw);
            legalMoves = moves.AsReadOnly();
            Status = InCheck(CurrentSide) ? "将军" : "行棋";
        }

        private void End(int winner, string status)
        {
            Winner = winner;
            Status = status;
            legalMoves = Array.Empty<BoardMove>();
        }

        private string FindDrawClaim(IReadOnlyList<BoardMove> moves)
        {
            if (Occurrences(currentKey) >= 3) return RepetitionClaim;
            if (halfmoves >= ClaimHalfmoves) return MoveCountClaim;
            if (repeatedPositions == 0 && halfmoves < ClaimHalfmoves - 1) return null;
            foreach (BoardMove move in moves)
            {
                if (move.From < 0) continue;
                string claim = IntendedDrawClaim(move);
                if (claim != null) return claim;
            }
            return null;
        }

        private string IntendedDrawClaim(BoardMove move)
        {
            if (Math.Abs(board[move.From]) == Pawn || board[move.To] != 0) return null;
            if (halfmoves >= ClaimHalfmoves - 1) return MoveCountClaim;
            if (repeatedPositions == 0) return null;

            UndoState undo = Apply(move);
            bool qualifies = Occurrences(Key()) >= 2;
            Revert(move, undo);
            return qualifies ? RepetitionClaim : null;
        }

        private UndoState Push(BoardMove move)
        {
            UndoState undo = Apply(move);
            currentKey = Key();
            AddOccurrence(currentKey);
            return undo;
        }

        private void Pop(BoardMove move, UndoState undo)
        {
            RemoveOccurrence(currentKey);
            Revert(move, undo);
            currentKey = undo.PreviousKey;
        }

        private int Occurrences(PositionKey key)
        {
            repetitions.TryGetValue(key, out int count);
            return count;
        }

        private void AddOccurrence(PositionKey key)
        {
            int count = Occurrences(key);
            if (count == 1) repeatedPositions++;
            repetitions[key] = count + 1;
            if (count > 0) repetitionSignature ^= key.Signature(count);
            repetitionSignature ^= key.Signature(count + 1);
        }

        private void RemoveOccurrence(PositionKey key)
        {
            int count = repetitions[key];
            if (count == 2) repeatedPositions--;
            if (count == 1) repetitions.Remove(key);
            else
            {
                repetitions[key] = count - 1;
                repetitionSignature ^= key.Signature(count - 1);
            }
            repetitionSignature ^= key.Signature(count);
        }

        private void LoadFen(string fen)
        {
            if (fen == null) throw new ArgumentNullException(nameof(fen));
            string[] fields = fen.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 6) throw InvalidFen("six fields are required");
            string[] ranks = fields[0].Split('/');
            if (ranks.Length != 8) throw InvalidFen("eight ranks are required");
            for (int row = 0; row < 8; row++)
            {
                int col = 0;
                foreach (char symbol in ranks[7 - row])
                {
                    if (symbol >= '1' && symbol <= '8')
                    {
                        col += symbol - '0';
                        continue;
                    }
                    int kind = " PNBRQK".IndexOf(char.ToUpperInvariant(symbol));
                    if (kind < Pawn || col >= 8 || (kind == Pawn && (row == 0 || row == 7)))
                        throw InvalidFen("invalid piece placement");
                    int side = char.IsUpper(symbol) ? White : Black;
                    int square = row * 8 + col++;
                    board[square] = Encode(side, kind);
                    if (kind == King)
                    {
                        if (kings[side] != NoSquare) throw InvalidFen("multiple kings of one color");
                        kings[side] = square;
                    }
                }
                if (col != 8) throw InvalidFen("rank does not contain eight squares");
            }
            if (kings[White] == NoSquare || kings[Black] == NoSquare) throw InvalidFen("both kings are required");
            if (fields[1] == "w") CurrentSide = White;
            else if (fields[1] == "b") CurrentSide = Black;
            else throw InvalidFen("invalid turn");

            if (fields[2] != "-")
            {
                foreach (char symbol in fields[2])
                {
                    int index = "KQkq".IndexOf(symbol);
                    if (index < 0 || (castleRights & (1 << index)) != 0) throw InvalidFen("invalid castling rights");
                    int side = index / 2;
                    int king = HomeKing(side);
                    int rook = king + (index % 2 == 0 ? 3 : -4);
                    if (kings[side] != king || board[rook] != Encode(side, Rook))
                        throw InvalidFen("castling rights need the king and rook on their home squares");
                    castleRights |= 1 << index;
                }
            }

            if (fields[3] != "-")
            {
                string target = fields[3];
                char targetRank = CurrentSide == White ? '6' : '3';
                if (target.Length != 2 || target[0] < 'a' || target[0] > 'h' || target[1] != targetRank)
                    throw InvalidFen("invalid en passant target");
                enPassant = (targetRank - '1') * 8 + target[0] - 'a';
                int step = PawnStep(CurrentSide);
                if (board[enPassant] != 0 || board[enPassant + step] != 0 || board[enPassant - step] != Encode(1 - CurrentSide, Pawn))
                    throw InvalidFen("en passant target needs the pawn's double step");
            }

            if (!int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out halfmoves)
                || !int.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out int fullmoves)
                || fullmoves < 1)
                throw InvalidFen("invalid move counters");
            if (InCheck(1 - CurrentSide)) throw InvalidFen("the side that just moved is in check");
        }

        private static FormatException InvalidFen(string message) => new FormatException("Invalid chess FEN: " + message);
    }
}
