using System;

namespace XanaduYonder.Runtime
{
    // Presentation state only; gameplay and result accounting remain with each game.
    internal sealed class ResultPresentationState
    {
        private enum Phase { Empty, Waiting, Ready, Presented, Inspecting }
        private Phase phase;
        private object identity;
        private double elapsed;
        private bool skipResumedFrame;
        public bool IsInspecting { get { return phase==Phase.Inspecting; } }
        public bool Request(object nextIdentity)
        {
            if(nextIdentity==null)throw new ArgumentNullException(nameof(nextIdentity));
            if(!ReferenceEquals(identity,nextIdentity))
            { identity=nextIdentity;phase=Phase.Waiting;elapsed=0; }
            if(phase!=Phase.Ready&&phase!=Phase.Presented)return false;
            phase=Phase.Presented;return true;
        }
        public bool Advance(double seconds,bool available)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(phase!=Phase.Waiting)return false;
            if(!available){elapsed=0;return false;}
            if(skipResumedFrame){skipResumedFrame=false;return false;}
            elapsed+=seconds;
            if(elapsed+1e-9<.8)return false;
            phase=Phase.Ready;return true;
        }
        public void Inspect(){if(phase!=Phase.Empty)phase=Phase.Inspecting;}
        public void Reopen(){if(phase==Phase.Inspecting)phase=Phase.Ready;}
        public void Interrupt(){elapsed=0;skipResumedFrame=true;}
        public void Reset(){identity=null;phase=Phase.Empty;elapsed=0;skipResumedFrame=false;}
    }
}
