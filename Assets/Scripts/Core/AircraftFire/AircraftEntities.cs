namespace XanaduYonder.Core.AircraftFire
{
    public enum AircraftMode { Classic, Level }
    public enum AircraftPhase { Running, Paused, GameOver, Completed }
    public enum AircraftEnemyKind { Small, Medium, Large, Boss }
    public enum AircraftPickupKind { Bomb, DoubleBullet }
    public sealed class AircraftEnemy
    {
        public int Id { get; internal set; }
        public AircraftEnemyKind Kind { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
        public int Hp { get; internal set; }
        public int MaxHp { get; internal set; }
        public float Age { get; internal set; }
        internal float Speed = 0, PreviousX = 0, PreviousY = 0, ShotTimer = 0, BurstTimer = 0;
        internal int BurstRemaining = 0;
        internal float Direction = 0;
        internal float StartX, StartY, EndX, EndY, TravelDuration;
        internal bool Curved, HasFired;
        internal int Ease;
    }
    public sealed class AircraftBullet
    {
        public int Id { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
        public bool Friendly { get; internal set; }
        public bool Doubled { get; internal set; }
        public float Age { get; internal set; }
        public float VelocityX { get; internal set; }
        public float VelocityY { get; internal set; }
        internal float PreviousX, PreviousY = 0;
    }
    public sealed class AircraftPickup
    {
        public int Id { get; internal set; }
        public AircraftPickupKind Kind { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
        public float Age { get; internal set; }
    }
    public sealed class AircraftExplosion
    {
        public int Id { get; internal set; }
        public AircraftEnemyKind Kind { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Width { get; internal set; }
        public float Height { get; internal set; }
        public float Age { get; internal set; }
        public float Duration { get; internal set; }
    }
}
