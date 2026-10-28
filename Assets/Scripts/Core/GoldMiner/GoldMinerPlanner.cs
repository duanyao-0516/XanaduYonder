using System;
using System.Collections.Generic;
namespace XanaduYonder.Core.GoldMiner
{
    /// <summary>Bounded offline beam search. Every child and final witness uses GoldMinerGame.</summary>
    public static class GoldMinerPlanner
    {
        public const int BeamWidth = 128, ExpansionBudget = 20000;
        sealed class Node
        {
            internal GoldMinerRunSnapshot Run;
            internal List<GoldMinerWitnessInput> Inputs;
            internal double Score;
            internal long Order;
        }
        sealed class Action
        {
            internal int Wait, P1 = -1, P2 = -1;
        }
        public static GoldMinerWitness FindWitness(GoldMinerMode mode, int level, GoldMinerLayout layout, out int expanded)
        {
            return FindWitness(mode, level, layout, ExpansionBudget, out expanded);
        }
        public static GoldMinerWitness FindWitness(GoldMinerMode mode, int level, GoldMinerLayout layout, int expansionBudget, out int expanded)
        {
            if (expansionBudget < 0 || expansionBudget > ExpansionBudget) throw new ArgumentOutOfRangeException("expansionBudget");
            expanded = 0; long order = 0;
            var initial = GoldMinerLevelGenerator.CreateProofRun(mode, level, layout);
            var initialGame = new GoldMinerGame(initial); initialGame.StartLevel();
            long opening = initial.Current.Cash, required = GoldMinerRules.RequiredIncome(mode, level);
            var beam = new List<Node> { new Node { Run = initialGame.GetSnapshot(), Inputs = new List<GoldMinerWitnessInput>() } };
            while (beam.Count > 0 && expanded < expansionBudget)
            {
                var next = new List<Node>();
                foreach (var node in beam)
                {
                    if (expanded >= expansionBudget) break;
                    expanded++;
                    foreach (var action in Actions(node.Run))
                    {
                        var game = new GoldMinerGame(node.Run);
                        for (int i = 0; i < action.Wait; i++) game.Step();
                        var run = game.OwnedSnapshot;
                        if (run.Current.Phase != GoldMinerPhase.Running) continue;
                        var inputs = new List<GoldMinerWitnessInput>(node.Inputs);
                        int tick = run.Current.SimulationTick;
                        bool launched = false;
                        if (action.P1 >= 0 && game.TryLaunch(0, run.LastCommandSeq + 1)) { inputs.Add(new GoldMinerWitnessInput { Tick = tick, PlayerId = 0 }); launched = true; }
                        if (action.P2 >= 0 && game.TryLaunch(1, run.LastCommandSeq + 1)) { inputs.Add(new GoldMinerWitnessInput { Tick = tick, PlayerId = 1 }); launched = true; }
                        if (!launched) continue;
                        // An action settles its launched hooks; other idle hooks still swing in Core.
                        do { game.Step(); } while (!game.SettlementReady && Busy(run.Current));
                        long income = run.Current.Cash - opening;
                        if (income >= required)
                        {
                            while (!game.SettlementReady) game.Step();
                            return new GoldMinerWitness { RequiredIncome = required, ExpectedIncome = run.Current.Cash - opening, EndTick = run.Current.SimulationTick, Inputs = inputs.ToArray() };
                        }
                        if (run.Current.RemainingTicks == 0 || game.SettlementReady || income == node.Run.Current.Cash - opening) continue;
                        double score = income - (double)run.Current.SimulationTick * required / GoldMinerRules.LevelTicks * .7;
                        next.Add(new Node { Run = game.GetSnapshot(), Inputs = inputs, Score = score, Order = order++ });
                    }
                }
                next.Sort((a, b) => { int n = b.Score.CompareTo(a.Score); return n != 0 ? n : a.Order.CompareTo(b.Order); });
                // Quantized deduplication is only a search heuristic; witnesses replay exact Core states.
                var keys = new HashSet<string>(); beam.Clear();
                foreach (var node in next)
                {
                    string key = Key(node.Run.Current);
                    if (!keys.Add(key)) continue;
                    beam.Add(node); if (beam.Count == BeamWidth) break;
                }
            }
            return null;
        }
        static bool Busy(GoldMinerState state) { foreach (var hook in state.Hooks) if (hook.State != GoldMinerHookState.Swinging) return true; return false; }
        static string Key(GoldMinerState state)
        {
            ulong bits = 0; foreach (var e in state.Entities) if (e.Status == GoldMinerEntityStatus.Collected && e.Id <= 64) bits |= 1UL << (e.Id - 1);
            string value = bits + ":" + state.SimulationTick;
            foreach (var h in state.Hooks) value += ":" + Math.Round(h.AngleRadians * 180 / Math.PI) + ":" + h.SwingDirection;
            return value;
        }
        static List<Action> Actions(GoldMinerRunSnapshot run)
        {
            var result = new List<Action>(); var state = run.Current;
            var angles = new double[state.Hooks.Length]; var directions = new int[angles.Length];
            for (int i = 0; i < angles.Length; i++) { angles[i] = state.Hooks[i].AngleRadians; directions[i] = state.Hooks[i].SwingDirection; }
            var seen1 = new HashSet<int>(); var seen2 = new HashSet<int>(); var pairs = new HashSet<long>();
            int horizon = Math.Min(320, state.RemainingTicks);
            for (int wait = 0; wait < horizon; wait++)
            {
                int p1 = Target(run, 0, angles[0], wait);
                int p2 = angles.Length > 1 ? Target(run, 1, angles[1], wait) : -1;
                if (p1 >= 0 && seen1.Add(p1)) result.Add(new Action { Wait = wait, P1 = p1 });
                if (p2 >= 0 && seen2.Add(p2)) result.Add(new Action { Wait = wait, P2 = p2 });
                if (p1 >= 0 && p2 >= 0 && p1 != p2 && pairs.Add(((long)p1 << 32) | (uint)p2)) result.Add(new Action { Wait = wait, P1 = p1, P2 = p2 });
                for (int i = 0; i < angles.Length; i++)
                {
                    angles[i] += directions[i] * GoldMinerRules.SwingSpeed * GoldMinerRules.TickSeconds;
                    if (angles[i] > GoldMinerRules.MaxAngle) { angles[i] = 2 * GoldMinerRules.MaxAngle - angles[i]; directions[i] = -1; }
                    else if (angles[i] < -GoldMinerRules.MaxAngle) { angles[i] = -2 * GoldMinerRules.MaxAngle - angles[i]; directions[i] = 1; }
                }
            }
            result.Sort((a, b) => { int n = a.Wait.CompareTo(b.Wait); if (n != 0) return n; n = a.P1.CompareTo(b.P1); return n != 0 ? n : a.P2.CompareTo(b.P2); });
            return result;
        }
        static int Target(GoldMinerRunSnapshot run, int player, double angle, int wait)
        {
            double dx = Math.Sin(angle), dy = Math.Cos(angle), origin = GoldMinerRules.OriginX(run.Mode, player), closest = double.MaxValue; int target = -1;
            foreach (var entity in run.Current.Entities)
            {
                if (entity.Status != GoldMinerEntityStatus.Available) continue;
                double x = entity.X;
                if (GoldMinerRules.IsAnimal(entity.Kind))
                {
                    double travel = (wait * GoldMinerRules.TickSeconds + entity.Y / (dy * GoldMinerRules.ExtendSpeed)) * GoldMinerRules.AnimalSpeed(run.Current.LevelIndex) * entity.MoveDirection;
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
                if (first < closest) { closest = first; target = entity.Id; }
            }
            return target;
        }
        public static bool ReplayWitness(GoldMinerMode mode, int level, GoldMinerLayout layout, out string reason)
        {
            reason = null; var witness = layout == null ? null : layout.Witness;
            if (witness == null || witness.Inputs == null || witness.Inputs.Length > 1024 || witness.RequiredIncome != GoldMinerRules.RequiredIncome(mode, level) || witness.ExpectedIncome < witness.RequiredIncome || witness.EndTick < 0 || witness.EndTick > 10000) { reason = "Invalid witness header."; return false; }
            var game = new GoldMinerGame(GoldMinerLevelGenerator.CreateProofRun(mode, level, layout)); game.StartLevel();
            var run = game.OwnedSnapshot; long opening = run.Current.Cash; int index = 0, lastTick = -1, lastPlayer = -1;
            foreach (var input in witness.Inputs)
            {
                if (input == null || input.Action != "launch" || input.Tick < 0 || input.Tick >= GoldMinerRules.LevelTicks || input.Tick < lastTick || input.Tick == lastTick && input.PlayerId <= lastPlayer || input.PlayerId < 0 || input.PlayerId >= GoldMinerRules.PlayerCount(mode)) { reason = "Invalid witness command."; return false; }
                lastTick = input.Tick; lastPlayer = input.PlayerId;
            }
            while (!game.SettlementReady && run.Current.SimulationTick < 10000)
            {
                while (index < witness.Inputs.Length && witness.Inputs[index].Tick == run.Current.SimulationTick)
                {
                    var input = witness.Inputs[index++];
                    if (!game.TryLaunch(input.PlayerId, run.LastCommandSeq + 1)) { reason = "Rejected witness launch."; return false; }
                }
                game.Step();
            }
            if (!game.SettlementReady || index != witness.Inputs.Length || run.Current.Cash - opening != witness.ExpectedIncome || run.Current.Cash - opening < witness.RequiredIncome || run.Current.Cash < run.Current.Target || run.Current.SimulationTick != witness.EndTick) { reason = "Witness replay did not match successful income/time."; return false; }
            var root = new GoldMinerRootSnapshot { LastMode = mode };
            if (mode == GoldMinerMode.Solo) root.Solo = game.GetSnapshot(); else root.Coop = game.GetSnapshot();
            var session = new GoldMinerSession(root);
            var settlement = session.PrepareSettlement();
            if (settlement == null || settlement.Snapshot.GetRun(mode).Current.Phase != (level == 10 ? GoldMinerPhase.Completed : GoldMinerPhase.Won))
            { reason = "Production settlement rejected successful witness."; return false; }
            return true;
        }
    }
}
