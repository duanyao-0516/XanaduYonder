using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.GoldMiner
{
    public sealed class GoldMinerGame
    {
        private readonly GoldMinerRunSnapshot run;
        private readonly List<GoldMinerEvent> pendingEvents = new List<GoldMinerEvent>();
        public GoldMinerGame(GoldMinerRunSnapshot snapshot)
        {
            string reason = null;
            if (snapshot == null || !snapshot.TryValidate(out reason))
                throw new ArgumentException(snapshot == null ? "Missing snapshot." : reason, "snapshot");
            run = snapshot.Copy();
        }

        internal GoldMinerRunSnapshot OwnedSnapshot
        {
            get
            {
                return run;
            }
        }

        public GoldMinerRunSnapshot GetSnapshot()
        {
            return run.Copy();
        }

        public GoldMinerState GetStateSnapshot()
        {
            return run.Current.Copy();
        }

        public bool SettlementReady
        {
            get
            {
                return run.Current.Phase == GoldMinerPhase.Draining && AllIdle();
            }
        }

        public bool StartLevel()
        {
            if (run.Current.Phase != GoldMinerPhase.Ready)
                return false;
            run.Current.Phase = GoldMinerPhase.Running;
            return true;
        }

        public bool TryLaunch(int playerId, long commandSeq)
        {
            if (!ConsumeCommand(playerId, commandSeq))
                return false;
            var state = run.Current;
            var hook = state.Hooks[playerId];
            if (state.Phase != GoldMinerPhase.Running || hook.State != GoldMinerHookState.Swinging)
                return false;
            hook.State = GoldMinerHookState.Extending;
            pendingEvents.Add(new GoldMinerEvent(GoldMinerEventKind.Launched, playerId));
            return true;
        }

        public bool TryUseDynamite(int playerId, long commandSeq)
        {
            if (!ConsumeCommand(playerId, commandSeq))
                return false;
            var state = run.Current;
            var hook = state.Hooks[playerId];
            if ((state.Phase != GoldMinerPhase.Running && state.Phase != GoldMinerPhase.Draining) || hook.State != GoldMinerHookState.RetractingLoaded || state.Players[playerId].Bombs == 0)
                return false;
            var entity = Find(hook.HeldEntityId.Value);
            entity.Status = GoldMinerEntityStatus.Destroyed;
            entity.OwnerPlayerId = null;
            state.Players[playerId].Bombs--;
            hook.HeldEntityId = null;
            hook.State = GoldMinerHookState.RetractingEmpty;
            pendingEvents.Add(new GoldMinerEvent(GoldMinerEventKind.Dynamite, playerId, entity.Id));
            return true;
        }

        private bool ConsumeCommand(int playerId, long seq)
        {
            if (playerId < 0 || playerId >= run.Current.Players.Length || seq <= run.LastCommandSeq || seq < 0 || run.LastCommandSeq == long.MaxValue)
                return false;
            run.LastCommandSeq = seq;
            return true;
        }

        private sealed class Contact
        {
            internal double Time;
            internal int PlayerId;
            internal GoldMinerEntity Entity;
        }

        public GoldMinerStepResult Step()
        {
            var events = new List<GoldMinerEvent>(pendingEvents);
            pendingEvents.Clear();
            var state = run.Current;
            if ((state.Phase != GoldMinerPhase.Running && state.Phase != GoldMinerPhase.Draining) || SettlementReady)
                return new GoldMinerStepResult(false, SettlementReady, events);
            if (state.SimulationTick == int.MaxValue)
                throw new InvalidOperationException("Simulation tick exhausted.");
            var paths = new Dictionary<int, List<GoldMinerCollision.Segment>>();
            foreach (var entity in state.Entities)
                if (entity.Status == GoldMinerEntityStatus.Available)
                    paths.Add(entity.Id, GoldMinerCollision.Trajectory(entity, run.Mode, state.LevelIndex));
            var contacts = new List<Contact>();
            var extensionTime = new double[state.Hooks.Length];
            for (int i = 0; i < state.Hooks.Length; i++)
            {
                var hook = state.Hooks[i];
                if (hook.State != GoldMinerHookState.Extending)
                    continue;
                double distance = GoldMinerRules.ExtendSpeed * GoldMinerRules.TickSeconds;
                double boundary = GoldMinerValidation.BoundaryLength(run.Mode, hook);
                double end = Math.Min(1, Math.Max(0, (boundary - hook.Length) / distance));
                extensionTime[i] = end;
                foreach (var entity in state.Entities)
                {
                    if (entity.Status != GoldMinerEntityStatus.Available)
                        continue;
                    double time;
                    if (GoldMinerCollision.FirstContact(GoldMinerRules.OriginX(run.Mode, i), hook.AngleRadians, hook.Length, distance, end, paths[entity.Id], GoldMinerRules.HookRadius + GoldMinerRules.Radius(entity.Kind), out time))
                        contacts.Add(new Contact { Time = time, PlayerId = i, Entity = entity });
                }
            }

            contacts.Sort((a, b) =>
            {
                int n = a.Time.CompareTo(b.Time);
                if (n != 0)
                    return n;
                n = a.PlayerId.CompareTo(b.PlayerId);
                return n != 0 ? n : a.Entity.Id.CompareTo(b.Entity.Id);
            });
            var catches = new Contact[state.Hooks.Length];
            foreach (var contact in contacts)
            {
                if (catches[contact.PlayerId] != null || contact.Entity.Status != GoldMinerEntityStatus.Available)
                    continue;
                catches[contact.PlayerId] = contact;
                contact.Entity.Status = GoldMinerEntityStatus.Held;
                contact.Entity.OwnerPlayerId = contact.PlayerId;
            }

            foreach (var entity in state.Entities)
            {
                if (entity.Status != GoldMinerEntityStatus.Available)
                    continue;
                var path = paths[entity.Id];
                var last = path[path.Count - 1];
                entity.X = last.X1;
                entity.Y = last.Y1;
                if (GoldMinerRules.IsAnimal(entity.Kind))
                    entity.MoveDirection = last.Direction;
            }

            for (int i = 0; i < state.Hooks.Length; i++)
            {
                var hook = state.Hooks[i];
                if (hook.State == GoldMinerHookState.Swinging)
                {
                    if (state.Phase == GoldMinerPhase.Running)
                        Swing(hook);
                    continue;
                }

                double remaining = 1;
                if (hook.State == GoldMinerHookState.Extending)
                {
                    var caught = catches[i];
                    double time = caught == null ? extensionTime[i] : caught.Time;
                    hook.Length += GoldMinerRules.ExtendSpeed * GoldMinerRules.TickSeconds * time;
                    remaining = 1 - time;
                    if (caught != null)
                    {
                        hook.State = GoldMinerHookState.RetractingLoaded;
                        hook.HeldEntityId = caught.Entity.Id;
                        events.Add(new GoldMinerEvent(GoldMinerEventKind.Caught, i, caught.Entity.Id));
                    }
                    else if (extensionTime[i] < 1 || hook.Length >= GoldMinerValidation.BoundaryLength(run.Mode, hook))
                        hook.State = GoldMinerHookState.RetractingEmpty;
                    else
                        continue;
                }

                Retract(hook, remaining, events);
            }

            state.SimulationTick++;
            if (state.RemainingTicks > 0)
                state.RemainingTicks--;
            if (state.RemainingTicks > 0 && state.RemainingTicks <= 5 * GoldMinerRules.TicksPerSecond && state.RemainingTicks % GoldMinerRules.TicksPerSecond == 0)
                events.Add(new GoldMinerEvent(GoldMinerEventKind.Countdown, amount: state.RemainingTicks / GoldMinerRules.TicksPerSecond));
            if (state.Phase == GoldMinerPhase.Running && (state.RemainingTicks == 0 || (!HasAvailable() && AllIdle())))
                state.Phase = GoldMinerPhase.Draining;
            if (SettlementReady)
                events.Add(new GoldMinerEvent(GoldMinerEventKind.SettlementReady));
            return new GoldMinerStepResult(true, SettlementReady, events);
        }

        private static void Swing(GoldMinerHook hook)
        {
            hook.AngleRadians += hook.SwingDirection * GoldMinerRules.SwingSpeed * GoldMinerRules.TickSeconds;
            if (hook.AngleRadians > GoldMinerRules.MaxAngle)
            {
                hook.AngleRadians = 2 * GoldMinerRules.MaxAngle - hook.AngleRadians;
                hook.SwingDirection = -1;
            }
            else if (hook.AngleRadians < -GoldMinerRules.MaxAngle)
            {
                hook.AngleRadians = -2 * GoldMinerRules.MaxAngle - hook.AngleRadians;
                hook.SwingDirection = 1;
            }
        }

        private void Retract(GoldMinerHook hook, double fraction, List<GoldMinerEvent> events)
        {
            if (hook.State != GoldMinerHookState.RetractingEmpty && hook.State != GoldMinerHookState.RetractingLoaded)
                return;
            var entity = hook.HeldEntityId == null ? null : Find(hook.HeldEntityId.Value);
            double speed = entity == null ? GoldMinerRules.EmptyRetractSpeed : GoldMinerRules.LoadedRetractSpeed / Math.Max(GoldMinerRules.Weight(entity.Kind), .5) * (run.Current.ActiveBuffs.Strength ? 1.5 : 1);
            hook.Length = Math.Max(GoldMinerRules.IdleLength, hook.Length - speed * GoldMinerRules.TickSeconds * fraction);
            if (entity != null)
            {
                entity.X = GoldMinerRules.OriginX(run.Mode, hook.PlayerId) + Math.Sin(hook.AngleRadians) * hook.Length;
                entity.Y = Math.Cos(hook.AngleRadians) * hook.Length;
            }

            if (hook.Length > GoldMinerRules.IdleLength)
                return;
            if (entity != null)
            {
                long value = GoldMinerRules.Value(entity, run.Current.ActiveBuffs);
                var player = run.Current.Players[hook.PlayerId];
                checked
                {
                    run.Current.Cash += value;
                    player.TotalCollected += value;
                    player.LevelCollected += value;
                }

                entity.Status = GoldMinerEntityStatus.Collected;
                entity.OwnerPlayerId = null;
                events.Add(new GoldMinerEvent(GoldMinerEventKind.Collected, hook.PlayerId, entity.Id, value));
            }
            else
                events.Add(new GoldMinerEvent(GoldMinerEventKind.ReturnedEmpty, hook.PlayerId));
            hook.HeldEntityId = null;
            hook.State = GoldMinerHookState.Swinging;
        }

        private bool AllIdle()
        {
            foreach (var h in run.Current.Hooks)
                if (h.State != GoldMinerHookState.Swinging)
                    return false;
            return true;
        }

        private bool HasAvailable()
        {
            foreach (var e in run.Current.Entities)
                if (e.Status == GoldMinerEntityStatus.Available)
                    return true;
            return false;
        }

        private GoldMinerEntity Find(int id)
        {
            foreach (var e in run.Current.Entities)
                if (e.Id == id)
                    return e;
            throw new InvalidOperationException("Missing entity.");
        }
    }
}
