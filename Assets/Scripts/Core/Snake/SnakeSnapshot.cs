using System;
using System.Collections.Generic;
namespace XanaduYonder.Core.Snake
{
    /// <summary>Persistence DTO. Validation never repairs a different board into a valid game.</summary>
    public sealed class SnakeSnapshot
    {
        public int[] Body;
        public int Food=-1, FoodEaten, Score;
        public SnakeDirection Direction;
        public SnakeState State;
        public long TickCount;
        public uint RandomState;
        public SnakeSnapshot Copy()
        {
            return new SnakeSnapshot { Body=Body==null?null:(int[])Body.Clone(),Food=Food,FoodEaten=FoodEaten,
                Score=Score,Direction=Direction,State=State,TickCount=TickCount,RandomState=RandomState };
        }
        public bool TryValidate(SnakeRules rules,out string reason)
        {
            reason=null;
            if(rules==null)return SnakeRules.Fail("Missing rules.",out reason);
            if(!rules.TryValidate(out reason))return false;
            if(!SnakeRules.ValidDirection(Direction)||State<SnakeState.Playing||State>SnakeState.WonFullBoard)return SnakeRules.Fail("Unknown direction or state.",out reason);
            if(Body==null||Body.Length<4||Body.Length>rules.Capacity)return SnakeRules.Fail("Invalid body length.",out reason);
            if(FoodEaten<0||FoodEaten>rules.Capacity-4||Body.Length!=4+FoodEaten||Score!=FoodEaten*10)return SnakeRules.Fail("Length, food and score disagree.",out reason);
            if(TickCount<FoodEaten||RandomState==0)return SnakeRules.Fail("Invalid tick count or RNG state.",out reason);
            var obstacles=new HashSet<int>(rules.Obstacles);var occupied=new HashSet<int>();
            for(int i=0;i<Body.Length;i++) {
                int cell=Body[i];
                if(cell<0||cell>=rules.Width*rules.Height||obstacles.Contains(cell)||!occupied.Add(cell))return SnakeRules.Fail("Body is outside board, on obstacle or duplicated.",out reason);
                if(i>0&&!rules.Adjacent(Body[i-1],cell))return SnakeRules.Fail("Body is not contiguous under this boundary.",out reason);
            }
            if(State!=SnakeState.Lost&&rules.NextCell(Body[1],Direction)!=Body[0])return SnakeRules.Fail("Direction does not follow the neck to the head.",out reason);
            if(Food < -1||Food>=rules.Width*rules.Height||(Food>=0&&(occupied.Contains(Food)||obstacles.Contains(Food))))return SnakeRules.Fail("Food must be a free cell or absent at terminal.",out reason);
            bool full=Body.Length==rules.Capacity;
            bool target=rules.Mode==SnakeMode.Campaign&&FoodEaten>=rules.TargetFood;
            if(rules.Mode==SnakeMode.Campaign&&FoodEaten>rules.TargetFood)return SnakeRules.Fail("Campaign food exceeds an earlier terminal target.",out reason);
            if(State==SnakeState.Playing&&(Food<0||full||target))return SnakeRules.Fail("Playing state already ended or has no food.",out reason);
            if(State==SnakeState.WonFullBoard&&(!full||Food!=-1))return SnakeRules.Fail("Full-board win requires full capacity and no food.",out reason);
            if(State==SnakeState.WonTarget&&(!target||full||FoodEaten!=rules.TargetFood||Food!=-1))return SnakeRules.Fail("Target win is inconsistent.",out reason);
            if(State==SnakeState.Lost) {
                if(full||target||TickCount==0||Food!=-1)return SnakeRules.Fail("Loss conflicts with a win, has no tick, or retains food.",out reason);
                int next=rules.NextCell(Body[0],Direction);
                bool collision=next<0||obstacles.Contains(next)||(occupied.Contains(next)&&next!=Body[Body.Length-1]);
                if(!collision)return SnakeRules.Fail("Loss has no collision in its attempted direction.",out reason);
            }
            return true;
        }
    }
}
