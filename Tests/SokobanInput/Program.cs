using System;
using XanaduYonder.Core;
using XanaduYonder.Runtime.Sokoban;

internal static class Program
{
    private static int assertions;
    private static void Check(bool value,string name) { assertions++; if(!value) throw new Exception(name); }
    private static void Near(double actual,double expected,string name) { Check(Math.Abs(actual-expected)<.00001,name+": "+actual+" != "+expected); }
    private static SokobanContact C(int id,double x,double y,SokobanContactPhase phase,SokobanContactRegion region=SokobanContactRegion.Board,Direction direction=Direction.Up)
    { return new SokobanContact(id,x,y,phase,region,direction); }
    private static void Main()
    {
        var v=new SokobanViewport(8,13,648,800);
        Near(v.CellSize,800.0/13,"tall map fit"); Near(v.OffsetY,0,"tall map fills height");
        Near(v.OffsetX,(648-8*v.CellSize)/2,"narrow axis centered");
        v.StepZoom(1); Near(v.Zoom,1.25,"quarter zoom"); v.Pan(10000,-10000);
        Near(v.OffsetX,(648-8*v.CellSize)/2,"undersized axis stays centered"); Near(v.OffsetY,800-13*v.CellSize,"vertical pan clamps");
        v.Reset(); Near(v.Zoom,1,"reset zoom"); Near(v.CenterX,.5,"reset x"); Near(v.CenterY,.5,"reset y");
        v.Restore(2,.5,.5); double center=v.CenterY; v.Resize(720,900); Near(v.CenterY,center,"resize preserves normalized center");
        double oldX=(200-v.OffsetX)/v.CellSize,oldY=(300-v.OffsetY)/v.CellSize;
        v.Transform(200,300,210,310,.8); Near((210-v.OffsetX)/v.CellSize,oldX,"pinch anchor x"); Near((310-v.OffsetY)/v.CellSize,oldY,"pinch anchor y");
        v.Restore(9,-2,3); Near(v.Zoom,2,"restored zoom clamps"); Check(v.CenterX>=0&&v.CenterY<=1,"restored center clamps");
        bool invalid=false; try { v.Restore(double.NaN,0,0); } catch(ArgumentOutOfRangeException) { invalid=true; } Check(invalid,"nonfinite view rejects");
        v=new SokobanViewport(8,8,640,640); var input=new SokobanInput(v);
        input.Process(true,C(1,100,100,SokobanContactPhase.Began));
        Check(!input.Process(true,C(1,200,100,SokobanContactPhase.Moved)).Direction.HasValue,"swipe waits for release");
        Check(input.Process(true,C(1,200,100,SokobanContactPhase.Ended)).Direction==Direction.Right,"fit swipe one move");
        Check(!input.Process(true).Direction.HasValue,"release cannot repeat");
        input.Process(true,C(1,100,100,SokobanContactPhase.Began)); input.Process(true,C(1,200,100,SokobanContactPhase.Ended));
        input.Process(true,C(2,100,100,SokobanContactPhase.Began));
        Check(input.Process(true,C(2,100,200,SokobanContactPhase.Ended)).Direction==Direction.Down,"new touch can start after every previous contact ended");
        input.Process(true,C(1,100,100,SokobanContactPhase.Began));
        input.Process(true,C(1,180,100,SokobanContactPhase.Moved),C(2,300,100,SokobanContactPhase.Began));
        Check(!input.Process(true,C(1,250,100,SokobanContactPhase.Ended),C(2,350,100,SokobanContactPhase.Ended)).Direction.HasValue,"second finger cancels swipe"); input.Process(true);
        v.Restore(1.5,.5,.5); input.Process(true,C(1,200,200,SokobanContactPhase.Began));
        Check(input.Process(true,C(1,240,240,SokobanContactPhase.Moved)).ViewChanged,"zoom drag pans");
        Check(!input.Process(true,C(1,240,240,SokobanContactPhase.Ended)).Direction.HasValue,"zoom drag never moves"); input.Process(true);
        v.Reset(); input.Process(true,C(1,200,200,SokobanContactPhase.Began)); input.Cancel();
        Check(!input.Process(true,C(1,350,200,SokobanContactPhase.Ended)).Direction.HasValue,"lifecycle cancel suppresses stale release"); input.Process(true);
        input.Process(true,C(1,100,100,SokobanContactPhase.Began));
        input.Process(true,C(1,100,100,SokobanContactPhase.Stationary),C(2,200,100,SokobanContactPhase.Began));
        var pinch=input.Process(true,C(1,50,100,SokobanContactPhase.Moved),C(2,250,100,SokobanContactPhase.Moved));
        Check(pinch.ViewChanged&&!pinch.Direction.HasValue,"pinch changes only viewport"); Near(v.Zoom,2,"pinch zoom clamp");
        input.Process(true,C(1,50,100,SokobanContactPhase.Stationary),C(2,250,100,SokobanContactPhase.Stationary),C(3,300,100,SokobanContactPhase.Began));
        Check(!input.Process(true,C(1,50,100,SokobanContactPhase.Ended)).Direction.HasValue,"third contact cancels"); input.Process(true);
        input.Process(true,C(10,0,0,SokobanContactPhase.Began,SokobanContactRegion.Control,Direction.Left));
        Check(input.Process(true,C(10,0,0,SokobanContactPhase.Ended,SokobanContactRegion.Control,Direction.Left)).Direction==Direction.Left,"dpad normal release once"); input.Process(true);
        input.Process(true,C(10,0,0,SokobanContactPhase.Began,SokobanContactRegion.Control,Direction.Left));
        Check(!input.Process(true,C(10,100,100,SokobanContactPhase.Ended,SokobanContactRegion.Board)).Direction.HasValue,"dpad cannot transfer into board"); input.Process(true);
        input.Process(true,C(10,0,0,SokobanContactPhase.Began,SokobanContactRegion.Other));
        Check(!input.Process(true,C(10,100,100,SokobanContactPhase.Ended)).Direction.HasValue,"outside contact cannot acquire board"); input.Process(true);
        input.Process(true,C(1,100,100,SokobanContactPhase.Began)); input.Process(false,C(1,200,100,SokobanContactPhase.Moved));
        Check(!input.Process(true,C(1,250,100,SokobanContactPhase.Ended)).Direction.HasValue,"blocked frame cancels");
        input.Process(true); v.Reset();
        input.Process(true,C(1,100,100,SokobanContactPhase.Began),C(2,200,100,SokobanContactPhase.Began));
        Check(input.Process(true,C(1,50,100,SokobanContactPhase.Moved),C(2,250,100,SokobanContactPhase.Moved)).ViewChanged,"simultaneous two-finger start owns pinch");
        foreach(double height in new[]{1320.0,784.0*720/388,1582.0})
        {
            var l=new SokobanLayout(720,height,false); Near(l.BoardWidth,349.0*720/388,"prototype board width");
            Near(l.ControlsTop+l.ControlsHeight,height,"bottom fixed to safe usable edge");
            Check(l.BoardHeight>0&&l.BoardTop+l.BoardHeight<l.ControlsTop,"board fills remaining height");
            var warning=new SokobanLayout(720,height,true); Check(warning.BoardHeight<l.BoardHeight,"warning reserves board height");
        }
        foreach(double dpWidth in new[]{320.0,360.0,388.0,432.0})
        {
            double s=720.0/388,min=SokobanLayout.MinimumTarget(720,dpWidth);
            foreach(string name in new[]{"Home","Book","Settings","Zoom out","Zoom in","Fit","Move Up","Move Down"})
            {
                double width=(name=="Fit"?74:name.StartsWith("Move")?48:44)*s;
                var hit=SokobanLayout.HitBounds(name,width,(name.StartsWith("Move")?48:44)*s,s,min);
                Check(hit.Width+1e-5>=min&&hit.Height+1e-5>=min,"48dp target "+dpWidth+" "+name);
            }
            var book=SokobanLayout.HitBounds("Book",44*s,44*s,s,min); var settings=SokobanLayout.HitBounds("Settings",44*s,44*s,s,min);
            Check(book.X+book.Width<=48*s+settings.X+1e-5,"neighbor navigation targets do not overlap");
            var undo=SokobanLayout.HitBounds("Undo",79*s,48*s,s,min); var zoom=SokobanLayout.HitBounds("Zoom out",44*s,44*s,s,min);
            Check(24*s+undo.Y+undo.Height<=84*s+zoom.Y,"action and zoom targets do not overlap");
        }
        Console.WriteLine("Sokoban input/viewport/layout: "+assertions+" assertions passed.");
    }
}
