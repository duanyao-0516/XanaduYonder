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
    private static SokobanContact Control(int id,SokobanContactPhase phase,Direction direction=Direction.Left)
    { return C(id,10,10,phase,SokobanContactRegion.Control,direction); }
    private static SokobanInput NewInput() { return new SokobanInput(); }
    private static void NoAction(SokobanInputResult result,string name)
    { Check(!result.Direction.HasValue,name+" does not move"); Check(!result.ViewChanged,name+" does not change view"); }
    private static void Rejects(Action action,string name)
    {
        bool invalid=false; try { action(); } catch(ArgumentOutOfRangeException) { invalid=true; }
        Check(invalid,name);
    }
    private static void Main()
    {
        BoardGesturesAreIgnored(); DirectionPressesMoveOnce(); CanceledPressesCannotMove(); AutomaticFitGeometry(); LayoutGeometry();
        Console.WriteLine("Sokoban input/viewport/layout: "+assertions+" assertions passed.");
    }
    private static void BoardGesturesAreIgnored()
    {
        var input=NewInput();
        NoAction(input.Process(true,C(1,100,100,SokobanContactPhase.Began)),"board press");
        NoAction(input.Process(true,C(1,200,100,SokobanContactPhase.Moved)),"board swipe");
        NoAction(input.Process(true,C(1,200,100,SokobanContactPhase.Ended)),"board swipe release");
        Check(!input.HasOwner,"board release clears ownership"); NoAction(input.Process(true),"board release cannot repeat");
        input=NewInput();
        NoAction(input.Process(true,C(1,100,100,SokobanContactPhase.Began),C(2,200,100,SokobanContactPhase.Began)),"two-finger board press");
        NoAction(input.Process(true,C(1,50,100,SokobanContactPhase.Moved),C(2,250,100,SokobanContactPhase.Moved)),"board pinch");
        NoAction(input.Process(true,C(1,50,100,SokobanContactPhase.Ended),C(2,250,100,SokobanContactPhase.Ended)),"board pinch release");
        foreach(var region in new[]{SokobanContactRegion.Board,SokobanContactRegion.Other})
        {
            input=NewInput(); input.Process(true,C(1,10,10,SokobanContactPhase.Began,region));
            NoAction(input.Process(true,Control(1,SokobanContactPhase.Ended)),region+" cannot transfer into control");
            Check(!input.HasOwner,region+" release clears ownership");
        }
    }
    private static void DirectionPressesMoveOnce()
    {
        var input=NewInput();
        foreach(Direction direction in Enum.GetValues(typeof(Direction)))
        {
            NoAction(input.Process(true,Control(10,SokobanContactPhase.Began,direction)),direction+" press waits for release");
            Check(input.HasOwner,direction+" press owns input");
            NoAction(input.Process(true,Control(10,SokobanContactPhase.Stationary,direction)),direction+" hold cannot repeat");
            NoAction(input.Process(true,Control(10,SokobanContactPhase.Moved,direction)),direction+" drag stays pending");
            var release=input.Process(true,Control(10,SokobanContactPhase.Ended,direction));
            Check(release.Direction==direction&&release.Completed&&!release.ViewChanged,direction+" releases one move");
            Check(!input.HasOwner,direction+" release clears ownership"); NoAction(input.Process(true),direction+" idle cannot repeat");
        }
        foreach(var region in new[]{SokobanContactRegion.Board,SokobanContactRegion.Other})
        {
            input=NewInput(); input.Process(true,Control(10,SokobanContactPhase.Began));
            NoAction(input.Process(true,C(10,10,10,SokobanContactPhase.Ended,region)),"control cannot release on "+region);
        }
        input=NewInput(); input.Process(true,Control(10,SokobanContactPhase.Began,Direction.Left));
        NoAction(input.Process(true,Control(10,SokobanContactPhase.Ended,Direction.Right)),"control cannot release on another direction");
    }
    private static void CanceledPressesCannotMove()
    {
        var input=NewInput(); input.Process(true,Control(1,SokobanContactPhase.Began));
        var blocked=input.Process(false,Control(1,SokobanContactPhase.Stationary));
        Check(blocked.CancelQueued,"blocked frame clears queued movement"); NoAction(blocked,"blocked frame");
        NoAction(input.Process(true,Control(1,SokobanContactPhase.Ended)),"modal dismissal cannot deliver stale release");
        input=NewInput(); input.Process(true,Control(1,SokobanContactPhase.Began)); input.Cancel();
        NoAction(input.Process(true,Control(1,SokobanContactPhase.Ended)),"background cancellation suppresses stale release");
        Check(!input.HasOwner,"background stale release clears ownership");
        input=NewInput(); input.Process(true,Control(1,SokobanContactPhase.Began));
        var missing=input.Process(true); NoAction(missing,"missing release");
        Check(missing.CancelQueued&&missing.Completed,"missing release cancels queued movement");
        NoAction(input.Process(true,Control(1,SokobanContactPhase.Ended)),"late release cannot reacquire input");
        input.Process(true); input.Process(true,Control(2,SokobanContactPhase.Began,Direction.Up));
        Check(input.Process(true,Control(2,SokobanContactPhase.Ended,Direction.Up)).Direction==Direction.Up,"fresh tap works after missing release");
        input=NewInput(); input.Process(true,Control(1,SokobanContactPhase.Began));
        var canceled=input.Process(true,Control(1,SokobanContactPhase.Canceled));
        NoAction(canceled,"explicit cancellation"); Check(canceled.CancelQueued,"explicit cancellation clears queued movement");
        NoAction(input.Process(true,Control(1,SokobanContactPhase.Ended)),"canceled release cannot move");
        input=NewInput(); input.Process(true,Control(1,SokobanContactPhase.Began));
        NoAction(input.Process(true,Control(1,SokobanContactPhase.Stationary),Control(2,SokobanContactPhase.Began,Direction.Right)),"second contact cancels control");
        NoAction(input.Process(true,Control(1,SokobanContactPhase.Ended),Control(2,SokobanContactPhase.Stationary,Direction.Right)),"first release waits for all fingers");
        Check(input.HasOwner,"remaining second finger keeps input blocked");
        NoAction(input.Process(true,Control(2,SokobanContactPhase.Ended,Direction.Right)),"remaining finger cannot move");
        Check(!input.HasOwner,"all fingers released clears ownership");
        input.Process(true,Control(3,SokobanContactPhase.Began,Direction.Down));
        Check(input.Process(true,Control(3,SokobanContactPhase.Ended,Direction.Down)).Direction==Direction.Down,"fresh tap works after multitouch");
        input=NewInput();
        NoAction(input.Process(true,Control(1,SokobanContactPhase.Began),Control(2,SokobanContactPhase.Began)),"simultaneous controls cancel");
        NoAction(input.Process(true,Control(1,SokobanContactPhase.Ended),Control(2,SokobanContactPhase.Ended)),"simultaneous controls release without move");
        input=NewInput(); input.Process(true,Control(1,SokobanContactPhase.Began));
        NoAction(input.Process(true,Control(2,SokobanContactPhase.Ended)),"different pointer cannot release owner");
        NoAction(input.Process(true,Control(1,SokobanContactPhase.Ended)),"original owner cannot survive pointer replacement");
        input=NewInput(); input.Process(true,Control(1,SokobanContactPhase.Began));
        NoAction(input.Process(true,Control(1,SokobanContactPhase.Began)),"duplicate press cancels owner");
        NoAction(input.Process(true,Control(1,SokobanContactPhase.Ended)),"duplicate press release cannot move");
        foreach(double coordinate in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
        {
            input=NewInput(); input.Process(true,Control(1,SokobanContactPhase.Began));
            NoAction(input.Process(true,C(1,coordinate,10,SokobanContactPhase.Ended,SokobanContactRegion.Control,Direction.Left)),"invalid coordinate release");
        }
    }
    private static void AutomaticFitGeometry()
    {
        foreach(var size in new[]{new[]{8,13},new[]{13,8},new[]{8,8},new[]{1,1}})
        {
            var view=new SokobanViewport(size[0],size[1],648,800); AssertFit(view,648,800,0,"initial "+size[0]+"x"+size[1]);
            view.Resize(720,500); AssertFit(view,720,500,0,"resized "+size[0]+"x"+size[1]);
            view=new SokobanViewport(size[0],size[1],648,800,24); AssertFit(view,648,800,24,"padded "+size[0]+"x"+size[1]);
            view.Resize(720,500); AssertFit(view,720,500,24,"resize preserves padding "+size[0]+"x"+size[1]);
            view.Resize(500,720,16); AssertFit(view,500,720,16,"resize updates padding "+size[0]+"x"+size[1]);
        }
        Rejects(()=>new SokobanViewport(0,8,640,640),"zero columns reject");
        Rejects(()=>new SokobanViewport(8,-1,640,640),"negative rows reject");
        var v=new SokobanViewport(8,8,640,640);
        foreach(double invalid in new[]{0.0,-1,double.NaN,double.PositiveInfinity,double.NegativeInfinity})
        {
            Rejects(()=>v.Resize(invalid,640),"invalid width rejects"); Rejects(()=>v.Resize(640,invalid),"invalid height rejects");
            AssertFit(v,640,640,0,"invalid resize preserves geometry");
        }
        foreach(double invalid in new[]{-1.0,320,double.NaN,double.PositiveInfinity,double.NegativeInfinity})
        {
            Rejects(()=>new SokobanViewport(8,8,640,640,invalid),"invalid initial padding rejects");
            Rejects(()=>v.Resize(640,640,invalid),"invalid resized padding rejects");
            AssertFit(v,640,640,0,"invalid padding preserves geometry");
        }
        var padded=new SokobanViewport(8,8,640,640,24);
        Rejects(()=>padded.Resize(48,640),"resize rejects no inner width");
        Rejects(()=>padded.Resize(640,48),"resize rejects no inner height");
        AssertFit(padded,640,640,24,"invalid inner size preserves geometry");
        foreach(string method in new[]{"Restore","Reset","StepZoom","Pan","Transform"})
            Check(typeof(SokobanViewport).GetMethod(method)==null,"automatic view has no manual "+method+" API");
    }
    private static void AssertFit(SokobanViewport view,double width,double height,double padding,string name)
    {
        Near(view.CellSize,Math.Min((width-2*padding)/view.Columns,(height-2*padding)/view.Rows),name+" cell size");
        Near(view.Padding,padding,name+" padding");
        Near(view.OffsetX,(width-view.Columns*view.CellSize)/2,name+" centered x"); Near(view.OffsetY,(height-view.Rows*view.CellSize)/2,name+" centered y");
        Near(view.Zoom,1,name+" zoom"); Near(view.CenterX,.5,name+" center x"); Near(view.CenterY,.5,name+" center y");
        Check(view.OffsetX+1e-5>=padding&&view.OffsetY+1e-5>=padding,name+" board starts inside padding");
        Check(view.OffsetX+view.Columns*view.CellSize<=width-padding+1e-5&&view.OffsetY+view.Rows*view.CellSize<=height-padding+1e-5,name+" board fully visible");
    }
    private static void LayoutGeometry()
    {
        foreach(double height in new[]{1150.0,1320.0,784.0*720/388,1582.0})
        {
            var l=new SokobanLayout(720,height,false); Near(l.BoardWidth,349.0*720/388,"prototype board width");
            Near(l.ControlsTop+l.ControlsHeight,height,"bottom fixed to safe usable edge");
            Check(l.BoardHeight>0&&l.BoardTop+l.BoardHeight<l.ControlsTop,"board fills remaining height");
            var warning=new SokobanLayout(720,height,true); Check(warning.BoardHeight<l.BoardHeight,"warning reserves board height");
        }
        foreach(double dpWidth in new[]{320.0,360.0,388.0,432.0})
        {
            double scale=720.0/388,minimum=SokobanLayout.MinimumTarget(720,dpWidth);
            foreach(string name in new[]{"Home","Book","Settings","Undo","Restart","Move Up","Move Down","Move Left","Move Right"})
            {
                bool action=name=="Undo"||name=="Restart",move=name.StartsWith("Move");
                double width=(action?166:move?48:44)*scale,height=(action||move?48:44)*scale;
                var hit=SokobanLayout.HitBounds(name,width,height,scale,minimum);
                Near(hit.X,0,"compact target starts at visible x "+name); Near(hit.Y,0,"compact target starts at visible y "+name);
                Near(hit.Width,width,"compact target keeps visible width "+name); Near(hit.Height,height,"compact target keeps visible height "+name);
            }
            var undo=SokobanLayout.HitBounds("Undo",166*scale,48*scale,scale,minimum);
            var restart=SokobanLayout.HitBounds("Restart",166*scale,48*scale,scale,minimum);
            Check(24*scale+undo.Y+undo.Height<80*scale+restart.Y,"stacked action targets keep their gap");
            var up=SokobanLayout.HitBounds("Move Up",48*scale,48*scale,scale,minimum);
            var left=SokobanLayout.HitBounds("Move Left",48*scale,48*scale,scale,minimum);
            Check(197*scale+left.X+left.Width<249*scale+up.X,"diagonal direction targets keep their gap");
        }
    }
}
