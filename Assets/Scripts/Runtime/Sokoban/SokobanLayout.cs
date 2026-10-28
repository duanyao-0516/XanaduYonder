using System;

namespace XanaduYonder.Runtime.Sokoban
{
    /// <summary>Approved 388 CSS-pixel usable content; platform safe bars are owned by the host.</summary>
    public sealed class SokobanLayout
    {
        public double Scale { get; private set; }
        public double Inset { get; private set; }
        public double BoardTop { get; private set; }
        public double BoardWidth { get; private set; }
        public double BoardHeight { get; private set; }
        public double ControlsTop { get; private set; }
        public double ControlsHeight { get; private set; }
        public double StatusTop { get; private set; }
        public SokobanLayout(double width,double height,bool warning)
        {
            Scale=width/388; Inset=19.5*Scale;
            BoardTop=163.421875*Scale; BoardWidth=width-2*Inset;
            ControlsHeight=152*Scale; ControlsTop=height-ControlsHeight;
            double status=warning?26*Scale:0;
            BoardHeight=Math.Max(1,ControlsTop-16*Scale-BoardTop-status);
            StatusTop=BoardTop+BoardHeight;
        }
        public static double MinimumTarget(double designWidth,double usableWidthDp) { return 48*designWidth/usableWidthDp; }
        public static SokobanHitBounds HitBounds(string name,double width,double height,double scale,double minimum)
        {
            // The approved compact controls and grids have narrow gaps. Expanding a
            // target would steal its neighbour's press; touch and raycasts use the
            // same visible rectangle, while responsive grids retain usable widths.
            return new SokobanHitBounds(0,0,width,height);
        }
    }
    public struct SokobanHitBounds
    {
        public readonly double X,Y,Width,Height;
        public SokobanHitBounds(double x,double y,double width,double height) { X=x; Y=y; Width=width; Height=height; }
    }
}
