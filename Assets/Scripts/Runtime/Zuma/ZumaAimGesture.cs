namespace XanaduYonder.Runtime.Zuma
{
    // Pointer ownership survives a move outside the field, but never a cancellation.
    public sealed class ZumaAimGesture
    {
        private int owner;
        public bool IsAiming { get; private set; }
        public bool Begin(int pointer, bool inside, bool acceptsInput)
        {
            if (IsAiming || !inside || !acceptsInput) return false;
            owner = pointer;
            IsAiming = true;
            return true;
        }
        public bool Owns(int pointer) { return IsAiming && pointer == owner; }
        public bool Release(int pointer, bool cancelled, bool inside, bool ready)
        {
            if (!Owns(pointer)) return false;
            IsAiming = false;
            return !cancelled && inside && ready;
        }
        public void Cancel() { IsAiming = false; }
    }
}
