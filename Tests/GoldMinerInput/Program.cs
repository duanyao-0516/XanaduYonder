using System;
using XanaduYonder.Core.GoldMiner;

internal static class Program
{
    private static int checks;

    private static int Main()
    {
        try
        {
            DownLaunchesImmediately();
            OwnershipAndRelease();
            UiGateAndBusyConsumeTheDown();
            GenerationAndPlayerValidation();
            QuarantineSurvivesResumeUntilRelease();
            CancelClearsEveryOwner();
            UiCommandsHaveOneDownAndRespectQuarantine();
            Console.WriteLine("Gold Miner input checks: " + checks + " passed, 0 failed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void DownLaunchesImmediately()
    {
        var state = new GoldMinerInputState(false, 1);
        var result = state.Process(new GoldMinerPointerEvent(GoldMinerPointerPhase.Down, 10, 0, false, true, 1));
        Assert(result.ShouldLaunch && result.PlayerId == 0, "a valid Down immediately emits exactly one launch");
    }

    private static void OwnershipAndRelease()
    {
        var state = new GoldMinerInputState(true, 7);
        Assert(Send(state, GoldMinerPointerPhase.Down, 1, 0).ShouldLaunch, "P1 first finger launches");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 1, 0).ShouldLaunch, "duplicate Down is consumed");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 1, 1).ShouldLaunch, "crossing center cannot transfer ownership");
        Assert(Send(state, GoldMinerPointerPhase.Down, 2, 1).ShouldLaunch, "P2 finger is independent");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 3, 0).ShouldLaunch, "P1 second finger is consumed");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 4, 1).ShouldLaunch, "P2 second finger is consumed");
        Assert(!Send(state, GoldMinerPointerPhase.Up, 1, 1).ShouldLaunch, "Up across center never launches");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 3, 0).ShouldLaunch, "ignored held finger cannot take over when owner releases");
        Assert(Send(state, GoldMinerPointerPhase.Down, 5, 0).ShouldLaunch, "a fresh P1 finger can own the freed player");
        Assert(!Send(state, GoldMinerPointerPhase.Up, 5, 0).ShouldLaunch, "Up releases without launch");
        Assert(!Send(state, GoldMinerPointerPhase.Cancel, 2, 1).ShouldLaunch, "native cancel never launches");
        Assert(!Send(state, GoldMinerPointerPhase.Up, 2, 1).ShouldLaunch, "Up after cancel never launches");
        Assert(Send(state, GoldMinerPointerPhase.Down, 6, 1).ShouldLaunch, "cancel frees the player");
    }

    private static void UiGateAndBusyConsumeTheDown()
    {
        var state = new GoldMinerInputState(true, 7);
        Assert(!Send(state, GoldMinerPointerPhase.Down, 10, 0, true).ShouldLaunch, "UI Down cannot launch");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 10, 0).ShouldLaunch, "a UI finger cannot fall through after UI closes");
        Assert(Send(state, GoldMinerPointerPhase.Down, 11, 0).ShouldLaunch, "UI finger does not steal a player's mine ownership");
        Send(state, GoldMinerPointerPhase.Up, 11, 0);
        Assert(!Send(state, GoldMinerPointerPhase.Down, 12, 0, false, false).ShouldLaunch, "closed gate consumes Down without launch");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 12, 0).ShouldLaunch, "opening gate never replays the consumed Down");
        Send(state, GoldMinerPointerPhase.Up, 12, 0);
        Assert(Send(state, GoldMinerPointerPhase.Down, 12, 0).ShouldLaunch, "after release a new press can launch");
        // A model that rejects this intent as busy receives no later queued action from this state.
        Assert(!Send(state, GoldMinerPointerPhase.Down, 12, 0).ShouldLaunch, "model busy rejection cannot queue a held press");
        Assert(!Send(state, GoldMinerPointerPhase.Up, 12, 0).ShouldLaunch, "model busy rejection cannot launch on Up");
    }

    private static void GenerationAndPlayerValidation()
    {
        var state = new GoldMinerInputState(true, 7);
        Assert(!Send(state, GoldMinerPointerPhase.Down, 1, 0, false, true, 6).ShouldLaunch, "old generation Down is rejected");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 1, 0, false, true, 8).ShouldLaunch, "future generation Down is rejected");
        Assert(Send(state, GoldMinerPointerPhase.Down, 1, 0).ShouldLaunch, "stale event does not consume current pointer");
        Send(state, GoldMinerPointerPhase.Up, 1, 0, false, true, 6);
        Assert(!Send(state, GoldMinerPointerPhase.Down, 2, 0).ShouldLaunch, "stale Up cannot release current owner");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 3, -1).ShouldLaunch, "negative player rejected");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 4, 2).ShouldLaunch, "unknown player rejected");
        var solo = new GoldMinerInputState(false, 7);
        Assert(!Send(solo, GoldMinerPointerPhase.Down, 5, 1).ShouldLaunch, "solo has no P2");
        Assert(Send(solo, GoldMinerPointerPhase.Down, 6, 0).ShouldLaunch, "solo P1 valid");
    }

    private static GoldMinerInputIntent Send(GoldMinerInputState state, GoldMinerPointerPhase phase,
        int pointerId, int playerId, bool uiConsumed = false, bool canAct = true, long generation = 7)
    {
        return state.Process(new GoldMinerPointerEvent(phase, pointerId, playerId, uiConsumed, canAct, generation));
    }

    private static void QuarantineSurvivesResumeUntilRelease()
    {
        var state = new GoldMinerInputState(true, 7);
        Send(state, GoldMinerPointerPhase.Down, 1, 0);
        state.QuarantineActivePointers(new[] { 1, 2, -1 });
        Assert(!Send(state, GoldMinerPointerPhase.Down, 1, 0).ShouldLaunch, "held mine finger is isolated on resume");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 2, 1).ShouldLaunch, "finger pressed while paused is isolated");
        Assert(!Send(state, GoldMinerPointerPhase.Down, -1, 0).ShouldLaunch, "held Continue mouse is isolated");
        Assert(Send(state, GoldMinerPointerPhase.Down, 3, 0).ShouldLaunch, "quarantined fingers do not retain ownership");
        Send(state, GoldMinerPointerPhase.Up, 3, 0);
        Send(state, GoldMinerPointerPhase.Up, 1, 0);
        Assert(Send(state, GoldMinerPointerPhase.Down, 1, 0).ShouldLaunch, "held finger is usable only after Up then new Down");
        state.ResetGeneration(8, new[] { 1, 2 });
        Assert(state.Generation == 8, "reset installs the newer generation");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 4, 0).ShouldLaunch, "old generation stays rejected after reset");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 1, 0, false, true, 8).ShouldLaunch, "reset isolates held fingers");
        Assert(Send(state, GoldMinerPointerPhase.Down, 5, 1, false, true, 8).ShouldLaunch, "new generation has no inherited owners");
        Send(state, GoldMinerPointerPhase.Up, 1, 0, false, true, 7);
        Assert(!Send(state, GoldMinerPointerPhase.Down, 1, 0, false, true, 8).ShouldLaunch, "stale Up cannot lift new-generation quarantine");
        Send(state, GoldMinerPointerPhase.Up, 1, 0, false, true, 8);
        Assert(Send(state, GoldMinerPointerPhase.Down, 1, 0, false, true, 8).ShouldLaunch, "current Up clears quarantine");
        bool rejected = false;
        try { state.ResetGeneration(8, new int[0]); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Assert(rejected && state.Generation == 8, "generation cannot be reused or rolled back");
    }

    private static void CancelClearsEveryOwner()
    {
        var state = new GoldMinerInputState(true, 7);
        Send(state, GoldMinerPointerPhase.Down, 1, 0);
        Send(state, GoldMinerPointerPhase.Down, 2, 1);
        Send(state, GoldMinerPointerPhase.Cancel, 1, 0);
        Assert(Send(state, GoldMinerPointerPhase.Down, 3, 1).ShouldLaunch, "native cancel clears other player's ownership too");
        Send(state, GoldMinerPointerPhase.Up, 3, 1);
        Assert(!Send(state, GoldMinerPointerPhase.Down, 2, 1).ShouldLaunch, "other held finger remains isolated after cancel");
        Send(state, GoldMinerPointerPhase.Up, 2, 1);
        Assert(Send(state, GoldMinerPointerPhase.Down, 2, 1).ShouldLaunch, "other finger can act after a fresh press");
    }

    private static void UiCommandsHaveOneDownAndRespectQuarantine()
    {
        var state = new GoldMinerInputState(true, 7);
        Assert(state.TryConsumeUiDown(1, 7, true), "first UI Down can issue its own command");
        Assert(!state.TryConsumeUiDown(1, 7, true), "same UI Down cannot repeat dynamite");
        Assert(!Send(state, GoldMinerPointerPhase.Down, 1, 0).ShouldLaunch, "dynamite Down never falls through to launch");
        Assert(Send(state, GoldMinerPointerPhase.Down, 2, 0).ShouldLaunch, "UI finger does not occupy P1 launch ownership");
        Assert(!state.TryConsumeUiDown(3, 7, false), "UI commands obey closed gate");
        Assert(!state.TryConsumeUiDown(3, 7, true), "opening gate does not replay UI commands");
        Assert(!state.TryConsumeUiDown(4, 6, true), "stale UI callback cannot issue commands");
        state.QuarantineActivePointers(new[] { 5 });
        Assert(!state.TryConsumeUiDown(5, 7, true), "held Continue finger cannot issue dynamite");
        Send(state, GoldMinerPointerPhase.Up, 5, 0);
        Assert(state.TryConsumeUiDown(5, 7, true), "fresh UI press is accepted after quarantine release");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }
}
