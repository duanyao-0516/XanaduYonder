using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace XanaduYonder.Core.Dino
{
    public sealed class DinoGame
    {
        private readonly List<DinoObstacle> obstacles = new List<DinoObstacle>(8);
        private readonly ReadOnlyCollection<DinoObstacle> obstacleView;
        private uint random;
        private bool jumpHeld, duckRequested;
        private double playerY, playerVelocity;
        private long nextSpawnTick = 180, nextId = 1;

        public DinoRules Rules { get; private set; }
        public double PlayerY { get { return playerY; } }
        public double PlayerVelocity { get { return playerVelocity; } }
        public bool Ducking { get { return duckRequested && !Jumping; } }
        public bool Jumping { get { return playerY > 0 || playerVelocity > 0; } }
        public double Speed { get; private set; }
        public double Distance { get; private set; }
        public double Elapsed { get { return Tick * DinoRules.StepSeconds; } }
        public long Tick { get; private set; }
        public long Score { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsGameOver { get; private set; }
        public IReadOnlyList<DinoObstacle> Obstacles { get { return obstacleView; } }

        public DinoGame(DinoRules rules, uint seed)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            Rules = rules; random = seed == 0 ? 0x6D2B79F5u : seed;
            obstacleView = obstacles.AsReadOnly(); Speed = rules.SpeedAt(0);
        }

        public bool Jump()
        {
            if (IsGameOver || Jumping || duckRequested) return false;
            IsStarted = true; jumpHeld = true; playerVelocity = DinoRules.JumpVelocity;
            return true;
        }

        public void ReleaseJump() { if (!IsGameOver) jumpHeld = false; }
        public void SetDuck(bool pressed) { if (!IsGameOver) duckRequested = pressed; }

        public void Step()
        {
            if (!IsStarted || IsGameOver) return;
            // Saturating counters keep all public values finite even after an impractically long session.
            if (Tick == long.MaxValue) return;
            long nextTick = Tick + 1;
            double speed = Rules.SpeedAt(nextTick), dx = speed * DinoRules.StepSeconds / DinoRules.Substeps;
            for (int sub = 0; sub < DinoRules.Substeps; sub++) {
                double oldY = playerY;
                DinoMotion.Advance(ref playerY, ref playerVelocity, jumpHeld, duckRequested);
                bool collision = false;
                for (int i = 0; i < obstacles.Count; i++) {
                    var obstacle = obstacles[i];
                    double nextX = obstacle.X - dx;
                    if (DinoCollision.HitsMotion(Rules, oldY, playerY, duckRequested, obstacle, nextX)) collision = true;
                    obstacles[i] = obstacle.At(nextX);
                }
                if (collision) { IsGameOver = true; return; }
            }
            Tick = nextTick; Speed = speed;
            Distance = Math.Min(double.MaxValue, Distance + speed * DinoRules.StepSeconds);
            Score = ScoreForDistance(Distance);
            if (!Jumping) jumpHeld = false; // Landing never replays a held or airborne press.
            for (int i = obstacles.Count - 1; i >= 0; i--) if (obstacles[i].X + obstacles[i].Width < -4) obstacles.RemoveAt(i);
            if (Tick >= nextSpawnTick) Generate();
        }

        private void Generate()
        {
            if (Tick > long.MaxValue - DinoRules.MaximumPlanTicks - 50) { nextSpawnTick = long.MaxValue; return; }
            DinoObstacleKind kind = DinoObstacleKind.SmallCactus;
            int group = 1;
            DinoPlan plan;
            for (int attempt = 0; attempt < 12; attempt++) {
                if (Tick >= 2700) kind = (DinoObstacleKind)(Next() % 4);
                else if (Tick >= 1200) kind = (DinoObstacleKind)(Next() % 2);
                group = kind < DinoObstacleKind.LowBird && Tick >= 1200 ? 1 + (int)(Next() % 3) : 1;
                if (DinoTraversal.TryPlan(Rules, kind, group, Tick, 0, out plan) && plan.ReactionWindowSeconds + 1e-9 >= DinoRules.ReactionSeconds) {
                    Add(kind, group, plan); return;
                }
            }
            // Rejected widths never become obstacles. A deterministic single-cactus fallback is also proved.
            if (DinoTraversal.TryPlan(Rules, DinoObstacleKind.SmallCactus, 1, Tick, 0, out plan)
                && plan.ReactionWindowSeconds + 1e-9 >= DinoRules.ReactionSeconds) Add(DinoObstacleKind.SmallCactus, 1, plan);
            else nextSpawnTick = Tick + 60;
        }

        private void Add(DinoObstacleKind kind, int group, DinoPlan plan)
        {
            obstacles.Add(new DinoObstacle(nextId, kind, group, Rules.ViewportWidth + 2, plan));
            if (nextId < long.MaxValue) nextId++;
            // Even the latest successful input has completed and recovered before the next object spawns.
            // Thus all presses inside each certified interval retain >= .25s to the next action interval.
            nextSpawnTick = plan.RecoveryTick <= long.MaxValue - 15 ? plan.RecoveryTick + 15 : long.MaxValue;
        }

        private uint Next()
        {
            uint value = random; value ^= value << 13; value ^= value >> 17; value ^= value << 5;
            random = value; return value;
        }

        internal static long ScoreForDistance(double distance)
        {
            double score = Math.Floor(distance / 30);
            return score >= long.MaxValue ? long.MaxValue : score <= 0 ? 0 : (long)score;
        }
    }
}
