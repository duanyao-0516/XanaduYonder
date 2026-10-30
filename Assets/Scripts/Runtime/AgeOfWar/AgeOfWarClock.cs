using System;

namespace XanaduYonder.Runtime.AgeOfWar
{
    // The original game clock is independent of the host display refresh rate.
    // In-game PAUSED remains a rule input; only host inactivity stops all ticks.
    public sealed class AgeOfWarClock
    {
        private readonly double step;
        private double accumulator;
        public AgeOfWarClock(int ticksPerSecond)
        {
            if(ticksPerSecond<=0||ticksPerSecond>1000)throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            step=1.0/ticksPerSecond;
        }
        public void Reset(){accumulator=0;}
        public int Advance(double elapsed,bool hostActive,Action tick)
        {
            if(tick==null)throw new ArgumentNullException(nameof(tick));
            if(!hostActive){Reset();return 0;}
            if(double.IsNaN(elapsed)||double.IsInfinity(elapsed)||elapsed<0)return 0;
            accumulator+=Math.Min(elapsed,.25);
            int count=0;
            while(accumulator+1e-10>=step)
            {
                tick();accumulator-=step;if(accumulator<0)accumulator=0;count++;
            }
            return count;
        }
    }
}
