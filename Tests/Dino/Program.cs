using System;
using System.Collections.Generic;
using System.Diagnostics;
using XanaduYonder.Core.Dino;

internal static class Program
{
    private static int passed;
    private static int failed;
    private static readonly double[] Widths = { 420, 600, 900, 1200 };

    private static int Main()
    {
        Run("invalid viewports and null rules are rejected", delegate {
            foreach (double width in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1, 0, 100, double.MaxValue })
                Throws<ArgumentOutOfRangeException>(delegate { new DinoRules(width); });
            Throws<ArgumentNullException>(delegate { new DinoGame(null, 1); });
        });
        Run("collision parts follow opaque atlas silhouettes rather than transparent corners", AtlasGeometry.Check);
        Run("dimensions preserve sprite scale and horizontal inset", delegate {
            foreach (double width in Widths) {
                var cold = Stopwatch.StartNew();
                var r = new DinoRules(width);
                cold.Stop(); Console.WriteLine("Cold rules width=" + width + ": " + cold.ElapsedMilliseconds + " ms");
                Near(r.PlayerWidth / 44, 1.35, "standing width");
                Near(r.PlayerHeight / 47, 1.35, "standing height");
                Near(r.DuckWidth / 59, 1.35, "duck width");
                Near(r.DuckHeight / 25, 1.35, "duck height");
                Assert(r.PlayerX >= width * .1 && r.PlayerX <= width * .15, "inset");
                Assert(r.MaxSpeed > 0 && r.MaxSpeed <= 360, "bounded speed");
            }
        });
        Run("ready freezes time and first press starts immediately", delegate {
            var g = New();
            for (int i = 0; i < 120; i++) g.Step();
            Assert(g.Tick == 0 && g.Elapsed == 0 && g.Score == 0 && !g.IsStarted, "ready is inert");
            Assert(g.Jump() && g.IsStarted && g.Jumping && g.PlayerVelocity > 0, "press starts jump");
            g.Step(); Assert(g.PlayerY > 0 && g.Distance > 0, "next fixed step moves");
        });
        Run("released jump is shorter and lower than held jump", delegate {
            double shortHeight, longHeight; int shortSteps, longSteps;
            Flight(true, false, out shortHeight, out shortSteps);
            Flight(false, false, out longHeight, out longSteps);
            Assert(shortHeight >= 55 && shortHeight + 20 < longHeight, "meaningful minimum and hold height");
            Assert(shortSteps + 5 < longSteps && longHeight < 180, "hold duration");
        });
        Run("airborne presses and held input never queue another jump", delegate {
            var g = New(); g.Jump();
            for (int i = 0; i < 100; i++) { if (g.Jumping) Assert(!g.Jump(), "airborne ignored"); g.Step(); }
            Assert(!g.Jumping && g.PlayerY == 0 && g.PlayerVelocity == 0, "stays grounded without another press");
        });
        Run("duck blocks jump and release restores standing", delegate {
            var g = New(); g.SetDuck(true);
            Assert(!g.Jump() && g.Ducking && !g.IsStarted, "duck owns posture");
            g.SetDuck(false); Assert(g.Jump(), "release permits jump");
        });
        Run("air duck accelerates descent and persists on landing", delegate {
            var g = New(); g.Jump(); for (int i = 0; i < 10; i++) g.Step();
            double y = g.PlayerY; g.SetDuck(true); g.Step();
            Assert(g.PlayerVelocity < 0 && g.PlayerY < y, "immediate downward priority");
            for (int i = 0; i < 60; i++) g.Step();
            Assert(!g.Jumping && g.Ducking && !g.Jump(), "lands crouched");
            g.SetDuck(false); Assert(!g.Ducking && g.Jump(), "new input releases posture");
        });
        Run("no stepping preserves paused long and short jump intent", delegate {
            foreach (bool released in new[] { false, true }) {
                var a = New(); var b = New(); a.Jump(); b.Jump();
                for (int i = 0; i < 8; i++) { a.Step(); b.Step(); }
                if (released) { a.ReleaseJump(); b.ReleaseJump(); }
                for (int i = 0; i < 1000; i++) { double unused = a.PlayerY + a.Elapsed; Assert(unused >= 0, "read only"); }
                for (int i = 0; i < 100; i++) { a.Step(); b.Step(); Same(a, b); }
            }
        });
        Run("opening remains safe for three seconds", delegate {
            var g = New(); g.Jump();
            for (int i = 0; i < 179; i++) { g.Step(); Assert(g.Obstacles.Count == 0, "no early spawn"); }
            Assert(!g.IsGameOver, "safe opening");
            g.Step(); Assert(g.Obstacles.Count == 1, "first obstacle begins at right edge");
            Assert(g.Obstacles[0].X >= g.Rules.ViewportWidth, "enters from outside");
        });
        Run("collision freezes score time geometry and all commands", delegate {
            var g = New(); g.Jump(); for (int i = 0; i < 1000 && !g.IsGameOver; i++) g.Step();
            Assert(g.IsGameOver, "no-input runner loses");
            long score = g.Score; double elapsed = g.Elapsed, y = g.PlayerY, x = g.Obstacles[0].X, distance = g.Distance;
            for (int i = 0; i < 100; i++) { Assert(!g.Jump(), "terminal press ignored"); g.ReleaseJump(); g.SetDuck(true); g.Step(); }
            Assert(g.Score == score && g.Elapsed == elapsed && g.Distance == distance && g.PlayerY == y && g.Obstacles[0].X == x, "terminal immutable");
        });
        Run("distance score floors and saturates without wraparound", delegate {
            Assert(DinoGame.ScoreForDistance(29.999) == 0 && DinoGame.ScoreForDistance(30) == 1, "score boundary");
            Assert(DinoGame.ScoreForDistance(double.MaxValue) == long.MaxValue, "saturation");
            Assert(DinoGame.ScoreForDistance(3000000) == 100000, "over five digits");
            var g = New(); g.Jump(); for (int i = 0; i < 170; i++) g.Step();
            Assert(g.Score == (long)Math.Floor(g.Distance / 30), "production score uses traveled distance");
        });
        Run("obstacle view is stable and callers cannot mutate ownership", delegate {
            var g = New(); var view = g.Obstacles; g.Jump(); for (int i = 0; i < 181; i++) g.Step();
            Assert(object.ReferenceEquals(view, g.Obstacles) && view.Count == 1, "one read-only wrapper");
            var mutable = view as IList<DinoObstacle>;
            Assert(mutable == null || mutable.IsReadOnly, "not mutable list");
        });
        Run("simulation including a generated obstacle allocates no per-frame objects", delegate {
            var warm = New(); warm.Jump(); for (int i = 0; i < 220; i++) warm.Step();
            var g = New(); g.Jump();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 220; i++) g.Step();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(bytes == 0, "allocated " + bytes + " bytes across stepping and generation");
        });
        Run("supported viewport bounds remain playable and finite", delegate {
            foreach (double width in new[] { 320.0, 4096.0 }) {
                var g = New(width); g.Jump(); bool observed = false;
                for (int i = 0; i < 3600; i++) { observed |= g.Obstacles.Count > 0; Drive(g); g.Step(); }
                Assert(observed && !g.IsGameOver && g.Elapsed == 60 && !double.IsInfinity(g.Distance), "supported viewport " + width);
            }
        });
        Run("same seed and actions replay despite extra reads", delegate {
            var a = New(600, 42); var b = New(600, 42); a.Jump(); b.Jump();
            for (int i = 0; i < 18000; i++) {
                Drive(a); Drive(b);
                for (int read = 0; read < 3; read++) { var obs = b.Obstacles; double ignored = b.Distance + b.PlayerY; Assert(ignored >= 0 && obs.Count < 32, "read"); }
                a.Step(); b.Step(); Same(a, b);
            }
            Assert(!a.IsGameOver && a.Elapsed >= 300, "replay reaches late phase");
        });
        Run("render rates execute identical fixed input ticks", delegate {
            var a = Render(30); var b = Render(60); var c = Render(120);
            Same(a, b); Same(b, c); Assert(a.Tick == 7200 && !a.IsGameOver, "120 seconds at all rates");
        });
        Run("low speed rejects physically impossible wide combinations", delegate {
            var r = new DinoRules(1200); DinoPlan plan;
            Assert(!DinoTraversal.TryPlan(r, DinoObstacleKind.LargeCactus, 3, 0, 80, out plan), "large triple cannot fit flight at 80 units/s");
            Assert(DinoTraversal.TryPlan(r, DinoObstacleKind.LargeCactus, 3, 0, 360, out plan), "same geometry is possible at sufficient crossing speed");
            Assert(!DinoTraversal.TryPlan(r, DinoObstacleKind.SmallCactus, 4, 0, 180, out plan), "four cacti disallowed");
        });
        Run("swept collision detects crossing between endpoint samples", delegate {
            Assert(DinoCollision.SweptRect(0, 0, 10, 10, 0, 25, 0, 2, 10, -40), "thin obstacle crossed in one interval");
            Assert(!DinoCollision.SweptRect(0, 20, 10, 10, 0, 25, 0, 2, 10, -40), "vertical separation");
        });
        Run("observed full visibility agrees with certificates and a one-tick late press loses", delegate {
            foreach (double width in Widths) {
                var g = New(width); g.Jump(); long visible = -1, latest = -1;
                for (int i = 0; i < 1200 && !g.IsGameOver; i++) {
                    if (g.Obstacles.Count > 0) {
                        var o = g.Obstacles[0]; latest = o.LatestActionTick;
                        if (visible < 0 && o.X + o.Width <= width) { visible = g.Tick; Assert(visible == o.FullVisibleTick, "actual full visibility tick"); }
                        if (g.Tick == latest + 1) Assert(g.Jump(), "late press still physically allowed");
                    }
                    g.Step();
                }
                Assert(visible >= 0 && g.IsGameOver, "one tick beyond certified latest must collide");
                Assert((latest - visible) / 60.0 - .1 >= 1.2, "independent observed response budget");
            }
        });
        Run("first small cactus admits a real released short jump", delegate {
            foreach (double width in Widths) {
                bool passedObstacle = false;
                // Search player input timing, not the planner's held-jump answer.
                for (int lead = 0; lead < 35 && !passedObstacle; lead++) {
                    var g = New(width); g.Jump(); long action = -1;
                    for (int i = 0; i < 900 && !g.IsGameOver; i++) {
                        if (g.Obstacles.Count > 0) {
                            var o = g.Obstacles[0];
                            if (action < 0) action = o.LatestActionTick - lead;
                            if (g.Tick == action) { Assert(g.Jump(), "short press"); g.ReleaseJump(); }
                            if (o.X + o.Width < g.Rules.PlayerX) { passedObstacle = true; break; }
                        }
                        g.Step();
                    }
                }
                Assert(passedObstacle, "short jump must clear small cactus at width " + width);
            }
        });
        Run("standing body is swept before an air-duck landing changes silhouette", delegate {
            var r = new DinoRules(600);
            var obstacle = new DinoObstacle(0, DinoObstacleKind.MidBird, 1, r.PlayerX + 20, default(DinoPlan));
            Assert(DinoCollision.Hits(r, 2, 0, false, obstacle, obstacle.X), "upright descent hits mid bird");
            Assert(!DinoCollision.Hits(r, 2, 0, true, obstacle, obstacle.X), "duck-only sweep would miss that collision");
            Assert(DinoCollision.HitsMotion(r, 2, 0, true, obstacle, obstacle.X), "landing must sweep standing before crouching");
        });
        Run("large triple uses its shorter middle plant silhouette", delegate {
            var r = new DinoRules(600);
            var obstacle = new DinoObstacle(0, DinoObstacleKind.LargeCactus, 3, r.PlayerX - 26 * DinoRules.SpriteScale, default(DinoPlan));
            Assert(!DinoCollision.Hits(r, 46 * DinoRules.SpriteScale, 46 * DinoRules.SpriteScale, false, obstacle, obstacle.X),
                "actual atlas has no opaque overlap above the short middle plant");
        });
        foreach (double w in Widths) {
            double width = w;
            Run("fair action windows and real sequence replay at width " + width, delegate { VerifyRuns(width); });
        }
        Run("rule initialization and generation have a bounded work budget", delegate {
            var clock = Stopwatch.StartNew();
            foreach (double width in Widths) { var g = New(width, 5); g.Jump(); for (int i = 0; i < 10800; i++) { Drive(g); g.Step(); } }
            clock.Stop(); Console.WriteLine("Four rule initializations + 720 simulated seconds: " + clock.ElapsedMilliseconds + " ms");
            Assert(clock.ElapsedMilliseconds < 15000, "bounded planner work");
        });
        Console.WriteLine("Dino checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void VerifyRuns(double width)
    {
        int groups = 0, low = 0, mid = 0, count = 0; double minWindow = double.MaxValue;
        var rules = new DinoRules(width);
        foreach (uint seed in new uint[] { 0, 1, 17, 42, 1729, 123456 }) {
            var g = new DinoGame(rules, seed); g.Jump(); long seen = 0, previousRecovery = 0;
            for (int i = 0; i < 14400; i++) {
                foreach (var o in g.Obstacles) if (o.Id > seen) {
                    seen = o.Id; count++;
                    Assert(o.GroupSize >= 1 && o.GroupSize <= 3, "at most three");
                    Assert(o.SpawnTick >= 180, "three second opening");
                    if (o.GroupSize > 1) { groups++; Assert(o.SpawnTick >= 1200, "combos after 20 seconds"); }
                    if (o.Kind == DinoObstacleKind.LowBird) { low++; Assert(o.SpawnTick >= 2700, "low bird after 45 seconds"); }
                    if (o.Kind == DinoObstacleKind.MidBird) { mid++; Assert(o.SpawnTick >= 2700, "mid bird after 45 seconds"); }
                    Assert(o.ReactionWindowSeconds + 1e-8 >= 1.2, "actual action window after input budget");
                    Assert(o.EarliestActionTick <= o.RecommendedActionTick && o.RecommendedActionTick <= o.LatestActionTick, "certified interval");
                    Assert(o.EarliestActionTick - previousRecovery >= 15, "all valid actions have recovery gap");
                    previousRecovery = o.RecoveryTick; minWindow = Math.Min(minWindow, o.ReactionWindowSeconds);
                }
                Drive(g, (int)(seed % 3)); g.Step();
                Assert(!g.IsGameOver, "certified actions must physically clear actual production collisions, seed=" + seed + ", tick=" + g.Tick);
                Assert(g.Speed <= rules.MaxSpeed + 1e-8 && g.Speed <= 360 && !double.IsNaN(g.PlayerY), "finite capped state");
            }
            Near(g.Speed, rules.MaxSpeed, "speed reaches cap by 90 seconds");
        }
        Assert(count > 100 && groups > 0 && low > 0 && mid > 0, "late content exercised: " + count + "/" + groups + "/" + low + "/" + mid);
        Console.WriteLine("width=" + width + " cap=" + rules.MaxSpeed.ToString("F3") + " obstacles=" + count + " groups=" + groups + " low=" + low + " mid=" + mid + " minEffectiveWindow=" + minWindow.ToString("F3"));
    }

    private static void Drive(DinoGame g, int policy = 0)
    {
        bool duck = false;
        foreach (var o in g.Obstacles) {
            if (o.Kind == DinoObstacleKind.MidBird && g.Tick >= ActionTick(o, policy) && g.Tick < o.RecoveryTick) duck = true;
        }
        g.SetDuck(duck);
        foreach (var o in g.Obstacles)
            if (o.Kind != DinoObstacleKind.MidBird && g.Tick == ActionTick(o, policy)) Assert(g.Jump(), "scheduled press grounded");
    }
    private static long ActionTick(DinoObstacle o, int policy)
    { return policy == 1 ? o.EarliestActionTick : policy == 2 ? o.LatestActionTick : o.RecommendedActionTick; }

    private static DinoGame Render(int hz)
    {
        var g = New(600, 7); g.Jump(); double accumulator = 0;
        for (int frame = 0; frame < hz * 120; frame++) {
            accumulator += 1.0 / hz;
            while (accumulator + 1e-12 >= DinoRules.StepSeconds) { Drive(g); g.Step(); accumulator -= DinoRules.StepSeconds; }
        }
        return g;
    }
    private static void Flight(bool release, bool duck, out double peak, out int steps)
    {
        var g = New(); g.Jump(); if (release) g.ReleaseJump(); if (duck) g.SetDuck(true);
        peak = 0; steps = 0;
        do { g.Step(); peak = Math.Max(peak, g.PlayerY); steps++; } while (g.Jumping && steps < 180);
        Assert(steps < 180, "eventually lands");
    }
    private static DinoGame New(double width = 600, uint seed = 17) { return new DinoGame(new DinoRules(width), seed); }
    private static void Same(DinoGame a, DinoGame b)
    {
        Assert(a.Tick == b.Tick && a.Score == b.Score && a.IsGameOver == b.IsGameOver && a.Obstacles.Count == b.Obstacles.Count, "state metadata");
        Near(a.Distance, b.Distance, "distance"); Near(a.PlayerY, b.PlayerY, "player y"); Near(a.PlayerVelocity, b.PlayerVelocity, "velocity");
        for (int i = 0; i < a.Obstacles.Count; i++) {
            var x = a.Obstacles[i]; var y = b.Obstacles[i];
            Assert(x.Id == y.Id && x.Kind == y.Kind && x.GroupSize == y.GroupSize && x.RecommendedActionTick == y.RecommendedActionTick, "same random stream"); Near(x.X, y.X, "obstacle x");
        }
    }
    private static void Run(string name, Action body) { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); } }
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Near(double a, double b, string message) { Assert(Math.Abs(a - b) < 1e-7, message + ": " + a + " != " + b); }
    private static void Throws<T>(Action body) where T : Exception { try { body(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
}
