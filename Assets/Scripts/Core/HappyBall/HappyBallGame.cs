using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.HappyBall
{
    public sealed class HappyBallGame
    {
        private HappyBallSnapshot state;
        private readonly bool generate;
        public bool GameOver { get { return state.GameOver; } }
        public long Score { get { return state.Score; } }

        public HappyBallGame(uint seed)
        {
            generate = true;
            state = new HappyBallSnapshot { RandomState = seed == 0 ? 0xA341316Cu : seed,
                BallY = HappyBallRules.BallRadius + .7, Layers = new HappyBallLayer[0] };
            FillWindow();
        }

        public HappyBallGame(HappyBallSnapshot snapshot) : this(snapshot, true) { }
        internal HappyBallGame(HappyBallSnapshot snapshot, bool generate)
        {
            this.generate = generate;
            state = NormalizeForCurrentRules(snapshot);
        }

        public static HappyBallSnapshot NormalizeForCurrentRules(HappyBallSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            string reason;
            if (!snapshot.TryValidate(out reason)) throw new ArgumentException(reason, "snapshot");
            var normalized = snapshot.Copy();
            if (normalized.RulesVersion == HappyBallRules.Version) return normalized;
            // Validate with the legacy geometry before changing any coordinates. Keep
            // the ball's position relative to its current layer, with the same motion
            // and old wall shapes; other layers move farther away as spacing grows.
            normalized.BallY += (normalized.OriginLayer - normalized.PassedLayers) *
                (HappyBallRules.LayerSpacing - HappyBallRules.LegacyLayerSpacing);
            foreach (var layer in normalized.Layers)
                layer.Y = (normalized.OriginLayer - layer.Index) * HappyBallRules.LayerSpacing;
            // The earlier test build deferred smash scoring until passage and kept
            // its charged streak. New impacts score immediately and clear the streak,
            // so this marker upgrades old in-flight saves exactly once without a new schema.
            if (normalized.Power == HappyBallPower.Spent && normalized.NaturalStreak >= HappyBallRules.SmashThreshold)
            {
                normalized.Score += Math.Min((long)normalized.NaturalStreak + 1, long.MaxValue - normalized.Score);
                normalized.NaturalStreak = 0;
            }
            normalized.RulesVersion = HappyBallRules.Version;
            if (!normalized.TryValidate(out reason)) throw new ArgumentException("Migrated snapshot is invalid: " + reason, "snapshot");
            return normalized;
        }

        public HappyBallSnapshot GetSnapshot() { return state.Copy(); }

        public HappyBallStepResult Step(double seconds, double angleDelta)
        {
            if (!HappyBallSnapshot.Finite(seconds) || seconds < 0 || seconds > .25)
                throw new ArgumentOutOfRangeException("seconds", "A step must be between zero and 250 milliseconds.");
            if (!HappyBallSnapshot.Finite(angleDelta) || Math.Abs(angleDelta) > HappyBallGeometry.Tau * 1024)
                throw new ArgumentOutOfRangeException("angleDelta");
            var events = new List<HappyBallEvent>();
            if (state.GameOver || seconds == 0) return new HappyBallStepResult();
            int pieces = Math.Max(1, (int)Math.Ceiling(seconds / HappyBallRules.FixedStep - 1e-10));
            double duration = seconds / pieces;
            for (int i = 0; i < pieces && !state.GameOver; i++)
            {
                var initial = state.Copy();
                var candidateEvents = new List<HappyBallEvent>();
                int blockedLayer;
                if (!Advance(duration, angleDelta / pieces / duration, candidateEvents, out blockedLayer))
                {
                    state = initial;
                    candidateEvents.Clear();
                    int ignored;
                    Advance(duration, 0, candidateEvents, out ignored);
                    candidateEvents.Insert(0, Event(HappyBallEventKind.Blocked, blockedLayer));
                }
                events.AddRange(candidateEvents);
                if (generate) MaintainWindow(initial.PassedLayers);
            }
            return new HappyBallStepResult { Events = events.ToArray() };
        }

        private bool Advance(double duration, double angularSpeed, List<HappyBallEvent> events, out int blockedLayer)
        {
            blockedLayer = -1;
            double remaining = duration;
            while (remaining > 1e-12 && !state.GameOver)
            {
                HappyBallLayer next = null;
                foreach (var layer in state.Layers) if (layer.Index == state.PassedLayers) { next = layer; break; }
                double passTime = next == null ? double.PositiveInfinity : HappyBallCollision.PassTime(state, next, remaining);
                var contact = HappyBallCollision.First(state, remaining, angularSpeed);
                double contactTime = contact == null ? double.PositiveInfinity : contact.Time;
                double time = Math.Min(remaining, Math.Min(passTime, contactTime));
                Move(time, angularSpeed);
                remaining -= time;
                if (passTime <= time && passTime < contactTime)
                {
                    Pass(next, events);
                    continue;
                }
                if (contactTime > time) break;
                // A sphere meets the rounded rim before its centre enters the face's
                // footprint. Classify that entry by the normal-projected approaching
                // motion, rather than making any infinitesimal drag turn a downward
                // red-face impact into a fatal side touch. Large lateral corner sweeps
                // still reject rotation, and a centre within the slab has no vertical
                // face component at all.
                double verticalApproach = state.BallY > contact.Top
                    ? Math.Max(0, (state.BallY - contact.Top) * -state.BallVelocity)
                    : state.BallY < contact.Bottom ? Math.Max(0, (contact.Bottom - state.BallY) * state.BallVelocity) : 0;
                bool angularSide = contact.HorizontalApproach > verticalApproach + 1e-12;
                bool fromTop = !angularSide && state.BallVelocity <= 0 && state.BallY > contact.Top + HappyBallRules.ContactTolerance;
                bool fromBottom = !angularSide && state.BallVelocity > 0 && state.BallY < contact.Bottom - HappyBallRules.ContactTolerance;
                // Vertical contact can have a horizontal normal component at a rounded
                // sphere/edge contact. Side contact is the centre inside the slab's height.
                if (fromTop)
                {
                    if (!contact.Wall && state.Power == HappyBallPower.Armed)
                    {
                        contact.Layer.State = HappyBallLayerState.Broken;
                        state.Power = HappyBallPower.Spent;
                        state.ImpactLayerIndex = contact.Layer.Index;
                        long delta = Math.Min((long)state.NaturalStreak + 1, long.MaxValue - state.Score);
                        state.Score += delta;
                        state.NaturalStreak = 0;
                        state.BallVelocity = HappyBallRules.BounceSpeed;
                        events.Add(ContactEvent(HappyBallEventKind.LayerBroken, contact.Layer.Index, delta));
                        continue;
                    }
                    if (contact.Dangerous) { Die(contact.Layer.Index, events); break; }
                    state.BallY = contact.Top + Math.Sqrt(Math.Max(0, HappyBallRules.BallRadius * HappyBallRules.BallRadius -
                        contact.HorizontalDistance * contact.HorizontalDistance)) + HappyBallRules.ContactTolerance * 2;
                    state.BallVelocity = HappyBallRules.BounceSpeed;
                    if (state.Power != HappyBallPower.Armed) state.NaturalStreak = 0;
                    // A safe wall-top bounce cannot spend a charged horizontal smash.
                    if (state.Power != HappyBallPower.Armed) state.Power = HappyBallPower.Normal;
                    events.Add(ContactEvent(HappyBallEventKind.Bounce, contact.Layer.Index));
                }
                else if (contact.Dangerous) { Die(contact.Layer.Index, events); break; }
                else if (fromBottom)
                {
                    state.BallY = contact.Bottom - Math.Sqrt(Math.Max(0, HappyBallRules.BallRadius * HappyBallRules.BallRadius -
                        contact.HorizontalDistance * contact.HorizontalDistance)) - HappyBallRules.ContactTolerance * 2;
                    state.BallVelocity = 0;
                }
                else if (Math.Abs(angularSpeed) > 1e-12)
                {
                    blockedLayer = contact.Layer.Index;
                    return false;
                }
                else
                {
                    // A static tangency is not an obstacle to vertical travel. Advance
                    // a tiny amount only at exact side tangencies (never through volume).
                    double nudge = Math.Min(remaining, 1e-6);
                    Move(nudge, 0); remaining -= nudge;
                }
            }
            return true;
        }

        private void Move(double seconds, double angularSpeed)
        {
            state.BallY = HappyBallCollision.Y(state.BallY, state.BallVelocity, seconds);
            state.BallVelocity = HappyBallCollision.Velocity(state.BallVelocity, seconds);
            state.TowerAngle = HappyBallGeometry.NormalizeAngle(state.TowerAngle + angularSpeed * seconds);
        }

        private void Pass(HappyBallLayer layer, List<HappyBallEvent> events)
        {
            bool broken = layer.State == HappyBallLayerState.Broken;
            state.PassedLayers++;
            long delta;
            if (broken)
            {
                delta = 0; state.NaturalStreak = 0;
                state.Power = HappyBallPower.Normal; state.ImpactLayerIndex = -1;
            }
            else
            {
                layer.State = HappyBallLayerState.Passed;
                state.NaturalStreak++;
                delta = state.NaturalStreak;
            }
            delta = Math.Min(delta, long.MaxValue - state.Score);
            state.Score += delta;
            events.Add(ContactEvent(HappyBallEventKind.LayerPassed, layer.Index, delta));
            if (!broken && state.Power == HappyBallPower.Normal && state.NaturalStreak >= HappyBallRules.SmashThreshold)
            {
                state.Power = HappyBallPower.Armed;
                events.Add(Event(HappyBallEventKind.Armed, layer.Index));
            }
        }

        private void Die(int layer, List<HappyBallEvent> events)
        {
            state.GameOver = true;
            events.Add(ContactEvent(HappyBallEventKind.Died, layer));
        }

        private HappyBallEvent ContactEvent(HappyBallEventKind kind, int layer, long score = 0)
        {
            var result = Event(kind, layer, score);
            result.HasContact = true;
            result.ContactBallY = state.BallY;
            result.ContactTowerAngle = state.TowerAngle;
            result.ContactOriginLayer = state.OriginLayer;
            return result;
        }

        private static HappyBallEvent Event(HappyBallEventKind kind, int layer, long score = 0)
        { return new HappyBallEvent { Kind = kind, LayerIndex = layer, ScoreDelta = score }; }

        private void FillWindow()
        {
            var layers = new List<HappyBallLayer>(state.Layers);
            while (state.NextLayerIndex < state.PassedLayers + HappyBallRules.WindowLayers)
            {
                var previous = layers.Count == 0 ? null : layers[layers.Count - 1];
                layers.Add(HappyBallGenerator.Next(state.NextLayerIndex, state.OriginLayer, previous, ref state.RandomState));
                state.NextLayerIndex++;
            }
            state.Layers = layers.ToArray();
        }

        private void MaintainWindow(int passedAtStart)
        {
            // Retain removed layers through a subsequent step so consumers can make
            // event-driven fragments from the same geometry without a hidden cache.
            int first = Math.Max(0, passedAtStart - 3);
            var kept = new List<HappyBallLayer>();
            foreach (var layer in state.Layers) if (layer.Index >= first) kept.Add(layer);
            state.Layers = kept.ToArray();
            if (state.PassedLayers - state.OriginLayer >= 64)
            {
                int shift = state.PassedLayers - state.OriginLayer;
                double height = shift * HappyBallRules.LayerSpacing;
                state.OriginLayer += shift; state.BallY += height;
                foreach (var layer in state.Layers) layer.Y += height;
            }
            FillWindow();
        }
    }
}
