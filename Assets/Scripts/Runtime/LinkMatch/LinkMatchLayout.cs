using System;
namespace XanaduYonder.Runtime.LinkMatch
{
    /// <summary>Approved safe-content geometry. Coordinates within the board are top-left based.</summary>
    public sealed class LinkMatchLayout
    {
        public double Width { get; private set; }
        public double Height { get; private set; }
        public double Scale { get; private set; }
        public double CssWidth { get { return Width/Scale; } }
        public double Margin { get; private set; }
        public double NavHeight { get; private set; }
        public double HeadingHeight { get; private set; }
        public double InfoHeight { get; private set; }
        public double BoardTop { get; private set; }
        public double BoardWidth { get; private set; }
        public double BoardHeight { get; private set; }
        public double ToolGap { get; private set; }
        public double ToolHeight { get; private set; }
        public double Bottom { get; private set; }
        public double Cell { get; private set; }
        public double DecorationScale { get; private set; }
        public double GridLeft { get; private set; }
        public double GridTop { get; private set; }
        public bool Compact { get; private set; }
        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public LinkMatchLayout(double width,double height,bool warning,int columns,int rows)
        {
            if(double.IsNaN(width)||double.IsInfinity(width)||width<=0||double.IsNaN(height)||double.IsInfinity(height)||height<=0||columns<=0||rows<=0) throw new ArgumentOutOfRangeException();
            Width=width; Height=height; Columns=columns; Rows=rows;
            double reference=height/width<1.9?360:388; Scale=width/reference;
            Compact=height/Scale<=680; Margin=(reference<=370?14:19.5)*Scale;
            NavHeight=(Compact?48:56)*Scale; HeadingHeight=(Compact?34:48)*Scale;
            InfoHeight=(Compact?32:38)*Scale;
            BoardTop=NavHeight+HeadingHeight+InfoHeight+(Compact?6:8)*Scale+(warning?40*Scale:0);
            ToolGap=(Compact?10:16)*Scale; ToolHeight=(Compact?48:52)*Scale; Bottom=(Compact?12:16)*Scale;
            BoardWidth=width-2*Margin; BoardHeight=Math.Max(24*Scale,height-BoardTop-ToolGap-ToolHeight-Bottom);
            Cell=Math.Max(0,Math.Min((BoardWidth-24*Scale)/columns,(BoardHeight-24*Scale)/rows));
            double originalCell=Math.Min((BoardWidth-24*Scale)/6,(BoardHeight-24*Scale)/8);
            DecorationScale=originalCell>0?Math.Min(1,Cell/originalCell):1;
            GridLeft=(BoardWidth-Cell*columns)/2; GridTop=(BoardHeight-Cell*rows)/2;
        }
        public int Hit(double x,double y)
        {
            if(double.IsNaN(x)||double.IsNaN(y)||double.IsInfinity(x)||double.IsInfinity(y)||Cell<=0||x<GridLeft||y<GridTop||x>=GridLeft+Columns*Cell||y>=GridTop+Rows*Cell) return -1;
            return (int)((y-GridTop)/Cell)*Columns+(int)((x-GridLeft)/Cell);
        }
    }
}
