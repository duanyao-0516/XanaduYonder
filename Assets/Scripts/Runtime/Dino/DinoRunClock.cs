using System;

namespace XanaduYonder.Runtime.Dino
{
    public sealed class DinoRunClock
    {
        public const double StepSeconds = 1.0 / 60.0;
        private double accumulated, countdown;
        public bool CountingDown { get; private set; }
        public int CountdownNumber => Math.Max(1, (int)Math.Ceiling(countdown - 1e-9));
        public bool AddFrame(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || seconds > .1)
            { accumulated = 0; return false; }
            accumulated += seconds;
            return true;
        }
        public bool ConsumeStep()
        {
            if (accumulated + 1e-9 < StepSeconds) return false;
            accumulated = Math.Max(0, accumulated - StepSeconds);
            return true;
        }
        public void BeginCountdown() { Reset(); countdown = 3; CountingDown = true; }
        public bool AdvanceCountdown(double seconds)
        {
            if (!CountingDown || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return false;
            countdown -= seconds;
            if (countdown > 1e-9) return false;
            CountingDown = false;
            return true;
        }
        public void Reset() { accumulated = countdown = 0; CountingDown = false; }
    }
}
