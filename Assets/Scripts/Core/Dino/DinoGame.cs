// Portions derived from Chromium, Copyright 2024 The Chromium Authors.
// Use of this source code is governed by the BSD-style license in docs/chromium-dino-reference/source/LICENSE.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace XanaduYonder.Core.Dino
{
    public sealed class DinoGame
    {
        private readonly List<DinoObstacle> obstacles = new List<DinoObstacle>(32);
        private readonly ReadOnlyCollection<DinoObstacle> obstacleView;
        private readonly DinoObstacleGenerator generator;
        private bool duckRequested;
        private double collisionTravel;
        private DinoMotionState motion = DinoMotionState.Grounded;

        public DinoRules Rules { get; private set; }
        public double PlayerY { get { return motion.WorldY; } }
        public double PlayerVelocity { get { return motion.WorldVelocity; } }
        public bool Ducking { get { return duckRequested && !Jumping; } }
        public bool Jumping { get { return motion.Jumping; } }
        public double Speed { get; private set; }
        public double Distance { get; private set; }
        // Chrome counters omit the crash tick; scenery still stops at the swept contact instant.
        public double PresentationDistance { get { return Distance + collisionTravel; } }
        public double Elapsed { get { return Tick * DinoRules.StepSeconds; } }
        public long Tick { get; private set; }
        public long Score { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsGameOver { get; private set; }
        public IReadOnlyList<DinoObstacle> Obstacles { get { return obstacleView; } }

        public DinoGame(DinoRules rules, uint seed)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            Rules = rules; generator = new DinoObstacleGenerator(seed);
            obstacleView = obstacles.AsReadOnly(); Speed = rules.SpeedAt(0);
        }

        public bool Jump()
        {
            if (IsGameOver || Jumping || duckRequested) return false;
            IsStarted = true; DinoMotion.Start(ref motion, Rules.OriginalSpeedAt(Tick));
            return true;
        }

        public void ReleaseJump() { if (!IsGameOver) DinoMotion.End(ref motion); }
        public void SetDuck(bool pressed)
        {
            if (IsGameOver) return;
            if (pressed && !duckRequested) DinoMotion.SetDrop(ref motion);
            if (!pressed) motion.SpeedDrop = false;
            duckRequested = pressed;
        }

        public void Step()
        {
            if (!IsStarted || IsGameOver || Tick == long.MaxValue) return;
            long nextTick = Tick + 1;
            double originalSpeed = Rules.OriginalSpeedAt(Tick);
            DinoMotionState predicted = motion;
            DinoMotion.Advance(ref predicted);
            double fraction = 1;
            bool collision = false;
            // Predict all endpoints before committing any geometry. Every object stops at the first contact.
            for (int i = 0; i < obstacles.Count; i++) {
                var obstacle = obstacles[i];
                double nextX = obstacle.X - DinoRules.ObstacleStep(originalSpeed, obstacle.SpeedOffset);
                double contact;
                bool collisionDuck = duckRequested && !motion.Jumping;
                if (DinoCollision.TryHitsMotion(Rules, motion.WorldY, predicted.WorldY, collisionDuck, obstacle, nextX, out contact)) {
                    collision = true; fraction = Math.Min(fraction, contact);
                }
                if (duckRequested && motion.Jumping && !predicted.Jumping
                    && DinoCollision.Hits(Rules, 0, 0, true, obstacle.At(nextX), nextX)) collision = true;
            }
            for (int i = 0; i < obstacles.Count; i++) {
                var obstacle = obstacles[i];
                obstacles[i] = obstacle.At(obstacle.X - DinoRules.ObstacleStep(originalSpeed, obstacle.SpeedOffset) * fraction);
            }
            if (collision) {
                collisionTravel = originalSpeed * DinoRules.SpriteScale * fraction;
                motion.Y += (predicted.Y - motion.Y) * fraction;
                motion.Velocity += (predicted.Velocity - motion.Velocity) * fraction;
                if (fraction >= 1) motion = predicted;
                IsGameOver = true; return;
            }
            motion = predicted;
            Tick = nextTick; Speed = Rules.SpeedAt(Tick);
            Distance = Math.Min(double.MaxValue, Distance + originalSpeed * DinoRules.SpriteScale);
            Score = ScoreForDistance(Distance);
            for (int i = obstacles.Count - 1; i >= 0; i--) if (obstacles[i].X + obstacles[i].Width <= 0) obstacles.RemoveAt(i);
            if (Tick > DinoRules.ClearTicks && (obstacles.Count == 0 || ReadyForFollowing(obstacles[obstacles.Count - 1]))) {
                if (obstacles.Count > 0) obstacles[obstacles.Count - 1] = obstacles[obstacles.Count - 1].WithFollower();
                obstacles.Add(generator.Create(Rules, Tick, originalSpeed));
            }
        }

        private bool ReadyForFollowing(DinoObstacle obstacle)
        {
            return !obstacle.FollowingObstacleCreated && obstacle.X + obstacle.Width + obstacle.Gap < Rules.ViewportWidth;
        }

        internal static long ScoreForDistance(double distance)
        {
            double score = DinoRules.Round(Math.Ceiling(distance / DinoRules.SpriteScale) * .025);
            return score >= long.MaxValue ? long.MaxValue : score <= 0 ? 0 : (long)score;
        }
    }
}
