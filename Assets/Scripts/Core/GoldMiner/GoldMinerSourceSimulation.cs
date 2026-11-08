using System;
using System.Collections.Generic;
namespace XanaduYonder.Core.GoldMiner
{
    internal static class GoldMinerSourceSimulation
    {
        static GoldMinerEntity Held(GoldMinerState state, GoldMinerHook hook)
        {
            return hook.HeldEntityId.HasValue ? state.Entities[hook.HeldEntityId.Value - 1] : null;
        }
        internal static bool Launch(GoldMinerRunSnapshot run, int player)
        {
            var s = run.Current;
            var h = s.Hooks[player];
            if (s.Phase != GoldMinerPhase.Running || h.State != GoldMinerHookState.Swinging || h.Source.ResetTicks != 0)
                return false;
            h.State = GoldMinerHookState.Extending;
            h.Source.TravelDirection = 1;
            return true;
        }
        internal static bool Bomb(GoldMinerRunSnapshot run, int player, List<GoldMinerEvent> events)
        {
            var s = run.Current;
            var h = s.Hooks[player];
            var e = Held(s, h);
            bool solo = run.Mode == GoldMinerMode.Solo;
            if (s.Phase != GoldMinerPhase.Running || e == null || h.Source.RewardTicks != 0 || h.Source.BombTicks != 0 ||
                (solo ? s.Players[player].Bombs == 0 || e.Source.Weight <= 0 : s.Source.SharedBombs == 0))
                return false;
            if (solo)
            {
                s.Players[player].Bombs--;
                Destroy(e);
                h.HeldEntityId = null;
                h.State = GoldMinerHookState.RetractingEmpty;
            }
            else
            {
                s.Source.SharedBombs--;
                h.Source.BombTicks = 30;
            }
            events.Add(new GoldMinerEvent(GoldMinerEventKind.Dynamite, player, e.Id));
            return true;
        }
        static void Destroy(GoldMinerEntity e)
        {
            e.Status = GoldMinerEntityStatus.Destroyed;
            e.OwnerPlayerId = null;
        }
        internal static GoldMinerStepResult Step(GoldMinerRunSnapshot run, List<GoldMinerEvent> events)
        {
            var s = run.Current;
            if (s.Phase != GoldMinerPhase.Running)
                return new GoldMinerStepResult(false, s.Phase == GoldMinerPhase.Draining, events);
            // Both shipped engines settle immediately on timeout, without a drain bonus.
            if (--s.RemainingTicks == 0)
            {
                s.SimulationTick++;
                s.Phase = GoldMinerPhase.Draining;
                events.Add(new GoldMinerEvent(GoldMinerEventKind.SettlementReady));
                return new GoldMinerStepResult(true, true, events);
            }
            foreach (var e in s.Entities)
            {
                var d = e.Source;
                if (e.Status != GoldMinerEntityStatus.Available || d.MotionEndX == d.InitialX)
                    continue;
                d.MotionTick = (d.MotionTick + 1) % 504;
                double q = d.MotionTick <= 252 ? d.MotionTick / 252.0 : (504 - d.MotionTick) / 252.0;
                e.X = d.InitialX + (d.MotionEndX - d.InitialX) * q;
                e.MoveDirection = (d.MotionEndX > d.InitialX ? 1 : -1) * (d.MotionTick < 252 ? 1 : -1);
            }
            for (int i = 0; i < s.Hooks.Length; i++)
            {
                var h = s.Hooks[i];
                var d = h.Source;
                bool solo = run.Mode == GoldMinerMode.Solo;
                if (d.BombTicks > 0 && --d.BombTicks == 0)
                {
                    var ore = Held(s, h);
                    if (ore != null)
                        Destroy(ore);
                    h.HeldEntityId = null;
                    if (h.State == GoldMinerHookState.RetractingLoaded)
                        h.State = d.TravelDirection > 0 ? GoldMinerHookState.Extending : GoldMinerHookState.RetractingEmpty;
                }
                if (d.ResetTicks > 0)
                {
                    if (--d.ResetTicks == 0)
                        h.State = GoldMinerHookState.Swinging;
                    continue;
                }
                if (d.RewardTicks > 0)
                {
                    if (--d.RewardTicks == 0)
                    {
                        Award(run, h, Held(s, h), events);
                        h.Source.PendingEntityId = null;
                        h.HeldEntityId = null;
                        h.State = GoldMinerHookState.Swinging;
                    }
                    continue;
                }
                if (h.State == GoldMinerHookState.Swinging)
                {
                    if (solo)
                    {
                        d.SwingTick = (d.SwingTick + 1) % 240;
                        h.AngleRadians = Math.PI / 3 * Math.Cos(Math.PI * d.SwingTick / 120);
                    }
                    else
                    {
                        double degrees = -h.AngleRadians * 180 / Math.PI;
                        int floor = (int)Math.Floor(degrees);
                        if (floor >= 60 && h.SwingDirection > 0 || floor <= -60 && h.SwingDirection < 0)
                            h.SwingDirection *= -1;
                        h.AngleRadians = -(degrees + h.SwingDirection) * Math.PI / 180;
                    }
                    continue;
                }
                var held = Held(s, h);
                if (solo && h.State == GoldMinerHookState.Extending)
                    Catch(run, h, events);
                held = Held(s, h);
                double step = solo
                    ? (h.State == GoldMinerHookState.Extending ? .1 : -GoldMinerSourceRules.RetractPerTick(run.Mode, s, held))
                    : GoldMinerSourceRules.RetractPerTick(run.Mode, s, held) * d.TravelDirection;
                if (!solo)
                {
                    bool far = d.Y >= 2.89 || d.X >= 5.1;
                    bool near = d.X < .4 || d.Y < 0;
                    if (d.Y < 0)
                    {
                        Return(run, h, events);
                        continue;
                    }
                    if (far || near)
                    {
                        d.TravelDirection *= -1;
                        if (held == null)
                            h.State = d.TravelDirection > 0 ? GoldMinerHookState.Extending : GoldMinerHookState.RetractingEmpty;
                        step = -step;
                    }
                }
                d.X += Math.Sin(h.AngleRadians) * step;
                d.Y += Math.Cos(h.AngleRadians) * step;
                double px, py;
                GoldMinerSourceRules.HookPoint(run.Mode, h, out px, out py);
                h.Length = Math.Sqrt((px - GoldMinerSourceRules.OriginX(run.Mode, i)) * (px - GoldMinerSourceRules.OriginX(run.Mode, i)) + py * py);
                if (held != null)
                {
                    held.X = px;
                    held.Y = py;
                }
                if (solo)
                {
                    if (h.State == GoldMinerHookState.Extending && (py > 4.34 || px < 0 || px > 6.4))
                        h.State = GoldMinerHookState.RetractingEmpty;
                    else if (h.State != GoldMinerHookState.Extending && d.Y < 0)
                        Return(run, h, events);
                }
                else if (!h.HeldEntityId.HasValue)
                    Catch(run, h, events);
            }
            s.SimulationTick++;
            if (s.RemainingTicks <= 300 && s.RemainingTicks % 60 == 0)
                events.Add(new GoldMinerEvent(GoldMinerEventKind.Countdown, amount: s.RemainingTicks / 60));
            return new GoldMinerStepResult(true, false, events);
        }
        static void Catch(GoldMinerRunSnapshot run, GoldMinerHook h, List<GoldMinerEvent> events)
        {
            var s = run.Current;
            double x, y;
            GoldMinerSourceRules.HookPoint(run.Mode, h, out x, out y);
            foreach (var e in s.Entities)
            {
                if (e.Status != GoldMinerEntityStatus.Available)
                    continue;
                double dx = e.X - x, dy = e.Y - y;
                if (dx * dx + dy * dy >= e.Source.Radius * e.Source.Radius)
                    continue;
                if (run.Mode == GoldMinerMode.Coop)
                    h.Source.TravelDirection *= -1;
                h.State = GoldMinerHookState.RetractingLoaded;
                h.HeldEntityId = e.Id;
                e.Status = GoldMinerEntityStatus.Held;
                e.OwnerPlayerId = h.PlayerId;
                events.Add(new GoldMinerEvent(GoldMinerEventKind.Caught, h.PlayerId, e.Id));
                if (run.Mode == GoldMinerMode.Solo && e.Source.Type == 9)
                {
                    foreach (var other in s.Entities)
                    {
                        double ox = other.X - e.X, oy = other.Y - e.Y;
                        if (other != e && other.Status == GoldMinerEntityStatus.Available && ox * ox + oy * oy <= 1)
                            Destroy(other);
                    }
                    events.Add(new GoldMinerEvent(GoldMinerEventKind.Dynamite, h.PlayerId, e.Id));
                }
                return;
            }
        }
        static void Return(GoldMinerRunSnapshot run, GoldMinerHook h, List<GoldMinerEvent> events)
        {
            h.Source.TravelDirection = 1;
            h.Source.X = GoldMinerSourceRules.OriginX(run.Mode, h.PlayerId);
            h.Source.Y = 0;
            h.Length = run.Mode == GoldMinerMode.Solo ? .4 : .3;
            var e = Held(run.Current, h);
            if (e == null)
            {
                h.State = GoldMinerHookState.Swinging;
                events.Add(new GoldMinerEvent(GoldMinerEventKind.ReturnedEmpty, h.PlayerId));
                return;
            }
            if (run.Mode == GoldMinerMode.Solo)
            {
                h.Source.PendingEntityId = e.Id;
                h.Source.RewardTicks = e.Source.Reward == "dynamite" ? 30 : 54;
            }
            else
            {
                Award(run, h, e, events);
                h.HeldEntityId = null;
                h.State = GoldMinerHookState.RetractingEmpty;
                h.Source.ResetTicks = 6;
            }
        }
        static void Award(GoldMinerRunSnapshot run, GoldMinerHook h, GoldMinerEntity e, List<GoldMinerEvent> events)
        {
            var s = run.Current;
            long value = 0;
            if (run.Mode == GoldMinerMode.Solo)
            {
                if (e.Source.Reward == "power")
                    s.Source.Strength += 5;
                else if (e.Source.Reward == "dynamite")
                    s.Players[h.PlayerId].Bombs++;
                else
                    value = GoldMinerSourceRules.Value(e, s.ActiveBuffs, run.Mode);
            }
            else if (e.Source.Type == 12)
            {
                int chance = s.ActiveBuffs.Clover ? 6 : 3;
                if (GoldMinerSourceRules.Random(ref s.BagRng, 10) < chance)
                    s.Source.Strength = 60;
                else
                    value = GoldMinerSourceRules.Random(ref s.BagRng, 600) + 1;
            }
            else
                value = e.Source.Value;
            e.Source.Award = value;
            e.Source.AwardPlayer = h.PlayerId;
            e.Status = GoldMinerEntityStatus.Collected;
            e.OwnerPlayerId = null;
            s.Cash = checked(s.Cash + value);
            s.Players[h.PlayerId].TotalCollected += value;
            s.Players[h.PlayerId].LevelCollected += value;
            events.Add(new GoldMinerEvent(GoldMinerEventKind.Collected, h.PlayerId, e.Id, value));
        }
    }
}
