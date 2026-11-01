namespace XanaduYonder.Runtime
{
    // Driven by unscaled time so pausing a game cannot extend the confirmation window.
    internal sealed class BackExitConfirmation
    {
        private bool pending;
        private int context;
        private double firstBackAt;

        internal bool TryConfirm(int currentContext, double now)
        {
            double elapsed = now - firstBackAt;
            if (pending && context == currentContext && elapsed >= 0 && elapsed < 2)
            {
                Reset();
                return true;
            }
            pending = true;
            context = currentContext;
            firstBackAt = now;
            return false;
        }

        internal void Reset() { pending = false; }
    }
}
