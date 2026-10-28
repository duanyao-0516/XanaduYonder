using System;

namespace XanaduYonder.Runtime.Sokoban
{
    /// <summary>Centered automatic fit in top-left coordinates; the complete board stays inside its padding.</summary>
    public sealed class SokobanViewport
    {
        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public double Padding { get; private set; }
        public double Zoom { get { return 1; } }
        public double CenterX { get { return .5; } }
        public double CenterY { get { return .5; } }
        public double FitCellSize { get { return Math.Min((Width-2*Padding)/Columns,(Height-2*Padding)/Rows); } }
        public double CellSize { get { return FitCellSize; } }
        public double OffsetX { get { return (Width-Columns*CellSize)/2; } }
        public double OffsetY { get { return (Height-Rows*CellSize)/2; } }
        public SokobanViewport(int columns,int rows,double width,double height,double padding=0)
        {
            if(columns<1) throw new ArgumentOutOfRangeException("columns");
            if(rows<1) throw new ArgumentOutOfRangeException("rows");
            Columns=columns; Rows=rows; Resize(width,height,padding);
        }
        public void Resize(double width,double height) { Resize(width,height,Padding); }
        public void Resize(double width,double height,double padding)
        {
            RequireFinite(width); RequireFinite(height); RequireFinite(padding);
            if(padding<0) throw new ArgumentOutOfRangeException("padding");
            if(width<=2*padding) throw new ArgumentOutOfRangeException("width");
            if(height<=2*padding) throw new ArgumentOutOfRangeException("height");
            Width=width; Height=height; Padding=padding;
        }
        private static void RequireFinite(double value)
        { if(double.IsNaN(value)||double.IsInfinity(value)) throw new ArgumentOutOfRangeException("value"); }
    }
}
