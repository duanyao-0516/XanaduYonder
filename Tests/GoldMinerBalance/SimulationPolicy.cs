using System;
using System.Collections.Generic;
using XanaduYonder.Core.GoldMiner;

// Test-only aiming policies. Their ray estimate chooses commands; production Core alone
// decides collisions, ownership, return times, collected money and the deadline.
internal static class SimulationPolicy
{
    internal sealed class Profile
    {
        internal string Name;
        internal int Interval;
        internal double ErrorDegrees, Reaction;
        internal Profile(string name, int interval, double errorDegrees, double reaction)
        { Name = name; Interval = interval; ErrorDegrees = errorDegrees; Reaction = reaction; }
    }
    internal sealed class Result
    {
        internal GoldMinerWitness Witness;
        internal GoldMinerRunSnapshot FinalRun;
        internal int FirstRequiredTick = -1;
    }
    internal static readonly Profile Coarse = new Profile("coarse", 18, 4, .7);
    internal static readonly Profile Focused = new Profile("focused", 6, 2, .95);

    internal static Result Run(GoldMinerMode mode, int level, GoldMinerLayout layout,
        Profile profile, int seed, bool stopAtRequired)
    {
        return Run(GoldMinerLevelGenerator.CreateProofRun(mode, level, layout), profile, seed, stopAtRequired, GoldMinerRules.RequiredIncome(mode, level));
    }

    internal static Result Run(GoldMinerRunSnapshot initial, Profile profile, int seed, bool stopAtRequired, long required)
    {
        var random = new Random(seed);
        var game = new GoldMinerGame(initial);
        game.StartLevel(); var run = game.OwnedSnapshot; long opening = run.Current.Cash;
        var inputs = new List<GoldMinerWitnessInput>(); var result = new Result();
        while (!game.SettlementReady)
        {
            if ((!stopAtRequired || run.Current.Cash - opening < required) && run.Current.Phase == GoldMinerPhase.Running)
            for (int player = 0; player < run.Current.Hooks.Length; player++)
            {
                if (run.Current.SimulationTick % profile.Interval != 0 ||
                    run.Current.Hooks[player].State != GoldMinerHookState.Swinging || random.NextDouble() > profile.Reaction) continue;
                double angle = run.Current.Hooks[player].AngleRadians +
                    (random.NextDouble() * 2 - 1) * profile.ErrorDegrees * Math.PI / 180;
                var entity = PredictTarget(run, player, angle);
                if (entity == null || GoldMinerRules.Value(entity, run.Current.ActiveBuffs) < 100) continue;
                if (game.TryLaunch(player, run.LastCommandSeq + 1))
                    inputs.Add(new GoldMinerWitnessInput { Tick = run.Current.SimulationTick, PlayerId = player });
            }
            game.Step();
            if (result.FirstRequiredTick < 0 && run.Current.Cash - opening >= required)
                result.FirstRequiredTick = run.Current.SimulationTick;
            if (run.Current.SimulationTick > GoldMinerRules.MaximumSimulationTicks(initial.Mode))
                throw new InvalidOperationException("Policy exceeded the production draining bound.");
        }
        result.Witness = new GoldMinerWitness { RequiredIncome = required, ExpectedIncome = run.Current.Cash - opening,
            EndTick = run.Current.SimulationTick, Inputs = inputs.ToArray() };
        result.FinalRun = game.GetSnapshot();
        return result;
    }

    // Same geometric estimate as the offline planner's zero-wait aim. This estimate is
    // deliberately not a substitute physics model; every generated command is replayed.
    static GoldMinerEntity PredictTarget(GoldMinerRunSnapshot run, int player, double angle)
    {
        double dx = Math.Sin(angle), dy = Math.Cos(angle), origin = GoldMinerRules.OriginX(run.Mode, player), closest = double.MaxValue;
        GoldMinerEntity target = null;
        foreach (var entity in run.Current.Entities)
        {
            if (entity.Status != GoldMinerEntityStatus.Available) continue;
            double x = entity.X;
            if (GoldMinerRules.IsAnimal(entity.Kind))
            {
                double travel = entity.Y / (dy * GoldMinerRules.ExtendSpeed) * GoldMinerRules.AnimalSpeed(run.Current.LevelIndex) * entity.MoveDirection;
                double radius = GoldMinerRules.Radius(entity.Kind), span = GoldMinerRules.Width(run.Mode) - 2 * radius;
                double phase = (x - radius + travel) % (2 * span); if (phase < 0) phase += 2 * span;
                x = radius + (phase <= span ? phase : 2 * span - phase);
            }
            x -= origin; double along = x * dx + entity.Y * dy;
            if (along < GoldMinerRules.IdleLength) continue;
            double perpendicular = Math.Abs(x * dy - entity.Y * dx);
            double r = (GoldMinerRules.HookRadius + GoldMinerRules.Radius(entity.Kind)) * .8;
            if (perpendicular > r) continue;
            double first = along - Math.Sqrt(r * r - perpendicular * perpendicular);
            if (first < closest) { closest = first; target = entity; }
        }
        return target;
    }
}
