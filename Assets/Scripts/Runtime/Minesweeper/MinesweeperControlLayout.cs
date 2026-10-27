using System;

namespace XanaduYonder.Runtime.Minesweeper
{
    /// <summary>Game-local touch-target sizing and toolbar layout in unscaled design units.</summary>
    public sealed class MinesweeperControlLayout
    {
        private const double PreferredGap = 8;
        public MinesweeperInputRect Reveal { get; private set; }
        public MinesweeperInputRect Flag { get; private set; }
        public MinesweeperInputRect ZoomOut { get; private set; }
        public MinesweeperInputRect ZoomValue { get; private set; }
        public MinesweeperInputRect ZoomIn { get; private set; }
        public MinesweeperInputRect Overview { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        private MinesweeperControlLayout() { }

        public static double TargetSize(double dpi, double pixelsPerUnit)
        {
            RequirePositive(pixelsPerUnit, "pixelsPerUnit");
            double density = dpi > 0 ? dpi / 160 : 1;
            double target = Math.Max(88, Math.Ceiling(44 * density / pixelsPerUnit));
            RequirePositive(target, "dpi");
            return target;
        }

        public static MinesweeperControlLayout Create(double availableWidth, double minimumTarget)
        {
            RequirePositive(availableWidth, "availableWidth");
            RequirePositive(minimumTarget, "minimumTarget");
            double overviewWidth = Math.Max(120, minimumTarget);
            // Four minimum-width controls must fit on the second row; overview also retains its label width.
            if (availableWidth < 3 * minimumTarget + overviewWidth)
                throw new ArgumentOutOfRangeException("availableWidth", "The three zoom targets and overview do not fit.");
            var result = new MinesweeperControlLayout { Width = availableWidth, Height = minimumTarget };
            double modeWidth = (availableWidth - overviewWidth - 3 * minimumTarget - 5 * PreferredGap) / 2;
            if (modeWidth < minimumTarget)
            {
                // Preserve all six targets by moving modes above zoom and overview.
                // Only compress horizontal gaps at the exact four-control width boundary.
                double gap = Math.Min(PreferredGap, Math.Max(0, (availableWidth - overviewWidth - 3 * minimumTarget) / 3));
                double zoomWidth = (availableWidth - overviewWidth - 3 * gap) / 3;
                modeWidth = (availableWidth - PreferredGap) / 2;
                double y = minimumTarget + PreferredGap;
                result.Height = 2 * minimumTarget + PreferredGap;
                result.Reveal = Rect(0, 0, modeWidth, minimumTarget);
                result.Flag = Rect(modeWidth + PreferredGap, 0, modeWidth, minimumTarget);
                result.ZoomOut = Rect(0, y, zoomWidth, minimumTarget);
                result.ZoomValue = Rect(zoomWidth + gap, y, zoomWidth, minimumTarget);
                result.ZoomIn = Rect(2 * (zoomWidth + gap), y, zoomWidth, minimumTarget);
                result.Overview = Rect(availableWidth - overviewWidth, y, overviewWidth, minimumTarget);
                return result;
            }
            result.Reveal = Rect(0, 0, modeWidth, minimumTarget);
            result.Flag = Rect(modeWidth + PreferredGap, 0, modeWidth, minimumTarget);
            double x = 2 * (modeWidth + PreferredGap);
            result.ZoomOut = Rect(x, 0, minimumTarget, minimumTarget);
            result.ZoomValue = Rect(x + minimumTarget + PreferredGap, 0, minimumTarget, minimumTarget);
            result.ZoomIn = Rect(x + 2 * (minimumTarget + PreferredGap), 0, minimumTarget, minimumTarget);
            result.Overview = Rect(availableWidth - overviewWidth, 0, overviewWidth, minimumTarget);
            return result;
        }

        private static MinesweeperInputRect Rect(double x, double y, double width, double height)
        { return new MinesweeperInputRect(x, y, width, height); }
        private static void RequirePositive(double value, string name)
        { if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0) throw new ArgumentOutOfRangeException(name); }
    }
}
