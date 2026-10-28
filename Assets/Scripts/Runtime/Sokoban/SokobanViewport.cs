using System;

namespace XanaduYonder.Runtime.Sokoban
{
    /// <summary>Viewport geometry in top-left coordinates; never changes a game or its history.</summary>
    public sealed class SokobanViewport
    {
        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public double Zoom { get; private set; }
        public double CenterX { get; private set; }
        public double CenterY { get; private set; }
        public double FitCellSize { get { return Math.Min(Width/Columns,Height/Rows); } }
        public double CellSize { get { return FitCellSize*Zoom; } }
        public double OffsetX { get { return Width/2-CenterX*Columns*CellSize; } }
        public double OffsetY { get { return Height/2-CenterY*Rows*CellSize; } }
        public SokobanViewport(int columns,int rows,double width,double height)
        {
            if(columns<1||rows<1) throw new ArgumentOutOfRangeException("columns");
            Columns=columns; Rows=rows; Zoom=1; CenterX=CenterY=.5; Resize(width,height);
        }
        public void Resize(double width,double height)
        {
            RequireFinite(width); RequireFinite(height);
            if(width<=0||height<=0) throw new ArgumentOutOfRangeException("width");
            Width=width; Height=height; ClampCenter();
        }
        public void Restore(double zoom,double centerX,double centerY)
        {
            RequireFinite(zoom); RequireFinite(centerX); RequireFinite(centerY);
            Zoom=Clamp(zoom,1,2); CenterX=Clamp(centerX,0,1); CenterY=Clamp(centerY,0,1); ClampCenter();
        }
        public void Reset() { Zoom=1; CenterX=CenterY=.5; }
        public void StepZoom(int direction)
        { Transform(Width/2,Height/2,Width/2,Height/2,Clamp(Zoom+Math.Sign(direction)*.25,1,2)/Zoom); }
        public void Pan(double dx,double dy)
        {
            RequireFinite(dx); RequireFinite(dy);
            CenterX-=dx/(Columns*CellSize); CenterY-=dy/(Rows*CellSize); ClampCenter();
        }
        public void Transform(double oldX,double oldY,double newX,double newY,double ratio)
        {
            RequireFinite(oldX); RequireFinite(oldY); RequireFinite(newX); RequireFinite(newY); RequireFinite(ratio);
            if(ratio<=0) throw new ArgumentOutOfRangeException("ratio");
            double anchorX=(oldX-OffsetX)/CellSize,anchorY=(oldY-OffsetY)/CellSize;
            Zoom=Clamp(Zoom*ratio,1,2);
            CenterX=(anchorX-(newX-Width/2)/CellSize)/Columns;
            CenterY=(anchorY-(newY-Height/2)/CellSize)/Rows; ClampCenter();
        }
        private void ClampCenter()
        {
            if(Zoom<=1) { CenterX=CenterY=.5; return; }
            CenterX=Axis(CenterX,Width,Columns*CellSize); CenterY=Axis(CenterY,Height,Rows*CellSize);
        }
        private static double Axis(double center,double available,double extent)
        { return extent<=available?.5:Clamp(center,available/(2*extent),1-available/(2*extent)); }
        private static double Clamp(double v,double lo,double hi) { return Math.Max(lo,Math.Min(hi,v)); }
        private static void RequireFinite(double v) { if(double.IsNaN(v)||double.IsInfinity(v)) throw new ArgumentOutOfRangeException("value"); }
    }
}
