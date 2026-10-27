using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Watermelon
{
    public sealed class WatermelonGame
    {
        private readonly WatermelonSnapshot state;

        public WatermelonGame(uint seed)
        {
            state = new WatermelonSnapshot {
                Balls = new WatermelonBallState[0], NextId = 1, RandomState = seed == 0 ? 0x6D2B79F5u : seed
            };
            state.CurrentLevel = NextStartingLevel();
            state.NextLevel = NextStartingLevel();
        }

        public WatermelonGame(WatermelonSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string reason;
            if (!snapshot.TryValidate(out reason)) throw new ArgumentException(reason, nameof(snapshot));
            state = snapshot.Copy();
        }

        public WatermelonSnapshot GetSnapshot() { return state.Copy(); }

        public int Score { get { return state.Score; } }
        public int CurrentLevel { get { return state.CurrentLevel; } }
        public int NextLevel { get { return state.NextLevel; } }
        public int NextId { get { return state.NextId; } }
        public int BallCount { get { return state.Balls.Length; } }
        public float DropCooldown { get { return state.DropCooldown; } }
        public float DangerTime { get { return state.DangerTime; } }
        public bool GameOver { get { return state.GameOver; } }

        public bool HasWatermelon
        {
            get
            {
                foreach (var ball in state.Balls) if (ball.Level == WatermelonRules.LevelCount - 1) return true;
                return false;
            }
        }

        public bool TryDrop(float x, out WatermelonBallState ball)
        {
            ball = null;
            if (!WatermelonRules.IsFinite(x) || state.GameOver || state.DropCooldown > 0
                || state.Balls.Length >= WatermelonRules.MaxBalls || state.NextId == int.MaxValue) return false;

            float radius = WatermelonRules.GetRadius(state.CurrentLevel);
            var created = new WatermelonBallState {
                Id = state.NextId, Level = state.CurrentLevel,
                X = WatermelonRules.Clamp(x, radius, WatermelonRules.Width - radius), Y = WatermelonRules.SpawnY
            };
            var updated = new WatermelonBallState[state.Balls.Length + 1];
            Array.Copy(state.Balls, updated, state.Balls.Length);
            updated[updated.Length - 1] = created;
            state.Balls = updated;
            state.NextId++;
            state.CurrentLevel = state.NextLevel;
            state.NextLevel = NextStartingLevel();
            state.DropCooldown = WatermelonRules.DropInterval;
            ball = created.Copy();
            return true;
        }

        // Collision detection belongs to the physics scene. A consumed id can never participate
        // twice, including duplicate callbacks or three simultaneous contacts in the same step.
        public bool TryMerge(int firstId, int secondId, out WatermelonMergeResult result)
        {
            result = null;
            if (state.GameOver || firstId == secondId || state.NextId == int.MaxValue) return false;
            int first = Find(firstId), second = Find(secondId);
            if (first < 0 || second < 0) return false;
            var a = state.Balls[first]; var b = state.Balls[second];
            if (a.Level != b.Level || a.Level >= WatermelonRules.LevelCount - 1) return false;

            int level = a.Level + 1;
            float radius = WatermelonRules.GetRadius(level);
            var created = new WatermelonBallState {
                Id = state.NextId, Level = level,
                X = WatermelonRules.Clamp((a.X + b.X) * .5f, radius, WatermelonRules.Width - radius),
                Y = WatermelonRules.Clamp((a.Y + b.Y) * .5f, radius, WatermelonRules.Height - radius),
                VelocityX = (a.VelocityX + b.VelocityX) * .5f,
                VelocityY = (a.VelocityY + b.VelocityY) * .5f,
                AngularVelocity = (a.AngularVelocity + b.AngularVelocity) * .5f, Contacted = true
            };
            var updated = new WatermelonBallState[state.Balls.Length - 1];
            int index = 0;
            for (int i = 0; i < state.Balls.Length; i++)
                if (i != first && i != second) updated[index++] = state.Balls[i];
            updated[index] = created;
            int added = Math.Min(level, int.MaxValue - state.Score);
            state.Balls = updated;
            state.NextId++;
            state.Score += added;
            result = new WatermelonMergeResult {
                FirstId = firstId, SecondId = secondId, NewBall = created.Copy(), AddedScore = added,
                ReachedWatermelon = level == WatermelonRules.LevelCount - 1
            };
            return true;
        }

        // Validate the entire batch before applying it: a malformed final body must not leave a
        // partially updated board. Ages and contact history are authoritative in this model.
        public bool UpdateMotion(IReadOnlyList<WatermelonBallState> motions)
        {
            if (state.GameOver || motions == null || motions.Count != state.Balls.Length) return false;
            var indexes = new int[motions.Count];
            var seen = new bool[state.Balls.Length];
            for (int i = 0; i < motions.Count; i++)
            {
                var motion = motions[i];
                if (!WatermelonRules.HasValidMotion(motion)) return false;
                int index = Find(motion.Id);
                if (index < 0 || seen[index] || state.Balls[index].Level != motion.Level) return false;
                seen[index] = true; indexes[i] = index;
            }
            for (int i = 0; i < motions.Count; i++)
            {
                var motion = motions[i]; var ball = state.Balls[indexes[i]];
                ball.X = motion.X; ball.Y = motion.Y; ball.VelocityX = motion.VelocityX; ball.VelocityY = motion.VelocityY;
                ball.Angle = motion.Angle; ball.AngularVelocity = motion.AngularVelocity;
                ball.Contacted |= motion.Contacted;
            }
            return true;
        }

        // Call after motion and pending merges for this fixed physics step. Pauses advance no time.
        public void Advance(float dt)
        {
            if (!WatermelonRules.IsFinite(dt) || dt < 0 || dt > WatermelonRules.MaximumStep)
                throw new ArgumentOutOfRangeException(nameof(dt));
            if (state.GameOver || dt == 0) return;
            state.DropCooldown = Math.Max(0, state.DropCooldown - dt);
            bool dangerous = false;
            foreach (var ball in state.Balls)
            {
                ball.Age = Math.Min(WatermelonRules.MaximumAge, ball.Age + dt);
                if (ball.Contacted && ball.Age >= WatermelonRules.DangerMinimumAge
                    && ball.Y + WatermelonRules.GetRadius(ball.Level) > WatermelonRules.DangerY
                    && ball.VelocityX * ball.VelocityX + ball.VelocityY * ball.VelocityY
                        <= WatermelonRules.DangerMaximumSpeed * WatermelonRules.DangerMaximumSpeed)
                    dangerous = true;
            }
            state.DangerTime = dangerous ? Math.Min(WatermelonRules.DangerDuration, state.DangerTime + dt) : 0;
            if (state.DangerTime >= WatermelonRules.DangerDuration - .00001f)
            {
                state.DangerTime = WatermelonRules.DangerDuration;
                state.GameOver = true;
            }
        }

        private int Find(int id)
        {
            for (int i = 0; i < state.Balls.Length; i++) if (state.Balls[i].Id == id) return i;
            return -1;
        }

        private int NextStartingLevel()
        {
            uint value = state.RandomState;
            value ^= value << 13; value ^= value >> 17; value ^= value << 5;
            state.RandomState = value;
            return (int)(value % WatermelonRules.StartingLevelCount);
        }
    }
}
