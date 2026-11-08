using System;
using System.Collections.Generic;
namespace XanaduYonder.Core.Snake
{
    public enum SnakeBoundary { Wrap, Solid }
    public enum SnakeDirection { Up, Right, Down, Left }
    public enum SnakeMode { Classic, Campaign }
    public enum SnakeState { Playing, Lost, WonTarget, WonFullBoard }

    /// <summary>Mutable configuration boundary. SnakeGame validates and owns a deep copy.</summary>
    public sealed class SnakeRules
    {
        public int Width=20, Height=24, Difficulty=1, TargetFood, InitialTickMs=200, MinimumTickMs=100;
        public SnakeMode Mode;
        public SnakeBoundary Boundary;
        public string LevelId;
        public int[] Obstacles=new int[0], InitialBody={406,405,404,403};
        public int Capacity { get { return Width*Height-(Obstacles==null?0:Obstacles.Length); } }

        public static SnakeRules Classic(int difficulty,SnakeBoundary boundary)
        {
            if(difficulty<0||difficulty>2)throw new ArgumentOutOfRangeException(nameof(difficulty));
            if(boundary!=SnakeBoundary.Wrap&&boundary!=SnakeBoundary.Solid)throw new ArgumentOutOfRangeException(nameof(boundary));
            return new SnakeRules { Difficulty=difficulty, Boundary=boundary,
                InitialTickMs=new[]{240,200,160}[difficulty], MinimumTickMs=new[]{160,100,80}[difficulty] };
        }
        public SnakeRules Copy()
        {
            return new SnakeRules { Width=Width,Height=Height,Difficulty=Difficulty,TargetFood=TargetFood,
                InitialTickMs=InitialTickMs,MinimumTickMs=MinimumTickMs,Mode=Mode,Boundary=Boundary,LevelId=LevelId,
                Obstacles=Obstacles==null?null:(int[])Obstacles.Clone(),InitialBody=InitialBody==null?null:(int[])InitialBody.Clone() };
        }
        public bool TryValidate(out string reason)
        {
            reason=null;
            if(Width<2||Width>64||Height<2||Height>64)return Fail("Board dimensions must be 2..64.",out reason);
            if(Mode!=SnakeMode.Classic&&Mode!=SnakeMode.Campaign)return Fail("Unknown mode.",out reason);
            if(Boundary!=SnakeBoundary.Wrap&&Boundary!=SnakeBoundary.Solid)return Fail("Unknown boundary.",out reason);
            if(Difficulty<0||Difficulty>2)return Fail("Unknown difficulty.",out reason);
            if(InitialTickMs<1||InitialTickMs>60000||MinimumTickMs<1||MinimumTickMs>InitialTickMs)return Fail("Invalid tick range.",out reason);
            if(Obstacles==null||InitialBody==null||InitialBody.Length!=4)return Fail("Missing obstacles or four-cell initial body.",out reason);
            var blocked=new HashSet<int>();
            foreach(int cell in Obstacles)if(cell<0||cell>=Width*Height||!blocked.Add(cell))return Fail("Obstacle outside board or duplicated.",out reason);
            if(Capacity<4)return Fail("Insufficient capacity.",out reason);
            if(Mode==SnakeMode.Classic&&(TargetFood!=0||!string.IsNullOrEmpty(LevelId)))return Fail("Classic has no target or level ID.",out reason);
            if(Mode==SnakeMode.Campaign&&(string.IsNullOrWhiteSpace(LevelId)||LevelId.Length>64||TargetFood<1||TargetFood>Capacity-4))return Fail("Invalid campaign target or level ID.",out reason);
            var initial=new HashSet<int>();
            for(int i=0;i<InitialBody.Length;i++) {
                int cell=InitialBody[i];
                if(cell<0||cell>=Width*Height||blocked.Contains(cell)||!initial.Add(cell))return Fail("Invalid initial body occupancy.",out reason);
                if(i>0&&!Adjacent(InitialBody[i-1],cell))return Fail("Initial body is not contiguous.",out reason);
            }
            if(NextCell(InitialBody[1],SnakeDirection.Right)!=InitialBody[0])return Fail("Initial head and neck must face right.",out reason);
            var reachable=new HashSet<int>{InitialBody[0]};var queue=new Queue<int>();queue.Enqueue(InitialBody[0]);
            while(queue.Count>0) {
                int cell=queue.Dequeue();
                for(int d=0;d<4;d++){int next=NextCell(cell,(SnakeDirection)d);if(next>=0&&!blocked.Contains(next)&&reachable.Add(next))queue.Enqueue(next);}
            }
            if(reachable.Count!=Capacity)return Fail("Non-obstacle cells are disconnected.",out reason);
            return true;
        }
        internal bool Adjacent(int a,int b)
        {
            for(int d=0;d<4;d++)if(NextCell(a,(SnakeDirection)d)==b)return true;
            return false;
        }
        internal int NextCell(int cell,SnakeDirection direction)
        {
            int x=cell%Width,y=cell/Width;
            switch(direction){case SnakeDirection.Up:y--;break;case SnakeDirection.Right:x++;break;case SnakeDirection.Down:y++;break;case SnakeDirection.Left:x--;break;default:throw new ArgumentOutOfRangeException(nameof(direction));}
            if(Boundary==SnakeBoundary.Wrap){x=(x+Width)%Width;y=(y+Height)%Height;}
            else if(x<0||x>=Width||y<0||y>=Height)return -1;
            return y*Width+x;
        }
        internal static bool ValidDirection(SnakeDirection value){return value>=SnakeDirection.Up&&value<=SnakeDirection.Left;}
        internal static bool Reverse(SnakeDirection a,SnakeDirection b){return ((int)a+2)%4==(int)b;}
        internal static bool Fail(string text,out string reason){reason=text;return false;}
    }
}
