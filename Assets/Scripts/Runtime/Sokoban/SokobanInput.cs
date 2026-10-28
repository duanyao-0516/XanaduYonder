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
    /// <summary>Only a single directional control press can move, once, on its matching release.</summary>
    public sealed class SokobanInput
    {
        private enum Mode { Idle, Control, WaitForRelease }
        private Mode mode;
        private int ownerId;
        private Direction direction;
        public bool HasOwner { get { return mode!=Mode.Idle; } }
        public void Cancel() { mode=Mode.WaitForRelease; }
        public SokobanInputResult Process(bool allowed,params SokobanContact[] contacts)
        {
            contacts=contacts??new SokobanContact[0];
            if(!allowed) return CancelResult(AllReleased(contacts));
            if(contacts.Length==0)
                return mode==Mode.Idle?new SokobanInputResult():CancelResult(true);
            if(mode==Mode.WaitForRelease)
            {
                if(AllReleased(contacts)) { mode=Mode.Idle; return new SokobanInputResult { Completed=true }; }
                return new SokobanInputResult();
            }
            if(contacts.Length!=1) return CancelResult(AllReleased(contacts));
            var contact=contacts[0];
            if(contact.Phase==SokobanContactPhase.Canceled||!Finite(contact.X)||!Finite(contact.Y))
                return CancelResult(AllReleased(contacts));
            if(mode==Mode.Idle)
            {
                if(contact.Phase!=SokobanContactPhase.Began||contact.Region!=SokobanContactRegion.Control)
                    return CancelResult(AllReleased(contacts));
                ownerId=contact.Id; direction=contact.Direction; mode=Mode.Control;
                return new SokobanInputResult();
            }
            if(contact.Id!=ownerId||contact.Phase==SokobanContactPhase.Began)
                return CancelResult(AllReleased(contacts));
            if(contact.Phase!=SokobanContactPhase.Ended) return new SokobanInputResult();
            mode=Mode.Idle;
            if(contact.Region!=SokobanContactRegion.Control||contact.Direction!=direction)
                return new SokobanInputResult { Completed=true,CancelQueued=true };
            return new SokobanInputResult { Direction=direction,Completed=true };
        }
        private SokobanInputResult CancelResult(bool released)
        { mode=released?Mode.Idle:Mode.WaitForRelease; return new SokobanInputResult { CancelQueued=true,Completed=true }; }
        private static bool AllReleased(SokobanContact[] contacts)
        { foreach(var contact in contacts) if(contact.Phase!=SokobanContactPhase.Ended&&contact.Phase!=SokobanContactPhase.Canceled) return false; return true; }
        private static bool Finite(double value) { return !double.IsNaN(value)&&!double.IsInfinity(value); }
    }
}
