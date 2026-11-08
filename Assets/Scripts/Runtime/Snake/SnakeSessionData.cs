using System;
using XanaduYonder.Core.Snake;
namespace XanaduYonder.Runtime.Snake
{
    public sealed class SnakeSessionData
    {
        public string SessionId;
        public SnakeSnapshot Board;
        public double ElapsedSeconds;
        public bool ResultRecorded;
        public SnakeSessionData Copy()
        {
            return new SnakeSessionData { SessionId=SessionId,Board=Board==null?null:Board.Copy(),
                ElapsedSeconds=ElapsedSeconds,ResultRecorded=ResultRecorded };
        }
        public bool TryValidate(SnakeRules rules,out string reason)
        {
            reason=null;
            if(!Guid.TryParseExact(SessionId,"D",out var id)||id==Guid.Empty||id.ToString("D")!=SessionId){reason="Session ID must be a canonical nonempty GUID.";return false;}
            if(Board==null){reason="Missing board.";return false;}
            if(!Board.TryValidate(rules,out reason))return false;
            if(double.IsNaN(ElapsedSeconds)||double.IsInfinity(ElapsedSeconds)||ElapsedSeconds<0){reason="Invalid effective elapsed seconds.";return false;}
            if(ResultRecorded&&Board.State==SnakeState.Playing){reason="An unfinished game cannot have a recorded result.";return false;}
            return true;
        }
    }
}
