using System;
using System.Globalization;

namespace XanaduYonder.Core.Gomoku
{
    public static class GomokuCoordinates
    {
        /// <summary>A-O from left to right; 1-15 from bottom to top. Storage starts at the top left.</summary>
        public static string Format(int index)
        {
            if (index < 0 || index >= GomokuGame.CellCount) throw new ArgumentOutOfRangeException(nameof(index));
            return ((char)('A' + index % GomokuGame.Size)).ToString() + (GomokuGame.Size - index / GomokuGame.Size).ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryParse(string text, out int index)
        {
            index = -1;
            if (text == null || text.Length < 2 || text.Length > 3) return false;
            char letter = char.ToUpperInvariant(text[0]);
            if (letter < 'A' || letter > 'O' || text[1] < '1' || text[1] > '9') return false;
            int row = text[1] - '0';
            if (text.Length == 3)
            {
                if (text[2] < '0' || text[2] > '9') return false;
                row = row * 10 + text[2] - '0';
            }
            if (row > GomokuGame.Size) return false;
            index = (GomokuGame.Size - row) * GomokuGame.Size + letter - 'A';
            return true;
        }
    }
}
