using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.GoldMiner
{
    public static class GoldMinerSourceValidation
    {
        public static string ContentHash(GoldMinerMode mode)
        {
            return mode == GoldMinerMode.Solo ? "1e5ae7c92a87a9e57ce5d5d9d42dcb95e475f292b607263af41c4c025c888f6f" : "78dfcc4bf8933451cb833e7610fe0abf59c4257612d57ad6bd4dcd793f7b56cb";
        }
        static bool Bad(string message, out string reason)
        {
            reason = message;
            return false;
        }
        static bool Finite(double n, double low, double high)
        {
            return GoldMinerRules.IsFinite(n) && n >= low && n <= high;
        }
        public static bool Record(GoldMinerRecord r, GoldMinerMode mode)
        {
            return r != null && r.BestClearedLevel >= 0 && r.BestClearedLevel <= GoldMinerSourceRules.MaximumLevel(mode) && r.BestRunGrossIncome >= 0 && r.CompletionCount >= 0 && (mode == GoldMinerMode.Solo ? r.CompletionCount == 0 : r.CompletionCount == 0 || r.BestClearedLevel == 30);
        }
        public static bool State(GoldMinerState s, GoldMinerMode mode, out string reason)
        {
            reason = null;
            if (s == null || !GoldMinerRules.ValidMode(mode) || s.Source == null || s.LevelIndex < 1 || s.LevelIndex > GoldMinerSourceRules.MaximumLevel(mode) ||
               (int)s.Phase < 0 || (int)s.Phase > 6 || s.CatalogVersion != 2 || s.LayoutProfileId != GoldMinerRules.ModeId(mode) + "-source-v1" ||
               s.LayoutContentHash != ContentHash(mode) || s.Source.MapIndex < 0 || s.Source.MapIndex > 29 ||
               s.LayoutId != GoldMinerRules.ModeId(mode) + "-source-" + s.Source.MapIndex.ToString("00", System.Globalization.CultureInfo.InvariantCulture) ||
               s.PatternId != "source-" + s.Source.MapIndex || s.LevelLayoutSeed == 0 || s.LayoutRng == 0 || s.BagRng == 0)
                return Bad("Invalid source campaign identity.", out reason);
            if (mode == GoldMinerMode.Solo ? s.Source.MapIndex / 3 != GoldMinerSourceRules.MapGroup(s.LevelIndex) : s.Source.MapIndex != s.LevelIndex - 1)
                return Bad("Source map does not belong to this level.", out reason);
            if (s.Target != GoldMinerSourceRules.Target(mode, s.LevelIndex) || s.RemainingTicks < 0 || s.RemainingTicks > GoldMinerSourceRules.DurationTicks(mode) ||
               s.SimulationTick < 0 || s.SimulationTick > GoldMinerSourceRules.DurationTicks(mode) || s.SimulationTick + s.RemainingTicks != GoldMinerSourceRules.DurationTicks(mode) || s.ShopSpent < 0 || !Finite(s.Source.Strength, 0, 10000) ||
               s.Source.SharedBombs < 0 || s.Source.SharedBombs > 1000 || s.ActiveBuffs == null || s.PendingBuffs == null ||
               (mode == GoldMinerMode.Coop && s.Cash < 0) || s.Cash < -100000000000L || s.Cash > 1000000000000L)
                return Bad("Invalid source balances or clocks.", out reason);
            int count = GoldMinerRules.PlayerCount(mode);
            if (s.Players == null || s.Hooks == null || s.Players.Length != count || s.Hooks.Length != count || s.Entities == null || s.Entities.Length < 1 || s.Entities.Length > 100)
                return Bad("Missing source objects.", out reason);
            long gross = 0;
            for (int i = 0; i < count; i++)
            {
                var p = s.Players[i];
                if (p == null || p.PlayerId != i || p.Bombs < 0 || p.Bombs > 1000 || p.TotalCollected < 0 || p.LevelCollected < 0 || p.LevelCollected > p.TotalCollected || p.TotalCollected > long.MaxValue - gross)
                    return Bad("Invalid source player.", out reason);
                gross += p.TotalCollected;
            }
            if (gross > 10000000000000L || gross - s.ShopSpent != s.Cash)
                return Bad("Source wallet does not balance.", out reason);
            var held = new HashSet<int>();
            for (int i = 0; i < s.Entities.Length; i++)
            {
                var e = s.Entities[i];
                var d = e == null ? null : e.Source;
                if (e == null || e.Id != i + 1 || d == null || string.IsNullOrEmpty(d.Key) || d.Key.Length > 32 || d.Type < 2 || d.Type > 14 ||
                   (int)e.Kind < 0 || (int)e.Kind > 12 || (int)e.Status < 0 || (int)e.Status > 3 || !Finite(e.X, -30, 30) || !Finite(e.Y, -10, 10) ||
                   !Finite(d.InitialX, -10, 20) || !Finite(d.InitialY, 0, 10) || !Finite(d.MotionEndX, -10, 20) || !Finite(d.Weight, -5, 95) ||
                   !Finite(d.Radius, .01, 2) || !Finite(d.ScaleX, .01, 10) || !Finite(d.ScaleY, .01, 10) || d.MotionTick < 0 || d.MotionTick >= 504 || d.Value < 0 || d.Value > 1000000 || d.Award < 0 || d.Award > 1000000 ||
                   (d.Reward != "money" && d.Reward != "power" && d.Reward != "dynamite") || e.BagValue != null || e.MoveDirection < -1 || e.MoveDirection > 1)
                    return Bad("Invalid source entity.", out reason);
                if (e.Status == GoldMinerEntityStatus.Held ? !e.OwnerPlayerId.HasValue || e.OwnerPlayerId < 0 || e.OwnerPlayerId >= count : e.OwnerPlayerId != null)
                    return Bad("Invalid source entity owner.", out reason);
                if (d.Type != 12 && d.Reward != "money")
                    return Bad("Only source bags have noncash rewards.", out reason);
                if (e.Status != GoldMinerEntityStatus.Collected && d.Award != 0)
                    return Bad("Uncollected source reward.", out reason);
            }
            for (int i = 0; i < count; i++)
            {
                var h = s.Hooks[i];
                var d = h == null ? null : h.Source;
                if (h == null || d == null || h.PlayerId != i || (int)h.State < 0 || (int)h.State > 3 || !Finite(h.AngleRadians, -1.2, 1.2) ||
                   !Finite(h.Length, 0, 20) || !Finite(d.X, -10, 20) || !Finite(d.Y, -1, 10) || d.SwingTick < 0 || d.SwingTick >= 240 ||
                   (d.TravelDirection != -1 && d.TravelDirection != 1) || d.RewardTicks < 0 || d.RewardTicks > 54 || d.ResetTicks < 0 || d.ResetTicks > 6 || d.BombTicks < 0 || d.BombTicks > 30 ||
                   (h.SwingDirection != 1 && h.SwingDirection != -1))
                    return Bad("Invalid source hook.", out reason);
                if (h.HeldEntityId.HasValue)
                {
                    int id = h.HeldEntityId.Value;
                    if (id < 1 || id > s.Entities.Length || !held.Add(id) || s.Entities[id - 1].Status != GoldMinerEntityStatus.Held || s.Entities[id - 1].OwnerPlayerId != i || h.State != GoldMinerHookState.RetractingLoaded)
                        return Bad("Invalid source caught object.", out reason);
                }
                else if (h.State == GoldMinerHookState.RetractingLoaded)
                    return Bad("Source loaded hook missing object.", out reason);
                if (mode == GoldMinerMode.Solo ? d.BombTicks != 0 || d.ResetTicks != 0 || d.TravelDirection != 1 : d.RewardTicks != 0 || d.PendingEntityId != null)
                    return Bad("Delayed source effect belongs to another mode.", out reason);
                if (d.RewardTicks > 0 && (!h.HeldEntityId.HasValue || !d.PendingEntityId.HasValue || d.PendingEntityId != h.HeldEntityId ||
                    h.State != GoldMinerHookState.RetractingLoaded || d.X != GoldMinerSourceRules.OriginX(mode, i) || d.Y != 0 || d.BombTicks != 0 || d.ResetTicks != 0))
                    return Bad("Delayed source reward requires the returned held object.", out reason);
                if (d.RewardTicks > 0 && s.Entities[h.HeldEntityId.Value - 1].Source.Reward == "dynamite" && d.RewardTicks > 30)
                    return Bad("Source dynamite reward exceeds its original delay.", out reason);
                double rayX = d.X - GoldMinerSourceRules.OriginX(mode, i);
                if (Math.Abs(rayX * Math.Cos(h.AngleRadians) - d.Y * Math.Sin(h.AngleRadians)) > 1e-7)
                    return Bad("Source hook left its accepted launch ray.", out reason);
                if (d.ResetTicks > 0 && (h.HeldEntityId.HasValue || h.State != GoldMinerHookState.RetractingEmpty))
                    return Bad("Source reset requires an empty returning hook.", out reason);
                if (d.PendingEntityId.HasValue && (d.PendingEntityId != h.HeldEntityId || d.RewardTicks == 0))
                    return Bad("Invalid delayed source reward.", out reason);
            }
            foreach (var e in s.Entities)
                if (e.Status == GoldMinerEntityStatus.Held && !held.Contains(e.Id))
                    return Bad("Orphaned source held entity.", out reason);
            bool terminal = s.Phase == GoldMinerPhase.Won || s.Phase == GoldMinerPhase.Failed || s.Phase == GoldMinerPhase.Completed;
            if (terminal != (s.Settlement != null) || (s.Phase == GoldMinerPhase.Shop) != (s.Shop != null))
                return Bad("Invalid source phase attachments.", out reason);
            if (s.Phase == GoldMinerPhase.Ready && s.SimulationTick != 0)
                return Bad("Ready source clock advanced.", out reason);
            if ((terminal || s.Phase == GoldMinerPhase.Shop || s.Phase == GoldMinerPhase.Draining) && s.RemainingTicks != 0)
                return Bad("Source settlement precedes the deadline.", out reason);
            return true;
        }
        public static bool Checkpoint(GoldMinerLevelStart p, GoldMinerMode mode, out string reason)
        {
            reason = null;
            if (p == null || !GoldMinerRules.ValidMode(mode) || p.Source == null || p.LevelIndex < 1 || p.LevelIndex > GoldMinerSourceRules.MaximumLevel(mode) || p.OpeningPlayers == null)
                return Bad("Missing source checkpoint.", out reason);
            var s = p.CreateState(mode);
            if (!State(s, mode, out reason))
                return false;
            if (!GoldMinerSourceSignatures.Matches(mode, p.Source.MapIndex, p.InitialEntities))
                return Bad("Source checkpoint differs from the shipped map.", out reason);
            double initialStrength = p.ActiveBuffs.Strength ? (mode == GoldMinerMode.Solo ? 2 : 20) : 0;
            if (p.Source.Strength != initialStrength || (mode == GoldMinerMode.Solo ? p.Source.SharedBombs != 0 : p.Source.SharedBombs > 1))
                return Bad("Source opening inventory differs from purchased effects.", out reason);
            if (p.LevelIndex == 1 && (p.OpeningCash != 0 || p.OpeningShopSpent != 0 || !p.ActiveBuffs.IsEmpty || p.Source.SharedBombs != 0))
                return Bad("Source campaign must start without money or purchased effects.", out reason);
            foreach (var player in p.OpeningPlayers)
                if ((mode == GoldMinerMode.Coop && player.Bombs != 0) || (p.LevelIndex == 1 && (player.Bombs != 0 || player.TotalCollected != 0)))
                    return Bad("Source opening player inventory changed.", out reason);
            foreach (var entity in p.InitialEntities)
            {
                var data = entity.Source;
                if (data.Type != 12) continue;
                if (mode == GoldMinerMode.Coop)
                {
                    if (data.Value != 333 || data.Weight != 80 || data.Reward != "money")
                        return Bad("Source duo bag definition changed.", out reason);
                    continue;
                }
                if (data.Weight != Math.Floor(data.Weight) || data.Weight == 0 || data.Weight < -5 || data.Weight > 9)
                    return Bad("Source solo bag weight is outside original outcomes.", out reason);
                if (data.Reward != "money")
                {
                    if (data.Value != 0 || (data.Reward == "dynamite" && p.OpeningPlayers[0].Bombs > 3))
                        return Bad("Source solo bag effect changed.", out reason);
                }
                else if (p.ActiveBuffs.Clover
                    ? !(data.Value == 700 || (p.OpeningPlayers[0].Bombs > 3 && data.Value >= 300 && data.Value <= 599))
                    : !(data.Value >= 1 && data.Value <= 600 || data.Value == 800))
                    return Bad("Source solo bag money is outside original outcomes.", out reason);
            }
            var hooks = GoldMinerSourceRules.Hooks(mode);
            for (int i = 0; i < hooks.Length; i++)
                if (s.Hooks[i].State != GoldMinerHookState.Swinging || s.Hooks[i].HeldEntityId != null || s.Hooks[i].Source.X != hooks[i].Source.X || s.Hooks[i].Source.Y != 0 || s.Hooks[i].Source.SwingTick != 0 || s.Hooks[i].Source.TravelDirection != 1 || s.Hooks[i].SwingDirection != hooks[i].SwingDirection || s.Hooks[i].Length != hooks[i].Length || s.Hooks[i].Source.RewardTicks != 0 || s.Hooks[i].Source.BombTicks != 0 || s.Hooks[i].Source.ResetTicks != 0 || s.Hooks[i].AngleRadians != hooks[i].AngleRadians)
                    return Bad("Noninitial source hook.", out reason);
            foreach (var e in s.Entities)
                if (e.Status != GoldMinerEntityStatus.Available || e.OwnerPlayerId != null || e.X != e.Source.InitialX || e.Y != e.Source.InitialY || e.Source.MotionTick != 0 || e.Source.Award != 0)
                    return Bad("Noninitial source entity.", out reason);
            return true;
        }
        public static bool Run(GoldMinerRunSnapshot r, out string reason)
        {
            reason = null;
            if (r == null || r.BalanceProfile != GoldMinerBalanceProfile.Source || !GoldMinerValidation.Hex(r.RunId, 32) || r.RunSeed == 0 || r.AttemptIndex < 0 || r.LastCommandSeq < 0 ||
                !Checkpoint(r.Checkpoint, r.Mode, out reason) || !State(r.Current, r.Mode, out reason))
                return false;
            var c = r.Current;
            var p = r.Checkpoint;
            if (c.LevelIndex != p.LevelIndex || c.LayoutId != p.LayoutId || c.LayoutContentHash != p.LayoutContentHash || c.Entities.Length != p.InitialEntities.Length || c.Source.MapIndex != p.Source.MapIndex || c.LevelLayoutSeed != p.LevelLayoutSeed || c.LayoutRng != p.LayoutRng)
                return Bad("Source checkpoint identity changed.", out reason);
            long[] awards = new long[c.Players.Length];
            for (int i = 0; i < c.Entities.Length; i++)
            {
                var a = c.Entities[i];
                var b = p.InitialEntities[i];
                var x = a.Source;
                var y = b.Source;
                if (a.Kind != b.Kind || x.Key != y.Key || x.Type != y.Type || x.Value != y.Value || x.Weight != y.Weight || x.Radius != y.Radius || x.ScaleX != y.ScaleX || x.ScaleY != y.ScaleY || x.Flip != y.Flip || x.InitialX != y.InitialX || x.InitialY != y.InitialY || x.MotionEndX != y.MotionEndX || x.Reward != y.Reward)
                    return Bad("Source object definition changed.", out reason);
                if (a.Status == GoldMinerEntityStatus.Collected)
                {
                    long expected = r.Mode == GoldMinerMode.Solo
                        ? (x.Reward == "money" ? GoldMinerSourceRules.Value(a, p.ActiveBuffs, r.Mode) : 0)
                        : x.Value;
                    if (r.Mode == GoldMinerMode.Coop && x.Type == 12
                        ? x.Award > 600 : x.Award != expected)
                        return Bad("Source award differs from its accepted value.", out reason);
                    // The award owner is persisted separately from active hook ownership.
                    if (x.AwardPlayer < 0 || x.AwardPlayer >= awards.Length)
                        return Bad("Invalid source award owner.", out reason);
                    awards[x.AwardPlayer] = checked(awards[x.AwardPlayer] + x.Award);
                }
            }
            for (int i = 0; i < awards.Length; i++)
                if (c.Players[i].TotalCollected != p.OpeningPlayers[i].TotalCollected + awards[i] || c.Players[i].LevelCollected != awards[i])
                    return Bad("Source awarded income changed.", out reason);
            if (c.Phase == GoldMinerPhase.Ready)
            {
                if (c.Cash != p.OpeningCash || c.ShopSpent != p.OpeningShopSpent || c.Source.Strength != p.Source.Strength || c.Source.SharedBombs != p.Source.SharedBombs)
                    return Bad("Ready source inventory changed.", out reason);
                for (int i = 0; i < c.Entities.Length; i++)
                if (c.Entities[i].X != p.InitialEntities[i].X || c.Entities[i].Y != p.InitialEntities[i].Y || c.Entities[i].Status != GoldMinerEntityStatus.Available)
                    return Bad("Ready source positions changed.", out reason);
            }
            bool cleared = c.Phase == GoldMinerPhase.Won || c.Phase == GoldMinerPhase.Shop || c.Phase == GoldMinerPhase.Completed;
            if (cleared ? !c.ActiveBuffs.IsEmpty : !c.ActiveBuffs.Same(p.ActiveBuffs))
                return Bad("Source active buff lifetime changed.", out reason);
            if (c.Shop == null && !c.PendingBuffs.IsEmpty)
                return Bad("Source pending buffs require a purchased shop offer.", out reason);
            if (c.Shop != null && (c.Shop.Id != r.RunId + "/" + c.LevelIndex || c.Shop.EntryShopSpent != p.OpeningShopSpent ||
                c.Shop.EntryCash != GoldMinerValidation.Gross(c.Players) - p.OpeningShopSpent))
                return Bad("Source shop opening ledger changed.", out reason);
            if (c.Shop != null && !Shop(c, r.Mode, out reason))
                return false;
            if (c.Settlement != null)
            {
                var t = c.Settlement;
                bool success = c.Cash >= c.Target;
                var expected = success ? (GoldMinerSourceRules.Completed(r.Mode, c.LevelIndex) ? GoldMinerOutcome.Completed : GoldMinerOutcome.Won) : GoldMinerOutcome.Failed;
                if ((int)c.Phase != (int)(expected == GoldMinerOutcome.Completed ? GoldMinerPhase.Completed : expected == GoldMinerOutcome.Won ? GoldMinerPhase.Won : GoldMinerPhase.Failed) || t.Id != r.RunId + "/" + c.LevelIndex + "/" + r.AttemptIndex || t.Outcome != expected || t.Cash != c.Cash || t.Target != c.Target || t.RunGrossIncome != GoldMinerValidation.Gross(c.Players) || t.RecordCommitted != success || t.Contributions == null || t.Contributions.Length != c.Players.Length)
                    return Bad("Invalid source settlement.", out reason);
                for (int i = 0; i < t.Contributions.Length; i++)
                    if (t.Contributions[i].PlayerId != i || t.Contributions[i].Amount != c.Players[i].LevelCollected)
                        return Bad("Invalid source contribution.", out reason);
            }
            return true;
        }
        static bool Shop(GoldMinerState c, GoldMinerMode mode, out string reason)
        {
            reason = null;
            var shop = c.Shop;
            if (shop.Offers == null || shop.Offers.Length != 5 || shop.EntryBombs == null ||
                shop.EntryBombs.Length != c.Players.Length || shop.Spent < 0 || shop.EntryShopSpent < 0)
                return Bad("Invalid source shop.", out reason);
            var expectedBuffs = new GoldMinerBuffs();
            long spent = 0;
            int available = 0;
            for (int i = 0; i < shop.Offers.Length; i++)
            {
                var offer = shop.Offers[i];
                if (offer == null || offer.ItemId != (GoldMinerItem)i || offer.Remaining < 0 || offer.Purchased < 0 ||
                    offer.Remaining + offer.Purchased > 1 || !GoldMinerSourceEconomy.ValidPrice(mode, c.LevelIndex, (GoldMinerItem)i, offer.Price))
                    return Bad("Invalid source offer.", out reason);
                spent = checked(spent + offer.Price * offer.Purchased);
                available += offer.Remaining + offer.Purchased;
                if (i == 0)
                {
                    if (offer.Allocations == null || offer.Allocations.Length != c.Players.Length)
                        return Bad("Invalid source bomb allocation.", out reason);
                    int allocated = 0;
                    for (int player = 0; player < c.Players.Length; player++)
                    {
                        var allocation = offer.Allocations[player];
                        var entry = shop.EntryBombs[player];
                        if (allocation == null || allocation.PlayerId != player || allocation.Count < 0 || allocation.Count > 1 ||
                            entry == null || entry.PlayerId != player || entry.Bombs < 0 || entry.Bombs > 1000)
                            return Bad("Invalid source shop player inventory.", out reason);
                        allocated += allocation.Count;
                        if (mode == GoldMinerMode.Solo ? c.Players[player].Bombs != entry.Bombs + allocation.Count :
                            c.Players[player].Bombs != 0 || entry.Bombs != 0)
                            return Bad("Source purchased bombs differ from the inventory.", out reason);
                    }
                    if (allocated != offer.Purchased || (mode == GoldMinerMode.Coop ? c.Source.SharedBombs != allocated : c.Source.SharedBombs != 0))
                        return Bad("Source shared bomb ledger mismatch.", out reason);
                }
                else
                {
                    if (offer.Allocations != null)
                        return Bad("A nonbomb offer cannot allocate bombs.", out reason);
                    if (offer.Purchased == 1)
                        GoldMinerEconomy.SetBuff(expectedBuffs, offer.ItemId);
                }
            }
            if (!c.PendingBuffs.Same(expectedBuffs))
                return Bad("Source purchased buffs differ from the inventory.", out reason);
            if (mode == GoldMinerMode.Coop && available != 3)
                return Bad("Source duo shop must contain three offers.", out reason);
            return spent == shop.Spent && c.Cash == shop.EntryCash - spent && c.ShopSpent == shop.EntryShopSpent + spent ||
                Bad("Source shop ledger mismatch.", out reason);
        }
    }
}
