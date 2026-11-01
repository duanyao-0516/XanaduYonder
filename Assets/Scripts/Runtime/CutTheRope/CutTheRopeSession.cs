using System;

namespace XanaduYonder.Runtime.CutTheRope
{
    public interface ICutTheRopeBridge
    {
        bool Open(bool soundEnabled);
        bool IsRunning();
        string GetLastError();
    }

    public sealed class CutTheRopeSession : IDisposable
    {
        private const double InputSettleSeconds = .25;
        private readonly ICutTheRopeBridge bridge;
        private bool disposed;
        private bool returning;
        private double inputReadyAt;
        public event Action<string> Returned;
        public bool IsBusy { get; private set; }
        public string LastError { get; private set; }

        public CutTheRopeSession(ICutTheRopeBridge bridge)
        {
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public bool TryOpen(bool soundEnabled)
        {
            if (disposed || IsBusy) return false;
            IsBusy = true;
            returning = false;
            LastError = null;
            try
            {
                if (bridge.Open(soundEnabled)) return true;
                LastError = bridge.GetLastError();
            }
            catch (Exception error) { LastError = error.Message; }
            IsBusy = false;
            return false;
        }

        // Called in LateUpdate so a completed native Back event cannot be handled by
        // AppController.Update or the EventSystem in the frame that releases ownership.
        public void Tick(double now, bool foreground, bool pendingInput)
        {
            if (disposed || !IsBusy) return;
            if (!foreground)
            {
                inputReadyAt = now + InputSettleSeconds;
                return;
            }
            if (!returning)
            {
                try
                {
                    // Android marks its queued launch as running before open returns.
                    if (bridge.IsRunning()) return;
                    LastError = bridge.GetLastError();
                }
                catch (Exception error) { LastError = error.Message; }
                returning = true;
                inputReadyAt = now + InputSettleSeconds;
            }
            if (pendingInput) inputReadyAt = now + InputSettleSeconds;
            if (now < inputReadyAt) return;
            IsBusy = false;
            returning = false;
            Returned?.Invoke(string.IsNullOrEmpty(LastError) ? null : LastError);
        }

        public void Dispose()
        {
            disposed = true;
            IsBusy = false;
            returning = false;
            Returned = null;
        }
    }
}
