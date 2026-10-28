using System;
using System.Collections.Generic;
using XanaduYonder.Core.Dino;

internal static class Program
{
    private static int passed, failed;
    private static int Main()
    {
        Run("Chrome normal speed and pre-acceleration distance", ChromeReferenceTests.Speed);
        Run("Chrome rounded jump, release and speed-drop traces", ChromeReferenceTests.JumpTrace);
        Run("Chrome clear time, spawn offset and score", ChromeReferenceTests.ClearTimeAndScore);
        Run("Chrome horizon sampling and actual spacing", ChromeReferenceTests.Generation);
        Run("each obstacle creates at most one follower", ChromeReferenceTests.OneFollowerPerObstacle);
        Run("Chrome high-speed launch and analytical travel", ChromeReferenceTests.SpeedDependentLaunchAndTravel);
        Run("diagnostic replay never changes production generation", ChromeReferenceTests.DiagnosticReplay);
        Run("whole-frame earliest collision commits all geometry", ChromeReferenceTests.CollisionCommit);
        Run("ground stays attached to cacti through acceleration and collision", ChromeReferenceTests.GroundTracksCacti);
        Run("zero-height airborne landing preserves standing sweep", ChromeReferenceTests.ZeroHeightAirDuckLanding);
        Run("official collision regressions", CollisionRegressionTests.Run);
        Run("atlas geometry contract", AtlasGeometry.Check);
        Run("invalid viewport inputs are rejected", delegate {
            foreach (double width in new[] { double.NaN, double.PositiveInfinity, -1, 0, 100, double.MaxValue })
                Throws<ArgumentOutOfRangeException>(delegate { new DinoRules(width); });
            Throws<ArgumentNullException>(delegate { new DinoGame(null, 1); });
        });
        Run("ready, input ownership and airborne press semantics", delegate {
            var g = New(); for (int i = 0; i < 100; i++) g.Step();
            Assert(!g.IsStarted && g.Tick == 0 && g.Distance == 0, "ready frozen");
            g.SetDuck(true); Assert(!g.Jump() && g.Ducking, "duck owns posture");
            g.SetDuck(false); Assert(g.Jump() && g.Jumping, "jump starts immediately");
            for (int i = 0; i < 100; i++) { if (g.Jumping) Assert(!g.Jump(), "air press never queues"); g.Step(); }
            Assert(!g.Jumping && g.PlayerY == 0 && g.PlayerVelocity == 0, "held press never repeats");
        });
        Run("pause by not stepping retains motion and input", delegate {
            var a = New(); var b = New(); a.Jump(); b.Jump();
            for (int i = 0; i < 8; i++) { a.Step(); b.Step(); }
            a.ReleaseJump(); b.ReleaseJump();
            for (int i = 0; i < 1000; i++) Assert(a.PlayerY >= 0, "read leaves state unchanged");
            for (int i = 0; i < 100; i++) { a.Step(); b.Step(); Same(a, b); }
        });
        Run("crash freezes all public commands and geometry", delegate {
            var g = New(); g.Jump(); for (int i = 0; i < 1000 && !g.IsGameOver; i++) g.Step();
            Assert(g.IsGameOver, "without input the first obstacle hits");
            long tick = g.Tick, score = g.Score; double distance = g.Distance, y = g.PlayerY, x = g.Obstacles[0].X;
            for (int i = 0; i < 100; i++) { Assert(!g.Jump(), "terminal input"); g.ReleaseJump(); g.SetDuck(true); g.Step(); }
            Assert(g.Tick == tick && g.Score == score && g.Distance == distance && g.PlayerY == y && g.Obstacles[0].X == x, "terminal immutable");
        });
        Run("obstacle view stays read-only and stable", delegate {
            var g = New(); var view = g.Obstacles; g.Jump(); for (int i = 0; i < 182; i++) g.Step();
            Assert(ReferenceEquals(view, g.Obstacles) && view.Count == 1, "stable view");
            var list = view as IList<DinoObstacle>; Assert(list == null || list.IsReadOnly, "immutable ownership");
        });
        Run("motion and first generation allocate no frame objects", delegate {
            var warm = New(); warm.Jump(); for (int i = 0; i < 220; i++) warm.Step();
            var g = New(); g.Jump(); long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 220; i++) g.Step();
            Assert(GC.GetAllocatedBytesForCurrentThread() == before, "per-frame allocation");
        });
        Run("terminal tick freezes motion, generation and counters without overflow", ChromeReferenceTests.TerminalTickFreezesStep);
        Run("widest horizon has no per-frame allocations through all speed gates", ChromeReferenceTests.WideHorizonDoesNotAllocate);
        Run("same seed and fixed input ticks replay across render rates", delegate { Same(Render(30), Render(60)); Same(Render(60), Render(120)); });
        Console.WriteLine("Dino tests: " + passed + " passed, " + failed + " failed"); return failed == 0 ? 0 : 1;
    }
    private static DinoGame Render(int hz)
    {
        var g = New(); g.Jump(); double remainder = 0;
        var plans = new Dictionary<long, DinoPlan>();
        for (int i = 0; i < hz * 100; i++) {
            remainder += 1.0 / hz;
            while (remainder + 1e-12 >= DinoRules.StepSeconds) {
                bool duck = false;
                foreach (var obstacle in g.Obstacles) {
                    DinoPlan plan;
                    if (!plans.TryGetValue(obstacle.Id, out plan)) {
                        Assert(DinoTraversal.TryPlan(g.Rules, obstacle, g.Tick, out plan), "render-rate diagnostic action");
                        plans.Add(obstacle.Id, plan);
                    }
                    if (plan.Action == DinoAction.Duck && g.Tick >= plan.RecommendedActionTick && g.Tick < plan.RecoveryTick) duck = true;
                }
                g.SetDuck(duck);
                foreach (var obstacle in g.Obstacles) {
                    var plan = plans[obstacle.Id];
                    if (plan.Action == DinoAction.Jump && g.Tick >= plan.RecommendedActionTick && g.Tick <= plan.LatestActionTick && !g.Jumping) g.Jump();
                }
                g.Step(); remainder -= DinoRules.StepSeconds;
            }
        }
        Assert(!g.IsGameOver && g.Tick == 6000, "complete live 100-second replay at " + hz + " Hz");
        return g;
    }
    private static DinoGame New() { return new DinoGame(new DinoRules(810), 17); }
    private static void Same(DinoGame a, DinoGame b) { Assert(a.Tick == b.Tick && a.Score == b.Score && a.IsGameOver == b.IsGameOver && a.Obstacles.Count == b.Obstacles.Count, "state metadata"); Near(a.PlayerY, b.PlayerY, "y"); Near(a.Distance, b.Distance, "distance"); Near(a.PlayerVelocity, b.PlayerVelocity, "velocity"); for (int i = 0; i < a.Obstacles.Count; i++) { Assert(a.Obstacles[i].Kind == b.Obstacles[i].Kind && a.Obstacles[i].GroupSize == b.Obstacles[i].GroupSize, "seeded kind"); Near(a.Obstacles[i].X, b.Obstacles[i].X, "x"); } }
    private static void Run(string name, Action body) { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); } }
    private static void Assert(bool value, string message) { ChromeReferenceTests.Assert(value, message); }
    private static void Near(double a, double b, string message) { ChromeReferenceTests.Near(a, b, message); }
    private static void Throws<T>(Action body) where T : Exception { try { body(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
}
