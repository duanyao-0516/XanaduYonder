using System;
using XanaduYonder.Runtime.LinkMatch;
internal static class Program
{
    static int checks;
    static void Check(bool value,string message) { checks++; if(!value) throw new Exception(message); }
    static void Near(double a,double b,string message) { Check(Math.Abs(a-b)<.001,message+": "+a); }
    static void Main()
    {
        var input=new LinkMatchInput();
        input.Begin(1,3,true); Check(input.End(1,3,true)==3,"one release commits"); Check(input.End(1,3,true)==-1,"cannot duplicate");
        input.Begin(1,3,true); input.Move(1,4); input.Move(1,3); Check(input.End(1,3,true)==-1,"leave and reenter cancels");
        input.Begin(1,3,true); input.Begin(2,4,true); Check(input.End(1,3,true)==-1,"second contact cancels first"); Check(input.End(2,4,true)==-1,"second contact cannot take over");
        input.Begin(1,3,true); input.Cancel(); Check(input.End(1,3,true)==-1,"popup or background cancels");
        input.Begin(1,3,true); Check(input.End(1,3,false)==-1,"release while blocked cancels");
        input.Begin(1,-1,true); Check(input.End(1,3,true)==-1,"press outside cannot enter");
        input.Begin(1,3,true); Check(input.End(2,3,true)==-1,"foreign release cancels"); Check(input.End(1,3,true)==-1,"foreign release invalidates old owner");
        input.Begin(1,3,true); input.Begin(1,3,true); Check(input.End(1,3,true)==-1,"duplicate down cancels");
        input.Begin(3,5,true); Check(input.End(3,5,true)==5,"fresh tap after cancel works");
        var large=new LinkMatchLayout(388,784,false,6,8);
        Near(large.Margin,19.5,"regular margin"); Near(large.BoardTop,150,"regular board top"); Near(large.BoardHeight,550,"regular board height");
        var small=new LinkMatchLayout(360,640,false,6,8);
        Near(small.Margin,14,"compact margin"); Near(small.BoardTop,120,"compact board top"); Near(small.BoardHeight,450,"compact board height"); Near(small.Cell,308.0/6,"compact target");
        var warning=new LinkMatchLayout(360,640,true,6,8); Near(warning.Cell,48.25,"warning retains48px slots"); Near(warning.BoardTop,160,"warning occupies actual space");
        var scaled=new LinkMatchLayout(720,1280,true,6,8); Near(scaled.Cell,warning.Cell*2,"uniform Unity scale");
        foreach(bool showWarning in new[]{false,true})
        foreach(var dimensions in new[]{new[]{360,640},new[]{388,784},new[]{720,1455}})
        {
            var original=new LinkMatchLayout(dimensions[0],dimensions[1],showWarning,6,8);
            foreach(var grid in new[]{new[]{8,12},new[]{12,16}})
            {
                var dense=new LinkMatchLayout(dimensions[0],dimensions[1],showWarning,grid[0],grid[1]);
                Near(dense.DecorationScale,dense.Cell/original.Cell,"dense decoration follows tile ratio");
                if(grid[0]==12) Near(dense.Cell,original.Cell/2,"192 cell is exactly half existing edge");
                for(int row=0;row<grid[1];row++)for(int col=0;col<grid[0];col++)
                {
                    var x=dense.GridLeft+(col+.5)*dense.Cell;var y=dense.GridTop+(row+.5)*dense.Cell;
                    Check(dense.Hit(x,y)==row*grid[0]+col,"dense center has one owner");
                    if(col+1<grid[0]) {double boundary=dense.GridLeft+(col+1)*dense.Cell;Check(dense.Hit(boundary-.001,y)==row*grid[0]+col,"dense boundary belongs left");Check(dense.Hit(boundary+.001,y)==row*grid[0]+col+1,"dense boundary belongs right");}
                }
            }
        }
        foreach(var layout in new[]{large,small,warning,scaled})
        {
            Check(layout.BoardTop+layout.BoardHeight+layout.ToolGap+layout.ToolHeight+layout.Bottom<=layout.Height+.001,"all content fits");
            for(int row=0;row<8;row++) for(int col=0;col<6;col++)
            {
                double x=layout.GridLeft+(col+.5)*layout.Cell,y=layout.GridTop+(row+.5)*layout.Cell;
                Check(layout.Hit(x,y)==row*6+col,"center hit exact tile");
            }
            Check(layout.Hit(layout.GridLeft-.01,layout.GridTop)==-1,"left border outside");
            Check(layout.Hit(layout.GridLeft+6*layout.Cell,layout.GridTop)==-1,"right border outside");
            Check(layout.Hit(layout.GridLeft,layout.GridTop+8*layout.Cell)==-1,"bottom outside");
            Check(layout.GridLeft-8*layout.Scale>=0,"outer line stays in board");
            Check(layout.Hit(double.NaN,0)==-1,"invalid pointer coordinate");
        }
        Console.WriteLine("LinkMatch input/layout: "+checks+" assertions passed.");
    }
}
