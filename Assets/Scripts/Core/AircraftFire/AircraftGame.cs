using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace XanaduYonder.Core.AircraftFire
{
    /// <summary>Deterministic bottom-up 640-unit battle. Runtime owns input, rendering and persistence.</summary>
    public sealed class AircraftGame
    {
        public const float Width = 640, HeroWidth = 136, HeroHeight = 168;
        private readonly uint initialSeed;
        private uint random;
        private int nextId;
        private readonly List<AircraftEnemy> enemies = new List<AircraftEnemy>(32);
        private readonly List<AircraftBullet> bullets = new List<AircraftBullet>(64);
        private readonly List<AircraftPickup> pickups = new List<AircraftPickup>(8);
        private readonly List<AircraftExplosion> explosions = new List<AircraftExplosion>(16);
        private readonly ReadOnlyCollection<AircraftEnemy> enemyView;
        private readonly ReadOnlyCollection<AircraftBullet> bulletView;
        private readonly ReadOnlyCollection<AircraftPickup> pickupView;
        private readonly ReadOnlyCollection<AircraftExplosion> explosionView;
        private double accumulator, stageElapsed;
        private float fireTimer, smallTimer, mediumTimer, largeTimer, doubleTimer, bombTimer;
        private bool revived, bossSpawned;
        private AircraftEnemy boss;
        public float Height { get; private set; }
        public AircraftMode Mode { get; private set; }
        public bool Crazy { get; private set; }
        public AircraftPhase Phase { get; private set; }
        public float HeroX { get; private set; }
        public float HeroY { get; private set; }
        public float HeroInvulnerability { get; private set; }
        public float HeroDeathAge { get; private set; }
        public float HeroDeathX { get; private set; }
        public float HeroDeathY { get; private set; }
        public double ElapsedTime { get; private set; }
        public long Score { get; private set; }
        public int Lives { get; private set; }
        public int Bombs { get; private set; }
        public int Stage { get; private set; }
        public int DoubleShots { get; private set; }
        public bool BossActive { get { return boss != null && boss.Hp > 0; } }
        public bool CanRevive { get { return Phase == AircraftPhase.GameOver && !revived; } }
        public IReadOnlyList<AircraftEnemy> Enemies { get { return enemyView; } }
        public IReadOnlyList<AircraftBullet> Bullets { get { return bulletView; } }
        public IReadOnlyList<AircraftPickup> Pickups { get { return pickupView; } }
        public IReadOnlyList<AircraftExplosion> Explosions { get { return explosionView; } }

        public AircraftGame(AircraftMode mode, bool crazy, uint seed, float height)
        {
            if (mode != AircraftMode.Classic && mode != AircraftMode.Level) throw new ArgumentOutOfRangeException("mode");
            if (!ValidHeight(height)) throw new ArgumentOutOfRangeException("height");
            Mode = mode; Crazy = mode == AircraftMode.Classic && crazy; Height = height;
            initialSeed = seed == 0 ? 0x9E3779B9u : seed;
            enemyView = enemies.AsReadOnly(); bulletView = bullets.AsReadOnly();
            pickupView = pickups.AsReadOnly(); explosionView = explosions.AsReadOnly();
            Start();
        }
        public void Start()
        {
            random = initialSeed; nextId = 1; accumulator = 0; ElapsedTime = 0; stageElapsed = 0;
            Score = 0; Lives = 3; Bombs = 0; Stage = 1; DoubleShots = 0; revived = false;
            enemies.Clear(); bullets.Clear(); pickups.Clear(); explosions.Clear();
            boss = null; bossSpawned = false; HeroInvulnerability = 0; HeroDeathAge = -1;
            Phase = AircraftPhase.Running; ResetHero(); fireTimer = .2f; ResetSpawnTimers();
        }
        public void Advance(float seconds)
        {
            if (Phase != AircraftPhase.Running || !Finite(seconds) || seconds <= 0) return;
            accumulator += Math.Min(seconds, AircraftRules.MaximumFrameTime);
            int steps = 0;
            while (accumulator + 1e-9 >= AircraftRules.FixedStep && steps < 30 && Phase == AircraftPhase.Running) {
                accumulator -= AircraftRules.FixedStep; Tick((float)AircraftRules.FixedStep); steps++;
            }
            if (Phase != AircraftPhase.Running) accumulator = 0;
        }
        public void MoveHero(float dx, float dy)
        {
            if (!Finite(dx) || !Finite(dy)) return;
            SetHeroPosition(HeroX + dx, HeroY + dy);
        }
        public void SetHeroPosition(float x, float y)
        {
            if (Phase != AircraftPhase.Running || !Finite(x) || !Finite(y)) return;
            HeroX = Clamp(x, HeroWidth / 2, Width - HeroWidth / 2);
            HeroY = Clamp(y, HeroHeight / 2, Height - HeroHeight / 2);
        }
        public void ResizeHeight(float height)
        {
            if (!ValidHeight(height)) return;
            float scale = height / Height; Height = height;
            HeroY = Clamp(HeroY * scale, HeroHeight / 2, Height - HeroHeight / 2); HeroDeathY *= scale;
            for (int i = 0; i < enemies.Count; i++) {
                var e = enemies[i]; e.Y *= scale; e.PreviousY *= scale; e.StartY *= scale; e.EndY *= scale;
            }
            for (int i = 0; i < bullets.Count; i++) { bullets[i].Y *= scale; bullets[i].PreviousY *= scale; }
            for (int i = 0; i < pickups.Count; i++) pickups[i].Y *= scale;
            for (int i = 0; i < explosions.Count; i++) explosions[i].Y *= scale;
        }
        public void Pause() { if (Phase == AircraftPhase.Running) { Phase = AircraftPhase.Paused; accumulator = 0; } }
        public void Resume() { if (Phase == AircraftPhase.Paused) Phase = AircraftPhase.Running; }
        public bool Revive()
        {
            if (!CanRevive) return false;
            revived = true; Lives = 3; HeroInvulnerability = 2; HeroDeathAge = -1;
            ResetHero(); RemoveOrdinaryEnemies(); bullets.Clear(); explosions.Clear(); Phase = AircraftPhase.Running; accumulator = 0;
            return true;
        }
        public bool UseBomb()
        {
            if (Phase != AircraftPhase.Running || Bombs <= 0) return false;
            Bombs--;
            for (int i = enemies.Count - 1; i >= 0; i--) if (enemies[i].Kind != AircraftEnemyKind.Boss) DamageEnemy(enemies[i], enemies[i].Hp);
            for (int i = bullets.Count - 1; i >= 0; i--) if (!bullets[i].Friendly) bullets.RemoveAt(i);
            if (BossActive) DamageEnemy(boss, 30);
            return true;
        }
        private void Tick(float dt)
        {
            ElapsedTime += AircraftRules.FixedStep; stageElapsed += AircraftRules.FixedStep;
            HeroInvulnerability = Math.Max(0, HeroInvulnerability - dt);
            if (HeroDeathAge >= 0) { HeroDeathAge += dt; if (HeroDeathAge >= .4f) HeroDeathAge = -1; }
            for (int i = explosions.Count - 1; i >= 0; i--) { explosions[i].Age += dt; if (explosions[i].Age >= explosions[i].Duration) explosions.RemoveAt(i); }
            SpawnScheduled(dt);
            fireTimer -= dt;
            if (fireTimer <= 0) {
                fireTimer += .2f;
                if (DoubleShots > 0) { SpawnBullet(HeroX, HeroY + HeroHeight / 2, true, true); DoubleShots--; }
                else SpawnBullet(HeroX, HeroY + HeroHeight / 2, true, false);
            }
            MoveEntities(dt);
            ResolveCollisions();
            RemoveDepartedEntities();
        }
        private void SpawnScheduled(float dt)
        {
            smallTimer -= dt; mediumTimer -= dt; largeTimer -= dt; doubleTimer -= dt; bombTimer -= dt;
            if (smallTimer <= 0) { SpawnScheduledEnemy(AircraftEnemyKind.Small); smallTimer += EnemyInterval(AircraftEnemyKind.Small); }
            if (mediumTimer <= 0) { SpawnScheduledEnemy(AircraftEnemyKind.Medium); mediumTimer += EnemyInterval(AircraftEnemyKind.Medium); }
            if (Mode == AircraftMode.Classic && largeTimer <= 0) { SpawnScheduledEnemy(AircraftEnemyKind.Large); largeTimer += EnemyInterval(AircraftEnemyKind.Large); }
            if (doubleTimer <= 0) { SpawnPickup(AircraftPickupKind.DoubleBullet, Sample(64, Width - 128), Height + 64); doubleTimer += PickupInterval(false); }
            if (bombTimer <= 0) { SpawnPickup(AircraftPickupKind.Bomb, Sample(64, Width - 128), Height + 64); bombTimer += PickupInterval(true); }
            if (Mode == AircraftMode.Level && !bossSpawned && stageElapsed >= AircraftRules.GetStage(Stage).Duration) SpawnEnemy(AircraftEnemyKind.Boss, Width / 2, Height + 172, 100);
        }
        private void SpawnScheduledEnemy(AircraftEnemyKind kind)
        {
            float width = kind == AircraftEnemyKind.Small ? 76 : kind == AircraftEnemyKind.Medium ? 92 : 225;
            float height = kind == AircraftEnemyKind.Small ? 56 : kind == AircraftEnemyKind.Medium ? 132 : 344;
            var e = SpawnEnemy(kind, Sample(width / 2, Width - width), Height + height / 2, EnemySpeed(kind));
            if (e == null || Mode != AircraftMode.Level || kind != AircraftEnemyKind.Small) return;
            var config = AircraftRules.GetStage(Stage);
            e.Curved = NextUnit() < config.RouteChance;
            e.Ease = NextUnit() < config.RouteChance ? 1 + (int)Math.Floor(NextUnit() * 2 + .5f) : 0;
            if (e.Curved) { e.EndX = NextUnit() < .5f ? 0 : Width; e.EndY = NextUnit() * Height * .5f; }
            float dx = e.EndX - e.StartX, dy = e.EndY - e.StartY;
            e.TravelDuration = (float)Math.Sqrt(dx * dx + dy * dy) / e.Speed;
        }
        internal AircraftEnemy SpawnEnemy(AircraftEnemyKind kind, float x, float y, float speed)
        {
            if (enemies.Count >= AircraftRules.MaximumEnemies || (kind == AircraftEnemyKind.Boss && BossActive)) return null;
            bool small = kind == AircraftEnemyKind.Small, medium = kind == AircraftEnemyKind.Medium;
            int hp = kind == AircraftEnemyKind.Boss ? AircraftRules.GetStage(Stage).BossHp : small ? 1 : medium ? 3 : 9;
            var e = new AircraftEnemy { Id = nextId++, Kind = kind, X = x, Y = y, Width = small ? 76 : medium ? 92 : 225,
                Height = small ? 56 : medium ? 132 : 344, Hp = hp, MaxHp = hp, Speed = speed, PreviousX = x, PreviousY = y,
                StartX = x, StartY = y, EndX = x, EndY = -(small ? 28 : medium ? 66 : 172), Direction = 1 };
            e.TravelDuration = speed > 0 ? (e.StartY - e.EndY) / speed : float.PositiveInfinity;
            if (Mode == AircraftMode.Level && medium) e.ShotTimer = NextUnit() * .7f * e.TravelDuration;
            enemies.Add(e);
            if (kind == AircraftEnemyKind.Boss) { boss = e; bossSpawned = true; e.ShotTimer = BossShotInterval(); e.Direction = NextUnit() < .5f ? -1 : 1; }
            return e;
        }
        internal AircraftPickup SpawnPickup(AircraftPickupKind kind, float x, float y)
        {
            if (pickups.Count >= AircraftRules.MaximumPickups) return null;
            var p = new AircraftPickup { Id = nextId++, Kind = kind, X = x, Y = y, Width = kind == AircraftPickupKind.Bomb ? 79 : 76, Height = kind == AircraftPickupKind.Bomb ? 141 : 116 };
            pickups.Add(p); return p;
        }
        private void SpawnBullet(float x, float y, bool friendly, bool doubled, float velocityX = 0, float velocityY = -200)
        {
            if (bullets.Count >= AircraftRules.MaximumBullets) return;
            bullets.Add(new AircraftBullet { Id = nextId++, X = x, Y = y, PreviousX = x, PreviousY = y, Width = doubled ? 96 : 12, Height = friendly ? 28 : 12, Friendly = friendly, Doubled = doubled, VelocityX = velocityX, VelocityY = friendly ? 1500 : velocityY });
        }
        private void MoveEntities(float dt)
        {
            for (int i = 0; i < enemies.Count; i++) {
                var e = enemies[i]; e.PreviousX = e.X; e.PreviousY = e.Y; e.Age += dt;
                if (e.Kind == AircraftEnemyKind.Boss) MoveBoss(e, dt);
                else if (e.Curved || e.Ease != 0) {
                    float t = Clamp(e.Age / e.TravelDuration, 0, 1); t = Ease(t, e.Ease);
                    if (e.Curved) {
                        float inv = 1 - t;
                        e.X = inv * inv * inv * e.StartX + 3 * inv * inv * t * e.StartX + 3 * inv * t * t * (e.StartX + e.EndX) / 2 + t * t * t * e.EndX;
                        e.Y = inv * inv * inv * e.StartY + 3 * inv * inv * t * (e.StartY + e.EndY) / 2 + 3 * inv * t * t * e.EndY + t * t * t * e.EndY;
                    } else e.Y = e.StartY + (e.EndY - e.StartY) * t;
                } else e.Y -= e.Speed * dt;
                if (Mode == AircraftMode.Level && e.Kind == AircraftEnemyKind.Medium && !e.HasFired) {
                    e.ShotTimer -= dt;
                    if (e.ShotTimer <= 0) {
                        e.HasFired = true; int count = 1 + (int)Math.Floor(NextUnit() * 2 + .5f);
                        for (int shot = 0; shot < count; shot++) {
                            float dx = (e.X > Width / 2 ? -6f : Width + 6f) - e.X;
                            float dy = NextUnit() * Height * .7f - e.Y;
                            float length = (float)Math.Sqrt(dx * dx + dy * dy);
                            SpawnBullet(e.X, e.Y, false, false, dx / length * 200, dy / length * 200);
                        }
                    }
                }
            }
            for (int i = 0; i < bullets.Count; i++) { var b = bullets[i]; b.PreviousX = b.X; b.PreviousY = b.Y; b.Age += dt; b.X += b.VelocityX * dt; b.Y += b.VelocityY * dt; }
            for (int i = 0; i < pickups.Count; i++) { pickups[i].Age += dt; pickups[i].Y -= 150 * dt; }
        }
        private void MoveBoss(AircraftEnemy e, float dt)
        {
            float targetY = Height - e.Height / 2 - 5;
            if (e.Y > targetY) { e.Y = Math.Max(targetY, e.Y - 100 * dt); return; }
            if (e.BurstRemaining == 0 && e.BurstTimer > 0) { e.BurstTimer -= dt; return; }
            if (e.BurstRemaining > 0) {
                e.BurstTimer -= dt;
                if (e.BurstTimer <= 0) { SpawnBullet(e.X, e.Y - e.Height / 2, false, false); e.BurstRemaining--; e.BurstTimer += .3f; if (e.BurstRemaining == 0) { e.Direction = NextUnit() < .5f ? -1 : 1; e.ShotTimer = BossShotInterval(); } }
                return;
            }
            e.X += e.Direction * 100 * dt;
            if (e.X < e.Width / 2 || e.X > Width - e.Width / 2) { e.X = Clamp(e.X, e.Width / 2, Width - e.Width / 2); e.Direction *= -1; }
            e.ShotTimer -= dt;
            if (e.ShotTimer <= 0) { e.BurstRemaining = AircraftRules.GetStage(Stage).BossBurst; e.BurstTimer = 0; }
        }
        private void ResolveCollisions()
        {
            for (int i = bullets.Count - 1; i >= 0; i--) {
                var b = bullets[i];
                if (b.Friendly) {
                    // Select first contact along travel, independent of enemy insertion order.
                    AircraftEnemy hit = null; float earliest = float.PositiveInfinity;
                    for (int j = 0; j < enemies.Count; j++) {
                        var e = enemies[j]; float contact;
                        if (Sweep(b.PreviousX - e.PreviousX, b.PreviousY - e.PreviousY, b.X - e.X, b.Y - e.Y, (b.Width + e.Width) / 2, (b.Height + e.Height) / 2, out contact) && contact < earliest) { earliest = contact; hit = e; }
                    }
                    if (hit != null) { bullets.RemoveAt(i); int before = Stage; DamageEnemy(hit, 1); if (Stage != before || Phase == AircraftPhase.Completed) return; }
                } else if (HeroInvulnerability <= 0 && HitsHero(b.X, b.Y, b.Width, b.Height)) { bullets.RemoveAt(i); LoseLife(); return; }
            }
            if (HeroInvulnerability <= 0) {
                for (int i = enemies.Count - 1; i >= 0; i--) {
                    var e = enemies[i];
                    if (HitsHero(e.X, e.Y, e.Width, e.Height)) {
                        if (e.Kind != AircraftEnemyKind.Boss) { Explode(e); enemies.RemoveAt(i); }
                        LoseLife(); return;
                    }
                }
            }
            for (int i = pickups.Count - 1; i >= 0; i--) {
                var p = pickups[i];
                if (!Overlap(HeroX, HeroY, HeroWidth, HeroHeight, p.X, p.Y, p.Width, p.Height)) continue;
                if (p.Kind == AircraftPickupKind.Bomb) Bombs = Math.Min(99, Bombs + 1); else DoubleShots = 90;
                pickups.RemoveAt(i);
            }
        }
        internal void DamageEnemy(AircraftEnemy enemy, int damage)
        {
            if (enemy == null || damage <= 0 || enemy.Hp <= 0 || !enemies.Contains(enemy)) return;
            enemy.Hp = Math.Max(0, enemy.Hp - damage);
            if (enemy.Hp > 0) return;
            Score += enemy.Kind == AircraftEnemyKind.Small ? 1000 : enemy.Kind == AircraftEnemyKind.Medium ? 6000 : enemy.Kind == AircraftEnemyKind.Large ? 30000 : enemy.MaxHp * 100;
            Explode(enemy); enemies.Remove(enemy);
            if (enemy.Kind == AircraftEnemyKind.Boss) {
                boss = null; bullets.Clear(); enemies.Clear(); pickups.Clear();
                if (Stage == AircraftRules.StageCount) Phase = AircraftPhase.Completed;
                else { Stage++; stageElapsed = 0; bossSpawned = false; ResetSpawnTimers(); HeroInvulnerability = Math.Max(HeroInvulnerability, 2); }
            }
        }
        private void LoseLife()
        {
            if (HeroInvulnerability > 0 || Phase != AircraftPhase.Running) return;
            HeroDeathX = HeroX; HeroDeathY = HeroY; HeroDeathAge = 0; Lives--; DoubleShots = 0;
            for (int i = bullets.Count - 1; i >= 0; i--) if (!bullets[i].Friendly) bullets.RemoveAt(i);
            if (Lives <= 0) Phase = AircraftPhase.GameOver;
            else { HeroInvulnerability = 3; ResetHero(); }
        }
        private void Explode(AircraftEnemy e)
        {
            if (explosions.Count >= AircraftRules.MaximumExplosions) explosions.RemoveAt(0);
            explosions.Add(new AircraftExplosion { Id = nextId++, Kind = e.Kind, X = e.X, Y = e.Y, Width = e.Width, Height = e.Height,
                Duration = e.Kind == AircraftEnemyKind.Large || e.Kind == AircraftEnemyKind.Boss ? .7f : .4f });
        }
        private void RemoveOrdinaryEnemies() { for (int i = enemies.Count - 1; i >= 0; i--) if (enemies[i].Kind != AircraftEnemyKind.Boss) enemies.RemoveAt(i); }
        private void RemoveDepartedEntities()
        {
            for (int i = enemies.Count - 1; i >= 0; i--) { var e = enemies[i]; if (e.Kind != AircraftEnemyKind.Boss && (e.Y + e.Height / 2 < 0 || ((e.Curved || e.Ease != 0) && e.Age >= e.TravelDuration))) enemies.RemoveAt(i); }
            for (int i = bullets.Count - 1; i >= 0; i--) if (bullets[i].Y > Height + 32 || bullets[i].Y < -32 || bullets[i].X < -32 || bullets[i].X > Width + 32) bullets.RemoveAt(i);
            for (int i = pickups.Count - 1; i >= 0; i--) if (pickups[i].Y < -pickups[i].Height / 2) pickups.RemoveAt(i);
        }
        private void ResetHero() { HeroX = Width / 2; HeroY = Math.Min(HeroHeight / 2 + 50, Height / 2); }
        private void ResetSpawnTimers()
        {
            smallTimer = EnemyInterval(AircraftEnemyKind.Small); mediumTimer = EnemyInterval(AircraftEnemyKind.Medium);
            largeTimer = EnemyInterval(AircraftEnemyKind.Large); doubleTimer = PickupInterval(false); bombTimer = PickupInterval(true);
        }
        private float EnemyInterval(AircraftEnemyKind kind)
        {
            float min, range;
            if (Mode == AircraftMode.Level) {
                var s = AircraftRules.GetStage(Stage); float shift = 1 - .5f * Clamp((float)(stageElapsed / s.Duration), 0, 1);
                min = kind == AircraftEnemyKind.Small ? 0 : s.MediumBase; range = (kind == AircraftEnemyKind.Small ? s.SmallInterval : s.MediumRange) * shift;
            } else return AircraftRules.ClassicInterval(kind, Crazy, ElapsedTime, NextUnit());
            return Math.Max((float)AircraftRules.FixedStep, Sample(min, range));
        }
        private float EnemySpeed(AircraftEnemyKind kind)
        {
            if (Mode == AircraftMode.Level) { var s = AircraftRules.GetStage(Stage); float shift = 150 * Clamp((float)(stageElapsed / s.Duration), 0, 1); return Sample(s.Speed + shift, 300 - shift); }
            return AircraftRules.ClassicSpeed(kind, Crazy, ElapsedTime, NextUnit());
        }
        private float PickupInterval(bool bombPickup)
        {
            if (Mode == AircraftMode.Classic) return bombPickup ? Sample(60, 120) : Sample(30, 60);
            var s = AircraftRules.GetStage(Stage); return bombPickup ? Sample(s.BombBase, s.BombRange) : Sample(s.DoubleBase, s.DoubleRange);
        }
        private float BossShotInterval() { return Sample(1, AircraftRules.GetStage(Stage).BossFireRange); }
        private float NextUnit() { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return (random >> 8) * (1f / 16777216f); }
        private float Sample(float min, float range) { return min + NextUnit() * range; }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool ValidHeight(float value) { return Finite(value) && value >= HeroHeight + 20 && value <= 10000; }
        private static float Clamp(float value, float min, float max) { return Math.Max(min, Math.Min(max, value)); }
        private static float Ease(float t, int mode)
        {
            if (mode == 1) return (float)Math.Sqrt(t);
            if (mode == 2) return t * t;
            if (mode == 3) return t < .5f ? (float)Math.Sqrt(t * 2) * .5f : 1 - (float)Math.Sqrt((1 - t) * 2) * .5f;
            return t;
        }
        private bool HitsHero(float x, float y, float width, float height)
        {
            return Overlap(HeroX, HeroY + 24, 42, 100, x, y, width, height) || Overlap(HeroX, HeroY + 12, 100, 32, x, y, width, height);
        }
        private static bool Overlap(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh)
        {
            return Math.Abs(ax - bx) < (aw + bw) / 2 && Math.Abs(ay - by) < (ah + bh) / 2;
        }
        private static bool Sweep(float x0, float y0, float x1, float y1, float halfW, float halfH, out float contact)
        {
            float enter = 0, exit = 1;
            if (!Slab(x0, x1 - x0, halfW, ref enter, ref exit) || !Slab(y0, y1 - y0, halfH, ref enter, ref exit)) { contact = 0; return false; }
            contact = enter; return true;
        }
        private static bool Slab(float start, float movement, float half, ref float enter, ref float exit)
        {
            if (Math.Abs(movement) < .00001f) return Math.Abs(start) <= half;
            float a = (-half - start) / movement, b = (half - start) / movement;
            if (a > b) { float swap = a; a = b; b = swap; }
            enter = Math.Max(enter, a); exit = Math.Min(exit, b); return enter <= exit;
        }
    }
}
