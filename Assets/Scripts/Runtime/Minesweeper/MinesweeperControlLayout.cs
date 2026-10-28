using System;

namespace XanaduYonder.Runtime.Minesweeper
{
    /// <summary>Game-local touch-target sizing and toolbar layout in unscaled design units.</summary>
    public sealed class MinesweeperControlLayout
    {
        private const double PreferredGap = 8;
        private const double VerticalPadding = 16;
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
            double rowHeight = minimumTarget + VerticalPadding;
            var result = new MinesweeperControlLayout { Width = availableWidth, Height = rowHeight };
            if (availableWidth < 7 * minimumTarget + 4 * PreferredGap)
            {
                // Compress only the top-row gaps at the exact four-target boundary.
                double gap = Math.Min(PreferredGap, (availableWidth - 4 * minimumTarget) / 3);
                double actionWidth = (availableWidth - 3 * gap) / 4;
                result.Height = 2 * rowHeight + PreferredGap;
                result.Fullscreen = Rect(0, 0, actionWidth, rowHeight);
                result.Reveal = Rect(actionWidth + gap, 0, actionWidth, rowHeight);
                result.Flag = Rect(2 * (actionWidth + gap), 0, actionWidth, rowHeight);
                result.Overview = Rect(3 * (actionWidth + gap), 0, actionWidth, rowHeight);
                result.SetZoomGroup((availableWidth - 3 * minimumTarget) / 2, rowHeight + PreferredGap, minimumTarget, rowHeight);
                return result;
            }
            double width = (availableWidth - 3 * minimumTarget - 4 * PreferredGap) / 4;
            result.Fullscreen = Rect(0, 0, width, rowHeight);
            result.Reveal = Rect(width + PreferredGap, 0, width, rowHeight);
            result.Flag = Rect(2 * (width + PreferredGap), 0, width, rowHeight);
            result.SetZoomGroup(3 * (width + PreferredGap), 0, minimumTarget, rowHeight);
            result.Overview = Rect(availableWidth - width, 0, width, rowHeight);
            return result;
        }

        private void SetZoomGroup(double x, double y, double segmentWidth, double height)
        {
            ZoomGroup = Rect(x, y, 3 * segmentWidth, height);
            ZoomOut = Rect(x, y, segmentWidth, height);
            ZoomValue = Rect(x + segmentWidth, y, segmentWidth, height);
            ZoomIn = Rect(x + 2 * segmentWidth, y, segmentWidth, height);
        }

        private static MinesweeperInputRect Rect(double x, double y, double width, double height)
        { return new MinesweeperInputRect(x, y, width, height); }
        private static void RequirePositive(double value, string name)
        { if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0) throw new ArgumentOutOfRangeException(name); }
    }
}
