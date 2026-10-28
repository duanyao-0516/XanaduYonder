using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace XanaduYonder.Core.FlappyBird
{
    public sealed class FlappyGame
    {
        private readonly List<FlappyPipe> pipes = new List<FlappyPipe>(FlappyRules.MaximumPipes);
        private readonly ReadOnlyCollection<FlappyPipe> pipeView;
        private uint random;
        private int lastGap = 2;
        private int nextId = 1;
        private int spawnCountdown;
        private readonly double spawnX;
        public double BirdY { get; private set; }
        public double BirdVelocity { get; private set; }
        public long Score { get; private set; }
        public bool GameOver { get; private set; }
        public double ElapsedTime { get; private set; }
        public IReadOnlyList<FlappyPipe> Pipes { get { return pipeView; } }

        public FlappyGame(uint seed)
        {
            FlappyReachability.EnsureVerified();
            random = seed == 0 ? 0x9E3779B9u : seed;
            pipeView = pipes.AsReadOnly(); BirdY = .55 * FlappyRules.PlayHeight;
            double firstX = FlappyRules.BirdX - FlappyRules.BirdRadius - FlappyRules.PipeWidth
                + FlappyRules.Speed * FlappyRules.FirstPassTick * FlappyRules.FixedStep;
            double travel = FlappyRules.Speed * FlappyRules.FixedStep;
            spawnCountdown = (int)Math.Ceiling((firstX - FlappyRules.Width) / travel);
            spawnX = firstX - travel * spawnCountdown + travel * FlappyRules.PipeIntervalTicks;
            pipes.Add(new FlappyPipe(nextId++, firstX, FlappyReachability.Center(lastGap)));
        }
        public FlappyStepResult Step(bool flap)
        {
            var result = new FlappyStepResult();
            if (GameOver) return result;
            result.Flapped = flap;
            double nextY, nextVelocity;
            FlappyMotion.Predict(BirdY, BirdVelocity, flap, out nextY, out nextVelocity);
            double travel = FlappyRules.Speed * FlappyRules.FixedStep;
            double collision = double.PositiveInfinity;
            if (nextY <= FlappyRules.BirdRadius)
                collision = BirdY <= FlappyRules.BirdRadius ? 0 : (BirdY - FlappyRules.BirdRadius) / (BirdY - nextY);
            for (int i = 0; i < pipes.Count; i++) {
                FlappyPipe pipe = pipes[i]; double contact;
                if (FlappyCollision.SweepPipe(FlappyRules.BirdX, BirdY, FlappyRules.BirdX + travel, nextY, pipe, out contact))
                    collision = Math.Min(collision, contact);
            }
            double fraction = Math.Min(1, collision);
            for (int i = 0; i < pipes.Count; i++) {
                FlappyPipe pipe = pipes[i]; bool passed = pipe.Passed;
                double crossing = (pipe.X + FlappyRules.PipeWidth - (FlappyRules.BirdX - FlappyRules.BirdRadius)) / travel;
                if (!passed && crossing <= 1 + FlappyRules.Epsilon && Math.Max(0, crossing) < collision - FlappyRules.Epsilon) {
                    passed = true;
                    if (Score < long.MaxValue) { Score++; result.Points++; }
                }
                pipes[i] = pipe.At(pipe.X - travel * fraction, passed);
            }
            BirdY += (nextY - BirdY) * fraction;
            double startVelocity = flap ? FlappyRules.FlapVelocity : BirdVelocity;
            BirdVelocity = startVelocity + (nextVelocity - startVelocity) * fraction;
            ElapsedTime += FlappyRules.FixedStep * fraction;
            if (collision <= 1) { GameOver = true; result.Died = true; return result; }
            for (int i = pipes.Count - 1; i >= 0; i--)
                if (pipes[i].X + FlappyRules.PipeWidth < 0) pipes.RemoveAt(i);
            if (--spawnCountdown == 0) {
                // Reuse a fixed spawn anchor. Chaining each new position from an older
                // moving pipe accumulates floating-point error across an unbounded run.
                spawnCountdown = FlappyRules.PipeIntervalTicks;
                int low = Math.Max(0, lastGap - 1), high = Math.Min(4, lastGap + 1);
                random ^= random << 13; random ^= random >> 17; random ^= random << 5;
                lastGap = low + (int)(random % (uint)(high - low + 1));
                if (pipes.Count >= FlappyRules.MaximumPipes) throw new InvalidOperationException("Flappy obstacle bound violated.");
                pipes.Add(new FlappyPipe(nextId, spawnX, FlappyReachability.Center(lastGap)));
                // Only a bounded window of IDs is live. Recycle on overflow without
                // aliasing any pipe still used by presentation.
                bool used;
                do {
                    nextId = nextId == int.MaxValue ? 1 : nextId + 1; used = false;
                    for (int i = 0; i < pipes.Count; i++) if (pipes[i].Id == nextId) { used = true; break; }
                } while (used);
            }
            return result;
        }
    }
}
