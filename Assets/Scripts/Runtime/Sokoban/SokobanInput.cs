using System;
using XanaduYonder.Core;

namespace XanaduYonder.Runtime.Sokoban
{
    public enum SokobanContactPhase { Began, Moved, Stationary, Ended, Canceled }
    public enum SokobanContactRegion { Board, Control, Other }
    public struct SokobanContact
    {
        public readonly int Id;
        public readonly double X,Y;
        public readonly SokobanContactPhase Phase;
        public readonly SokobanContactRegion Region;
        public readonly Direction Direction;
        public SokobanContact(int id,double x,double y,SokobanContactPhase phase,SokobanContactRegion region= SokobanContactRegion.Board,Direction direction=Direction.Up)
        { Id=id; X=x; Y=y; Phase=phase; Region=region; Direction=direction; }
    }
    public struct SokobanInputResult
    {
        public Direction? Direction;
        public bool ViewChanged, Completed, CancelQueued;
    }
    /// <summary>One owner from press to release. Multitouch never releases a pending character move.</summary>
    public sealed class SokobanInput
    {
        private enum Mode { Idle, Swipe, Pan, Control, Pinch, WaitForRelease }
        private readonly SokobanViewport viewport;
        private Mode mode;
        private int firstId,secondId;
        private double startX,startY,lastX,lastY,lastDistance;
        private Direction direction;
        public double SwipeThreshold { get; set; }
        public bool HasOwner { get { return mode!=Mode.Idle; } }
        public SokobanInput(SokobanViewport view) { viewport=view; SwipeThreshold=28; }
        public void Cancel() { mode=Mode.WaitForRelease; }
        public SokobanInputResult Process(bool allowed,params SokobanContact[] contacts)
        {
            var result=new SokobanInputResult(); contacts=contacts??new SokobanContact[0];
            if(!allowed) { mode=contacts.Length==0||AllReleased(contacts)?Mode.Idle:Mode.WaitForRelease; result.CancelQueued=true; return result; }
            if(contacts.Length==0) { result.Completed=mode!=Mode.Idle; mode=Mode.Idle; return result; }
            if(mode==Mode.WaitForRelease) { if(AllReleased(contacts)) mode=Mode.Idle; return result; }
            if(contacts.Length>2) return CancelResult(AllReleased(contacts));
            for(int i=0;i<contacts.Length;i++) if(contacts[i].Phase==SokobanContactPhase.Canceled||!Finite(contacts[i].X)||!Finite(contacts[i].Y)) return CancelResult(AllReleased(contacts));
            if(mode==Mode.Idle)
            {
                if(contacts.Length==2&&contacts[0].Phase==SokobanContactPhase.Began&&contacts[1].Phase==SokobanContactPhase.Began&&
                    contacts[0].Region==SokobanContactRegion.Board&&contacts[1].Region==SokobanContactRegion.Board)
                {
                    firstId=contacts[0].Id; secondId=contacts[1].Id; mode=Mode.Pinch;
                    lastX=(contacts[0].X+contacts[1].X)/2; lastY=(contacts[0].Y+contacts[1].Y)/2; lastDistance=Distance(contacts[0],contacts[1]); result.CancelQueued=true; return result;
                }
                if(contacts.Length!=1||contacts[0].Phase!=SokobanContactPhase.Began) return CancelResult();
                var c=contacts[0]; firstId=c.Id; startX=lastX=c.X; startY=lastY=c.Y;
                if(c.Region==SokobanContactRegion.Board) mode=viewport.Zoom>1.00001?Mode.Pan:Mode.Swipe;
                else if(c.Region==SokobanContactRegion.Control) { mode=Mode.Control; direction=c.Direction; }
                else return CancelResult();
                return result;
            }
            if(contacts.Length==2)
            {
                if(mode==Mode.Control) return CancelResult();
                SokobanContact a,b;
                if(mode!=Mode.Pinch)
                {
                    if(contacts[0].Id==firstId) { a=contacts[0]; b=contacts[1]; } else if(contacts[1].Id==firstId) { a=contacts[1]; b=contacts[0]; } else return CancelResult();
                    if(b.Phase!=SokobanContactPhase.Began||b.Region!=SokobanContactRegion.Board||Ended(a)||Ended(b)) return CancelResult();
                    secondId=b.Id; mode=Mode.Pinch; lastX=(a.X+b.X)/2; lastY=(a.Y+b.Y)/2; lastDistance=Distance(a,b); result.CancelQueued=true; return result;
                }
                if(contacts[0].Id==firstId&&contacts[1].Id==secondId) { a=contacts[0]; b=contacts[1]; }
                else if(contacts[1].Id==firstId&&contacts[0].Id==secondId) { a=contacts[1]; b=contacts[0]; }
                else return CancelResult();
                if(Ended(a)||Ended(b)) { result.Completed=true; mode=AllReleased(contacts)?Mode.Idle:Mode.WaitForRelease; return result; }
                double x=(a.X+b.X)/2,y=(a.Y+b.Y)/2,d=Distance(a,b);
                viewport.Transform(lastX,lastY,x,y,lastDistance>1&&d>1?d/lastDistance:1);
                result.ViewChanged=true; lastX=x; lastY=y; lastDistance=d; return result;
            }
            var one=contacts[0];
            if(mode==Mode.Pinch) { result.Completed=true; mode=AllReleased(contacts)?Mode.Idle:Mode.WaitForRelease; return result; }
            if(one.Id!=firstId) return CancelResult();
            if(mode==Mode.Pan)
            { viewport.Pan(one.X-lastX,one.Y-lastY); result.ViewChanged=one.X!=lastX||one.Y!=lastY; lastX=one.X; lastY=one.Y; }
            if(Ended(one))
            {
                if(mode==Mode.Swipe)
                {
                    double dx=one.X-startX,dy=one.Y-startY;
                    if(Math.Max(Math.Abs(dx),Math.Abs(dy))>=SwipeThreshold)
                        result.Direction=Math.Abs(dx)>=Math.Abs(dy)?(dx>0?Core.Direction.Right:Core.Direction.Left):(dy>0?Core.Direction.Down:Core.Direction.Up);
                }
                else if(mode==Mode.Control&&one.Region==SokobanContactRegion.Control&&one.Direction==direction) result.Direction=direction;
                result.Completed=true; mode=Mode.Idle;
            }
            return result;
        }
        private SokobanInputResult CancelResult(bool released=false) { mode=released?Mode.Idle:Mode.WaitForRelease; return new SokobanInputResult { CancelQueued=true,Completed=true }; }
        private static bool Ended(SokobanContact c) { return c.Phase==SokobanContactPhase.Ended; }
        private static bool AllReleased(SokobanContact[] contacts)
        { foreach(var contact in contacts) if(contact.Phase!=SokobanContactPhase.Ended&&contact.Phase!=SokobanContactPhase.Canceled) return false; return true; }
        private static double Distance(SokobanContact a,SokobanContact b) { double x=a.X-b.X,y=a.Y-b.Y; return Math.Sqrt(x*x+y*y); }
        private static bool Finite(double v) { return !double.IsNaN(v)&&!double.IsInfinity(v); }
    }
}
