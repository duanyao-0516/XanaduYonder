using System;

namespace XanaduYonder.Core.Dino
{
    /// <summary>Editor diagnostics only. Production obstacle generation never invokes or depends on this search.</summary>
    internal static class DinoTraversal
    {
        internal static bool TryPlan(DinoRules rules, DinoObstacle obstacle, long observationTick, out DinoPlan plan)
        {
            plan = new DinoPlan { SpawnTick = obstacle.SpawnTick, EarliestActionTick = -1, LatestActionTick = -1, RecommendedActionTick = -1 };
            long groundHit = -1, passed = -1;
            double x = obstacle.X;
            for (int n = 1; n <= DinoRules.MaximumPlanTicks && observationTick <= long.MaxValue - n; n++) {
                long tick = observationTick + n;
                double nextX = x - DinoRules.ObstacleStep(rules.OriginalSpeedAt(tick - 1), obstacle.SpeedOffset);
                if (groundHit < 0 && DinoCollision.Hits(rules, 0, 0, false, obstacle.At(x), nextX)) groundHit = tick;
                x = nextX;
                if (x + obstacle.Width < rules.PlayerX) { passed = tick; break; }
            }
            if (passed < 0) return false;
            plan.RecoveryTick = passed;
            if (groundHit < 0) { plan.Action = DinoAction.None; return true; }
            if (obstacle.Kind == DinoObstacleKind.MidBird) {
                long recovery;
                if (Simulate(rules, obstacle, observationTick, observationTick, passed, true, out recovery)) {
                    plan.Action = DinoAction.Duck;
                    plan.EarliestActionTick = observationTick; plan.LatestActionTick = groundHit - 1;
                    plan.RecommendedActionTick = groundHit - 1; plan.RecoveryTick = recovery;
                    return true;
                }
            }
            long first = -1, last = -1, finalRecovery = 0;
            for (long action = Math.Max(observationTick, groundHit - 60); action < groundHit; action++) {
                long recovery;
                if (Simulate(rules, obstacle, observationTick, action, passed, false, out recovery)) {
                    if (first < 0) first = action;
                    last = action; finalRecovery = Math.Max(finalRecovery, recovery);
                } else if (first >= 0) break;
            }
            if (first < 0) return false;
            plan.Action = DinoAction.Jump; plan.EarliestActionTick = first; plan.LatestActionTick = last;
            plan.RecommendedActionTick = first; plan.RecoveryTick = finalRecovery;
            return true;
        }

        private static bool Simulate(DinoRules rules, DinoObstacle template, long observed, long action, long passed, bool duck, out long recovery)
        {
            recovery = action;
            double x = template.X;
            for (long tick = observed; tick < action; tick++) x -= DinoRules.ObstacleStep(rules.OriginalSpeedAt(tick), template.SpeedOffset);
            DinoMotionState state = DinoMotionState.Grounded;
            if (!duck) DinoMotion.Start(ref state, rules.OriginalSpeedAt(action));
            long end = Math.Max(passed, action + 60);
            for (long tick = action; tick < end; tick++) {
                double previousY = state.WorldY;
                DinoMotion.Advance(ref state);
                double nextX = x - DinoRules.ObstacleStep(rules.OriginalSpeedAt(tick), template.SpeedOffset);
                if (DinoCollision.HitsMotion(rules, previousY, state.WorldY, duck, template.At(x), nextX)) return false;
                x = nextX;
                if (!state.Jumping && x + template.Width < rules.PlayerX) { recovery = tick + 1; return true; }
            }
            return false;
        }
    }
}
