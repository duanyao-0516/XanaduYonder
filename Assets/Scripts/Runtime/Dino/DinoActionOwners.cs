namespace XanaduYonder.Runtime.Dino
{
    // Physical ownership only. Clear must not turn lifecycle cancellation into a game command.
    public sealed class DinoActionOwners
    {
        private int? jump, duck;
        public bool JumpHeld => jump.HasValue;
        public bool DuckHeld => duck.HasValue;
        public bool PressJump(int id) { if (jump.HasValue) return false; jump = id; return true; }
        public bool PressDuck(int id) { if (duck.HasValue) return false; duck = id; return true; }
        public bool ReleaseJump(int id) { if (jump != id) return false; jump = null; return true; }
        public bool ReleaseDuck(int id) { if (duck != id) return false; duck = null; return true; }
        public void Clear() { jump = duck = null; }
    }
}
