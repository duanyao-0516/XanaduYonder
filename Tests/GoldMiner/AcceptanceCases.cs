using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using XanaduYonder.Core.GoldMiner;

internal static partial class Program
{
    private static readonly JsonSerializerOptions SnapshotHashOptions = new JsonSerializerOptions { IncludeFields = true };

    private static void RunAcceptanceCases()
    {
        Check("restored snapshots match complete state and income-event hashes after every tick", () =>
        {
            foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
                VerifyEveryTickReplay(mode);
        });
        Check("normal bags use only 100 250 500 and match 60 30 10 over one million fixed draws", () => VerifyBagDistribution(false));
        Check("clover bags use only 100 250 500 and match 20 40 40 over one million fixed draws", () => VerifyBagDistribution(true));
        Check("selected bags and RNG never redraw through pause cold restore collection and retry", () =>
        {
            foreach (var mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
                foreach (bool clover in new[] { false, true })
                    VerifyBagLifecycle(mode, clover);
        });
    }

    private static string SnapshotHash(object value)
    {
        // Tests serialize all public DTO fields, including the checkpoint, RNG and ownership.
        // This is a comparison oracle, not a replacement for the production save codec.
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), SnapshotHashOptions);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static GoldMinerRunSnapshot ReplayRun(GoldMinerMode mode)
    {
        var run = mode == GoldMinerMode.Solo ? Run(5) : CompetingRun(false);
        var checkpoint = run.Checkpoint;
        checkpoint.LevelIndex = 5;
        checkpoint.LayoutId = GoldMinerRules.ModeId(mode) + "-05-balanced-0";
        checkpoint.PreviousPatternId = "left";
        checkpoint.InitialHooks = GoldMinerRules.InitialHooks(mode);
        checkpoint.ActiveBuffs = new GoldMinerBuffs { Strength = true, Clover = true, RockBook = true, DiamondOil = true };
        checkpoint.InitialEntities = new[]
        {
            new GoldMinerEntity { Id = 1, Kind = GoldMinerKind.MysteryBag, BagValue = 250, X = 3, Y = 2 },
            new GoldMinerEntity { Id = 2, Kind = GoldMinerKind.RockLarge, X = mode == GoldMinerMode.Solo ? 4.8 : 9, Y = 4.2 },
            new GoldMinerEntity { Id = 3, Kind = GoldMinerKind.DiamondMole, X = .3, Y = 3.8, MoveDirection = -1 }
        };
        run.Current = checkpoint.CreateState(mode);
        run.Current.Phase = GoldMinerPhase.Running;
        foreach (var hook in run.Current.Hooks)
            hook.AngleRadians = 0;
        return run;
    }

    private static void VerifyEveryTickReplay(GoldMinerMode mode)
    {
        var original = new GoldMinerGame(ReplayRun(mode));
        long sequence = 1;
        Assert(original.TryLaunch(0, sequence++));
        if (mode == GoldMinerMode.Coop)
            Assert(original.TryLaunch(1, sequence++));
        for (int i = 0; original.GetStateSnapshot().Hooks[0].State != GoldMinerHookState.RetractingLoaded; i++)
        {
            Assert(i < 100, "The restore fixture never caught its first bag.");
            original.Step();
        }
        var saved = original.GetSnapshot();
        Assert(saved.Current.Cash == 0 && saved.Current.Hooks[0].State == GoldMinerHookState.RetractingLoaded);
        if (mode == GoldMinerMode.Coop)
            Assert(saved.Current.Hooks[1].State == GoldMinerHookState.Extending);
        var restored = new GoldMinerGame(saved);
        Assert(SnapshotHash(original.GetSnapshot()) == SnapshotHash(restored.GetSnapshot()));
        var incomeA = new List<GoldMinerEvent>();
        var incomeB = new List<GoldMinerEvent>();
        long collected = 0;
        int comparedTicks = 0;
        for (int tick = 0; tick < GoldMinerRules.MaximumSimulationTicks(mode); tick++)
        {
            if (tick % 73 == 0)
            {
                for (int player = 0; player < GoldMinerRules.PlayerCount(mode); player++)
                {
                    Assert(original.TryLaunch(player, sequence) == restored.TryLaunch(player, sequence));
                    sequence++;
                }
            }
            if (tick > 0 && tick % 137 == 0)
            {
                int player = (tick / 137) % GoldMinerRules.PlayerCount(mode);
                Assert(original.TryUseDynamite(player, sequence) == restored.TryUseDynamite(player, sequence));
                sequence++;
            }
            var a = original.Step();
            var b = restored.Step();
            Assert(SnapshotHash(a) == SnapshotHash(b), mode + " step/event mismatch at " + tick);
            foreach (var e in a.Events)
                if (e.Kind == GoldMinerEventKind.Collected)
                {
                    incomeA.Add(e);
                    collected += e.Amount;
                }
            foreach (var e in b.Events)
                if (e.Kind == GoldMinerEventKind.Collected)
                    incomeB.Add(e);
            Assert(SnapshotHash(incomeA) == SnapshotHash(incomeB), mode + " income ledger mismatch at " + tick);
            var snapshotA = original.GetSnapshot();
            var snapshotB = restored.GetSnapshot();
            Assert(SnapshotHash(snapshotA) == SnapshotHash(snapshotB), mode + " full snapshot mismatch at " + tick);
            Assert(snapshotA.Current.Cash - saved.Current.Cash == collected, "Income events do not match the cash delta.");
            comparedTicks++;
            if (a.SettlementReady)
                break;
        }
        Assert(original.SettlementReady && restored.SettlementReady);
        Assert(incomeA.Count > 0 && collected >= saved.Current.Entities[0].BagValue.Value);
        // A different retrieval balance may collect the entire small fixture before the
        // deadline. Both exhaustion and timeout legitimately enter the same drain phase.
        Assert(comparedTicks > 100, "Replay did not cover a sustained active level and settlement.");
        Console.WriteLine("INFO " + mode + " replay: " + comparedTicks + " complete per-tick hashes, "
            + incomeA.Count + " income events, final " + SnapshotHash(original.GetSnapshot()));
    }

    private static void VerifyBagDistribution(bool clover)
    {
        const int sampleCount = 1000000;
        uint actual = 0x13579BDF, expected = actual;
        int[] counts = new int[3];
        for (int i = 0; i < sampleCount; i++)
        {
            long value = GoldMinerRules.DrawBag(ref actual, clover);
            GoldMinerRules.NextUInt(ref expected);
            Assert(actual == expected, "A bag must consume exactly one draw.");
            int index = value == 100 ? 0 : value == 250 ? 1 : value == 500 ? 2 : -1;
            Assert(index >= 0, "A bag returned an undocumented value.");
            counts[index]++;
        }
        double[] probabilities = clover ? new[] { .2, .4, .4 } : new[] { .6, .3, .1 };
        for (int i = 0; i < counts.Length; i++)
            Assert(Math.Abs(counts[i] / (double)sampleCount - probabilities[i]) < .004,
                "Fixed-sample frequency differs by at least 0.4 percentage points for bucket " + i);
        Assert(counts[0] + counts[1] + counts[2] == sampleCount);
        Console.WriteLine("INFO " + (clover ? "clover" : "normal") + " bags: 100=" + counts[0]
            + ", 250=" + counts[1] + ", 500=" + counts[2] + ", final RNG=" + actual);
    }

    private static GoldMinerRunSnapshot BagRun(GoldMinerMode mode, bool clover)
    {
        var run = ReplayRun(mode);
        var checkpoint = run.Checkpoint;
        checkpoint.ActiveBuffs = new GoldMinerBuffs { Clover = clover };
        uint rng = 12345;
        double angle = checkpoint.InitialHooks[0].AngleRadians;
        checkpoint.InitialEntities = new[]
        {
            new GoldMinerEntity { Id = 1, Kind = GoldMinerKind.MysteryBag, BagValue = GoldMinerRules.DrawBag(ref rng, clover), X = 3 + Math.Sin(angle) * 2, Y = Math.Cos(angle) * 2 },
            new GoldMinerEntity { Id = 2, Kind = GoldMinerKind.MysteryBag, BagValue = GoldMinerRules.DrawBag(ref rng, clover), X = GoldMinerRules.Width(mode) - .5, Y = GoldMinerRules.Height(mode) - .5 }
        };
        checkpoint.BagRng = rng;
        run.Current = checkpoint.CreateState(mode);
        return run;
    }

    private static void AssertBagsUnchanged(GoldMinerRunSnapshot actual, GoldMinerRunSnapshot selected)
    {
        Assert(actual.Current.BagRng == selected.Checkpoint.BagRng && actual.Checkpoint.BagRng == selected.Checkpoint.BagRng);
        Assert(actual.Current.LayoutRng == selected.Checkpoint.LayoutRng && actual.Checkpoint.LayoutRng == selected.Checkpoint.LayoutRng);
        Assert(actual.Current.ActiveBuffs.Clover == selected.Checkpoint.ActiveBuffs.Clover);
        Assert(actual.Current.Entities.Length == selected.Checkpoint.InitialEntities.Length);
        for (int i = 0; i < actual.Current.Entities.Length; i++)
        {
            Assert(actual.Current.Entities[i].Id == selected.Checkpoint.InitialEntities[i].Id);
            Assert(actual.Current.Entities[i].BagValue == selected.Checkpoint.InitialEntities[i].BagValue);
            Assert(actual.Checkpoint.InitialEntities[i].BagValue == selected.Checkpoint.InitialEntities[i].BagValue);
        }
    }

    private static void VerifyBagLifecycle(GoldMinerMode mode, bool clover)
    {
        var selected = BagRun(mode, clover);
        var session = new GoldMinerSession(RootFor(mode, selected));
        session.Resume(GoldMinerSuspendReason.NeedsExplicitContinue);
        Assert(session.StartLevel());
        Assert(session.TryLaunch(0, 1));
        for (int i = 0; session.GetStateSnapshot().Hooks[0].State != GoldMinerHookState.RetractingLoaded; i++)
        {
            Assert(i < 100);
            session.Step();
            AssertBagsUnchanged(session.GetRunSnapshot(), selected);
        }
        session.Suspend(GoldMinerSuspendReason.AppPaused);
        session.Suspend(GoldMinerSuspendReason.FocusLost);
        string pausedHash = SnapshotHash(session.GetSnapshot());
        for (int i = 0; i < 120; i++)
        {
            Assert(!session.Step().Advanced);
            Assert(SnapshotHash(session.GetSnapshot()) == pausedHash);
            AssertBagsUnchanged(session.GetRunSnapshot(), selected);
        }
        var restored = new GoldMinerSession(session.GetSnapshot());
        Assert(!restored.Step().Advanced);
        Assert(SnapshotHash(restored.GetSnapshot()) == pausedHash);
        restored.Resume(GoldMinerSuspendReason.NeedsExplicitContinue);
        session.Resume(GoldMinerSuspendReason.AppPaused);
        Assert(!session.Step().Advanced);
        session.Resume(GoldMinerSuspendReason.FocusLost);
        Assert(!session.Step().Advanced);
        session.Resume(GoldMinerSuspendReason.NeedsExplicitContinue);
        for (int i = 0; restored.GetStateSnapshot().Entities[0].Status != GoldMinerEntityStatus.Collected; i++)
        {
            Assert(i < 500);
            var warmStep = session.Step();
            var coldStep = restored.Step();
            Assert(SnapshotHash(warmStep) == SnapshotHash(coldStep));
            Assert(SnapshotHash(session.GetSnapshot()) == SnapshotHash(restored.GetSnapshot()));
            AssertBagsUnchanged(session.GetRunSnapshot(), selected);
            AssertBagsUnchanged(restored.GetRunSnapshot(), selected);
        }
        long fixedBagValue = selected.Checkpoint.InitialEntities[0].BagValue.Value;
        Assert(restored.GetStateSnapshot().Cash == fixedBagValue);
        Assert(restored.Commit(restored.PrepareRetryLevel()), restored.LastError);
        AssertBagsUnchanged(restored.GetRunSnapshot(), selected);
        Assert(restored.GetStateSnapshot().Cash == 0 && restored.GetStateSnapshot().RemainingTicks == GoldMinerRules.LevelTicks);
        restored.Resume(GoldMinerSuspendReason.NeedsExplicitContinue);
        Assert(restored.StartLevel());
        int retryPlayer = mode == GoldMinerMode.Coop ? 1 : 0;
        if (mode == GoldMinerMode.Coop)
            for (int i = 0; i < 112; i++)
            {
                restored.Step();
                AssertBagsUnchanged(restored.GetRunSnapshot(), selected);
            }
        Assert(restored.TryLaunch(retryPlayer, 2));
        for (int i = 0; restored.GetStateSnapshot().Entities[0].Status != GoldMinerEntityStatus.Collected; i++)
        {
            Assert(i < 500, "Retry did not collect the same selected bag.");
            restored.Step();
            AssertBagsUnchanged(restored.GetRunSnapshot(), selected);
        }
        Assert(restored.GetStateSnapshot().Cash == fixedBagValue);
        Assert(restored.GetStateSnapshot().Players[retryPlayer].LevelCollected == fixedBagValue);
        if (mode == GoldMinerMode.Coop)
            Assert(restored.GetStateSnapshot().Players[0].LevelCollected == 0);
    }
}
