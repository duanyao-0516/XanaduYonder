using System;
using System.Globalization;

namespace XanaduYonder.Runtime.Gomoku
{
    // All rendering and input use top-left-origin, 648-unit design coordinates.
    internal static class GomokuBoardGeometry
    {
        public const double Size = 648, Margin = 52, Pitch = (Size - 2 * Margin) / 14;
        public static double XAt(int cell) { return Margin + cell % 15 * Pitch; }
        public static double YAt(int cell) { return Margin + cell / 15 * Pitch; }
        public static string ColumnLabel(int column) { return ((char)('A' + column)).ToString(); }
        public static string RowLabel(int row) { return (15 - row).ToString(CultureInfo.InvariantCulture); }

        public static int HitTest(double x, double y)
        {
            double min = Margin - Pitch / 2, max = Size - min;
            if (double.IsNaN(x) || double.IsNaN(y) || x < min || x > max || y < min || y > max) return -1;
            int column = Math.Max(0, Math.Min(14, (int)Math.Floor((x - Margin) / Pitch + .5)));
            int row = Math.Max(0, Math.Min(14, (int)Math.Floor((y - Margin) / Pitch + .5)));
            return row * 15 + column;
        }
    }
}
