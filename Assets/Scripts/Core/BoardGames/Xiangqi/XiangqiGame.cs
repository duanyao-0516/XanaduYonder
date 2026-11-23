using System;
using System.Collections.Generic;
using System.Globalization;

namespace XanaduYonder.Core.BoardGames
{
    public sealed partial class XiangqiGame : IBoardPosition
    {
        private const int Rows = 10;
        private const int Columns = 9;
        private const int Squares = Rows * Columns;
        private const int King = 1;
        private const int Advisor = 2;
        private const int Elephant = 3;
        private const int Horse = 4;
        private const int Rook = 5;
        private const int Cannon = 6;
        private const int Pawn = 7;
        private const int NaturalLimit = 100;
        private const string StartingFen = "rheakaehr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RHEAKAEHR w";
        private static readonly IReadOnlyList<BoardMove> NoMoves = Array.Empty<BoardMove>();
        private static readonly ulong[,] PieceKeys = BuildPieceKeys();
        private readonly sbyte[] board;
        private readonly int[] kings;
        private readonly List<Turn> history;
        private ulong positionKey;
        private int lastIrreversible;
        private int naturalMoves;
        private int pieceCount;
        private int redChecks;
        private int blackChecks;
        private int winner = BoardResult.Ongoing;
        private string resultMessage;

        public int CurrentSide { get; private set; }
        public int Winner => winner;
        public int PointCount => Squares;
        public string Status => resultMessage ?? (history[history.Count - 1].Check ? "将军" : "");

        public XiangqiGame() : this(StartingFen) { }

        private XiangqiGame(string fen)
        {
            if (fen == null) throw new ArgumentNullException(nameof(fen));
            string[] fields = fen.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 2 && fields.Length != 6)
                throw new FormatException("Xiangqi FEN requires board and side, optionally followed by four fields.");
            string[] ranks = fields[0].Split('/');
            if (ranks.Length != Rows) throw new FormatException("Xiangqi FEN must have ten ranks.");
            switch (fields[1])
            {
                case "w":
                case "r": CurrentSide = 0; break;
                case "b": CurrentSide = 1; break;
                default: throw new FormatException("Invalid Xiangqi FEN side.");
            }
            if (fields.Length == 6 && (fields[2] != "-" || fields[3] != "-" ||
                !int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out naturalMoves) ||
                !int.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out int fullMoves) ||
                fullMoves < 1))
                throw new FormatException("Invalid Xiangqi FEN move counters.");
            board = new sbyte[Squares];
            kings = new[] { -1, -1 };
            for (int row = 0; row < Rows; row++)
            {
                int column = 0;
                foreach (char symbol in ranks[row])
                {
                    if (symbol >= '1' && symbol <= '9')
                    {
                        column += symbol - '0';
                        continue;
                    }
                    int kind = ParseKind(symbol);
                    if (column >= Columns || kind == 0) throw new FormatException("Invalid Xiangqi FEN rank.");
                    int side = char.IsUpper(symbol) ? 0 : 1;
                    int square = Index(row, column);
                    sbyte piece = (sbyte)(side == 0 ? kind : -kind);
                    board[square] = piece;
                    pieceCount++;
                    positionKey ^= PieceKeys[square, piece + Pawn];
                    if (kind == King)
                    {
                        if (kings[side] >= 0 || !InPalace(side, row, column))
                            throw new FormatException("Each side must have one king in its palace.");
                        kings[side] = square;
                    }
                    column++;
                }
                if (column != Columns) throw new FormatException("Xiangqi FEN ranks must contain nine intersections.");
            }
            if (kings[0] < 0 || kings[1] < 0) throw new FormatException("Xiangqi FEN must contain both kings.");
            if (CurrentSide == 1) positionKey ^= SideKey;
            history = new List<Turn> { new Turn(positionKey, positionKey, BoardMove.None, 0, KingInCheck(CurrentSide), 0, 0, 0, 0) };
            UpdateResult();
        }

        private XiangqiGame(XiangqiGame source)
        {
            board = (sbyte[])source.board.Clone();
            kings = (int[])source.kings.Clone();
            history = new List<Turn>(source.history);
            CurrentSide = source.CurrentSide;
            positionKey = source.positionKey;
            lastIrreversible = source.lastIrreversible;
            naturalMoves = source.naturalMoves;
            pieceCount = source.pieceCount;
            redChecks = source.redChecks;
            blackChecks = source.blackChecks;
            winner = source.winner;
            resultMessage = source.resultMessage;
        }

        public static XiangqiGame FromFen(string fen) => new XiangqiGame(fen);

        public BoardPiece PieceAt(int point, int viewer)
        {
            if ((uint)point >= Squares) return new BoardPiece(-1, 0);
            sbyte piece = board[point];
            return piece == 0 ? new BoardPiece(-1, 0) : new BoardPiece(SideOf(piece), Math.Abs(piece));
        }

        public IReadOnlyList<BoardMove> LegalMoves() =>
            winner == BoardResult.Ongoing ? GenerateMoves(CurrentSide, false) : NoMoves;

        public bool TryPlay(BoardMove move)
        {
            if (winner != BoardResult.Ongoing || move.Choice != 0 || move.PathLength != 0 ||
                (uint)move.From >= Squares || (uint)move.To >= Squares || !IsLegalMove(move.From, move.To, CurrentSide))
                return false;
            PushMove(move);
            UpdateResult();
            return true;
        }

        public IBoardPosition Copy() => new XiangqiGame(this);

        private void UpdateResult()
        {
            if (!HasLegalMove(CurrentSide))
            {
                winner = 1 - CurrentSide;
                resultMessage = history[history.Count - 1].Check ? "将死" : "困毙";
                return;
            }
            winner = RuleResult(out resultMessage);
        }

        private void PushMove(BoardMove move)
        {
            sbyte piece = board[move.From];
            sbyte captured = board[move.To];
            positionKey ^= PieceKeys[move.From, piece + Pawn] ^ PieceKeys[move.To, piece + Pawn] ^ SideKey;
            if (captured != 0) positionKey ^= PieceKeys[move.To, captured + Pawn];
            MovePiece(move.From, move.To, piece);
            int side = CurrentSide;
            CurrentSide = 1 - side;
            bool check = KingInCheck(CurrentSide);
            bool irreversible = captured != 0 || (Math.Abs(piece) == Pawn && move.From / Columns != move.To / Columns);
            ulong reversibleKey = irreversible ? positionKey :
                unchecked((history[history.Count - 1].ReversibleKey ^ positionKey) * 1099511628211UL);
            history.Add(new Turn(positionKey, reversibleKey, move, captured, check, lastIrreversible, naturalMoves, redChecks, blackChecks));
            if (irreversible) lastIrreversible = history.Count - 1;
            if (captured != 0)
            {
                pieceCount--;
                naturalMoves = redChecks = blackChecks = 0;
            }
            else
            {
                naturalMoves++;
                if (check && side == 0) redChecks++;
                if (check && side == 1) blackChecks++;
            }
        }

        private void PopMove()
        {
            int last = history.Count - 1;
            Turn turn = history[last];
            RestorePiece(turn.Move.From, turn.Move.To, board[turn.Move.To], turn.Captured);
            history.RemoveAt(last);
            if (turn.Captured != 0) pieceCount++;
            positionKey = history[last - 1].Key;
            CurrentSide = 1 - CurrentSide;
            lastIrreversible = turn.PreviousIrreversible;
            naturalMoves = turn.PreviousNaturalMoves;
            redChecks = turn.PreviousRedChecks;
            blackChecks = turn.PreviousBlackChecks;
        }

        private readonly struct Turn
        {
            internal readonly ulong Key;
            internal readonly ulong ReversibleKey;
            internal readonly BoardMove Move;
            internal readonly sbyte Captured;
            internal readonly bool Check;
            internal readonly int PreviousIrreversible;
            internal readonly int PreviousNaturalMoves;
            internal readonly int PreviousRedChecks;
            internal readonly int PreviousBlackChecks;

            internal Turn(ulong key, ulong reversibleKey, BoardMove move, sbyte captured, bool check, int previousIrreversible,
                int previousNaturalMoves, int previousRedChecks, int previousBlackChecks)
            {
                Key = key;
                ReversibleKey = reversibleKey;
                Move = move;
                Captured = captured;
                Check = check;
                PreviousIrreversible = previousIrreversible;
                PreviousNaturalMoves = previousNaturalMoves;
                PreviousRedChecks = previousRedChecks;
                PreviousBlackChecks = previousBlackChecks;
            }
        }

        private const ulong SideKey = 0xc53dc295ac16077aUL;

        private static ulong[,] BuildPieceKeys()
        {
            var keys = new ulong[Squares, 2 * Pawn + 1];
            ulong state = 0x61db2a86df47bd36UL;
            for (int square = 0; square < Squares; square++)
                for (int piece = -Pawn; piece <= Pawn; piece++)
                    if (piece != 0) keys[square, piece + Pawn] = NextHash(ref state);
            return keys;
        }

        private static ulong NextHash(ref ulong state)
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong value = (state ^ (state >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        private static int ParseKind(char symbol)
        {
            switch (char.ToLowerInvariant(symbol))
            {
                case 'k': return King;
                case 'a': return Advisor;
                case 'e':
                case 'b': return Elephant;
                case 'h':
                case 'n': return Horse;
                case 'r': return Rook;
                case 'c': return Cannon;
                case 'p': return Pawn;
                default: return 0;
            }
        }
    }
}
