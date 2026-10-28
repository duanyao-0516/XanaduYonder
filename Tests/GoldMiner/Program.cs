using System;
using XanaduYonder.Core.GoldMiner;

internal static partial class Program
{
    static int passed, failed;
    static void Main()
    {
        RunBalanceCases();
        Check("rules match the approved targets, weights and economy", () =>
        {
            long[] curve = { 1200, 3000, 5100, 7600, 10400, 13600, 17200, 21200, 25600, 30400 };
            Assert(GoldMinerRules.Version == 3);
            for (int level = 1; level <= 10; level++)
            {
                Assert(GoldMinerRules.Target(GoldMinerMode.Solo, level) == curve[level - 1]);
                Assert(GoldMinerRules.Target(GoldMinerMode.Coop, level) == curve[level - 1] * 2);
            }
            Assert(GoldMinerRules.Count(GoldMinerMode.Coop, 1, GoldMinerKind.GoldSmall) == 7);
            Near(GoldMinerRules.Weight(GoldMinerKind.GoldLarge), 4);
            Assert(GoldMinerRules.Price(GoldMinerItem.Dynamite, 1) == 70);
            Assert(GoldMinerRules.RequiredIncome(GoldMinerMode.Solo, 2) == 2470);
            Assert(GoldMinerRules.Value(new GoldMinerEntity { Kind = GoldMinerKind.DiamondMole }, new GoldMinerBuffs { DiamondOil = true }) == 930);
        });
        Check("new root has two independent records and validates", () =>
        {
            var root = new GoldMinerRootSnapshot();
            string reason;
            Assert(root.TryValidate(out reason), reason);
            var copy = root.Copy();
            copy.SoloRecord.CompletionCount = 3;
            Assert(root.SoloRecord.CompletionCount == 0);
        });
        Check("launch consumes failed commands and cannot queue a busy shot", () =>
        {
            var g = Game();
            Assert(g.StartLevel());
            Assert(g.TryLaunch(0, 1));
            Assert(!g.TryLaunch(0, 2));
            Assert(g.GetSnapshot().LastCommandSeq == 2);
            while (g.GetSnapshot().Current.Hooks[0].State != GoldMinerHookState.Swinging)
                g.Step();
            Assert(!g.TryLaunch(0, 2));
            Assert(g.TryLaunch(0, 3));
        });
        Check("deadline takes exactly 3600 ticks and draining rejects launches", () =>
        {
            var g = Game();
            g.StartLevel();
            for (int i = 0; i < 3599; i++)
                g.Step();
            Assert(g.GetSnapshot().Current.RemainingTicks == 1);
            g.Step();
            Assert(g.GetSnapshot().Current.Phase == GoldMinerPhase.Draining);
            Assert(!g.TryLaunch(0, 1));
            Assert(g.GetSnapshot().Current.RemainingTicks == 0);
        });
        Check("catch is not income; returning load credits exactly once", () =>
        {
            var r = Run();
            r.Current.Phase = GoldMinerPhase.Running;
            r.Current.Hooks[0].AngleRadians = 0;
            var g = new GoldMinerGame(r);
            Assert(g.TryLaunch(0, 1));
            bool caught = false;
            for (int i = 0; i < 500; i++)
            {
                g.Step();
                var c = g.GetSnapshot().Current;
                if (c.Hooks[0].State == GoldMinerHookState.RetractingLoaded)
                {
                    caught = true;
                    Assert(c.Cash == 0);
                }

                if (c.Cash > 0)
                    break;
            }

            var s = g.GetSnapshot().Current;
            Assert(caught && s.Cash == 100 && s.Players[0].LevelCollected == 100);
            for (int i = 0; i < 5; i++)
                g.Step();
            Assert(g.GetSnapshot().Current.Cash == 100);
        });
        Check("deadline preserves in-flight catch and final income", () =>
        {
            var r = Run();
            r.Current.Phase = GoldMinerPhase.Running;
            r.Current.RemainingTicks = 1;
            r.Current.SimulationTick = 3599;
            r.Current.Hooks[0].AngleRadians = 0;
            var g = new GoldMinerGame(r);
            g.TryLaunch(0, 1);
            g.Step();
            Assert(g.GetSnapshot().Current.Phase == GoldMinerPhase.Draining);
            for (int i = 0; i < 200; i++)
                g.Step();
            Assert(g.GetSnapshot().Current.Cash == 100);
            Assert(g.GetSnapshot().Current.RemainingTicks == 0);
        });
        Check("dynamite during draining destroys only the held object", () =>
        {
            var r = Run();
            r.Current.Phase = GoldMinerPhase.Running;
            r.Current.RemainingTicks = 1;
            r.Current.SimulationTick = 3599;
            r.Current.Hooks[0].AngleRadians = 0;
            var g = new GoldMinerGame(r);
            g.TryLaunch(0, 1);
            while (g.GetSnapshot().Current.Hooks[0].State != GoldMinerHookState.RetractingLoaded)
                g.Step();
            Assert(g.TryUseDynamite(0, 2));
            Assert(!g.TryUseDynamite(0, 3));
            for (int i = 0; i < 100; i++)
                g.Step();
            var c = g.GetSnapshot().Current;
            Assert(c.Cash == 0 && c.Players[0].Bombs == 0 && c.Entities[0].Status == GoldMinerEntityStatus.Destroyed);
        });
        Check("relative swept circle catches a crossing with no endpoint overlap", () =>
        {
            double t;
            Assert(GoldMinerCollision.TrySweptCircle(0, 0, 2, 0, 1, -1, 1, 1, .1, out t));
            Assert(t > 0 && t < 1);
            Assert(!GoldMinerCollision.TrySweptCircle(0, 0, 1, 0, 0, 2, 1, 2, .1, out t));
        });
        Check("animal reflection remains in bounds and reverses direction", () =>
        {
            var r = Run();
            r.Current.Phase = GoldMinerPhase.Running;
            r.Current.Entities[0].Kind = GoldMinerKind.Mole;
            r.Current.Entities[0].X = 5.799;
            r.Current.Entities[0].MoveDirection = 1;
            r.Checkpoint.InitialEntities[0] = r.Current.Entities[0].Copy();
            var g = new GoldMinerGame(r);
            g.Step();
            var e = g.GetSnapshot().Current.Entities[0];
            Assert(e.X < 5.8 && e.MoveDirection == -1);
        });
        Check("snapshot copies isolate current checkpoint and caller arrays", () =>
        {
            var r = Run();
            var g = new GoldMinerGame(r);
            r.Current.Entities[0].X = 4;
            var a = g.GetSnapshot();
            a.Current.Entities[0].X = 5;
            a.Checkpoint.InitialEntities[0].X = 5;
            Near(g.GetSnapshot().Current.Entities[0].X, 3);
            Near(g.GetSnapshot().Checkpoint.InitialEntities[0].X, 3);
        });
        Check("held ownership, bag and money corruption are rejected", () =>
        {
            string reason;
            var r = Run();
            Assert(r.TryValidate(out reason), reason);
            r.Current.Cash = 1;
            Assert(!r.TryValidate(out reason));
            r = Run();
            r.Current.Entities[0].BagValue = 100;
            Assert(!r.TryValidate(out reason));
            r = Run();
            r.Current.Entities[0].Status = GoldMinerEntityStatus.Held;
            r.Current.Entities[0].OwnerPlayerId = 0;
            Assert(!r.TryValidate(out reason));
        });
        Check("transaction candidates do not mutate before commit and cannot repeat", () =>
        {
            var s = new GoldMinerSession();
            var c = s.PrepareNewRun(GoldMinerMode.Solo, Id, 1, Start());
            Assert(s.GetSnapshot().Solo == null);
            Assert(s.Commit(c));
            Assert(!s.Commit(c));
            Assert(s.GetSnapshot().TxnSeq == 1);
            s.EnterMode(GoldMinerMode.Solo);
            s.Resume(GoldMinerSuspendReason.NeedsExplicitContinue);
            Assert(s.StartLevel());
            var retry = s.PrepareRetryLevel();
            s.Step();
            Assert(!s.Commit(retry));
        });
        Check("retry restores checkpoint but preserves command and transaction order", () =>
        {
            var s = new GoldMinerSession();
            Assert(s.Commit(s.PrepareNewRun(GoldMinerMode.Solo, Id, 1, Start())));
            s.EnterMode(GoldMinerMode.Solo);
            s.Resume(GoldMinerSuspendReason.NeedsExplicitContinue);
            s.StartLevel();
            s.TryLaunch(0, 9);
            for (int i = 0; i < 20; i++)
                s.Step();
            Assert(s.Commit(s.PrepareRetryLevel()));
            var r = s.GetSnapshot().Solo;
            Assert(r.AttemptIndex == 1 && r.LastCommandSeq == 9 && r.Current.RemainingTicks == 3600 && r.Current.Phase == GoldMinerPhase.Ready && r.Current.Cash == 0);
        });
        Check("successful settlement and purchases are atomic and money remains balanced", () =>
        {
            var s = new GoldMinerSession(TerminalRoot(1, 2000));
            s.EnterMode(GoldMinerMode.Solo);
            var candidate = s.PrepareSettlement();
            Assert(s.GetSnapshot().SoloRecord.BestClearedLevel == 0);
            Assert(s.Commit(candidate));
            Assert(s.GetSnapshot().SoloRecord.BestClearedLevel == 1);
            Assert(s.Commit(s.PrepareEnterShop()));
            Assert(s.Commit(s.PreparePurchase(GoldMinerItem.Strength, 0)));
            var r = s.GetSnapshot().Solo;
            Assert(r.Current.Cash == 1860 && r.Current.ShopSpent == 140 && r.Current.PendingBuffs.Strength);
            string reason;
            Assert(s.GetSnapshot().TryValidate(out reason), reason);
            Assert(s.PreparePurchase(GoldMinerItem.Strength, 0) == null);
            Assert(s.Commit(s.PreparePurchase(GoldMinerItem.DiamondOil, 0)));
            Assert(s.Commit(s.PreparePurchase(GoldMinerItem.Clover, 0)));
            Assert(s.Commit(s.PreparePurchase(GoldMinerItem.RockBook, 0)));
            Assert(s.GetSnapshot().Solo.Current.Cash == 1540);
            Assert(s.GetSnapshot().TryValidate(out reason), reason);
        });
        Check("completion increments record once and forbids a final shop", () =>
        {
            var s = new GoldMinerSession(TerminalRoot(10, 40000));
            s.EnterMode(GoldMinerMode.Solo);
            var c = s.PrepareSettlement();
            Assert(s.Commit(c));
            Assert(!s.Commit(c));
            Assert(s.GetSnapshot().SoloRecord.CompletionCount == 1 && s.GetSnapshot().Solo.Current.Phase == GoldMinerPhase.Completed);
            Assert(s.PrepareEnterShop() == null);
            Assert(s.PrepareSettlement() == null);
            var restored = new GoldMinerSession(s.GetSnapshot());
            Assert(restored.GetSnapshot().SoloRecord.CompletionCount == 1);
        });
        Check("suspension reasons compose and freeze real commands and time", () =>
        {
            var s = new GoldMinerSession();
            s.Commit(s.PrepareNewRun(GoldMinerMode.Solo, Id, 1, Start()));
            s.EnterMode(GoldMinerMode.Solo);
            s.Resume(GoldMinerSuspendReason.NeedsExplicitContinue);
            s.StartLevel();
            s.Suspend(GoldMinerSuspendReason.AppPaused);
            s.Suspend(GoldMinerSuspendReason.FocusLost);
            s.Resume(GoldMinerSuspendReason.AppPaused);
            Assert(!s.TryLaunch(0, 1));
            s.Step();
            Assert(s.GetSnapshot().Solo.Current.SimulationTick == 0);
            s.Resume(GoldMinerSuspendReason.FocusLost);
            s.Resume(GoldMinerSuspendReason.NeedsExplicitContinue);
            s.Step();
            Assert(s.GetSnapshot().Solo.Current.SimulationTick == 1);
        });
        Check("simultaneous hooks give an exact tie to P1 and loser keeps its path", () =>
        {
            var g = new GoldMinerGame(CompetingRun(false));
            g.Step();
            var c = g.GetSnapshot().Current;
            Assert(c.Hooks[0].HeldEntityId == 1 && c.Hooks[1].HeldEntityId == 2, "held=" + c.Hooks[0].HeldEntityId + "," + c.Hooks[1].HeldEntityId + " owners=" + c.Entities[0].OwnerPlayerId + "," + c.Entities[1].OwnerPlayerId);
            Assert(c.Entities[0].OwnerPlayerId == 0 && c.Entities[1].OwnerPlayerId == 1);
            string reason;
            Assert(g.GetSnapshot().TryValidate(out reason), reason);
        });
        Check("earlier P2 collision beats P1 rather than update order", () =>
        {
            var g = new GoldMinerGame(CompetingRun(true));
            g.Step();
            var c = g.GetSnapshot().Current;
            Assert(c.Entities[0].OwnerPlayerId == 1);
        });
        Check("extending into a wall retracts and never leaves the mine", () =>
        {
            var r = Run();
            r.Current.Phase = GoldMinerPhase.Running;
            r.Current.Hooks[0].AngleRadians = GoldMinerRules.MaxAngle;
            var g = new GoldMinerGame(r);
            g.TryLaunch(0, 1);
            bool retract = false;
            for (int i = 0; i < 70; i++)
            {
                g.Step();
                var c = g.GetSnapshot();
                string why;
                Assert(c.TryValidate(out why), why);
                retract |= c.Current.Hooks[0].State == GoldMinerHookState.RetractingEmpty;
            }

            Assert(retract && g.GetSnapshot().Current.Hooks[0].State == GoldMinerHookState.Swinging);
        });
        Check("same snapshot and commands replay each state deterministically", () =>
        {
            var a = Game();
            a.StartLevel();
            var b = new GoldMinerGame(a.GetSnapshot());
            for (int i = 0; i < 700; i++)
            {
                if (i % 81 == 0)
                {
                    a.TryLaunch(0, i + 1);
                    b.TryLaunch(0, i + 1);
                }

                a.Step();
                b.Step();
                var x = a.GetSnapshot();
                var y = b.GetSnapshot();
                Near(x.Current.Hooks[0].Length, y.Current.Hooks[0].Length, 0);
                Near(x.Current.Hooks[0].AngleRadians, y.Current.Hooks[0].AngleRadians, 0);
                Assert(x.Current.Cash == y.Current.Cash && x.LastCommandSeq == y.LastCommandSeq);
            }
        });
        Check("early target achievement keeps running and exhausted mine drains", () =>
        {
            var r = Run(1, 1000);
            var g = new GoldMinerGame(r);
            g.StartLevel();
            g.Step();
            Assert(g.GetSnapshot().Current.Phase == GoldMinerPhase.Running);
            r = Run();
            r.Current.Phase = GoldMinerPhase.Running;
            r.Current.Entities[0].Status = GoldMinerEntityStatus.Destroyed;
            r.Current.Entities[1].Status = GoldMinerEntityStatus.Destroyed;
            r.Checkpoint.OpeningPlayers[0].Bombs = 2;
            r.Current.Players[0].Bombs = 0;
            g = new GoldMinerGame(r);
            var step = g.Step();
            Assert(step.SettlementReady && g.GetSnapshot().Current.RemainingTicks == 3599);
        });
        Check("five countdown cues occur once and freeze when settled", () =>
        {
            var g = Game();
            g.StartLevel();
            int cues = 0;
            for (int i = 0; i < 3700; i++)
                foreach (var e in g.Step().Events)
                    if (e.Kind == GoldMinerEventKind.Countdown)
                        cues++;
            Assert(cues == 5 && g.GetSnapshot().Current.SimulationTick == 3600);
        });
        Check("shop dynamite has shared stock and per-player allocation", () =>
        {
            var r = CompetingRun(false);
            r.Current.Hooks = GoldMinerRules.InitialHooks(GoldMinerMode.Coop);
            r.Current.Phase = GoldMinerPhase.Draining;
            r.Current.RemainingTicks = 0;
            r.Current.SimulationTick = 3600;
            r.Current.Players[0].TotalCollected = 3000;
            r.Current.Cash = 3000;
            r.Checkpoint.OpeningPlayers[0].TotalCollected = 3000;
            r.Checkpoint.OpeningCash = 3000;
            var session = new GoldMinerSession(new GoldMinerRootSnapshot { LastMode = GoldMinerMode.Coop, Coop = r });
            Assert(session.Commit(session.PrepareSettlement()));
            Assert(session.Commit(session.PrepareEnterShop()));
            Assert(session.Commit(session.PreparePurchase(GoldMinerItem.Dynamite, 1)));
            Assert(session.Commit(session.PreparePurchase(GoldMinerItem.Dynamite, 0)));
            Assert(session.Commit(session.PreparePurchase(GoldMinerItem.Dynamite, 1)));
            Assert(session.PreparePurchase(GoldMinerItem.Dynamite, 0) == null);
            var c = session.GetSnapshot().Coop.Current;
            Assert(c.Players[0].Bombs == 2 && c.Players[1].Bombs == 3 && c.Cash == 2790);
            string why;
            Assert(session.GetSnapshot().TryValidate(out why), why);
        });
        Check("next level activates bought buffs and retry preserves shop spend", () =>
        {
            var s = new GoldMinerSession(TerminalRoot(1, 2000));
            s.EnterMode(GoldMinerMode.Solo);
            Assert(s.Commit(s.PrepareSettlement()));
            Assert(s.Commit(s.PrepareEnterShop()));
            Assert(s.Commit(s.PreparePurchase(GoldMinerItem.Strength)));
            var c = s.GetStateSnapshot();
            var next = Start(2, c.Cash);
            next.PatternId = "left";
            next.LayoutId = "solo-02-left-0";
            next.PreviousPatternId = c.PatternId;
            next.OpeningShopSpent = c.ShopSpent;
            next.OpeningPlayers[0].TotalCollected = c.Players[0].TotalCollected;
            next.ActiveBuffs = c.PendingBuffs.Copy();
            Assert(s.Commit(s.PrepareNextLevel(next)), s.LastError);
            var r = s.GetSnapshot().Solo;
            Assert(r.Current.ActiveBuffs.Strength && r.Current.PendingBuffs.IsEmpty && r.Current.Cash == 1860 && r.Current.ShopSpent == 140);
            Assert(r.Current.Target == GoldMinerRules.Target(GoldMinerMode.Solo, 2), "A new v3 campaign keeps its new curve on next level.");
            Assert(s.Commit(s.PrepareRetryLevel()));
            r = s.GetSnapshot().Solo;
            Assert(r.Current.ActiveBuffs.Strength && r.Current.Cash == 1860 && r.Current.ShopSpent == 140);
            Assert(r.Current.Target == GoldMinerRules.Target(GoldMinerMode.Solo, 2), "Retry keeps the new campaign target.");
        });
        Check("new run resets only selected mode and keeps records", () =>
        {
            var root = TerminalRoot(1, 2000);
            root.Coop = CompetingRun(false);
            var s = new GoldMinerSession(root);
            var copy = s.GetSnapshot().Coop;
            Assert(s.Commit(s.PrepareNewRun(GoldMinerMode.Solo, new string ('2', 32), 7, Start())));
            var after = s.GetSnapshot();
            Assert(after.Coop.RunId == copy.RunId && after.Coop.Current.Hooks[0].Length == copy.Current.Hooks[0].Length && after.Solo.RunSeed == 7);
        });
        Check("failed settlement never publishes best income", () =>
        {
            var root = TerminalRoot(1, 0);
            var s = new GoldMinerSession(root);
            Assert(s.Commit(s.PrepareSettlement()));
            Assert(s.GetSnapshot().Solo.Current.Phase == GoldMinerPhase.Failed && s.GetSnapshot().SoloRecord.BestRunGrossIncome == 0);
            Assert(s.Commit(s.PrepareRetryLevel()));
            Assert(s.GetSnapshot().Solo.Current.Cash == 0);
        });
        Check("sequence exhaustion rejects transactions and retry without wraparound", () =>
        {
            var root = new GoldMinerRootSnapshot
            {
                TxnSeq = long.MaxValue
            };
            var s = new GoldMinerSession(root);
            Assert(s.PrepareNewRun(GoldMinerMode.Solo, Id, 1, Start()) == null);
            var r = Run();
            r.LastCommandSeq = long.MaxValue;
            r.AttemptIndex = int.MaxValue;
            s = new GoldMinerSession(new GoldMinerRootSnapshot { Solo = r });
            Assert(s.PrepareRetryLevel() == null);
            var g = new GoldMinerGame(r);
            g.StartLevel();
            Assert(!g.TryLaunch(0, long.MaxValue));
        });
        Check("candidate data cannot be mutated through its snapshot getter", () =>
        {
            var s = new GoldMinerSession();
            var candidate = s.PrepareNewRun(GoldMinerMode.Solo, Id, 1, Start());
            candidate.Snapshot.Solo.Current.Cash = 999;
            Assert(s.Commit(candidate));
            Assert(s.GetSnapshot().Solo.Current.Cash == 0);
        });
        Check("corrupt layout identity and impossible draining state are rejected", () =>
        {
            string why;
            var r = Run();
            r.Current.PatternId = "invented";
            r.Checkpoint.PatternId = "invented";
            Assert(!r.TryValidate(out why));
            r = Run();
            r.Current.LayoutId = "coop-01-balanced-0";
            r.Checkpoint.LayoutId = r.Current.LayoutId;
            Assert(!r.TryValidate(out why));
            r = Run();
            r.Current.Phase = GoldMinerPhase.Draining;
            Assert(!r.TryValidate(out why));
        });
        Check("shop entry baseline cannot rewrite a prior checkpoint", () =>
        {
            var s = new GoldMinerSession(TerminalRoot(1, 2000));
            s.EnterMode(GoldMinerMode.Solo);
            s.Commit(s.PrepareSettlement());
            s.Commit(s.PrepareEnterShop());
            var r = s.GetSnapshot().Solo;
            r.Current.Shop.EntryShopSpent = 1;
            r.Current.ShopSpent = 1;
            r.Current.Shop.EntryCash = 999;
            r.Current.Cash = 999;
            string why;
            Assert(!r.TryValidate(out why));
        });
        Check("restoration rejects balances that cannot accept a legal collection", () =>
        {
            var r = Run(1, long.MaxValue);
            string why;
            Assert(!r.TryValidate(out why));
        });
        Check("checkpoint validation reports an unknown mode instead of throwing", () =>
        {
            string why;
            Assert(!Start().TryValidate((GoldMinerMode)99, out why));
        });
        Check("destroyed entities and consumed dynamite must balance", () =>
        {
            var r = Run();
            r.Current.Phase = GoldMinerPhase.Running;
            r.Current.Players[0].Bombs = 0;
            string why;
            Assert(!r.TryValidate(out why));
        });
        Check("historical best requires a feasible cleared-level income", () =>
        {
            var root = new GoldMinerRootSnapshot();
            root.SoloRecord.BestClearedLevel = 5;
            root.SoloRecord.BestRunGrossIncome = 1;
            string why;
            Assert(!root.TryValidate(out why));
        });
        Check("reflection segment can capture only after animal reverses", () =>
        {
            var e = new GoldMinerEntity
            {
                Id = 1,
                Kind = GoldMinerKind.Mole,
                Status = GoldMinerEntityStatus.Available,
                X = .20001,
                Y = 2,
                MoveDirection = -1
            };
            var path = GoldMinerCollision.Trajectory(e, GoldMinerMode.Solo, 10);
            Assert(path.Count == 2);
            double t;
            // Fixed hook at x=.482, y=2: the animal starts .282 away and enters its .28 reach only after reversing.
            double angle = Math.Atan2(.482 - 3, 2), length = Math.Sqrt((.482 - 3) * (.482 - 3) + 4);
            Assert(GoldMinerCollision.FirstContact(3, angle, length, 0, 1, path, .28, out t));
            Assert(t > path[0].T1 && t < 1);
        });
        Check("buffs change only the designated values and carried return speed", () =>
        {
            var buffs = new GoldMinerBuffs
            {
                Strength = true,
                RockBook = true,
                DiamondOil = true
            };
            Assert(GoldMinerRules.Value(new GoldMinerEntity { Kind = GoldMinerKind.RockSmall }, buffs) == 60);
            Assert(GoldMinerRules.Value(new GoldMinerEntity { Kind = GoldMinerKind.RockLarge }, buffs) == 150);
            Assert(GoldMinerRules.Value(new GoldMinerEntity { Kind = GoldMinerKind.Diamond }, buffs) == 900);
            Assert(GoldMinerRules.Value(new GoldMinerEntity { Kind = GoldMinerKind.Mole }, buffs) == 30);
            var r = Run();
            r.Current.Phase = GoldMinerPhase.Running;
            r.Current.Hooks[0].AngleRadians = 0;
            var normal = new GoldMinerGame(r);
            r.Current.ActiveBuffs.Strength = true;
            r.Checkpoint.ActiveBuffs.Strength = true;
            var fast = new GoldMinerGame(r);
            normal.TryLaunch(0, 1);
            fast.TryLaunch(0, 1);
            int normalReturn = 0, fastReturn = 0;
            for (int i = 1; i < 100; i++)
            {
                normal.Step();
                fast.Step();
                if (normalReturn == 0 && normal.GetSnapshot().Current.Cash > 0)
                    normalReturn = i;
                if (fastReturn == 0 && fast.GetSnapshot().Current.Cash > 0)
                    fastReturn = i;
            }

            Assert(fastReturn > 0 && fastReturn < normalReturn);
        });
        Check("xorshift and bag streams are fixed and do not move during simulation", () =>
        {
            uint rng = 1;
            Assert(GoldMinerRules.NextUInt(ref rng) == 270369);
            Assert(GoldMinerRules.NextUInt(ref rng) == 67634689);
            var g = Game();
            g.StartLevel();
            for (int i = 0; i < 100; i++)
                g.Step();
            var c = g.GetSnapshot().Current;
            Assert(c.LayoutRng == 2 && c.BagRng == 3);
            uint a = 9, b = 9;
            for (int i = 0; i < 100; i++)
                Assert(GoldMinerRules.DrawBag(ref a, false) == GoldMinerRules.DrawBag(ref b, false));
        });
        Check("swept collision rejects nonfinite intermediate arithmetic", () =>
        {
            double time;
            Assert(!GoldMinerCollision.TrySweptCircle(double.MaxValue, 0, -double.MaxValue, 0, 0, 0, 1, 1, .2, out time));
            Assert(GoldMinerRules.IsFinite(time));
        });
        Check("animal reflection lands exactly on walls and survives repeated steps", () =>
        {
            var e = new GoldMinerEntity
            {
                Id = 1,
                Kind = GoldMinerKind.Mole,
                X = .2 + GoldMinerRules.AnimalSpeed(10) / 60,
                Y = 2,
                MoveDirection = -1
            };
            for (int i = 0; i < 10000; i++)
            {
                var path = GoldMinerCollision.Trajectory(e, GoldMinerMode.Solo, 10);
                var last = path[path.Count - 1];
                Assert(last.X1 >= .2 && last.X1 <= 5.8);
                e.X = last.X1;
                e.MoveDirection = last.Direction;
            }
        });
        Check("draining freezes idle swing but continues available animals", () =>
        {
            var r = CompetingRun(false);
            r.Current.Phase = GoldMinerPhase.Draining;
            r.Current.RemainingTicks = 0;
            r.Current.SimulationTick = 3600;
            r.Current.Hooks[1] = GoldMinerRules.InitialHooks(GoldMinerMode.Coop)[1];
            r.Current.Entities[1].Kind = GoldMinerKind.Mole;
            r.Current.Entities[1].MoveDirection = 1;
            r.Current.Entities[1].X = 10;
            r.Current.Entities[1].Y = 4;
            r.Checkpoint.InitialEntities[1] = r.Current.Entities[1].Copy();
            var g = new GoldMinerGame(r);
            double angle = r.Current.Hooks[1].AngleRadians, x = r.Current.Entities[1].X;
            g.Step();
            var c = g.GetSnapshot().Current;
            Near(c.Hooks[1].AngleRadians, angle);
            Assert(c.Entities[1].X > x);
        });
        Check("root restore rejects impossible draining tick counts before game construction", () =>
        {
            foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
            {
                var run = DeadlineRun(mode);
                var game = new GoldMinerGame(run);
                Assert(game.TryLaunch(0, 1));
                game.Step();
                var root = RootFor(mode, game.GetSnapshot());
                string reason;
                Assert(root.TryValidate(out reason), reason);
                var state = root.GetRun(mode).Current;
                Assert(state.Phase == GoldMinerPhase.Draining && state.Hooks[0].State == GoldMinerHookState.Extending);
                state.SimulationTick = int.MaxValue;
                Assert(!root.TryValidate(out reason), "Draining int.MaxValue was accepted.");
                state.SimulationTick = int.MaxValue - 100000;
                Assert(!root.TryValidate(out reason), "A huge finite draining tick count was accepted.");
                state.SimulationTick = DrainTickLimit(mode) + 1;
                Assert(!root.TryValidate(out reason), "The geometry-derived draining limit was exceeded.");
            }
        });
        Check("in-flight restore reserves enough ticks to finish its remaining action", () =>
        {
            foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
            {
                var game = new GoldMinerGame(DeadlineRun(mode));
                game.TryLaunch(0, 1);
                while (game.GetStateSnapshot().Hooks[0].State != GoldMinerHookState.RetractingLoaded)
                    game.Step();
                var root = RootFor(mode, game.GetSnapshot());
                root.GetRun(mode).Current.SimulationTick = DrainTickLimit(mode) - 1;
                string reason;
                Assert(!root.TryValidate(out reason), "Restored long hook has no room to drain before the tick limit.");
            }
        });
        Check("legal last-tick heavy loads drain within the geometry limit in both modes", () =>
        {
            foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
            {
                var running = RootFor(mode, DeadlineRun(mode));
                string reason;
                Assert(running.TryValidate(out reason), reason);
                var game = new GoldMinerGame(running.GetRun(mode));
                Assert(game.TryLaunch(0, 1));
                int steps = 0;
                do
                {
                    game.Step();
                    var root = RootFor(mode, game.GetSnapshot());
                    Assert(root.TryValidate(out reason), reason);
                    Assert(root.GetRun(mode).Current.SimulationTick <= DrainTickLimit(mode));
                    steps++;
                    Assert(steps <= DrainTickLimit(mode) - GoldMinerRules.LevelTicks + 1);
                }
                while (!game.SettlementReady);
                Assert(game.GetStateSnapshot().Cash == 50, "Expected the heavy rock income, got " + game.GetStateSnapshot().Cash);
                var ended = RootFor(mode, game.GetSnapshot());
                ended.GetRun(mode).Current.SimulationTick = DrainTickLimit(mode);
                Assert(ended.TryValidate(out reason), "The inclusive idle draining boundary must remain valid: " + reason);
                ended.GetRun(mode).Current.Hooks[0].State = GoldMinerHookState.RetractingEmpty;
                Assert(!ended.TryValidate(out reason), "An unsettled hook at idle length still requires one simulation tick.");
                ended.GetRun(mode).Current.Hooks[0].State = GoldMinerHookState.Swinging;
                ended.GetRun(mode).Current.SimulationTick++;
                Assert(!ended.TryValidate(out reason));
            }
        });
        RunAcceptanceCases();
        Console.WriteLine("GoldMiner: " + passed + " passed, " + failed + " failed");
        if (failed != 0)
            Environment.Exit(1);
    }

    const string Id = "11111111111111111111111111111111";
    static GoldMinerLevelStart Start(int level = 1, long cash = 0)
    {
        return new GoldMinerLevelStart
        {
            LevelIndex = level,
            CatalogVersion = 1,
            LayoutProfileId = "solo-v1",
            LayoutId = "solo-" + level.ToString("00") + "-balanced-0",
            LayoutContentHash = new string ('a', 64),
            PatternId = "balanced",
            PreviousPatternId = level == 1 ? null : "left",
            LevelLayoutSeed = 1,
            LayoutRng = 2,
            BagRng = 3,
            OpeningCash = cash,
            OpeningShopSpent = 0,
            OpeningPlayers = new[]
            {
                new GoldMinerOpeningPlayer
                {
                    PlayerId = 0,
                    Bombs = 1,
                    TotalCollected = cash
                }
            },
            ActiveBuffs = new GoldMinerBuffs(),
            InitialHooks = GoldMinerRules.InitialHooks(GoldMinerMode.Solo),
            InitialEntities = new[]
            {
                new GoldMinerEntity
                {
                    Id = 1,
                    Kind = GoldMinerKind.GoldSmall,
                    Status = GoldMinerEntityStatus.Available,
                    X = 3,
                    Y = 2
                },
                new GoldMinerEntity
                {
                    Id = 2,
                    Kind = GoldMinerKind.RockSmall,
                    Status = GoldMinerEntityStatus.Available,
                    X = 5,
                    Y = 6
                }
            }
        };
    }

    static GoldMinerRunSnapshot Run(int level = 1, long cash = 0)
    {
        var start = Start(level, cash);
        return new GoldMinerRunSnapshot
        {
            RunId = Id,
            Mode = GoldMinerMode.Solo,
            RunSeed = 1,
            Checkpoint = start,
            Current = start.CreateState(GoldMinerMode.Solo)
        };
    }

    static GoldMinerRunSnapshot CompetingRun(bool p2Earlier)
    {
        var start = Start();
        start.LayoutProfileId = "coop-v1";
        start.LayoutId = "coop-01-balanced-0";
        start.OpeningPlayers = new[]
        {
            new GoldMinerOpeningPlayer
            {
                PlayerId = 0,
                Bombs = 1
            },
            new GoldMinerOpeningPlayer
            {
                PlayerId = 1,
                Bombs = 1
            }
        };
        start.InitialHooks = GoldMinerRules.InitialHooks(GoldMinerMode.Coop);
        double diagonal = Math.Sqrt(18);
        start.InitialEntities = new[]
        {
            new GoldMinerEntity
            {
                Id = 1,
                Kind = GoldMinerKind.GoldSmall,
                X = 6,
                Y = 3
            },
            new GoldMinerEntity
            {
                Id = 2,
                Kind = GoldMinerKind.Diamond,
                X = 9 - Math.Sin(Math.PI / 4) * (diagonal + .02),
                Y = Math.Cos(Math.PI / 4) * (diagonal + .02)
            }
        };
        var r = new GoldMinerRunSnapshot
        {
            RunId = Id,
            Mode = GoldMinerMode.Coop,
            RunSeed = 1,
            Checkpoint = start,
            Current = start.CreateState(GoldMinerMode.Coop)
        };
        r.Current.Phase = GoldMinerPhase.Running;
        for (int i = 0; i < 2; i++)
        {
            r.Current.Hooks[i].State = GoldMinerHookState.Extending;
            r.Current.Hooks[i].AngleRadians = i == 0 ? Math.PI / 4 : -Math.PI / 4;
            r.Current.Hooks[i].Length = diagonal - .27 - .06 + (p2Earlier && i == 1 ? .02 : 0);
        }

        return r;
    }

    static int DrainTickLimit(GoldMinerMode mode)
    {
        double width = GoldMinerRules.Width(mode), height = GoldMinerRules.Height(mode);
        double diagonal = Math.Sqrt(width * width + height * height);
        return GoldMinerRules.LevelTicks
            + (int)Math.Ceiling(diagonal / GoldMinerRules.ExtendSpeed * GoldMinerRules.TicksPerSecond)
            + (int)Math.Ceiling(diagonal / (GoldMinerRules.LoadedRetractSpeed / 5) * GoldMinerRules.TicksPerSecond) + 2;
    }

    static GoldMinerRootSnapshot RootFor(GoldMinerMode mode, GoldMinerRunSnapshot run)
    {
        return new GoldMinerRootSnapshot { LastMode = mode, Solo = mode == GoldMinerMode.Solo ? run : null, Coop = mode == GoldMinerMode.Coop ? run : null };
    }

    static GoldMinerRunSnapshot DeadlineRun(GoldMinerMode mode)
    {
        var run = mode == GoldMinerMode.Solo ? Run() : CompetingRun(false);
        run.Current.Phase = GoldMinerPhase.Running;
        run.Current.RemainingTicks = 1;
        run.Current.SimulationTick = GoldMinerRules.LevelTicks - 1;
        run.Current.Hooks = GoldMinerRules.InitialHooks(mode);
        var rock = run.Current.Entities[0];
        rock.Kind = GoldMinerKind.RockLarge;
        rock.X = GoldMinerRules.Width(mode) - GoldMinerRules.Radius(rock.Kind);
        rock.Y = GoldMinerRules.Height(mode) - GoldMinerRules.Radius(rock.Kind);
        run.Checkpoint.InitialEntities[0] = rock.Copy();
        run.Current.Entities[1].X = .5;
        run.Current.Entities[1].Y = 1.8;
        run.Checkpoint.InitialEntities[1] = run.Current.Entities[1].Copy();
        run.Current.Hooks[0].AngleRadians = Math.Atan2(rock.X - GoldMinerRules.OriginX(mode, 0), rock.Y);
        return run;
    }

    static GoldMinerGame Game()
    {
        return new GoldMinerGame(Run());
    }

    static GoldMinerRootSnapshot TerminalRoot(int level, long cash)
    {
        var r = Run(level, cash);
        r.Current.Phase = GoldMinerPhase.Draining;
        r.Current.RemainingTicks = 0;
        r.Current.SimulationTick = 3600;
        return new GoldMinerRootSnapshot
        {
            Solo = r
        };
    }

    static void Check(string name, Action test)
    {
        try
        {
            test();
            passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception e)
        {
            failed++;
            Console.Error.WriteLine("FAIL " + name + ": " + e.Message);
        }
    }

    static void Assert(bool value, string why = "assertion failed")
    {
        if (!value)
            throw new Exception(why);
    }

    static void Near(double a, double b, double epsilon = 1e-8)
    {
        Assert(Math.Abs(a - b) <= epsilon, "expected " + b + ", got " + a);
    }
}
