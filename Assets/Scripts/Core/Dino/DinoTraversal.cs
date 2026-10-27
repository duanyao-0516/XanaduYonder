using System;

namespace XanaduYonder.Core.Dino
{
    /// <summary>Bounded exact-input search using the same motion and swept collision as the live game.</summary>
    internal static class DinoTraversal
    {
        internal static bool TryPlan(DinoRules rules, DinoObstacleKind kind, int group, long spawnTick, double constantSpeed, out DinoPlan plan)
        {
            plan = default(DinoPlan);
            if (group < 1 || group > 3 || (kind >= DinoObstacleKind.LowBird && group != 1)
                || kind < DinoObstacleKind.SmallCactus || kind > DinoObstacleKind.MidBird) return false;
            var obstacle = new DinoObstacle(0, kind, group, rules.ViewportWidth + 2, plan);
            long visible = -1, groundHit = -1, passed = -1;
            double x = obstacle.X;
            // One horizon scan finds full visibility and the first actual standing collision;
            // distance/front-edge division is insufficient for the multi-part silhouettes.
            for (int n = 1; n <= DinoRules.MaximumPlanTicks; n++) {
                long tick = spawnTick + n;
                double dx = Speed(rules, tick, constantSpeed) * DinoRules.StepSeconds;
                double nextX = x - dx;
                if (visible < 0 && nextX + obstacle.Width <= rules.ViewportWidth) visible = tick;
                if (groundHit < 0 && DinoCollision.Hits(rules, 0, 0, false, obstacle.At(x), nextX)) groundHit = tick;
                x = nextX;
                if (x + obstacle.Width < rules.PlayerX) { passed = tick; break; }
            }
            if (visible < 0 || groundHit < 0 || passed < 0) return false;

            if (kind == DinoObstacleKind.MidBird) {
                // Duck is immediate, but its wider silhouette is checked for the complete pass.
                if (!Simulate(rules, obstacle, spawnTick, groundHit - 1, passed, constantSpeed, true, out passed)) return false;
                plan = new DinoPlan { SpawnTick = spawnTick, FullVisibleTick = visible, EarliestActionTick = visible,
                    LatestActionTick = groundHit - 1, RecommendedActionTick = (visible + groundHit - 1) / 2, RecoveryTick = passed };
                return true;
            }

            long first = -1, last = -1, recovery = 0;
            // A held flight lasts < 48 ticks. Search at most 49 candidate input ticks; never enumerate
            // the entire viewport. Choose one contiguous success interval so every contained press works.
            long lower = Math.Max(spawnTick, groundHit - 48);
            for (long action = lower; action < groundHit; action++) {
                long finished;
                bool clears = Simulate(rules, obstacle, spawnTick, action, passed, constantSpeed, false, out finished);
                if (clears) { if (first < 0) first = action; last = action; recovery = Math.Max(recovery, finished); }
                else if (first >= 0) break;
            }
            if (first < 0 || last - first < 6) return false; // At least 100ms of valid press timing, not a single lucky tick.
            plan = new DinoPlan { SpawnTick = spawnTick, FullVisibleTick = visible, EarliestActionTick = first,
                LatestActionTick = last, RecommendedActionTick = (first + last) / 2, RecoveryTick = recovery };
            return true;
        }

        private static bool Simulate(DinoRules rules, DinoObstacle template, long spawnTick, long actionTick, long passedTick,
            double constantSpeed, bool duck, out long recovery)
        {
            recovery = actionTick;
            double traveled = constantSpeed > 0 ? (actionTick - spawnTick) * constantSpeed * DinoRules.StepSeconds : rules.Travel(spawnTick, actionTick);
            double x = template.X - traveled, y = 0, velocity = duck ? 0 : DinoRules.JumpVelocity;
            long end = Math.Max(passedTick + 1, actionTick + 49);
            for (long tick = actionTick + 1; tick <= end; tick++) {
                double dx = Speed(rules, tick, constantSpeed) * DinoRules.StepSeconds / DinoRules.Substeps;
                for (int sub = 0; sub < DinoRules.Substeps; sub++) {
                    double oldY = y;
                    DinoMotion.Advance(ref y, ref velocity, true, duck);
                    double nextX = x - dx;
                    if (DinoCollision.HitsMotion(rules, oldY, y, duck, template.At(x), nextX)) return false;
                    x = nextX;
                }
                if (y == 0 && velocity == 0 && x + template.Width < rules.PlayerX) { recovery = tick; return true; }
            }
            return false;
        }

        private static double Speed(DinoRules rules, long tick, double constantSpeed)
        { return constantSpeed > 0 ? constantSpeed : rules.SpeedAt(tick); }
    }
}
