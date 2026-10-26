using System;
using XanaduYonder.Core.Jump;
using XanaduYonder.Runtime.Jump;

internal static class Program
{
    private static int checks;
    private static int failed;

    private static int Main()
    {
        Run("snapshot copy is independent", delegate {
            var original = ValidSnapshot();
            var copy = original.Copy();
            copy.Score = 99;
            Assert(original.Score == 0, "copy must not alias mutable state");
        });
        Run("valid live and terminal snapshots", delegate {
            string error;
            Assert(ValidSnapshot().TryValidate(out error), error);
            var terminal = ValidSnapshot();
            terminal.IsGameOver = true;
            terminal.PlayerX = -2;
            Assert(terminal.TryValidate(out error), error);
        });
        Run("snapshot rejects invalid external values", delegate {
            Invalid(delegate(JumpSnapshot s) { s.CurrentHalfSize = double.NaN; });
            Invalid(delegate(JumpSnapshot s) { s.NextX = double.PositiveInfinity; });
            Invalid(delegate(JumpSnapshot s) { s.NextZ = double.NegativeInfinity; });
            Invalid(delegate(JumpSnapshot s) { s.NextHalfSize = .59; });
            Invalid(delegate(JumpSnapshot s) { s.CurrentHalfSize = 1.01; });
            Invalid(delegate(JumpSnapshot s) { s.NextX = 4.01; });
            Invalid(delegate(JumpSnapshot s) { s.NextX = 2.49; });
            Invalid(delegate(JumpSnapshot s) { s.NextZ = 2.8; });
            Invalid(delegate(JumpSnapshot s) { s.NextX = 0; });
            Invalid(delegate(JumpSnapshot s) { s.RandomState = 0; });
            Invalid(delegate(JumpSnapshot s) { s.Score = -1; });
            Invalid(delegate(JumpSnapshot s) { s.Steps = -1; });
            Invalid(delegate(JumpSnapshot s) { s.Streak = -1; });
            Invalid(delegate(JumpSnapshot s) { s.Steps = 1; });
            Invalid(delegate(JumpSnapshot s) { s.Streak = 1; });
            Invalid(delegate(JumpSnapshot s) { s.Score = 100; });
            Invalid(delegate(JumpSnapshot s) { s.Score = 3; s.Steps = 2; s.Streak = 2; });
            Invalid(delegate(JumpSnapshot s) { s.PlayerX = .951; });
            Invalid(delegate(JumpSnapshot s) { s.PlayerZ = double.NaN; });
            Invalid(delegate(JumpSnapshot s) { s.IsGameOver = true; });
            Invalid(delegate(JumpSnapshot s) { s.IsGameOver = true; s.PlayerX = 2.8; });
            Invalid(delegate(JumpSnapshot s) { s.IsGameOver = true; s.PlayerX = 1000000; });
            Invalid(delegate(JumpSnapshot s) { s.IsGameOver = true; s.PlayerX = -2; s.Score = 2; s.Steps = 1; s.Streak = 1; });
        });
        GameChecks();
        GestureChecks();
        Console.WriteLine("Jump checks: " + checks + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void GameChecks()
    {
        Run("first target and normalized seed", delegate {
            var first = new JumpGame(0).GetSnapshot();
            Assert(first.CurrentHalfSize == .95 && first.NextX == 2.8 && first.NextZ == 0 && first.NextHalfSize == .85, "fixed opening");
            Assert(first.RandomState != 0 && first.Score == 0 && first.Steps == 0 && first.Streak == 0 && !first.IsGameOver, "fresh state");
            Assert(first.RandomState == new JumpGame(0).GetSnapshot().RandomState, "zero seed is deterministic");
            Assert(new JumpGame(17).GetSnapshot().RandomState == 17, "nonzero seed is preserved");
        });
        Run("snapshots cannot mutate game ownership", delegate {
            var imported = ValidSnapshot();
            var game = new JumpGame(imported);
            imported.NextX = 99;
            var exported = game.GetSnapshot();
            exported.NextX = 99;
            Assert(game.GetSnapshot().NextX == 2.8, "both import and export copy state");
        });
        Run("constructor rejects null and invalid snapshots", delegate {
            Throws<ArgumentNullException>(delegate { new JumpGame((JumpSnapshot)null); });
            var invalid = ValidSnapshot(); invalid.PlayerX = 100;
            Throws<ArgumentException>(delegate { new JumpGame(invalid); });
        });
        Run("perfect streak awards 2 4 6", delegate {
            var game = new JumpGame(17);
            for (int n = 1; n <= 3; n++)
            {
                var before = game.GetSnapshot();
                var result = game.Jump(CenterCharge(before));
                var after = game.GetSnapshot();
                Assert(result.Outcome == JumpOutcome.Perfect && result.DeltaScore == n * 2, "perfect award sequence");
                Assert(after.Score == n * (n + 1) && after.Streak == n && after.Steps == n, "perfect counters");
                Near(after.PlayerX, 0, "rebase x"); Near(after.PlayerZ, 0, "rebase z");
                Near(after.CurrentHalfSize, before.NextHalfSize, "target becomes current");
                Near(result.TargetX, before.NextX, "pre-rebase target x");
                Near(result.TargetZ, before.NextZ, "pre-rebase target z");
                Near(result.EndX, before.NextX, "pre-rebase endpoint x");
                Near(result.EndZ, before.NextZ, "pre-rebase endpoint z");
            }
        });
        Run("normal landing gives one and resets streak", delegate {
            var game = new JumpGame(17);
            game.Jump(CenterCharge(game.GetSnapshot()));
            var before = game.GetSnapshot();
            var result = game.Jump(Charge(CenterDistance(before) + .3));
            var after = game.GetSnapshot();
            Assert(result.Outcome == JumpOutcome.Landed && result.DeltaScore == 1, "normal award");
            Assert(after.Score == 3 && after.Steps == 2 && after.Streak == 0, "normal counters");
            Near(Math.Sqrt(after.PlayerX * after.PlayerX + after.PlayerZ * after.PlayerZ), .3, "landing offset survives rebase");
        });
        Run("staying moves player and preserves random sequence", delegate {
            var game = new JumpGame(17);
            game.Jump(CenterCharge(game.GetSnapshot()));
            var before = game.GetSnapshot();
            var result = game.Jump(0);
            var after = game.GetSnapshot();
            Assert(result.Outcome == JumpOutcome.Stayed && result.DeltaScore == 0, "short hop stays");
            Assert(after.RandomState == before.RandomState && after.NextX == before.NextX && after.NextZ == before.NextZ, "no new random target");
            Assert(after.Score == before.Score && after.Steps == before.Steps && after.Streak == 0, "stay resets streak only");
            Near(Math.Sqrt(after.PlayerX * after.PlayerX + after.PlayerZ * after.PlayerZ), .35, "short hop updates actual origin");
        });
        Run("all later jumps aim from actual player position", delegate {
            var state = ValidSnapshot(); state.PlayerX = -.6; state.PlayerZ = .8; state.NextX = 0; state.NextZ = 4;
            var game = new JumpGame(state);
            var result = game.Jump(CenterCharge(state));
            Assert(result.Outcome == JumpOutcome.Perfect, "offset player aims at center");
            Near(result.StartX, -.6, "start x"); Near(result.StartZ, .8, "start z");
            Near(result.EndX, 0, "end x"); Near(result.EndZ, 4, "end z");
        });
        Run("platform edges are inclusive and nearby outside is missed", delegate {
            Assert(new JumpGame(17).Jump(Charge(.95)).Outcome == JumpOutcome.Stayed, "current boundary");
            Assert(new JumpGame(17).Jump(Charge(.95001)).Outcome == JumpOutcome.Missed, "outside current boundary");
            Assert(new JumpGame(17).Jump(Charge(2.8 - .85)).Outcome == JumpOutcome.Landed, "near target boundary");
            Assert(new JumpGame(17).Jump(Charge(2.8 + .85)).Outcome == JumpOutcome.Landed, "far target boundary");
            Assert(new JumpGame(17).Jump(Charge(2.8 + .85001)).Outcome == JumpOutcome.Missed, "outside target boundary");
            Assert(new JumpGame(17).Jump(Charge(2.8 - .85001)).Outcome == JumpOutcome.Missed, "gap before target");
        });
        Run("perfect boundary is inclusive", delegate {
            Assert(new JumpGame(17).Jump(Charge(2.8 + .16)).Outcome == JumpOutcome.Perfect, "perfect boundary");
            Assert(new JumpGame(17).Jump(Charge(2.8 + .16001)).Outcome == JumpOutcome.Landed, "outside perfect boundary");
        });
        Run("invalid charge cannot change game state", delegate {
            var game = new JumpGame(17);
            var before = game.GetSnapshot();
            foreach (double duration in new[] { -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                Throws<ArgumentOutOfRangeException>(delegate { game.Jump(duration); });
            Same(before, game.GetSnapshot(), "invalid durations are atomic");
        });
        Run("long hold clamps and missed state remains renderable", delegate {
            var game = new JumpGame(17);
            var result = game.Jump(double.MaxValue);
            var terminal = game.GetSnapshot();
            Near(result.Distance, 5.21, "maximum distance");
            Assert(result.Outcome == JumpOutcome.Missed && terminal.IsGameOver, "overshoot loses");
            Assert(terminal.RandomState == 17 && terminal.Score == 0 && terminal.Steps == 0, "miss does not consume generation");
            string error;
            Assert(terminal.TryValidate(out error), error);
            var restored = new JumpGame(terminal);
            Throws<InvalidOperationException>(delegate { restored.Jump(.5); });
            Same(terminal, restored.GetSnapshot(), "finished game is immutable");
        });
        Run("counter and score saturation never wraps", delegate {
            var state = ValidSnapshot(); state.Steps = int.MaxValue; state.Streak = int.MaxValue; state.Score = long.MaxValue - 3;
            var game = new JumpGame(state);
            var result = game.Jump(CenterCharge(state));
            Assert(result.DeltaScore == 3 && game.GetSnapshot().Score == long.MaxValue, "score saturates with accurate delta");
            Assert(game.GetSnapshot().Steps == int.MaxValue && game.GetSnapshot().Streak == int.MaxValue, "integer counters saturate");
            result = game.Jump(CenterCharge(game.GetSnapshot()));
            Assert(result.DeltaScore == 0 && game.GetSnapshot().Score == long.MaxValue, "saturated score remains stable");
        });
        Run("same seed and restored state continue identically", delegate {
            var first = new JumpGame(123456);
            var second = new JumpGame(123456);
            for (int n = 0; n < 300; n++)
            {
                var before = first.GetSnapshot();
                double held = Charge(CenterDistance(before) + (n % 3 - 1) * .3);
                first.Jump(held); second.Jump(held);
                Same(first.GetSnapshot(), second.GetSnapshot(), "deterministic sequence " + n);
                if (n % 7 == 0) second = new JumpGame(second.GetSnapshot());
            }
            var different = new JumpGame(123457); different.Jump(CenterCharge(different.GetSnapshot()));
            var original = new JumpGame(123456); original.Jump(CenterCharge(original.GetSnapshot()));
            Assert(original.GetSnapshot().RandomState != different.GetSnapshot().RandomState, "seeds diverge");
        });
        Run("twenty thousand random landings stay valid and corner reachable", delegate {
            var game = new JumpGame(731);
            var random = new Random(991);
            long expectedScore = 0;
            int expectedStreak = 0;
            for (int n = 0; n < 20000; n++)
            {
                var before = game.GetSnapshot();
                foreach (int xSign in new[] { -1, 1 }) foreach (int zSign in new[] { -1, 1 })
                {
                    var corner = before.Copy(); corner.PlayerX = xSign * before.CurrentHalfSize; corner.PlayerZ = zSign * before.CurrentHalfSize;
                    double distance = CenterDistance(corner);
                    Assert(distance <= 5.21 && distance >= .35, "target must be reachable from every corner");
                }
                double error = (random.NextDouble() - .5) * .9;
                var result = game.Jump(Charge(CenterDistance(before) + error));
                Assert(result.Outcome == JumpOutcome.Landed || result.Outcome == JumpOutcome.Perfect, "generated target can be landed");
                if (Math.Abs(error) <= .16) { expectedStreak++; expectedScore += expectedStreak * 2L; }
                else { expectedStreak = 0; expectedScore++; }
                var after = game.GetSnapshot();
                string validation;
                Assert(after.TryValidate(out validation), validation);
                Assert(after.Steps == n + 1 && after.Score == expectedScore && after.Streak == expectedStreak, "long-run counters agree");
                Assert(Math.Abs(after.PlayerX) <= 1 && Math.Abs(after.PlayerZ) <= 1, "coordinates remain rebased");
            }
        });
    }

    private static void GestureChecks()
    {
        Run("charge requires one matching release", delegate {
            var gesture = new ChargeGesture(); double seconds;
            Assert(!gesture.Release(1, 1, false, out seconds) && seconds == 0, "release needs owner");
            Assert(gesture.Begin(1, 10) && gesture.IsCharging, "begin owns charge");
            Assert(gesture.Release(1, 10.7, false, out seconds), "owner releases"); Near(seconds, .7, "held duration");
            Assert(!gesture.IsCharging && !gesture.Release(1, 12, false, out seconds), "release consumes charge once");
        });
        Run("second finger cannot replace or release charge", delegate {
            var gesture = new ChargeGesture(); double seconds;
            gesture.Begin(1, 10);
            Assert(!gesture.Begin(2, 20), "new finger cannot overwrite");
            Assert(!gesture.Begin(1, 20), "duplicate begin cannot reset time");
            Assert(!gesture.Release(2, 30, true, out seconds) && gesture.IsCharging, "non-owner cancellation ignored");
            Assert(gesture.Release(1, 11, false, out seconds), "owner retains charge"); Near(seconds, 1, "original press time retained");
        });
        Run("cancellation pause disable never commit", delegate {
            var gesture = new ChargeGesture(); double seconds;
            gesture.Begin(-1, 0);
            Assert(!gesture.Release(-1, 1, true, out seconds) && seconds == 0 && !gesture.IsCharging, "cancelled release consumes without jump");
            gesture.Begin(-1, 1); gesture.Cancel(); gesture.Cancel();
            Assert(!gesture.Release(-1, 2, false, out seconds) && !gesture.IsCharging, "lifecycle cancel clears ownership");
            Assert(gesture.Begin(2, 3), "fresh interaction allowed after cancel");
        });
        Run("bad timestamps clear owner without a jump", delegate {
            var gesture = new ChargeGesture(); double seconds;
            foreach (double time in new[] { -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                Assert(!gesture.Begin(1, time) && !gesture.IsCharging, "invalid press time rejected");
            foreach (double time in new[] { 9.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                gesture.Begin(1, 10);
                Assert(!gesture.Release(1, time, false, out seconds) && seconds == 0 && !gesture.IsCharging, "invalid release cancels safely");
            }
            gesture.Begin(1, 10);
            Assert(gesture.Release(1, 10, false, out seconds) && seconds == 0, "same-time tap is valid zero charge");
        });
    }

    private static double CenterDistance(JumpSnapshot state)
    {
        return Math.Sqrt(Math.Pow(state.NextX - state.PlayerX, 2) + Math.Pow(state.NextZ - state.PlayerZ, 2));
    }

    private static double CenterCharge(JumpSnapshot state) { return Charge(CenterDistance(state)); }
    private static double Charge(double distance) { return (distance - .35) / 3.6; }
    private static void Near(double actual, double expected, string message) { Assert(Math.Abs(actual - expected) < 1e-8, message + ": " + actual + " != " + expected); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
    private static void Same(JumpSnapshot a, JumpSnapshot b, string message)
    {
        Assert(a.CurrentHalfSize == b.CurrentHalfSize && a.NextX == b.NextX && a.NextZ == b.NextZ && a.NextHalfSize == b.NextHalfSize &&
            a.PlayerX == b.PlayerX && a.PlayerZ == b.PlayerZ && a.Score == b.Score && a.Steps == b.Steps && a.Streak == b.Streak &&
            a.IsGameOver == b.IsGameOver && a.RandomState == b.RandomState, message);
    }

    private static JumpSnapshot ValidSnapshot()
    {
        return new JumpSnapshot { CurrentHalfSize = .95, NextX = 2.8, NextZ = 0, NextHalfSize = .85,
            PlayerX = 0, PlayerZ = 0, Score = 0, Steps = 0, Streak = 0, RandomState = 17 };
    }

    private static void Invalid(Action<JumpSnapshot> change)
    {
        var value = ValidSnapshot();
        change(value);
        string error;
        Assert(!value.TryValidate(out error), "invalid snapshot must be rejected");
        Assert(!string.IsNullOrEmpty(error), "invalid snapshot must explain rejection");
    }

    private static void Run(string name, Action test)
    {
        try { test(); checks++; }
        catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error.Message); }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
