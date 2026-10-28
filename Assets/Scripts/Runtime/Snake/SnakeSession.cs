using System;
using System.Collections.Generic;
using XanaduYonder.Core.Snake;
namespace XanaduYonder.Runtime.Snake
{
    /// <summary>Owns tick accumulation and input gates. Its caller supplies foreground frame deltas only.</summary>
    public sealed class SnakeSession
    {
        private const double Epsilon=1e-10;
        private readonly List<SnakeDirection> directions=new List<SnakeDirection>(2);
        private readonly double minimumTickSeconds;
        private double accumulator;
        public SnakeSession(SnakeGame game)
        {
            if(game==null)throw new ArgumentNullException(nameof(game));
            Game=game;minimumTickSeconds=game.Rules.MinimumTickMs/1000.0;SessionId=Guid.NewGuid().ToString("D");Paused=true;
        }
        public SnakeSession(SnakeRules rules,SnakeSessionData data)
        {
            if(data==null)throw new ArgumentNullException(nameof(data));
            if(!data.TryValidate(rules,out var reason))throw new ArgumentException(reason,nameof(data));
            Game=new SnakeGame(rules,data.Board);minimumTickSeconds=Game.Rules.MinimumTickMs/1000.0;SessionId=data.SessionId;ElapsedSeconds=data.ElapsedSeconds;
            ResultRecorded=data.ResultRecorded;Paused=true;
        }
        public SnakeGame Game { get; private set; }
        public string SessionId { get; private set; }
        public bool Paused { get; private set; }
        public bool CanPlay { get { return !Paused&&CountdownSeconds<=0&&Game.State==SnakeState.Playing; } }
        public double CountdownSeconds { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public bool ResultRecorded { get; private set; }

        public bool QueueDirection(SnakeDirection direction)
        {
            if(!CanPlay||direction<SnakeDirection.Up||direction>SnakeDirection.Left||directions.Count==2)return false;
            var previous=directions.Count==0?Game.Direction:directions[directions.Count-1];
            if(direction==previous||((int)previous+2)%4==(int)direction)return false;
            directions.Add(direction);return true;
        }
        public int Advance(double seconds)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(Paused||Game.State!=SnakeState.Playing)return 0;
            if(seconds>.25){Pause();return 0;}
            if(CountdownSeconds>0){CountdownSeconds=Math.Max(0,CountdownSeconds-seconds);if(CountdownSeconds<Epsilon)CountdownSeconds=0;return 0;}
            double available=accumulator+seconds;
            if(WouldExceedTwoTicks(available)){Pause();return 0;}
            int moved=0;
            while(Game.State==SnakeState.Playing) {
                double tick=Game.CurrentTickMilliseconds/1000.0;
                if(available+Epsilon<tick)break;
                available=Math.Max(0,available-tick);
                SnakeDirection direction=Game.Direction;
                if(directions.Count>0){direction=directions[0];directions.RemoveAt(0);}
                Game.Step(direction);moved++;
            }
            if(Game.State!=SnakeState.Playing) {
                // Previous frames already counted their partial tick; exclude only this frame's post-terminal remainder.
                ElapsedSeconds+=Math.Max(0,seconds-available);accumulator=0;ClearInput();CountdownSeconds=0;
            } else {ElapsedSeconds+=seconds;accumulator=available;}
            return moved;
        }
        private bool WouldExceedTwoTicks(double available)
        {
            double current=Game.CurrentTickMilliseconds/1000.0;
            if(available+Epsilon<current+2*minimumTickSeconds)return false;
            // A food can change the next interval. Preview on an isolated state so a possible third tick
            // is detected before committing any movement, input, time or RNG from this frame.
            var preview=new SnakeGame(Game.Rules,Game.GetSnapshot());int moved=0;
            while(preview.State==SnakeState.Playing) {
                double tick=preview.CurrentTickMilliseconds/1000.0;
                if(available+Epsilon<tick)return false;
                available-=tick;
                var direction=moved<directions.Count?directions[moved]:preview.Direction;
                preview.Step(direction);moved++;
                if(moved>2)return true;
            }
            return false;
        }
        public void Pause(){Paused=true;CountdownSeconds=0;accumulator=0;ClearInput();}
        public void Resume()
        {
            if(!Paused||Game.State!=SnakeState.Playing)return;
            Paused=false;CountdownSeconds=3;accumulator=0;ClearInput();
        }
        public void ClearInput(){directions.Clear();}
        public SnakeSessionData Export()
        {
            return new SnakeSessionData { SessionId=SessionId,Board=Game.GetSnapshot(),ElapsedSeconds=ElapsedSeconds,ResultRecorded=ResultRecorded };
        }
        public void MarkResultRecorded()
        {
            if(Game.State==SnakeState.Playing)throw new InvalidOperationException("Cannot record an unfinished game.");
            ResultRecorded=true;
        }
    }
}
