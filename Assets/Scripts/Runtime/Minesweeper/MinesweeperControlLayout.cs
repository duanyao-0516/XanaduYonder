using System;

namespace XanaduYonder.Runtime.Minesweeper
{
    /// <summary>Game-local touch-target sizing and toolbar layout in unscaled design units.</summary>
    public sealed class MinesweeperControlLayout
    {
        private const double PreferredGap = 8;
        public MinesweeperInputRect Fullscreen { get; private set; }
        public MinesweeperInputRect Reveal { get; private set; }
        public MinesweeperInputRect Flag { get; private set; }
        public MinesweeperInputRect ZoomGroup { get; private set; }
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
            // The fallback keeps four actions on top and the zoom group below.
            if (availableWidth < 4 * minimumTarget)
                throw new ArgumentOutOfRangeException("availableWidth", "The four action targets do not fit.");
            var result = new MinesweeperControlLayout { Width = availableWidth, Height = minimumTarget };
            if (availableWidth < 7 * minimumTarget + 4 * PreferredGap)
            {
                // Compress only the top-row gaps at the exact four-target boundary.
                double gap = Math.Min(PreferredGap, (availableWidth - 4 * minimumTarget) / 3);
                double actionWidth = (availableWidth - 3 * gap) / 4;
                result.Height = 2 * minimumTarget + PreferredGap;
                result.Fullscreen = Rect(0, 0, actionWidth, minimumTarget);
                result.Reveal = Rect(actionWidth + gap, 0, actionWidth, minimumTarget);
                result.Flag = Rect(2 * (actionWidth + gap), 0, actionWidth, minimumTarget);
                result.Overview = Rect(3 * (actionWidth + gap), 0, actionWidth, minimumTarget);
                result.SetZoomGroup((availableWidth - 3 * minimumTarget) / 2, minimumTarget + PreferredGap, minimumTarget);
                return result;
            }
            double width = (availableWidth - 3 * minimumTarget - 4 * PreferredGap) / 4;
            result.Fullscreen = Rect(0, 0, width, minimumTarget);
            result.Reveal = Rect(width + PreferredGap, 0, width, minimumTarget);
            result.Flag = Rect(2 * (width + PreferredGap), 0, width, minimumTarget);
            result.SetZoomGroup(3 * (width + PreferredGap), 0, minimumTarget);
            result.Overview = Rect(availableWidth - width, 0, width, minimumTarget);
            return result;
        }

        private void SetZoomGroup(double x, double y, double segmentSize)
        {
            ZoomGroup = Rect(x, y, 3 * segmentSize, segmentSize);
            ZoomOut = Rect(x, y, segmentSize, segmentSize);
            ZoomValue = Rect(x + segmentSize, y, segmentSize, segmentSize);
            ZoomIn = Rect(x + 2 * segmentSize, y, segmentSize, segmentSize);
        }

        private static MinesweeperInputRect Rect(double x, double y, double width, double height)
        { return new MinesweeperInputRect(x, y, width, height); }
        private static void RequirePositive(double value, string name)
        { if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0) throw new ArgumentOutOfRangeException(name); }
    }
}
