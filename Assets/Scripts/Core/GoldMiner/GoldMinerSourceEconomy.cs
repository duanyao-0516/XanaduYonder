using System;
namespace XanaduYonder.Core.GoldMiner
{
    internal static class GoldMinerSourceEconomy
    {
        static long Price(GoldMinerMode mode, int level, int item, ref uint rng)
        {
            switch (item)
            {
                case 0:
                    return GoldMinerSourceRules.Random(ref rng, 300) + 1 + (mode == GoldMinerMode.Solo ? 2 * level : 0);
                case 1:
                    return GoldMinerSourceRules.Random(ref rng, 300) + 100;
                case 2:
                    return GoldMinerSourceRules.Random(ref rng, mode == GoldMinerMode.Solo ? 50 * level : 100) + 1 + (mode == GoldMinerMode.Solo ? 2 * level : 0);
                case 3:
                    return GoldMinerSourceRules.Random(ref rng, 150) + 1;
                default:
                    return mode == GoldMinerMode.Solo ? GoldMinerSourceRules.Random(ref rng, 100 * level) + 201 : GoldMinerSourceRules.Random(ref rng, 300) + 200;
            }
        }
        internal static bool ValidPrice(GoldMinerMode mode, int level, GoldMinerItem item, long price)
        {
            switch (item)
            {
                case GoldMinerItem.Dynamite:
                    return price >= 1 + (mode == GoldMinerMode.Solo ? 2 * level : 0) && price <= 300 + (mode == GoldMinerMode.Solo ? 2 * level : 0);
                case GoldMinerItem.Strength:
                    return price >= 100 && price <= 399;
                case GoldMinerItem.Clover:
                    return price >= 1 + (mode == GoldMinerMode.Solo ? 2 * level : 0) && price <= (mode == GoldMinerMode.Solo ? 52 * level : 100);
                case GoldMinerItem.RockBook:
                    return price >= 1 && price <= 150;
                default:
                    return mode == GoldMinerMode.Solo ? price >= 201 && price <= 100 * level + 200 : price >= 200 && price <= 499;
            }
        }
        internal static GoldMinerShop Create(GoldMinerRunSnapshot run)
        {
            var s = run.Current;
            bool solo = run.Mode == GoldMinerMode.Solo;
            var shop = new GoldMinerShop { Id = run.RunId + "/" + s.LevelIndex, EntryCash = s.Cash, EntryShopSpent = s.ShopSpent, EntryBombs = new GoldMinerBombStock[s.Players.Length], Offers = new GoldMinerOffer[5] };
            if (!solo)
                s.Source.SharedBombs = 0;
            for (int i = 0; i < s.Players.Length; i++)
                shop.EntryBombs[i] = new GoldMinerBombStock { PlayerId = i, Bombs = s.Players[i].Bombs };
            bool[] stock = new bool[5];
            if (solo)
            {
                stock[0] = s.Players[0].Bombs < 5;
                stock[1] = GoldMinerSourceRules.Random(ref s.BagRng, 10) + 1 < 5;
                stock[2] = GoldMinerSourceRules.Random(ref s.BagRng, 10) + 1 < 5;
                stock[3] = GoldMinerSourceRules.Random(ref s.BagRng, 10) + 1 < 7;
                stock[4] = GoldMinerSourceRules.Random(ref s.BagRng, 10) + 1 < 6;
            }
            else
            {
                // The source chooses three distinct shelves using a random comparator.
                // Its comparator is engine-dependent; persist a deterministic random ordering.
                int[] ids = { 0, 1, 2, 3, 4 };
                for (int i = 1; i < ids.Length; i++)
                {
                    int j = i;
                    while (j > 0 && GoldMinerSourceRules.Random(ref s.BagRng, 2) == 0)
                    {
                        int t = ids[j];
                        ids[j] = ids[j - 1];
                        ids[j - 1] = t;
                        j--;
                    }
                }
                for (int i = 0; i < 3; i++)
                    stock[ids[i]] = true;
            }
            for (int i = 0; i < 5; i++)
            {
                var offer = new GoldMinerOffer { ItemId = (GoldMinerItem)i, Price = Price(run.Mode, s.LevelIndex, i, ref s.BagRng), Remaining = stock[i] ? 1 : 0 };
                if (i == 0)
                {
                    offer.Allocations = new GoldMinerAllocation[s.Players.Length];
                    for (int p = 0; p < offer.Allocations.Length; p++)
                        offer.Allocations[p] = new GoldMinerAllocation { PlayerId = p };
                }
                shop.Offers[i] = offer;
            }
            return shop;
        }
        internal static bool CanPurchase(GoldMinerState state, GoldMinerItem item, int player, out string reason)
        {
            reason = null;
            if (state == null || state.Source == null || state.Phase != GoldMinerPhase.Shop || state.Shop == null || (int)item < 0 || (int)item > 4 || player < 0 || player >= state.Players.Length)
            {
                reason = "No matching source shop.";
                return false;
            }
            var offer = state.Shop.Offers[(int)item];
            if (offer.Remaining != 1)
            {
                reason = "Sold out or unavailable.";
                return false;
            }
            if (state.Players.Length == 2 && state.Cash < offer.Price)
            {
                reason = "Insufficient cash.";
                return false;
            }
            return true;
        }
        internal static bool Purchase(GoldMinerState state, GoldMinerItem item, int player)
        {
            string reason;
            if (!CanPurchase(state, item, player, out reason))
                return false;
            var o = state.Shop.Offers[(int)item];
            state.Cash -= o.Price;
            state.ShopSpent += o.Price;
            state.Shop.Spent += o.Price;
            o.Remaining = 0;
            o.Purchased = 1;
            if (item == GoldMinerItem.Dynamite)
            {
                if (state.Players.Length == 1)
                    state.Players[0].Bombs++;
                else
                    state.Source.SharedBombs++;
                o.Allocations[player].Count++;
            }
            else
                GoldMinerEconomy.SetBuff(state.PendingBuffs, item);
            return true;
        }
    }
}
