using System;
namespace XanaduYonder.Runtime.AgeOfWar
{
    // Moon's Engine consumes integer getTimer millisecond differences once per stage event.
    public sealed class AgeOfWarStageClock
    {
        private long previous = -1;
        public double Sample(double realtimeSeconds)
        {
            long now = (long)(realtimeSeconds * 1000);
            double elapsed = previous < 0 ? 0 : Math.Max(0, now - previous) / 1000.0;
            previous = now;
            return elapsed;
        }
        public void Reset() { previous = -1; }
    }
}
