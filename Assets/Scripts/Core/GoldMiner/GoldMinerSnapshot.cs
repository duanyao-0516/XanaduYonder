using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.GoldMiner
{
    [Serializable]
    public sealed class GoldMinerRootSnapshot
    {
        public int SchemaVersion = 1, RulesVersion = GoldMinerRules.Version;
        public string GameId = "gold-miner";
        public long TxnSeq;
        public GoldMinerMode LastMode;
        public GoldMinerRecord SoloRecord = new GoldMinerRecord(), CoopRecord = new GoldMinerRecord();
        public GoldMinerRunSnapshot Solo, Coop;
        public GoldMinerRootSnapshot Copy()
        {
            var c = (GoldMinerRootSnapshot)MemberwiseClone();
            c.SoloRecord = SoloRecord == null ? null : SoloRecord.Copy();
            c.CoopRecord = CoopRecord == null ? null : CoopRecord.Copy();
            c.Solo = Solo == null ? null : Solo.Copy();
            c.Coop = Coop == null ? null : Coop.Copy();
            return c;
        }

        public bool TryValidate(out string reason)
        {
            return GoldMinerValidation.Root(this, out reason);
        }

        public GoldMinerRunSnapshot GetRun(GoldMinerMode mode)
        {
            return mode == GoldMinerMode.Solo ? Solo : Coop;
        }

        public GoldMinerRecord GetRecord(GoldMinerMode mode)
        {
            return mode == GoldMinerMode.Solo ? SoloRecord : CoopRecord;
        }
    }

    [Serializable]
    public sealed class GoldMinerRunSnapshot
    {
        public string RunId;
        public GoldMinerMode Mode;
        public uint RunSeed;
        public int AttemptIndex;
        public long LastCommandSeq;
        public GoldMinerState Current;
        public GoldMinerLevelStart Checkpoint;
        public GoldMinerRunSnapshot Copy()
        {
            var c = (GoldMinerRunSnapshot)MemberwiseClone();
            c.Current = Current == null ? null : Current.Copy();
            c.Checkpoint = Checkpoint == null ? null : Checkpoint.Copy();
            return c;
        }

        public bool TryValidate(out string reason)
        {
            return GoldMinerValidation.Run(this, out reason);
        }
    }

    [Serializable]
    public sealed class GoldMinerState
    {
        public GoldMinerPhase Phase;
        public int LevelIndex, CatalogVersion, RemainingTicks, SimulationTick;
        public string LayoutProfileId, LayoutId, LayoutContentHash, PatternId, PreviousPatternId;
        public uint LevelLayoutSeed, LayoutRng, BagRng;
        public long Cash, Target, ShopSpent;
        public GoldMinerPlayer[] Players;
        public GoldMinerHook[] Hooks;
        public GoldMinerEntity[] Entities;
        public GoldMinerBuffs ActiveBuffs, PendingBuffs;
        public GoldMinerShop Shop;
        public GoldMinerSettlement Settlement;
        public GoldMinerState Copy()
        {
            var c = (GoldMinerState)MemberwiseClone();
            c.Players = GoldMinerCopies.Array(Players, x => x.Copy());
            c.Hooks = GoldMinerCopies.Array(Hooks, x => x.Copy());
            c.Entities = GoldMinerCopies.Array(Entities, x => x.Copy());
            c.ActiveBuffs = ActiveBuffs == null ? null : ActiveBuffs.Copy();
            c.PendingBuffs = PendingBuffs == null ? null : PendingBuffs.Copy();
            c.Shop = Shop == null ? null : Shop.Copy();
            c.Settlement = Settlement == null ? null : Settlement.Copy();
            return c;
        }

        public bool TryValidate(GoldMinerMode mode, out string reason)
        {
            return GoldMinerValidation.State(this, mode, out reason);
        }
    }

    [Serializable]
    public sealed class GoldMinerLevelStart
    {
        public int LevelIndex, CatalogVersion;
        public string LayoutProfileId, LayoutId, LayoutContentHash, PatternId, PreviousPatternId;
        public uint LevelLayoutSeed, LayoutRng, BagRng;
        public long OpeningCash, OpeningShopSpent;
        public GoldMinerOpeningPlayer[] OpeningPlayers;
        public GoldMinerBuffs ActiveBuffs;
        public GoldMinerHook[] InitialHooks;
        public GoldMinerEntity[] InitialEntities;
        public GoldMinerLevelStart Copy()
        {
            var c = (GoldMinerLevelStart)MemberwiseClone();
            c.OpeningPlayers = GoldMinerCopies.Array(OpeningPlayers, x => x.Copy());
            c.InitialHooks = GoldMinerCopies.Array(InitialHooks, x => x.Copy());
            c.InitialEntities = GoldMinerCopies.Array(InitialEntities, x => x.Copy());
            c.ActiveBuffs = ActiveBuffs == null ? null : ActiveBuffs.Copy();
            return c;
        }

        public GoldMinerState CreateState(GoldMinerMode mode, long? acceptedTarget = null)
        {
            long target = acceptedTarget ?? GoldMinerRules.Target(mode, LevelIndex);
            if (!GoldMinerRules.IsSupportedTarget(mode, LevelIndex, target))
                throw new ArgumentException("Unsupported checkpoint target.", "acceptedTarget");
            var p = OpeningPlayers == null ? null : new GoldMinerPlayer[OpeningPlayers.Length];
            if (p != null)
                for (int i = 0; i < p.Length; i++)
                    if (OpeningPlayers[i] != null)
                        p[i] = new GoldMinerPlayer
                        {
                            PlayerId = OpeningPlayers[i].PlayerId,
                            Bombs = OpeningPlayers[i].Bombs,
                            TotalCollected = OpeningPlayers[i].TotalCollected
                        };
            return new GoldMinerState
            {
                Phase = GoldMinerPhase.Ready,
                LevelIndex = LevelIndex,
                CatalogVersion = CatalogVersion,
                LayoutProfileId = LayoutProfileId,
                LayoutId = LayoutId,
                LayoutContentHash = LayoutContentHash,
                PatternId = PatternId,
                PreviousPatternId = PreviousPatternId,
                LevelLayoutSeed = LevelLayoutSeed,
                LayoutRng = LayoutRng,
                BagRng = BagRng,
                Cash = OpeningCash,
                ShopSpent = OpeningShopSpent,
                Target = target,
                RemainingTicks = GoldMinerRules.LevelTicks,
                Players = p,
                Hooks = GoldMinerCopies.Array(InitialHooks, x => x.Copy()),
                Entities = GoldMinerCopies.Array(InitialEntities, x => x.Copy()),
                ActiveBuffs = ActiveBuffs == null ? null : ActiveBuffs.Copy(),
                PendingBuffs = new GoldMinerBuffs()
            };
        }

        public bool TryValidate(GoldMinerMode mode, out string reason)
        {
            return GoldMinerValidation.Checkpoint(this, mode, out reason);
        }
    }

    internal static class GoldMinerValidation
    {
        const double Epsilon = 1e-7;
        static bool Bad(string message, out string reason)
        {
            reason = message;
            return false;
        }

        internal static bool Hex(string s, int length)
        {
            if (s == null || s.Length != length)
                return false;
            foreach (char c in s)
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f'))
                    return false;
            return true;
        }

        internal static bool Root(GoldMinerRootSnapshot s, out string reason)
        {
            reason = null;
            // V3 retains the wire format and each already accepted level target.
            // Both legacy rules versions can upgrade without rewriting either mode's progress.
            if (s == null || s.SchemaVersion != 1 || (s.RulesVersion != 1 && s.RulesVersion != 2 && s.RulesVersion != GoldMinerRules.Version) || s.GameId != "gold-miner" || s.TxnSeq < 0 || !GoldMinerRules.ValidMode(s.LastMode))
                return Bad("Unsupported root or version.", out reason);
            if (!Record(s.SoloRecord, GoldMinerMode.Solo) || !Record(s.CoopRecord, GoldMinerMode.Coop))
                return Bad("Invalid records.", out reason);
            foreach (var mode in new[]
            {
                GoldMinerMode.Solo,
                GoldMinerMode.Coop
            }

            )
            {
                var run = s.GetRun(mode);
                if (run == null)
                    continue;
                if (run.Mode != mode || !Run(run, out reason))
                    return Bad(reason ?? "Slot mode mismatch.", out reason);
                if (s.RulesVersion < 3 && run.Current.Target != GoldMinerRules.LegacyTarget(mode, run.Current.LevelIndex))
                    return Bad("Legacy rules cannot contain a new target.", out reason);
                var r = s.GetRecord(mode);
                var phase = run.Current.Phase;
                if (phase == GoldMinerPhase.Won || phase == GoldMinerPhase.Completed || phase == GoldMinerPhase.Shop)
                {
                    long gross = Gross(run.Current.Players);
                    if (r.BestClearedLevel < run.Current.LevelIndex || r.BestRunGrossIncome < gross || (phase == GoldMinerPhase.Completed && r.CompletionCount < 1))
                        return Bad("Settlement and record disagree.", out reason);
                }
            }

            return true;
        }

        // Historical records have no per-record rules version. The explicitly supported
        // legacy curve remains their minimum; current settlements still validate their own target.
        static bool Record(GoldMinerRecord r, GoldMinerMode mode)
        {
            return r != null && r.BestClearedLevel >= 0 && r.BestClearedLevel <= 10 && r.BestRunGrossIncome >= 0 && r.CompletionCount >= 0 && (r.CompletionCount == 0 || r.BestClearedLevel == 10) && (r.BestClearedLevel != 0 || r.BestRunGrossIncome == 0) && (r.BestClearedLevel == 0 || r.BestRunGrossIncome >= GoldMinerRules.LegacyTarget(mode, r.BestClearedLevel));
        }

        internal static long Gross(GoldMinerPlayer[] p)
        {
            long n = 0;
            foreach (var player in p)
                n = checked(n + player.TotalCollected);
            return n;
        }

        internal static bool Run(GoldMinerRunSnapshot r, out string reason)
        {
            reason = null;
            if (r == null || !Hex(r.RunId, 32) || !GoldMinerRules.ValidMode(r.Mode) || r.RunSeed == 0 || r.AttemptIndex < 0 || r.LastCommandSeq < 0)
                return Bad("Invalid run identity or sequence.", out reason);
            if (!State(r.Current, r.Mode, out reason) || !Checkpoint(r.Checkpoint, r.Mode, out reason))
                return false;
            var c = r.Current;
            var p = r.Checkpoint;
            if (c.LevelIndex != p.LevelIndex || c.LayoutProfileId != p.LayoutProfileId || c.LayoutId != p.LayoutId || c.CatalogVersion != p.CatalogVersion || c.LayoutContentHash != p.LayoutContentHash || c.PatternId != p.PatternId || c.PreviousPatternId != p.PreviousPatternId || c.LevelLayoutSeed != p.LevelLayoutSeed || c.LayoutRng != p.LayoutRng || c.BagRng != p.BagRng)
                return Bad("Current/checkpoint identity mismatch.", out reason);
            if (c.Entities.Length != p.InitialEntities.Length)
                return Bad("Entity membership changed.", out reason);
            var initial = new Dictionary<int, GoldMinerEntity>();
            foreach (var e in p.InitialEntities)
                initial.Add(e.Id, e);
            foreach (var e in c.Entities)
            {
                GoldMinerEntity old;
                if (!initial.TryGetValue(e.Id, out old) || old.Kind != e.Kind || old.BagValue != e.BagValue)
                    return Bad("Entity identity changed.", out reason);
                if (e.Status == GoldMinerEntityStatus.Available && !GoldMinerRules.IsAnimal(e.Kind) && (e.X != old.X || e.Y != old.Y))
                    return Bad("Static entity moved.", out reason);
            }

            long collected = 0;
            foreach (var e in c.Entities)
                if (e.Status == GoldMinerEntityStatus.Collected)
                    collected = checked(collected + GoldMinerRules.Value(e, p.ActiveBuffs));
            long contributions = 0;
            for (int i = 0; i < c.Players.Length; i++)
            {
                var player = c.Players[i];
                if (player.TotalCollected - p.OpeningPlayers[i].TotalCollected != player.LevelCollected)
                    return Bad("Player opening total mismatch.", out reason);
                contributions = checked(contributions + player.LevelCollected);
                if (c.Phase != GoldMinerPhase.Shop && player.Bombs > p.OpeningPlayers[i].Bombs)
                    return Bad("Bomb inventory increased outside shop.", out reason);
            }

            if (collected != contributions)
                return Bad("Collected entities and income disagree.", out reason);
            if (c.Phase != GoldMinerPhase.Shop && c.ShopSpent != p.OpeningShopSpent)
                return Bad("Spending changed outside shop.", out reason);
            bool cleared = c.Phase == GoldMinerPhase.Won || c.Phase == GoldMinerPhase.Completed || c.Phase == GoldMinerPhase.Shop;
            if (cleared ? !c.ActiveBuffs.IsEmpty : !c.ActiveBuffs.Same(p.ActiveBuffs))
                return Bad("Active buff lifetime mismatch.", out reason);
            if (c.Phase == GoldMinerPhase.Ready && !SameInitial(c, p))
                return Bad("Ready must match checkpoint.", out reason);
            if (c.Shop != null && c.Shop.EntryShopSpent != p.OpeningShopSpent)
                return Bad("Shop entry spending changed from checkpoint.", out reason);
            if (c.Shop != null && !Shop(c, r.RunId, out reason))
                return false;
            int consumed = 0, destroyed = 0;
            foreach (var e in c.Entities)
                if (e.Status == GoldMinerEntityStatus.Destroyed)
                    destroyed++;
            for (int i = 0; i < c.Players.Length; i++)
            {
                int inventory = c.Shop == null ? c.Players[i].Bombs : c.Shop.EntryBombs[i].Bombs;
                if (inventory > p.OpeningPlayers[i].Bombs)
                    return Bad("Bombs increased before entering shop.", out reason);
                consumed += p.OpeningPlayers[i].Bombs - inventory;
            }

            if (consumed != destroyed)
                return Bad("Destroyed entities and dynamite consumption disagree.", out reason);
            if (c.Settlement != null && !Settlement(c, r.RunId, r.AttemptIndex, out reason))
                return false;
            return true;
        }

        internal static bool Checkpoint(GoldMinerLevelStart p, GoldMinerMode mode, out string reason)
        {
            reason = null;
            if (p == null || !GoldMinerRules.ValidMode(mode) || p.LevelIndex < 1 || p.LevelIndex > 10)
                return Bad("Missing checkpoint.", out reason);
            var s = p.CreateState(mode);
            if (!State(s, mode, out reason))
                return false;
            for (int i = 0; i < s.Hooks.Length; i++)
            {
                var expected = GoldMinerRules.InitialHooks(mode)[i];
                if (!SameHook(s.Hooks[i], expected))
                    return Bad("Checkpoint hook not initial.", out reason);
            }

            foreach (var e in s.Entities)
                if (e.Status != GoldMinerEntityStatus.Available || e.OwnerPlayerId != null)
                    return Bad("Checkpoint entity not available.", out reason);
            return true;
        }

        internal static bool State(GoldMinerState s, GoldMinerMode mode, out string reason)
        {
            reason = null;
            if (s == null || !GoldMinerRules.ValidMode(mode) || (int)s.Phase < 0 || (int)s.Phase > 6 || s.LevelIndex < 1 || s.LevelIndex > 10)
                return Bad("Invalid state/phase/level.", out reason);
            if (s.LayoutProfileId != GoldMinerRules.LayoutProfileId(mode) || s.CatalogVersion != 1 || !Hex(s.LayoutContentHash, 64) || string.IsNullOrEmpty(s.LayoutId) || s.LayoutId.Length > 80 || string.IsNullOrEmpty(s.PatternId) || s.PatternId.Length > 24 || s.LevelLayoutSeed == 0 || s.LayoutRng == 0 || s.BagRng == 0)
                return Bad("Invalid layout metadata or random state.", out reason);
            if (!Pattern(s.PatternId) || !LayoutIdentity(s, mode))
                return Bad("Invalid layout identity.", out reason);
            if (s.LevelIndex == 1 ? s.PreviousPatternId != null : !Pattern(s.PreviousPatternId) || s.PreviousPatternId == s.PatternId)
                return Bad("Invalid previous pattern.", out reason);
            if (s.Cash < 0 || s.ShopSpent < 0 || !GoldMinerRules.IsSupportedTarget(mode, s.LevelIndex, s.Target) || s.RemainingTicks < 0 || s.RemainingTicks > GoldMinerRules.LevelTicks || s.SimulationTick < 0 || s.SimulationTick > GoldMinerRules.MaximumSimulationTicks(mode))
                return Bad("Invalid cash, target or time.", out reason);
            if (s.Players == null || s.Hooks == null || s.Players.Length != GoldMinerRules.PlayerCount(mode) || s.Hooks.Length != s.Players.Length || s.ActiveBuffs == null || s.PendingBuffs == null)
                return Bad("Invalid players/hooks/buffs.", out reason);
            long gross = 0;
            for (int i = 0; i < s.Players.Length; i++)
            {
                var p = s.Players[i];
                if (p == null || p.PlayerId != i || p.Bombs < 0 || p.Bombs > GoldMinerRules.MaxBombs || p.TotalCollected < 0 || p.LevelCollected < 0 || p.LevelCollected > p.TotalCollected || p.TotalCollected > long.MaxValue - gross)
                    return Bad("Invalid player balances.", out reason);
                gross += p.TotalCollected;
            }

            if (s.ShopSpent > gross || s.Cash != gross - s.ShopSpent)
                return Bad("Cash does not balance.", out reason);
            if (s.Entities == null || s.Entities.Length == 0 || s.Entities.Length > GoldMinerRules.MaxEntities)
                return Bad("Invalid entities.", out reason);
            var ids = new Dictionary<int, GoldMinerEntity>();
            foreach (var e in s.Entities)
            {
                if (e == null || e.Id < 1 || ids.ContainsKey(e.Id) || (int)e.Kind < 0 || (int)e.Kind > 8 || (int)e.Status < 0 || (int)e.Status > 3 || !GoldMinerRules.IsFinite(e.X) || !GoldMinerRules.IsFinite(e.Y))
                    return Bad("Invalid entity identity/position.", out reason);
                ids.Add(e.Id, e);
                double radius = GoldMinerRules.Radius(e.Kind);
                if (GoldMinerRules.IsAnimal(e.Kind) ? (e.MoveDirection != -1 && e.MoveDirection != 1) : e.MoveDirection != 0)
                    return Bad("Invalid entity direction.", out reason);
                if (e.Kind == GoldMinerKind.MysteryBag ? (e.BagValue != 100 && e.BagValue != 250 && e.BagValue != 500) : e.BagValue != null)
                    return Bad("Invalid bag value.", out reason);
                if (e.Status == GoldMinerEntityStatus.Held ? (e.OwnerPlayerId == null || e.OwnerPlayerId < 0 || e.OwnerPlayerId >= s.Players.Length) : e.OwnerPlayerId != null)
                    return Bad("Invalid entity owner.", out reason);
                if (e.Status == GoldMinerEntityStatus.Available && (e.X < radius - Epsilon || e.X > GoldMinerRules.Width(mode) - radius + Epsilon || e.Y < radius - Epsilon || e.Y > GoldMinerRules.Height(mode) - radius + Epsilon))
                    return Bad("Available entity outside world.", out reason);
            }

            long potentialIncome = 0;
            foreach (var e in s.Entities)
                if (e.Status == GoldMinerEntityStatus.Available || e.Status == GoldMinerEntityStatus.Held)
                    potentialIncome += GoldMinerRules.Value(e, s.ActiveBuffs);
            if (gross > long.MaxValue - potentialIncome)
                return Bad("Income would overflow during a legal collection.", out reason);
            var held = new HashSet<int>();
            bool busy = false;
            for (int i = 0; i < s.Hooks.Length; i++)
            {
                var h = s.Hooks[i];
                if (h == null || h.PlayerId != i || (int)h.State < 0 || (int)h.State > 3 || !GoldMinerRules.IsFinite(h.AngleRadians) || Math.Abs(h.AngleRadians) > GoldMinerRules.MaxAngle + Epsilon || !GoldMinerRules.IsFinite(h.Length) || h.Length < GoldMinerRules.IdleLength - Epsilon || h.Length > BoundaryLength(mode, h) + Epsilon || (h.SwingDirection != 1 && h.SwingDirection != -1))
                    return Bad("Invalid hook geometry.", out reason);
                if (h.State == GoldMinerHookState.Swinging && Math.Abs(h.Length - GoldMinerRules.IdleLength) > Epsilon)
                    return Bad("Idle hook length.", out reason);
                busy |= h.State != GoldMinerHookState.Swinging;
                if (h.State == GoldMinerHookState.RetractingLoaded)
                {
                    GoldMinerEntity e;
                    if (h.HeldEntityId == null || !ids.TryGetValue(h.HeldEntityId.Value, out e) || !held.Add(e.Id) || e.Status != GoldMinerEntityStatus.Held || e.OwnerPlayerId != i)
                        return Bad("Invalid held ownership.", out reason);
                    if (Math.Abs(e.X - (GoldMinerRules.OriginX(mode, i) + Math.Sin(h.AngleRadians) * h.Length)) > Epsilon || Math.Abs(e.Y - Math.Cos(h.AngleRadians) * h.Length) > Epsilon)
                        return Bad("Held entity not at hook.", out reason);
                }
                else if (h.HeldEntityId != null)
                    return Bad("Empty hook owns load.", out reason);
            }

            foreach (var e in s.Entities)
                if (e.Status == GoldMinerEntityStatus.Held && !held.Contains(e.Id))
                    return Bad("Orphan held entity.", out reason);
            bool terminal = s.Phase == GoldMinerPhase.Won || s.Phase == GoldMinerPhase.Failed || s.Phase == GoldMinerPhase.Completed || s.Phase == GoldMinerPhase.Shop;
            if (terminal && busy)
                return Bad("Terminal hooks still travelling.", out reason);
            if (s.Phase == GoldMinerPhase.Ready && (s.RemainingTicks != GoldMinerRules.LevelTicks || s.SimulationTick != 0 || busy))
                return Bad("Invalid ready state.", out reason);
            if (s.Phase == GoldMinerPhase.Running && (s.RemainingTicks == 0 || s.SimulationTick != GoldMinerRules.LevelTicks - s.RemainingTicks))
                return Bad("Invalid running time.", out reason);
            if (s.Phase == GoldMinerPhase.Draining || terminal)
            {
                bool available = false;
                foreach (var e in s.Entities)
                    available |= e.Status == GoldMinerEntityStatus.Available;
                if (s.RemainingTicks == 0 ? s.SimulationTick < GoldMinerRules.LevelTicks : s.SimulationTick != GoldMinerRules.LevelTicks - s.RemainingTicks || available || busy)
                    return Bad("Impossible draining/terminal time.", out reason);
            }

            if (s.Phase == GoldMinerPhase.Draining && s.RemainingTicks == 0
                && RemainingDrainTicks(s, mode) > GoldMinerRules.MaximumSimulationTicks(mode) - s.SimulationTick)
                return Bad("Insufficient simulation ticks remain for the in-flight hooks to drain.", out reason);

            if (s.Phase != GoldMinerPhase.Shop && !s.PendingBuffs.IsEmpty)
                return Bad("Pending buffs outside shop.", out reason);
            if ((s.Phase == GoldMinerPhase.Shop) != (s.Shop != null))
                return Bad("Shop phase mismatch.", out reason);
            bool result = s.Phase == GoldMinerPhase.Won || s.Phase == GoldMinerPhase.Failed || s.Phase == GoldMinerPhase.Completed;
            if (result != (s.Settlement != null))
                return Bad("Settlement phase mismatch.", out reason);
            if (s.Phase == GoldMinerPhase.Won && (s.LevelIndex >= 10 || s.Cash < s.Target) || s.Phase == GoldMinerPhase.Shop && s.LevelIndex >= 10 || s.Phase == GoldMinerPhase.Completed && (s.LevelIndex != 10 || s.Cash < s.Target) || s.Phase == GoldMinerPhase.Failed && s.Cash >= s.Target)
                return Bad("Outcome inconsistent with cash/level.", out reason);
            return true;
        }

        private static int RemainingDrainTicks(GoldMinerState state, GoldMinerMode mode)
        {
            int longest = 0;
            foreach (var hook in state.Hooks)
            {
                if (hook.State == GoldMinerHookState.Swinging)
                    continue;
                double seconds;
                if (hook.State == GoldMinerHookState.Extending)
                {
                    double boundary = Math.Max(hook.Length, BoundaryLength(mode, hook));
                    seconds = (boundary - hook.Length) / GoldMinerRules.ExtendSpeed
                        + (boundary - GoldMinerRules.IdleLength) / GoldMinerRules.SlowestRetractSpeed;
                }
                else
                {
                    seconds = Math.Max(0, hook.Length - GoldMinerRules.IdleLength)
                        / GoldMinerRules.SlowestRetractSpeed;
                }
                // Even a hook already at idle length needs a Step to finish its action.
                // One additional tick also covers endpoint rounding in repeated subtraction.
                int actionTicks = (int)Math.Ceiling(seconds * GoldMinerRules.TicksPerSecond) + 1;
                longest = Math.Max(longest, actionTicks);
            }
            return longest;
        }

        static bool Pattern(string value)
        {
            return value == "balanced" || value == "left" || value == "right";
        }

        static bool LayoutIdentity(GoldMinerState s, GoldMinerMode mode)
        {
            string prefix = GoldMinerRules.ModeId(mode) + "-" + s.LevelIndex.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + "-" + s.PatternId + "-";
            return s.LayoutId.StartsWith(prefix, StringComparison.Ordinal) && s.LayoutId.Length == prefix.Length + 1 && s.LayoutId[prefix.Length] >= '0' && s.LayoutId[prefix.Length] <= '3';
        }

        internal static double BoundaryLength(GoldMinerMode mode, GoldMinerHook h)
        {
            double dx = Math.Sin(h.AngleRadians), dy = Math.Cos(h.AngleRadians), origin = GoldMinerRules.OriginX(mode, h.PlayerId);
            double n = (GoldMinerRules.Height(mode) - GoldMinerRules.HookRadius) / dy;
            if (dx > 0)
                n = Math.Min(n, (GoldMinerRules.Width(mode) - GoldMinerRules.HookRadius - origin) / dx);
            else if (dx < 0)
                n = Math.Min(n, (GoldMinerRules.HookRadius - origin) / dx);
            return n;
        }

        static bool SameHook(GoldMinerHook a, GoldMinerHook b)
        {
            return a.PlayerId == b.PlayerId && a.State == b.State && a.SwingDirection == b.SwingDirection && a.AngleRadians == b.AngleRadians && a.Length == b.Length && a.HeldEntityId == b.HeldEntityId;
        }

        static bool SameInitial(GoldMinerState c, GoldMinerLevelStart p)
        {
            if (c.Cash != p.OpeningCash || c.ShopSpent != p.OpeningShopSpent)
                return false;
            for (int i = 0; i < c.Players.Length; i++)
                if (c.Players[i].Bombs != p.OpeningPlayers[i].Bombs || c.Players[i].LevelCollected != 0 || !SameHook(c.Hooks[i], p.InitialHooks[i]))
                    return false;
            for (int i = 0; i < c.Entities.Length; i++)
            {
                var a = c.Entities[i];
                var b = p.InitialEntities[i];
                if (a.Id != b.Id || a.Status != b.Status || a.X != b.X || a.Y != b.Y || a.MoveDirection != b.MoveDirection)
                    return false;
            }

            return true;
        }

        static bool Shop(GoldMinerState c, string runId, out string reason)
        {
            reason = null;
            var shop = c.Shop;
            if (shop.Id != runId + "/" + c.LevelIndex || shop.EntryCash < 0 || shop.EntryShopSpent < 0 || shop.Spent < 0 || shop.EntryBombs == null || shop.EntryBombs.Length != c.Players.Length || shop.Offers == null || shop.Offers.Length != 5)
                return Bad("Invalid shop header.", out reason);
            long spent = 0;
            var pending = new GoldMinerBuffs();
            for (int i = 0; i < 5; i++)
            {
                var o = shop.Offers[i];
                int stock = i == 0 ? 3 : 1;
                if (o == null || o.ItemId != (GoldMinerItem)i || o.Price != GoldMinerRules.Price(o.ItemId, c.LevelIndex) || o.Remaining < 0 || o.Purchased < 0 || o.Remaining + o.Purchased != stock)
                    return Bad("Invalid shop offer.", out reason);
                spent += o.Price * o.Purchased;
                if (i == 0)
                {
                    if (o.Allocations == null || o.Allocations.Length != c.Players.Length)
                        return Bad("Invalid allocations.", out reason);
                    int n = 0;
                    for (int j = 0; j < c.Players.Length; j++)
                    {
                        var a = o.Allocations[j];
                        var b = shop.EntryBombs[j];
                        if (a == null || b == null || a.PlayerId != j || b.PlayerId != j || a.Count < 0 || b.Bombs < 0 || b.Bombs > 9 || c.Players[j].Bombs != b.Bombs + a.Count)
                            return Bad("Bomb transaction mismatch.", out reason);
                        n += a.Count;
                    }

                    if (n != o.Purchased)
                        return Bad("Bomb allocations mismatch.", out reason);
                }
                else
                {
                    if (o.Allocations != null)
                        return Bad("Unexpected allocations.", out reason);
                    if (o.Purchased == 1)
                        GoldMinerEconomy.SetBuff(pending, o.ItemId);
                }
            }

            if (spent != shop.Spent || shop.EntryCash - spent != c.Cash || shop.EntryShopSpent > long.MaxValue - spent || shop.EntryShopSpent + spent != c.ShopSpent || !pending.Same(c.PendingBuffs))
                return Bad("Shop transaction mismatch.", out reason);
            return true;
        }

        static bool Settlement(GoldMinerState c, string runId, int attempt, out string reason)
        {
            reason = null;
            var s = c.Settlement;
            var expected = c.Phase == GoldMinerPhase.Failed ? GoldMinerOutcome.Failed : c.Phase == GoldMinerPhase.Completed ? GoldMinerOutcome.Completed : GoldMinerOutcome.Won;
            if (s.Id != runId + "/" + c.LevelIndex + "/" + attempt || s.Outcome != expected || s.Cash != c.Cash || s.Target != c.Target || s.RunGrossIncome != Gross(c.Players) || s.RecordCommitted != (expected != GoldMinerOutcome.Failed) || s.Contributions == null || s.Contributions.Length != c.Players.Length)
                return Bad("Settlement mismatch.", out reason);
            for (int i = 0; i < c.Players.Length; i++)
                if (s.Contributions[i] == null || s.Contributions[i].PlayerId != i || s.Contributions[i].Amount != c.Players[i].LevelCollected)
                    return Bad("Contribution mismatch.", out reason);
            return true;
        }
    }
}
