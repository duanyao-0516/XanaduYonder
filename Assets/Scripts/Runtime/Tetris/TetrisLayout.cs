using System;

namespace XanaduYonder.Runtime.Tetris
{
    /// <summary>Coordinates use the actual safe root. The host owns system insets and width scaling.</summary>
    public sealed class TetrisLayout
    {
        public double Scale { get; private set; }
        public double Inset { get; private set; }
        public double ContentWidth { get; private set; }
        public double ArenaTop { get; private set; }
        public double ArenaHeight { get; private set; }
        public double BoardLeft { get; private set; }
        public double BoardTop { get; private set; }
        public double BoardWidth { get; private set; }
        public double BoardHeight { get; private set; }
        public double ControlsTop { get; private set; }
        public double ControlsHeight { get; private set; }
        public double StatusTop { get; private set; }
        public double MinimumTarget { get; private set; }

        public TetrisLayout(double width, double height, bool warning)
        {
            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
                throw new ArgumentOutOfRangeException("width");
            if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
                throw new ArgumentOutOfRangeException("height");
            Scale = width / 388;
            Inset = 19.5 * Scale;
            ContentWidth = width - 2 * Inset;
            ArenaTop = 144 * Scale;
            ControlsHeight = 104 * Scale;
            ControlsTop = height - 12 * Scale - ControlsHeight;
            double statusHeight = warning ? 26 * Scale : 0;
            ArenaHeight = Math.Max(0, ControlsTop - 16 * Scale - statusHeight - ArenaTop);
            double cell = Math.Max(0, Math.Min((ContentWidth - 112 * Scale) / 10,
                (ArenaHeight - 24 * Scale) / 20));
            BoardWidth = cell * 10;
            BoardHeight = cell * 20;
            BoardLeft = (ContentWidth - BoardWidth) / 2;
            BoardTop = (ArenaHeight - BoardHeight) / 2;
            StatusTop = ArenaTop + ArenaHeight;
            MinimumTarget = 48 * Scale;
        }
    }
}
