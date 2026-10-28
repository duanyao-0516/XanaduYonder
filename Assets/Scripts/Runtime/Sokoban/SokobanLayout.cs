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
            double w=Math.Max(width,minimum),h=Math.Max(height,minimum),x=-(w-width)/2,y=-(h-height)/2;
            if(name=="Home"||name=="Book"||name=="Settings") y=Math.Max(y,-6*scale);
            if(name=="Book"&&w>width) x=-(w-width)+2*scale;
            if(name=="Settings"&&w>width) x=-2*scale;
            if(name=="Fit")
            {
                double grow=Math.Max(0,(minimum-44*scale)/2);
                x=Math.Max(0,grow-2*scale); w=Math.Min(width,78*scale-2*grow);
            }
            if(name=="Move Down"||name=="Undo"||name=="Restart") y=-(h-height);
            return new SokobanHitBounds(x,y,w,h);
        }
    }
    public struct SokobanHitBounds
    {
        public readonly double X,Y,Width,Height;
        public SokobanHitBounds(double x,double y,double width,double height) { X=x; Y=y; Width=width; Height=height; }
    }
}
