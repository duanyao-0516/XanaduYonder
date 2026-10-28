using System;

namespace XanaduYonder.Core.GoldMiner
{
    public static class GoldMinerEconomy
    {
        public static GoldMinerShop CreateShop(GoldMinerRunSnapshot run)
        {
            if (run == null || run.Current == null || run.Current.LevelIndex >= GoldMinerRules.LevelCount)
                throw new ArgumentException("A shop requires a completed level from 1 to 9.");
            var state = run.Current;
            var shop = new GoldMinerShop
            {
                Id = run.RunId + "/" + state.LevelIndex,
                EntryCash = state.Cash,
                EntryShopSpent = state.ShopSpent,
                EntryBombs = new GoldMinerBombStock[state.Players.Length],
                Offers = new GoldMinerOffer[5]
            };
            for (int i = 0; i < state.Players.Length; i++)
                shop.EntryBombs[i] = new GoldMinerBombStock
                {
                    PlayerId = i,
                    Bombs = state.Players[i].Bombs
                };
            for (int i = 0; i < shop.Offers.Length; i++)
            {
                var offer = new GoldMinerOffer
                {
                    ItemId = (GoldMinerItem)i,
                    Price = GoldMinerRules.Price((GoldMinerItem)i, state.LevelIndex),
                    Remaining = i == 0 ? 3 : 1
                };
                if (i == 0)
                {
                    offer.Allocations = new GoldMinerAllocation[state.Players.Length];
                    for (int p = 0; p < state.Players.Length; p++)
                        offer.Allocations[p] = new GoldMinerAllocation
                        {
                            PlayerId = p
                        };
                }

                shop.Offers[i] = offer;
            }

            return shop;
        }

        public static bool CanPurchase(GoldMinerState state, GoldMinerItem item, int playerId, out string reason)
        {
            reason = null;
            if (state == null || state.Phase != GoldMinerPhase.Shop || state.Shop == null || (int)item < 0 || (int)item >= 5 || playerId < 0 || playerId >= state.Players.Length)
            {
                reason = "No matching shop/player.";
                return false;
            }

            var offer = state.Shop.Offers[(int)item];
            if (offer.Remaining == 0)
            {
                reason = "Sold out.";
                return false;
            }

            if (state.Cash < offer.Price)
            {
                reason = "Insufficient cash.";
                return false;
            }

            if (item == GoldMinerItem.Dynamite && state.Players[playerId].Bombs >= GoldMinerRules.MaxBombs)
            {
                reason = "Dynamite inventory is full.";
                return false;
            }

            return true;
        }

        // Only candidate-owned state is mutated; callers publish after durable storage succeeds.
        internal static bool Purchase(GoldMinerState state, GoldMinerItem item, int playerId)
        {
            string reason;
            if (!CanPurchase(state, item, playerId, out reason))
                return false;
            var offer = state.Shop.Offers[(int)item];
            state.Cash -= offer.Price;
            state.ShopSpent = checked(state.ShopSpent + offer.Price);
            state.Shop.Spent = checked(state.Shop.Spent + offer.Price);
            offer.Remaining--;
            offer.Purchased++;
            if (item == GoldMinerItem.Dynamite)
            {
                state.Players[playerId].Bombs++;
                offer.Allocations[playerId].Count++;
            }
            else
                SetBuff(state.PendingBuffs, item);
            return true;
        }

        internal static void SetBuff(GoldMinerBuffs buffs, GoldMinerItem item)
        {
            switch (item)
            {
                case GoldMinerItem.Strength:
                    buffs.Strength = true;
                    break;
                case GoldMinerItem.Clover:
                    buffs.Clover = true;
                    break;
                case GoldMinerItem.RockBook:
                    buffs.RockBook = true;
                    break;
                case GoldMinerItem.DiamondOil:
                    buffs.DiamondOil = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("item");
            }
        }
    }
}
