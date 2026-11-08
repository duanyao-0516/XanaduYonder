using System;
using System.Globalization;
using XanaduYonder.Core.GoldMiner;

internal static partial class Program
{
    static void RunBalanceCases()
    {
        Check("mid-depth loads visibly separate light treasure from heavy gold and stone", () =>
        {
            var kinds = new[] { GoldMinerKind.GoldSmall, GoldMinerKind.GoldMedium, GoldMinerKind.GoldLarge,
                GoldMinerKind.RockSmall, GoldMinerKind.RockLarge, GoldMinerKind.Diamond };
            var seconds = new double[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                seconds[i] = MeasureReturn(kinds[i], 3, false);
                Console.WriteLine("TIMING depth=3 " + kinds[i] + " return=" + seconds[i].ToString("F3", CultureInfo.InvariantCulture));
            }
            Assert(seconds[0] >= 1 && seconds[0] <= 1.2, "small gold should still feel responsive");
            Assert(seconds[1] >= 1.9 && seconds[1] <= 2.2, "medium gold must visibly take longer");
            Assert(seconds[2] >= 3.7 && seconds[2] <= 4, "large gold must require a deliberate time investment");
            Assert(seconds[3] >= 3 && seconds[3] <= 3.3, "small stone must impose a cost");
            Assert(seconds[4] >= 4.7 && seconds[4] <= 5, "large stone must be the slowest common load");
            Assert(seconds[5] >= .6 && seconds[5] <= .75, "diamond remains a rewarding quick catch");
            Assert(seconds[2] - seconds[0] >= 2.5 && seconds[4] - seconds[2] >= .8);
        });
        Check("strength and dynamite recover meaningful time on deep heavy loads", () =>
        {
            var normal = MeasureReturn(GoldMinerKind.RockLarge, 6.6, false);
            var strong = MeasureReturn(GoldMinerKind.RockLarge, 6.6, true);
            var dynamite = MeasureReturn(GoldMinerKind.RockLarge, 6.6, false, true);
            Console.WriteLine("TIMING depth=6.6 RockLarge return=" + normal.ToString("F3", CultureInfo.InvariantCulture)
                + " strength=" + strong.ToString("F3", CultureInfo.InvariantCulture)
                + " dynamite=" + dynamite.ToString("F3", CultureInfo.InvariantCulture));
            Assert(normal >= 12 && normal <= 13, "deep large stone should occupy about twelve seconds");
            Assert(normal - strong >= 3.5 && normal - dynamite >= 10, "shop tools need a tangible payoff");
            Near(strong / normal, 2.0 / 3, .003);
        });
        Check("each shop exposes three products with increasing prices and no RNG consumption", () =>
        {
            int[] masks = { 7, 11, 21, 19, 13, 19, 25, 7, 19 };
            foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
            for (int level = 1; level < 10; level++)
            {
                var run = mode == GoldMinerMode.Solo ? Run(level, 100000) : CompetingRun(false);
                if (mode == GoldMinerMode.Coop)
                {
                    run.Current.LevelIndex = level;
                    run.Current.Cash = 100000;
                }
                uint layout = run.Current.LayoutRng, bags = run.Current.BagRng;
                var shop = GoldMinerEconomy.CreateShop(run);
                int mask = 0; long total = 0;
                foreach (var offer in shop.Offers)
                {
                    if (offer.Remaining > 0) mask |= 1 << (int)offer.ItemId;
                    total += offer.Price * offer.Remaining;
                    if (level > 1) Assert(offer.Price > GoldMinerRules.Price(offer.ItemId, level - 1));
                }
                Assert(mask == masks[level - 1]);
                Assert(shop.Offers[0].Remaining == GoldMinerRules.PlayerCount(mode));
                Assert(total == GoldMinerRules.ShopMaximum(level, mode));
                Assert(run.Current.LayoutRng == layout && run.Current.BagRng == bags);
            }
        });
        Check("old run profiles retain old values targets prices and full shelves", () =>
        {
            foreach (var profile in new[] { GoldMinerBalanceProfile.Legacy, GoldMinerBalanceProfile.Native })
            {
                var run = Run(); run.BalanceProfile = profile;
                run.Current.Target = GoldMinerRules.Target(run.Mode, 1, profile);
                run.Current.Phase = GoldMinerPhase.Running;
                run.Current.Hooks[0].AngleRadians = 0;
                var game = new GoldMinerGame(run);
                Assert(game.TryLaunch(0, 1));
                while (game.GetStateSnapshot().Cash == 0 && !game.SettlementReady) game.Step();
                Assert(game.GetStateSnapshot().Cash == 100);
                string reason; Assert(game.GetSnapshot().TryValidate(out reason), reason);
                var shop = GoldMinerEconomy.CreateShop(run);
                Assert(shop.Offers[0].Remaining == 3 && shop.Offers[0].Price == 70);
                for (int i = 1; i < 5; i++) Assert(shop.Offers[i].Remaining == 1);
            }
        });
        Check("validated v1 saves upgrade without changing either slot or transaction progress", () =>
        {
            var legacy = new GoldMinerRootSnapshot { RulesVersion = 1, Solo = DeadlineRun(GoldMinerMode.Solo),
                Coop = DeadlineRun(GoldMinerMode.Coop), TxnSeq = 19 };
            legacy.Solo.BalanceProfile = legacy.Coop.BalanceProfile = GoldMinerBalanceProfile.Legacy;
            legacy.Solo.Current.Target = GoldMinerRules.LegacyTarget(GoldMinerMode.Solo, 1);
            legacy.Coop.Current.Target = GoldMinerRules.LegacyTarget(GoldMinerMode.Coop, 1);
            string reason;
            Assert(legacy.TryValidate(out reason), reason);
            var soloHash = SnapshotHash(legacy.Solo); var coopHash = SnapshotHash(legacy.Coop);
            var session = new GoldMinerSession(legacy); var upgraded = session.GetSnapshot();
            Assert(upgraded.RulesVersion == GoldMinerRules.Version && session.Dirty, "v1 is migrated and scheduled for normal save");
            Assert(legacy.RulesVersion == 1 && upgraded.TxnSeq == 19, "never mutate the loaded source or invent transactions");
            Assert(SnapshotHash(upgraded.Solo) == soloHash && SnapshotHash(upgraded.Coop) == coopHash);
            foreach (var run in new[] { upgraded.Solo, upgraded.Coop })
            {
                var game = new GoldMinerGame(run);
                Assert(game.TryLaunch(0, 1));
                while (!game.SettlementReady) game.Step();
                Assert(game.GetStateSnapshot().Cash == 50, "last-second heavy catch survives migration and draining");
            }
        });
    }

    static double MeasureReturn(GoldMinerKind kind, double depth, bool strength, bool dynamite = false)
    {
        var run = Run(); var initial = run.Checkpoint;
        initial.InitialEntities = new[] { new GoldMinerEntity { Id = 1, Kind = kind, X = 3, Y = depth } };
        initial.ActiveBuffs.Strength = strength;
        run.Current = initial.CreateState(GoldMinerMode.Solo);
        run.Current.Phase = GoldMinerPhase.Running;
        run.Current.Hooks[0].AngleRadians = 0;
        var game = new GoldMinerGame(run); Assert(game.TryLaunch(0, 1));
        int caughtAt = -1;
        while (game.GetStateSnapshot().SimulationTick < GoldMinerRules.LevelTicks)
        {
            game.Step(); var state = game.GetStateSnapshot();
            if (caughtAt < 0 && state.Hooks[0].State == GoldMinerHookState.RetractingLoaded)
            {
                caughtAt = state.SimulationTick;
                if (dynamite) Assert(game.TryUseDynamite(0, 2));
            }
            if (caughtAt >= 0 && state.Hooks[0].State == GoldMinerHookState.Swinging)
                return (state.SimulationTick - caughtAt) * GoldMinerRules.TickSeconds;
        }
        throw new Exception("calibration catch failed");
    }
}
