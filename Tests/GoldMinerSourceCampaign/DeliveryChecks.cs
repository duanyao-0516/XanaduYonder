using System;
using System.IO;
using XanaduYonder.Core.GoldMiner;
using XanaduYonder.Runtime.GoldMiner;

partial class Program
{
    private sealed class DeliveryCase
    {
        internal string Name;
        internal int EntityId, Bombs;
        internal long Cash, Award;
        internal double Strength;
        internal bool AlreadyPaid;
    }

    private static GoldMinerRootSnapshot ReadDeliveryFixture(string name)
    {
        string text = File.ReadAllText(Path.Combine(root, "Tests/GoldMinerSourceCampaign/Fixtures", "legacy-" + name + ".json"));
        GoldMinerRootSnapshot snapshot;
        string error;
        Assert(GoldMinerSaveCodec.TryDecode(text, out snapshot, out error), "old serialized delivery fixture: " + error);
        return snapshot;
    }

    private static int CollectionEvents(GoldMinerStepResult step, int entityId)
    {
        int count = 0;
        foreach (var item in step.Events)
            if (item.Kind == GoldMinerEventKind.Collected && item.EntityId == entityId && item.PlayerId == 0)
                count++;
        return count;
    }

    private static void AssertDelivered(GoldMinerRunSnapshot run, DeliveryCase expected)
    {
        var state = run.Current;
        var hook = state.Hooks[0];
        var ore = state.Entities[expected.EntityId - 1];
        Assert(state.Cash == expected.Cash, expected.Name + " cash is credited once");
        Assert(state.Players[0].TotalCollected == expected.Cash && state.Players[0].LevelCollected == expected.Cash,
            expected.Name + " income ledger matches delivered cash");
        Assert(state.Source.Strength == expected.Strength && state.Players[0].Bombs == expected.Bombs,
            expected.Name + " noncash reward is credited once");
        Assert(ore.Status == GoldMinerEntityStatus.Collected && ore.Source.Award == expected.Award && ore.OwnerPlayerId == null,
            expected.Name + " object and ledger commit together");
        Assert(hook.State == GoldMinerHookState.Swinging && hook.HeldEntityId == null && hook.Source.PendingEntityId == null &&
            hook.Source.RewardTicks == 0 && hook.Source.ResetTicks == 0, expected.Name + " hook is immediately available");
        string reason;
        Assert(run.TryValidate(out reason), expected.Name + " committed snapshot validates: " + reason);
    }

    private static void CheckImmediateDelivery()
    {
        // Fixed expectations from actual 9074aac runs, not values recomputed by the current reward implementation.
        var cases = new[] {
            new DeliveryCase { Name = "solo-money", EntityId = 2, Cash = 100, Award = 100 },
            new DeliveryCase { Name = "solo-power", EntityId = 12, Cash = 100, Strength = 5 },
            new DeliveryCase { Name = "solo-dynamite", EntityId = 1, Bombs = 1 },
            new DeliveryCase { Name = "coop-reset", EntityId = 2, Cash = 50, Award = 50, AlreadyPaid = true }
        };
        foreach (var expected in cases)
        {
            var beforeReturn = ReadDeliveryFixture(expected.Name + "-before-return");
            var returning = new GoldMinerGame(beforeReturn.GetRun(beforeReturn.LastMode));
            var arrived = returning.Step();
            AssertDelivered(returning.GetSnapshot(), expected);
            Assert(CollectionEvents(arrived, expected.EntityId) == 1, expected.Name + " emits collection at arrival");
            Assert(returning.TryLaunch(0, returning.GetSnapshot().LastCommandSeq + 1), expected.Name + " can relaunch without waiting for animation");

            var legacy = ReadDeliveryFixture(expected.Name);
            string untouched = GoldMinerSaveCodec.Encode(legacy);
            var session = new GoldMinerSession(legacy);
            session.Resume(GoldMinerSuspendReason.NeedsExplicitContinue);
            var resumed = session.Step();
            AssertDelivered(session.GetRunSnapshot(), expected);
            Assert(session.Dirty, expected.Name + " compatibility update enters the normal save path");
            Assert(CollectionEvents(resumed, expected.EntityId) == (expected.AlreadyPaid ? 0 : 1), expected.Name + " legacy reward emitted exactly once");
            Assert(GoldMinerSaveCodec.Encode(legacy) == untouched, expected.Name + " input save remains immutable");

            // Reopen the committed serialized state repeatedly; no in-memory flag may be needed to avoid a second award.
            var committed = RoundTrip(session.GetSnapshot());
            for (int replay = 0; replay < 3; replay++)
            {
                var restored = new GoldMinerGame(committed.GetRun(committed.LastMode));
                Assert(CollectionEvents(restored.Step(), expected.EntityId) == 0, expected.Name + " committed reload does not replay collection");
                AssertDelivered(restored.GetSnapshot(), expected);
                committed = RoundTrip(Root(restored.GetSnapshot()));
            }
            Assert(session.TryLaunch(0, session.GetRunSnapshot().LastCommandSeq + 1), expected.Name + " legacy hook unlocks on first resumed tick");
        }

        var lastTick = ReadDeliveryFixture("solo-money");
        lastTick.Solo.Current.RemainingTicks = 1;
        lastTick.Solo.Current.SimulationTick = GoldMinerSourceRules.DurationTicks(GoldMinerMode.Solo) - 1;
        var deadline = new GoldMinerGame(RoundTrip(lastTick).Solo);
        var finalStep = deadline.Step();
        Assert(finalStep.SettlementReady && deadline.GetStateSnapshot().Cash == 100 && CollectionEvents(finalStep, 2) == 1,
            "already-returned legacy reward commits before the final timer tick");

        var archived = ReadDeliveryFixture("solo-money");
        archived.Solo.Current.Phase = GoldMinerPhase.Draining;
        archived.Solo.Current.RemainingTicks = 0;
        archived.Solo.Current.SimulationTick = GoldMinerSourceRules.DurationTicks(GoldMinerMode.Solo);
        var settled = new GoldMinerGame(RoundTrip(archived).Solo);
        Assert(!settled.Step().Advanced && settled.GetStateSnapshot().Cash == 0,
            "already-settled legacy deadline keeps its historical unpaid reward");
        Console.WriteLine("PASS immediate arrival and actual v5 pending/reset fixtures; cash, power and dynamite commit once across reloads");
    }
}
