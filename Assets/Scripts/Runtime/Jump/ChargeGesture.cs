namespace XanaduYonder.Runtime.Jump
{
    public sealed class ChargeGesture
    {
        private bool isCharging;
        private int owner;
        private double startedAt;
        private bool inFlight;

        public bool IsCharging { get { return isCharging; } }

        public bool Begin(int id, double time)
        {
            if (isCharging || !ValidTime(time)) return false;
            owner = id;
            startedAt = time;
            isCharging = true;
            return true;
        }

        public bool Release(int id, double time, bool cancelled, out double seconds)
        {
            seconds = 0;
            if (!isCharging || owner != id) return false;
            isCharging = false;
            if (cancelled || inFlight || !ValidTime(time) || time < startedAt) return false;
            seconds = time - startedAt;
            return true;
        }

        public void Cancel() { isCharging = false; }

        // Keep a new DOWN during flight, but never queue a release for later execution.
        public void StartFlight() { Cancel(); inFlight = true; }
        public void FinishFlight() { inFlight = false; }

        private static bool ValidTime(double time)
        {
            return time >= 0 && !double.IsNaN(time) && !double.IsInfinity(time);
        }
    }
}
