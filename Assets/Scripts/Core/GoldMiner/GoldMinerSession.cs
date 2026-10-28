using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.GoldMiner
{
    public sealed class GoldMinerCandidate
    {
        private readonly GoldMinerRootSnapshot snapshot;
        internal readonly object Owner;
        internal readonly bool ChangesGeneration;
        internal GoldMinerCandidate(object owner, GoldMinerRootSnapshot value, long baseTxn, long revision, long generation, GoldMinerMode mode, bool changesGeneration)
        {
            Owner = owner;
            snapshot = value.Copy();
            BaseTxnSeq = baseTxn;
            BaseStateRevision = revision;
            BasePageGeneration = generation;
            BaseMode = mode;
            ChangesGeneration = changesGeneration;
        }

        public GoldMinerRootSnapshot Snapshot
        {
            get
            {
                return snapshot.Copy();
            }
        }

        public long BaseTxnSeq { get; private set; }
        public long BaseStateRevision { get; private set; }
        public long BasePageGeneration { get; private set; }
        public GoldMinerMode BaseMode { get; private set; }
    }

    public sealed class GoldMinerSession
    {
        private GoldMinerRootSnapshot root;
        private GoldMinerGame game;
        private readonly object identity = new object ();
        public long StateRevision { get; private set; }
        public long PageGeneration { get; private set; }
        public bool Dirty { get; private set; }
        public GoldMinerSuspendReason SuspendReasons { get; private set; }

        public bool IsSuspended
        {
            get
            {
                return SuspendReasons != GoldMinerSuspendReason.None;
            }
        }

        public GoldMinerMode ActiveMode
        {
            get
            {
                return root.LastMode;
            }
        }

        public string LastError { get; private set; }

        public GoldMinerSession(GoldMinerRootSnapshot snapshot = null)
        {
            root = snapshot == null ? new GoldMinerRootSnapshot() : snapshot.Copy();
            string reason;
            if (!root.TryValidate(out reason))
                throw new ArgumentException(reason, "snapshot");
            if (root.RulesVersion != GoldMinerRules.Version)
            {
                // Upgrade only the owned, fully validated copy. Cash, clocks, layouts,
                // in-flight hooks and both slots survive until the next normal save.
                root.RulesVersion = GoldMinerRules.Version;
                Dirty = true;
            }
            SuspendReasons = GoldMinerSuspendReason.NeedsExplicitContinue;
            RebuildGame();
        }

        public GoldMinerRootSnapshot GetSnapshot()
        {
            return root.Copy();
        }

        public GoldMinerRunSnapshot GetRunSnapshot()
        {
            var run = root.GetRun(ActiveMode);
            return run == null ? null : run.Copy();
        }

        public GoldMinerState GetStateSnapshot()
        {
            return game == null ? null : game.GetStateSnapshot();
        }

        public void MarkSaved(long revision)
        {
            if (revision == StateRevision)
                Dirty = false;
        }

        public void EnterMode(GoldMinerMode mode)
        {
            if (!GoldMinerRules.ValidMode(mode))
                throw new ArgumentOutOfRangeException("mode");
            root.LastMode = mode;
            PageGeneration = checked(PageGeneration + 1);
            SuspendReasons &= ~GoldMinerSuspendReason.NotVisible;
            SuspendReasons |= GoldMinerSuspendReason.NeedsExplicitContinue;
            Changed();
            RebuildGame();
        }

        public void Suspend(GoldMinerSuspendReason reason)
        {
            var before = SuspendReasons;
            SuspendReasons |= reason;
            if (reason != GoldMinerSuspendReason.None)
                SuspendReasons |= GoldMinerSuspendReason.NeedsExplicitContinue;
            if (reason == GoldMinerSuspendReason.NotVisible)
                PageGeneration = checked(PageGeneration + 1);
            if (before != SuspendReasons)
                StateRevision = checked(StateRevision + 1);
        }

        public void Resume(GoldMinerSuspendReason reason)
        {
            var before = SuspendReasons;
            SuspendReasons &= ~reason;
            if (before != SuspendReasons)
                StateRevision = checked(StateRevision + 1);
        }

        public bool StartLevel()
        {
            if (IsSuspended || game == null || !game.StartLevel())
                return false;
            Changed();
            return true;
        }

        public bool TryLaunch(int playerId, long commandSeq)
        {
            return Command(playerId, commandSeq, false);
        }

        public bool TryUseDynamite(int playerId, long commandSeq)
        {
            return Command(playerId, commandSeq, true);
        }

        private bool Command(int playerId, long commandSeq, bool dynamite)
        {
            if (IsSuspended || game == null)
                return false;
            long previous = root.GetRun(ActiveMode).LastCommandSeq;
            bool result = dynamite ? game.TryUseDynamite(playerId, commandSeq) : game.TryLaunch(playerId, commandSeq);
            if (root.GetRun(ActiveMode).LastCommandSeq != previous)
                Changed();
            return result;
        }

        public GoldMinerStepResult Step()
        {
            if (IsSuspended || game == null)
                return new GoldMinerStepResult(false, false, new List<GoldMinerEvent>());
            var result = game.Step();
            if (result.Advanced)
                Changed();
            return result;
        }

        public GoldMinerCandidate PrepareNewRun(GoldMinerMode mode, string runId, uint seed, GoldMinerLevelStart start)
        {
            LastError = null;
            string reason;
            if (!GoldMinerRules.ValidMode(mode) || !GoldMinerValidation.Hex(runId, 32) || seed == 0 || start == null || !start.TryValidate(mode, out reason))
                return Fail("Invalid new challenge or selected layout.");
            if (start.LevelIndex != 1 || start.OpeningCash != 0 || start.OpeningShopSpent != 0 || !start.ActiveBuffs.IsEmpty)
                return Fail("New challenge must start at level 1 with no income or buffs.");
            foreach (var p in start.OpeningPlayers)
                if (p.Bombs != 1 || p.TotalCollected != 0)
                    return Fail("Invalid initial player inventory.");
            var existing = root.GetRun(mode);
            if (existing != null && existing.RunId == runId)
                return Fail("A new challenge needs a new run ID.");
            var candidate = root.Copy();
            candidate.LastMode = mode;
            SetRun(candidate, mode, new GoldMinerRunSnapshot { RunId = runId, Mode = mode, RunSeed = seed, Current = start.CreateState(mode), Checkpoint = start.Copy() });
            return Candidate(candidate, true);
        }

        public GoldMinerCandidate PrepareRetryLevel()
        {
            LastError = null;
            var run = root.GetRun(ActiveMode);
            if (run == null || run.AttemptIndex == int.MaxValue)
                return Fail("No retry available or attempt sequence exhausted.");
            var phase = run.Current.Phase;
            if (phase != GoldMinerPhase.Failed && phase != GoldMinerPhase.Running && phase != GoldMinerPhase.Draining && phase != GoldMinerPhase.Ready)
                return Fail("This phase cannot retry.");
            var candidate = root.Copy();
            var next = candidate.GetRun(ActiveMode);
            next.AttemptIndex++;
            // A failed legacy level keeps the goal the player accepted, even after rules upgrade.
            next.Current = next.Checkpoint.CreateState(ActiveMode, run.Current.Target);
            return Candidate(candidate, true);
        }

        public GoldMinerCandidate PrepareEnterShop()
        {
            LastError = null;
            var run = root.GetRun(ActiveMode);
            if (run == null || run.Current.Phase != GoldMinerPhase.Won || run.Current.LevelIndex >= GoldMinerRules.LevelCount)
                return Fail("No shop available.");
            var candidate = root.Copy();
            var next = candidate.GetRun(ActiveMode);
            next.Current.Shop = GoldMinerEconomy.CreateShop(next);
            next.Current.Settlement = null;
            next.Current.Phase = GoldMinerPhase.Shop;
            return Candidate(candidate, false);
        }

        public GoldMinerCandidate PreparePurchase(GoldMinerItem item, int playerId = 0)
        {
            LastError = null;
            var run = root.GetRun(ActiveMode);
            if (run == null)
                return Fail("No active challenge.");
            string reason;
            if (!GoldMinerEconomy.CanPurchase(run.Current, item, playerId, out reason))
                return Fail(reason);
            var candidate = root.Copy();
            GoldMinerEconomy.Purchase(candidate.GetRun(ActiveMode).Current, item, playerId);
            return Candidate(candidate, false);
        }

        public GoldMinerCandidate PrepareNextLevel(GoldMinerLevelStart start)
        {
            LastError = null;
            var run = root.GetRun(ActiveMode);
            string reason;
            if (run == null || run.Current.Phase != GoldMinerPhase.Shop || start == null || !start.TryValidate(ActiveMode, out reason))
                return Fail("Cannot enter the next selected level.");
            var c = run.Current;
            if (start.LevelIndex != c.LevelIndex + 1 || start.PreviousPatternId != c.PatternId || start.OpeningCash != c.Cash || start.OpeningShopSpent != c.ShopSpent || !start.ActiveBuffs.Same(c.PendingBuffs))
                return Fail("Next level does not preserve the completed shop transaction.");
            for (int i = 0; i < c.Players.Length; i++)
                if (start.OpeningPlayers[i].Bombs != c.Players[i].Bombs || start.OpeningPlayers[i].TotalCollected != c.Players[i].TotalCollected)
                    return Fail("Next level player inventory changed.");
            var candidate = root.Copy();
            var next = candidate.GetRun(ActiveMode);
            // Cumulative targets must stay on the accepted campaign curve. Switching a legacy
            // wallet to a later v3 target can require more income than the entire mine contains.
            bool legacyCurve = c.Target == GoldMinerRules.LegacyTarget(ActiveMode, c.LevelIndex)
                && c.Target != GoldMinerRules.Target(ActiveMode, c.LevelIndex);
            long target = legacyCurve ? GoldMinerRules.LegacyTarget(ActiveMode, start.LevelIndex)
                : GoldMinerRules.Target(ActiveMode, start.LevelIndex);
            next.Current = start.CreateState(ActiveMode, target);
            next.Checkpoint = start.Copy();
            next.AttemptIndex = 0;
            return Candidate(candidate, true);
        }

        public GoldMinerCandidate PrepareSettlement()
        {
            LastError = null;
            var run = root.GetRun(ActiveMode);
            if (game == null || !game.SettlementReady || run == null)
                return Fail("Settlement is not ready.");
            var candidate = root.Copy();
            var next = candidate.GetRun(ActiveMode);
            var state = next.Current;
            bool success = state.Cash >= state.Target;
            state.Phase = success ? (state.LevelIndex == 10 ? GoldMinerPhase.Completed : GoldMinerPhase.Won) : GoldMinerPhase.Failed;
            state.Settlement = new GoldMinerSettlement
            {
                Id = next.RunId + "/" + state.LevelIndex + "/" + next.AttemptIndex,
                Outcome = success ? (state.LevelIndex == 10 ? GoldMinerOutcome.Completed : GoldMinerOutcome.Won) : GoldMinerOutcome.Failed,
                Cash = state.Cash,
                Target = state.Target,
                RunGrossIncome = GoldMinerValidation.Gross(state.Players),
                RecordCommitted = success,
                Contributions = new GoldMinerContribution[state.Players.Length]
            };
            for (int i = 0; i < state.Players.Length; i++)
                state.Settlement.Contributions[i] = new GoldMinerContribution
                {
                    PlayerId = i,
                    Amount = state.Players[i].LevelCollected
                };
            if (success)
            {
                state.ActiveBuffs = new GoldMinerBuffs();
                var record = candidate.GetRecord(ActiveMode);
                record.BestClearedLevel = Math.Max(record.BestClearedLevel, state.LevelIndex);
                record.BestRunGrossIncome = Math.Max(record.BestRunGrossIncome, state.Settlement.RunGrossIncome);
                if (state.Phase == GoldMinerPhase.Completed && record.CompletionCount < long.MaxValue)
                    record.CompletionCount++;
            }

            return Candidate(candidate, false);
        }

        public bool Commit(GoldMinerCandidate candidate)
        {
            if (candidate == null || candidate.Owner != identity || candidate.BaseTxnSeq != root.TxnSeq || candidate.BaseStateRevision != StateRevision || candidate.BasePageGeneration != PageGeneration || candidate.BaseMode != ActiveMode)
            {
                LastError = "The transaction candidate is stale.";
                return false;
            }

            var next = candidate.Snapshot;
            string reason;
            if (root.TxnSeq == long.MaxValue || next.TxnSeq != root.TxnSeq + 1 || !next.TryValidate(out reason))
            {
                LastError = "The transaction candidate is invalid.";
                return false;
            }

            root = next;
            StateRevision = checked(StateRevision + 1);
            if (candidate.ChangesGeneration)
            {
                PageGeneration = checked(PageGeneration + 1);
                SuspendReasons |= GoldMinerSuspendReason.NeedsExplicitContinue;
            }

            Dirty = false;
            LastError = null;
            RebuildGame();
            return true;
        }

        private GoldMinerCandidate Candidate(GoldMinerRootSnapshot value, bool changesGeneration)
        {
            if (root.TxnSeq == long.MaxValue)
                return Fail("The save transaction sequence is exhausted.");
            value.TxnSeq = root.TxnSeq + 1;
            string reason;
            if (!value.TryValidate(out reason))
                return Fail(reason);
            return new GoldMinerCandidate(identity, value, root.TxnSeq, StateRevision, PageGeneration, ActiveMode, changesGeneration);
        }

        private GoldMinerCandidate Fail(string reason)
        {
            LastError = reason;
            return null;
        }

        private void Changed()
        {
            StateRevision = checked(StateRevision + 1);
            Dirty = true;
        }

        private static void SetRun(GoldMinerRootSnapshot target, GoldMinerMode mode, GoldMinerRunSnapshot value)
        {
            if (mode == GoldMinerMode.Solo)
                target.Solo = value;
            else
                target.Coop = value;
        }

        private void RebuildGame()
        {
            var run = root.GetRun(ActiveMode);
            game = run == null ? null : new GoldMinerGame(run);
            if (game != null)
                SetRun(root, ActiveMode, game.OwnedSnapshot);
        }
    }
}
