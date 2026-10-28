using System;
using System.Collections.Generic;
namespace XanaduYonder.Core.Snake
{
    /// <summary>Deterministic integer-grid rules; no Unity objects, clocks or global random source.</summary>
    public sealed class SnakeGame
    {
        private readonly SnakeRules rules;
        private readonly SnakeSnapshot state;
        private readonly HashSet<int> obstacles;
        public SnakeGame(SnakeRules rules,uint seed)
        {
            this.rules=ValidateRules(rules);obstacles=new HashSet<int>(this.rules.Obstacles);
            state=new SnakeSnapshot { Body=(int[])this.rules.InitialBody.Clone(),Direction=SnakeDirection.Right,
                RandomState=seed==0?0x6D2B79F5u:seed,State=SnakeState.Playing };
            if(state.Body.Length==this.rules.Capacity)state.State=SnakeState.WonFullBoard;
            else GenerateFood();
        }
        public SnakeGame(SnakeRules rules,SnakeSnapshot snapshot)
        {
            this.rules=ValidateRules(rules);
            if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            if(!snapshot.TryValidate(this.rules,out var reason))throw new ArgumentException(reason,nameof(snapshot));
            state=snapshot.Copy();obstacles=new HashSet<int>(this.rules.Obstacles);
        }
        private static SnakeRules ValidateRules(SnakeRules value)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            if(!value.TryValidate(out var reason))throw new ArgumentException(reason,nameof(value));
            return value.Copy();
        }
        public SnakeRules Rules { get { return rules.Copy(); } }
        public SnakeState State { get { return state.State; } }
        public int[] Body { get { return (int[])state.Body.Clone(); } }
        public int Food { get { return state.Food; } }
        public int FoodEaten { get { return state.FoodEaten; } }
        public int Score { get { return state.Score; } }
        public long TickCount { get { return state.TickCount; } }
        public uint RandomState { get { return state.RandomState; } }
        public SnakeDirection Direction { get { return state.Direction; } }
        public int CurrentTickMilliseconds { get { return rules.Mode==SnakeMode.Campaign?rules.InitialTickMs:Math.Max(rules.MinimumTickMs,rules.InitialTickMs-(state.FoodEaten/5)*5); } }
        public SnakeSnapshot GetSnapshot(){return state.Copy();}

        /// <summary>Returns false for a terminal no-op; a collision still consumes one logical tick.</summary>
        public bool Step(SnakeDirection direction)
        {
            if(state.State!=SnakeState.Playing)return false;
            if(!SnakeRules.ValidDirection(direction))throw new ArgumentOutOfRangeException(nameof(direction));
            if(!SnakeRules.Reverse(direction,state.Direction))state.Direction=direction;
            int next=rules.NextCell(state.Body[0],state.Direction);
            bool growing=next==state.Food;
            if(state.TickCount<long.MaxValue)state.TickCount++;
            int occupiedLength=state.Body.Length-(growing?0:1);
            bool collision=next<0||obstacles.Contains(next);
            for(int i=0;i<occupiedLength&&!collision;i++)collision=state.Body[i]==next;
            if(collision){state.State=SnakeState.Lost;state.Food=-1;return true;}
            var moved=new int[state.Body.Length+(growing?1:0)];moved[0]=next;
            Array.Copy(state.Body,0,moved,1,moved.Length-1);state.Body=moved;
            if(growing){state.FoodEaten++;state.Score=state.FoodEaten*10;state.Food=-1;}
            if(state.Body.Length==rules.Capacity){state.State=SnakeState.WonFullBoard;state.Food=-1;return true;}
            if(rules.Mode==SnakeMode.Campaign&&state.FoodEaten>=rules.TargetFood){state.State=SnakeState.WonTarget;state.Food=-1;return true;}
            if(growing)GenerateFood();
            return true;
        }
        private void GenerateFood()
        {
            var occupied=new HashSet<int>(state.Body);var free=new List<int>();
            for(int cell=0;cell<rules.Width*rules.Height;cell++)if(!occupied.Contains(cell)&&!obstacles.Contains(cell))free.Add(cell);
            if(free.Count==0)throw new InvalidOperationException("Food generation requires a nonterminal free cell.");
            // xorshift32 visits 1..uint.MaxValue; subtract one to obtain a uniform 0..uint.MaxValue-1 domain.
            // Rejection samples only an index range, never repeatedly guesses board cells.
            uint count=(uint)free.Count,limit=uint.MaxValue-(uint.MaxValue%count),sample;
            do{sample=NextRandom()-1;}while(sample>=limit);
            state.Food=free[(int)(sample%count)];
        }
        private uint NextRandom()
        {
            uint value=state.RandomState;value^=value<<13;value^=value>>17;value^=value<<5;state.RandomState=value;return value;
        }
    }
}
