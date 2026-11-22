using System;
using System.Collections.Generic;
using System.Threading;

namespace XanaduYonder.Core.BoardGames
{
    public enum BoardDifficulty { Easy, Normal, Hard }

    public static class BoardResult
    {
        public const int Ongoing = -2;
        public const int Draw = -1;
    }

    public readonly struct BoardPiece
    {
        public int Side { get; }
        public int Kind { get; }
        public BoardPiece(int side, int kind) { Side = side; Kind = kind; }
    }

    public readonly struct BoardMove : IEquatable<BoardMove>
    {
        private readonly int[] path;
        public int From { get; }
        public int To { get; }
        public int Choice { get; }
        public int[] Path => path == null ? Array.Empty<int>() : (int[])path.Clone();
        public int PathLength => path?.Length ?? 0;
        public int PathAt(int index) => path[index];

        public BoardMove(int from, int to, int choice = 0, int[] path = null)
        {
            From = from;
            To = to;
            Choice = choice;
            this.path = path == null || path.Length == 0 ? null : (int[])path.Clone();
        }

        public static readonly BoardMove None = new BoardMove(-3, -3);
        public static readonly BoardMove ClaimDraw = new BoardMove(-2, -2);
        public static readonly BoardMove Pass = new BoardMove(-1, -1);

        public bool Equals(BoardMove other)
        {
            if (From != other.From || To != other.To || Choice != other.Choice || PathLength != other.PathLength) return false;
            for (int i = 0; i < PathLength; i++)
                if (path[i] != other.path[i]) return false;
            return true;
        }

        public override bool Equals(object other) => other is BoardMove move && Equals(move);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ((From * 397) ^ To) * 397 ^ Choice;
                for (int i = 0; i < PathLength; i++) hash = hash * 397 ^ path[i];
                return hash;
            }
        }

        public static bool operator ==(BoardMove left, BoardMove right) => left.Equals(right);
        public static bool operator !=(BoardMove left, BoardMove right) => !left.Equals(right);
    }

    public delegate BoardMove BoardSearch(BoardDifficulty difficulty, int seed, CancellationToken cancellation);

    public interface IBoardPosition
    {
        int CurrentSide { get; }
        int Winner { get; }
        int PointCount { get; }
        string Status { get; }
        BoardPiece PieceAt(int point, int viewer);
        IReadOnlyList<BoardMove> LegalMoves();
        bool TryPlay(BoardMove move);
        IBoardPosition Copy();
        BoardSearch CaptureSearch();
    }
}
