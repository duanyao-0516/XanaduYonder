using System;

namespace XanaduYonder.Runtime.Minesweeper
{
    /// <summary>Board coordinates and clamped rendering, independent of Unity and game rules.</summary>
    public sealed class MinesweeperViewport
    {
        public const double MinimumScale = .5;
        public const double MaximumScale = 2;
        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public double BaseCellSize { get; private set; }
        public double ZoomScale { get; private set; }
        // Persist these desired coordinates. Pure layout changes never overwrite them.
        public double CenterColumn { get; private set; }
        public double CenterRow { get; private set; }
        public bool IsOverview { get; private set; }
        public double RenderScale { get { return IsOverview ? Math.Min(Width / Columns, Height / Rows) / BaseCellSize : ZoomScale; } }
        public double CellSize { get { return BaseCellSize * RenderScale; } }
        public double OffsetX { get { return Offset(Width, Columns, CellSize, CenterColumn); } }
        public double OffsetY { get { return Offset(Height, Rows, CellSize, CenterRow); } }
        public double RenderedCenterColumn { get { return (Width / 2 - OffsetX) / CellSize; } }
        public double RenderedCenterRow { get { return (Height / 2 - OffsetY) / CellSize; } }
        /// <summary>The remembered normal visible rectangle in continuous board coordinates, for the overview outline.</summary>
        public MinesweeperInputRect NormalVisibleBounds
        {
            get
            {
                double size = BaseCellSize * ZoomScale;
                double x = Math.Max(0, -Offset(Width, Columns, size, CenterColumn) / size);
                double y = Math.Max(0, -Offset(Height, Rows, size, CenterRow) / size);
                return new MinesweeperInputRect(x, y, Math.Min(Columns, Width / size), Math.Min(Rows, Height / size));
            }
        }

        public MinesweeperViewport(int columns, int rows, double width, double height, double baseCellSize = 72)
        {
            if (columns <= 0 || rows <= 0 || (long)columns * rows > int.MaxValue)
                throw new ArgumentOutOfRangeException("columns", "Board dimensions must be positive and indexable.");
            Columns = columns; Rows = rows; ZoomScale = 1;
            SetLayout(width, height, baseCellSize);
            CenterColumn = Math.Min(columns / 2.0, width / (2 * baseCellSize));
            CenterRow = Math.Min(rows / 2.0, height / (2 * baseCellSize));
        }

        public void SetLayout(double width, double height, double baseCellSize)
        {
            RequirePositive(width, "width"); RequirePositive(height, "height"); RequirePositive(baseCellSize, "baseCellSize");
            RequirePositive(baseCellSize * Math.Max(Columns, Rows) * MaximumScale, "baseCellSize");
            Width = width; Height = height; BaseCellSize = baseCellSize;
        }

        public void RestoreNormal(double scale, double centerColumn, double centerRow)
        {
            if (!Finite(scale) || scale < MinimumScale || scale > MaximumScale) throw new ArgumentOutOfRangeException("scale");
            if (!Finite(centerColumn) || centerColumn < 0 || centerColumn > Columns) throw new ArgumentOutOfRangeException("centerColumn");
            if (!Finite(centerRow) || centerRow < 0 || centerRow > Rows) throw new ArgumentOutOfRangeException("centerRow");
            ZoomScale = scale; CenterColumn = centerColumn; CenterRow = centerRow;
        }

        public bool Contains(MinesweeperInputPosition position)
        { return Finite(position.X) && Finite(position.Y) && position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height; }

        public int HitTest(MinesweeperInputPosition position)
        {
            if (!Contains(position)) return -1;
            var board = BoardPosition(position);
            if (board.X < 0 || board.Y < 0 || board.X >= Columns || board.Y >= Rows) return -1;
            return (int)Math.Floor(board.Y) * Columns + (int)Math.Floor(board.X);
        }

        public MinesweeperInputPosition BoardPosition(MinesweeperInputPosition position)
        { return new MinesweeperInputPosition((position.X - OffsetX) / CellSize, (position.Y - OffsetY) / CellSize); }

        public MinesweeperInputPosition BoardToViewport(MinesweeperInputPosition position)
        { return new MinesweeperInputPosition(OffsetX + position.X * CellSize, OffsetY + position.Y * CellSize); }

        public void Pan(double deltaX, double deltaY)
        {
            if (IsOverview) return;
            RequireFinite(deltaX, "deltaX"); RequireFinite(deltaY, "deltaY");
            if (deltaX == 0 && deltaY == 0) return;
            SetUserCenter(RenderedCenterColumn - deltaX / CellSize, RenderedCenterRow - deltaY / CellSize);
        }

        public void Transform(MinesweeperInputPosition previousMidpoint, MinesweeperInputPosition currentMidpoint, double scaleRatio)
        {
            if (IsOverview) return;
            RequirePositive(scaleRatio, "scaleRatio");
            RequireFinite(previousMidpoint.X, "previousMidpoint"); RequireFinite(previousMidpoint.Y, "previousMidpoint");
            RequireFinite(currentMidpoint.X, "currentMidpoint"); RequireFinite(currentMidpoint.Y, "currentMidpoint");
            double scale = Clamp(ZoomScale * scaleRatio, MinimumScale, MaximumScale);
            if (scale == ZoomScale && previousMidpoint.X == currentMidpoint.X && previousMidpoint.Y == currentMidpoint.Y) return;
            var anchor = BoardPosition(previousMidpoint);
            ZoomScale = scale;
            SetUserCenter(anchor.X - (currentMidpoint.X - Width / 2) / CellSize,
                anchor.Y - (currentMidpoint.Y - Height / 2) / CellSize);
        }

        public void StepZoom(int direction)
        {
            if (IsOverview || direction == 0) return;
            SetScaleAtCenter(Clamp(ZoomScale + Math.Sign(direction) * .25, MinimumScale, MaximumScale));
        }

        public void ResetZoom() { if (!IsOverview) SetScaleAtCenter(1); }
        public void EnterOverview() { IsOverview = true; }
        public void ExitOverview() { IsOverview = false; }

        public bool LocateFromOverview(MinesweeperInputPosition position)
        {
            if (!IsOverview || HitTest(position) < 0) return false;
            var location = BoardPosition(position);
            IsOverview = false;
            CenterColumn = location.X; CenterRow = location.Y;
            return true;
        }

        private void SetScaleAtCenter(double scale)
        {
            var center = new MinesweeperInputPosition(Width / 2, Height / 2);
            Transform(center, center, scale / ZoomScale);
        }

        private void SetUserCenter(double column, double row)
        {
            CenterColumn = ClampedCenter(Width, Columns, CellSize, column);
            CenterRow = ClampedCenter(Height, Rows, CellSize, row);
        }

        private static double Offset(double extent, int cells, double size, double desiredCenter)
        {
            double boardExtent = cells * size;
            if (boardExtent <= extent) return (extent - boardExtent) / 2;
            return Clamp(extent / 2 - desiredCenter * size, extent - boardExtent, 0);
        }

        private static double ClampedCenter(double extent, int cells, double size, double desiredCenter)
        { return (extent / 2 - Offset(extent, cells, size, desiredCenter)) / size; }
        private static double Clamp(double value, double minimum, double maximum) { return Math.Max(minimum, Math.Min(maximum, value)); }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        private static void RequirePositive(double value, string name) { if (!Finite(value) || value <= 0) throw new ArgumentOutOfRangeException(name); }
        private static void RequireFinite(double value, string name) { if (!Finite(value)) throw new ArgumentOutOfRangeException(name); }
    }
}
