using System;
using System.Collections.Generic;
using System.Reflection;
using XanaduYonder.Core.Dino;

// Independent transcription of the pinned Chromium 60 Hz equations in original canvas units.
internal static class ChromeReferenceTests
{
    internal static void Speed()
    {
        foreach (double width in new[] { 320.0, 486, 675, 810, 1200, 4096 }) {
            var rules = new DinoRules(width);
            double canvas = width / 1.35;
            double initial = canvas < 600 ? Math.Min(6, 6 * canvas / 600 * 1.2) : 6;
            Near(rules.PlayerX, 50 * 1.35, "Chrome x position");
            Near(rules.MaxSpeed, 13 * 60 * 1.35, "normal maximum speed");
            foreach (long tick in new[] { 0L, 1L, 300L, 2500L, 6999L, 7000L, 10000L, long.MaxValue })
                Near(rules.SpeedAt(tick), Math.Min(13, initial + tick * .001) * 60 * 1.35, "speed at tick " + tick);
            var game = new DinoGame(rules, 17); game.Jump();
            double distance = 0;
            for (int tick = 0; tick < 170; tick++) {
                distance += (initial + tick * .001) * 1.35;
                game.Step(); Near(game.Distance, distance, "distance uses pre-acceleration speed");
            }
        }
    }

    internal static void JumpTrace()
    {
        foreach (double width in new[] { 320.0, 486, 810, 1200 })
            foreach (int releaseTick in new[] { -1, 0, 4, 8, 15 })
                foreach (int dropTick in new[] { -1, 8, 16 }) {
                    var game = new DinoGame(new DinoRules(width), 17);
                    double originalSpeed = game.Speed / (60 * 1.35);
                    var expected = new ReferenceJump(originalSpeed); game.Jump();
                    for (int tick = 0; tick < 70; tick++) {
                        if (tick == releaseTick) { game.ReleaseJump(); expected.End(); }
                        if (tick == dropTick) { game.SetDuck(true); expected.Drop(); }
                        if (tick == dropTick + 3 && dropTick >= 0) { game.SetDuck(false); expected.SpeedDrop = false; }
                        expected.Advance(); game.Step();
                        Near(game.PlayerY, (93 - expected.Y) * 1.35, "Chrome rounded y width=" + width + " release=" + releaseTick + " drop=" + dropTick + " tick=" + tick);
                        Near(game.PlayerVelocity, -expected.Velocity * 60 * 1.35, "Chrome velocity");
                        Assert(game.Jumping == expected.Jumping, "Chrome landing threshold");
                    }
                }
    }

    internal static void ClearTimeAndScore()
    {
        var game = new DinoGame(new DinoRules(1200), 17); game.Jump();
        for (int i = 0; i < 180; i++) { game.Step(); Assert(game.Obstacles.Count == 0, "clearTime uses >3000ms"); }
        game.Step(); Assert(game.Obstacles.Count == 1, "first spawn at tick181");
        var obstacle = game.Obstacles[0];
        double singleWidth = obstacle.Kind == DinoObstacleKind.SmallCactus ? 17 : obstacle.Kind == DinoObstacleKind.LargeCactus ? 25 : 46;
        Near(obstacle.X, 1200 + singleWidth * 1.35, "source spawn xOffset is one type width");
        foreach (double original in new[] { 0.0, 18, 19, 19.1, 20, 39, 60, 10000 })
            Assert(DinoGame.ScoreForDistance(original * 1.35) == (long)Math.Floor(Math.Ceiling(original) * .025 + .5), "Chrome ceil then round score " + original);
        Assert(DinoGame.ScoreForDistance(double.MaxValue) == long.MaxValue, "score saturates");
    }

    internal static void Generation()
    {
        foreach (double width in new[] { 320.0, 486, 810, 1200, 4096 }) {
            var game = new DinoGame(new DinoRules(width), 42); game.Jump();
            // Isolate horizon generation from death: a stationary off-track player allows all speed gates to be observed.
            typeof(DinoGame).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game,
                new DinoMotionState { Y = -100000 });
            long lastId = 0; int priorType = -1, repeated = 0, count = 0, birds = 0, triples = 0;
            for (int tick = 0; tick < 13000; tick++) {
                bool priorExists = game.Obstacles.Count > 0;
                DinoObstacle last = priorExists ? game.Obstacles[game.Obstacles.Count - 1] : default(DinoObstacle);
                var previous = new List<DinoObstacle>(game.Obstacles);
                double speed = game.Speed / (60 * 1.35);
                game.Step(); Assert(!game.IsGameOver, "generation fixture remains isolated");
                foreach (var moved in game.Obstacles) {
                    foreach (var before in previous) if (before.Id == moved.Id)
                        Near(moved.X, before.X - Math.Floor(speed + before.SpeedOffset) * 1.35, "source integer obstacle movement");
                }
                if (game.Obstacles.Count == 0) continue;
                var added = game.Obstacles[game.Obstacles.Count - 1];
                if (added.Id <= lastId) continue;
                Assert(game.Tick > 180, "no obstacles until clear time exceeded");
                if (lastId != 0 && priorExists && last.X + last.Width - Math.Floor(speed + last.SpeedOffset) * 1.35 > 0)
                    Assert(last.X - Math.Floor(speed + last.SpeedOffset) * 1.35 + last.Width + last.Gap < width, "next spawn obeys original right-edge-plus-gap threshold width=" + width + " tick=" + game.Tick + " last=" + last.Id + " lastRight=" + (last.X + last.Width) + " gap=" + last.Gap + " added=" + added.Id);
                Assert(added.SpawnTick == game.Tick, "spawn tick describes actual addition");
                int type = added.Kind < DinoObstacleKind.LowBird ? (int)added.Kind : 2;
                repeated = type == priorType ? repeated + 1 : 1; priorType = type;
                Assert(repeated <= 2, "maximum two consecutive types regardless of bird height");
                double single = type == 0 ? 17 : type == 1 ? 25 : 46;
                Near(added.X, width + single * 1.35, "source one-type-width spawn offset");
                Near(added.Width, single * added.GroupSize * 1.35, "group width");
                Assert(added.GroupSize >= 1 && added.GroupSize <= 3, "source group range");
                if (speed < (type == 0 ? 4 : type == 1 ? 7 : 999)) Assert(added.GroupSize == 1, "multiple-speed gate");
                if (type == 2) {
                    Assert(speed >= 8.5, "bird speed gate"); birds++;
                    Assert(added.Kind == DinoObstacleKind.LowBird || added.Kind == DinoObstacleKind.HighBird, "mobile bird heights in both orientations");
                    Near(added.Y, added.Kind == DinoObstacleKind.LowBird ? 0 : 50 * 1.35, "mobile source y transform");
                    Near(Math.Abs(added.SpeedOffset), .8, "bird relative speed");
                } else Near(added.SpeedOffset, 0, "stationary obstacle relative speed");
                int min = (int)Math.Floor(single * added.GroupSize * speed + (type == 2 ? 150 : 120) * .6 + .5);
                int max = (int)Math.Floor(min * 1.5 + .5);
                double gap = added.Gap / 1.35;
                Assert(gap >= min - 1e-7 && gap <= max + 1e-7, "Chrome gap range");
                Near(gap, Math.Round(gap), "integer source gap");
                if (added.GroupSize == 3) triples++;
                lastId = added.Id; count++;
            }
            Assert(count > 150 && birds > 20 && triples > 20, "observe complete generation distribution");
            Near(game.Speed, 13 * 60 * 1.35, "sequence reaches capped speed");
            Console.WriteLine("Chrome horizon width=" + width + " obstacles=" + count + " birds=" + birds + " triples=" + triples);
        }
    }

    internal static void SpeedDependentLaunchAndTravel()
    {
        foreach (double width in new[] { 320.0, 810, 4096 }) {
            var rules = new DinoRules(width);
            foreach (long tick in new[] { 0L, 299L, 5000L, 7000L, 10000L }) {
                var game = new DinoGame(rules, 1);
                typeof(DinoGame).GetField("<Tick>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, tick);
                game.Jump();
                double canvas = width / 1.35, start = canvas < 600 ? Math.Min(6, 6 * canvas / 600 * 1.2) : 6;
                double speed = Math.Min(13, start + tick * .001);
                var reference = new ReferenceJump(speed);
                for (int i = 0; i < 44; i++) {
                    reference.Advance(); game.Step();
                    ((List<DinoObstacle>)typeof(DinoGame).GetField("obstacles", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game)).Clear();
                    Near(game.PlayerY, (93 - reference.Y) * 1.35, "speed-dependent launch trace");
                }
            }
            foreach (long start in new[] { 0L, 5000L, 6999L, 7000L, 10000L, long.MaxValue - 2048 }) {
                double sum = 0;
                for (int i = 0; i < 2048; i++) sum += rules.SpeedAt(start + i) / 60;
                Near(rules.Travel(start, start + 2048), sum, "analytical pre-acceleration distance");
            }
        }
    }

    internal static void DiagnosticReplay()
    {
        foreach (double width in new[] { 320.0, 486, 656 / 1.2, 810, 1200, 1451 / 1.2, 4096 }) {
            var game = new DinoGame(new DinoRules(width), 17); game.Jump();
            var plans = new Dictionary<long, DinoPlan>();
            for (int tick = 0; tick < 6000 && !game.IsGameOver; tick++) {
                bool duck = false;
                foreach (var obstacle in game.Obstacles) {
                    DinoPlan plan;
                    if (!plans.TryGetValue(obstacle.Id, out plan)) {
                        Assert(DinoTraversal.TryPlan(game.Rules, obstacle, game.Tick, out plan), "diagnostic finds action width=" + width + " tick=" + tick);
                        plans.Add(obstacle.Id, plan);
                    }
                    if (plan.Action == DinoAction.Duck && game.Tick >= plan.RecommendedActionTick && game.Tick < plan.RecoveryTick) duck = true;
                }
                game.SetDuck(duck);
                foreach (var obstacle in game.Obstacles) {
                    var plan = plans[obstacle.Id];
                    if (plan.Action == DinoAction.Jump && game.Tick >= plan.RecommendedActionTick && game.Tick <= plan.LatestActionTick && !game.Jumping)
                        Assert(game.Jump(), "diagnostic input grounded width=" + width + " at tick=" + tick + " obstacle=" + obstacle.Id + " first=" + plan.EarliestActionTick + " last=" + plan.LatestActionTick);
                }
                game.Step();
            }
            Assert(!game.IsGameOver && game.Tick == 6000, "diagnostic full generated replay width=" + width + " tick=" + game.Tick);
        }
    }

    internal static void CollisionCommit()
    {
        var game = new DinoGame(new DinoRules(810), 17); game.Jump();
        typeof(DinoGame).GetField("<Tick>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, 7000L);
        typeof(DinoGame).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, DinoMotionState.Grounded);
        var obstacles = (List<DinoObstacle>)typeof(DinoGame).GetField("obstacles", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
        obstacles.Add(new DinoObstacle(1, DinoObstacleKind.SmallCactus, 1, game.Rules.PlayerX + 48 * 1.35, 0, 0, 0));
        obstacles.Add(new DinoObstacle(2, DinoObstacleKind.SmallCactus, 1, game.Rules.PlayerX + 42 * 1.35, 0, 0, 0));
        obstacles.Add(new DinoObstacle(3, DinoObstacleKind.HighBird, 1, 500, 0, 0, .8));
        var before = obstacles.ToArray(); double earliest = 1;
        foreach (var obstacle in before) {
            double fraction;
            if (DinoCollision.TryHitsMotion(game.Rules, 0, 0, false, obstacle, obstacle.X - Math.Floor(13 + obstacle.SpeedOffset) * 1.35, out fraction))
                earliest = Math.Min(earliest, fraction);
        }
        Assert(earliest > 0 && earliest < 1, "controlled interior contact");
        game.Step(); Assert(game.IsGameOver, "multiple-obstacle collision");
        for (int i = 0; i < before.Length; i++) Near(game.Obstacles[i].X, before[i].X - Math.Floor(13 + before[i].SpeedOffset) * 1.35 * earliest, "all obstacles stop at earliest fraction, independent of list order");
        Assert(game.Tick == 7000 && game.Distance == 0 && game.Score == 0, "Chrome excludes crash tick from counters");
        PropertyInfo presentation = typeof(DinoGame).GetProperty("PresentationDistance");
        Assert(presentation != null, "render distance must include fractional crash travel separately from Chrome counters");
        Near((double)presentation.GetValue(game), 13 * 1.35 * earliest, "ground and sky stop at the same fractional time");
        double stopped = (double)presentation.GetValue(game); game.Step();
        Near((double)presentation.GetValue(game), stopped, "presentation distance freezes after crash");
    }

    internal static void ZeroHeightAirDuckLanding()
    {
        var game = new DinoGame(new DinoRules(810), 17); game.Jump();
        typeof(DinoGame).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game,
            new DinoMotionState { Y = 93, Velocity = .6, Jumping = true });
        typeof(DinoGame).GetField("duckRequested", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, true);
        var obstacles = (List<DinoObstacle>)typeof(DinoGame).GetField("obstacles", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
        bool fixtureFound = false;
        for (int x = -40; x < 100; x++) {
            var obstacle = new DinoObstacle(1, DinoObstacleKind.MidBird, 1, game.Rules.PlayerX + x * 1.35, default(DinoPlan));
            double standing, ducking;
            if (DinoCollision.TryHitsMotion(game.Rules, 0, 0, false, obstacle, obstacle.X - 6 * 1.35, out standing)
                && standing < 1 && !DinoCollision.TryHitsMotion(game.Rules, 0, 0, true, obstacle, obstacle.X - 6 * 1.35, out ducking)) {
                obstacles.Add(obstacle); fixtureFound = true; break;
            }
        }
        Assert(fixtureFound, "standing-only zero-height landing fixture");
        game.Step(); Assert(game.IsGameOver && !game.Ducking, "airborne pose remains standing before the exact landing endpoint");
    }

    internal static void GroundTracksCacti()
    {
        foreach (double width in new[] { 656 / 1.2, 1451 / 1.2 })
        foreach (long start in new[] { 0L, 499L, 3000L, 6999L, 7000L, 8140L, 8141L, 10000L }) {
            var game = new DinoGame(new DinoRules(width), 17); game.Jump();
            typeof(DinoGame).GetField("<Tick>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, start);
            typeof(DinoGame).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, new DinoMotionState { Y = -100000 });
            var obstacles = (List<DinoObstacle>)typeof(DinoGame).GetField("obstacles", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            obstacles.Add(new DinoObstacle(1, DinoObstacleKind.SmallCactus, 1, 10000, start, 0, 0));
            obstacles.Add(new DinoObstacle(2, DinoObstacleKind.LargeCactus, 3, 11000, start, 0, 0));
            obstacles.Add(new DinoObstacle(3, DinoObstacleKind.HighBird, 1, 12000, start, 0, -.8));
            double initial = Math.Min(6, 6 * (width / 1.35) / 600 * 1.2);
            double expectedGround = 0, expectedScoreDistance = 0;
            for (int i = 0; i < 125; i++) {
                double speed = Math.Min(13, initial + (start + i) * .001);
                double oldGround = GroundDistance(game), cactusX = game.Obstacles[0].X, largeX = game.Obstacles[1].X, birdX = game.Obstacles[2].X;
                game.Step();
                expectedGround += Math.Floor(speed) * 1.35;
                expectedScoreDistance += speed * 1.35;
                Near(GroundDistance(game) - oldGround, cactusX - game.Obstacles[0].X, "small cactus remains fixed to ground width=" + width + " tick=" + (start + i));
                Near(GroundDistance(game) - oldGround, largeX - game.Obstacles[1].X, "grouped large cactus remains fixed to ground");
                Near(GroundDistance(game), expectedGround, "Chrome horizon rounds each frame before accumulating");
                Near(game.Distance, expectedScoreDistance, "score distance keeps original fractional travel");
                Near(birdX - game.Obstacles[2].X, Math.Floor(speed - .8) * 1.35, "flying bird retains its independent speed offset");
            }

            typeof(DinoGame).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, DinoMotionState.Grounded);
            obstacles.Clear();
            double fullStep = Math.Floor(Math.Min(13, initial + game.Tick * .001)) * 1.35;
            double fraction = 0; DinoObstacle contact = default(DinoObstacle);
            for (int x = 80; x < 240; x++) {
                var candidate = new DinoObstacle(4, DinoObstacleKind.SmallCactus, 1, game.Rules.PlayerX + x * .25 * 1.35, game.Tick, 0, 0);
                if (DinoCollision.TryHitsMotion(game.Rules, 0, 0, false, candidate, candidate.X - fullStep, out fraction)
                    && fraction > 0 && fraction < 1) { contact = candidate; break; }
            }
            Assert(contact.Id == 4, "partial collision fixture exists");
            obstacles.Add(contact);
            double beforeContact = GroundDistance(game), scoreDistance = game.Distance;
            game.Step(); Assert(game.IsGameOver, "contact stops game");
            Near(GroundDistance(game) - beforeContact, contact.X - game.Obstacles[0].X, "ground and cactus stop at the same partial-frame contact");
            Near(GroundDistance(game) - beforeContact, fullStep * fraction, "partial ground movement uses the rounded complete step");
            Near(game.Distance, scoreDistance, "collision ground travel does not enter score distance");
            double stopped = GroundDistance(game);
            for (int i = 0; i < 20; i++) game.Step();
            Near(GroundDistance(game), stopped, "ground freezes after collision");
        }
    }

    private static double GroundDistance(DinoGame game)
    {
        return game.GroundDistance;
    }

    internal static void OneFollowerPerObstacle()
    {
        var game = new DinoGame(new DinoRules(4096), 17); game.Jump();
        typeof(DinoGame).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, new DinoMotionState { Y = -100000 });
        for (int i = 0; i < 1000 && game.Obstacles.Count < 2; i++) game.Step();
        var obstacles = (List<DinoObstacle>)typeof(DinoGame).GetField("obstacles", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
        Assert(obstacles.Count >= 2, "two actual generated obstacles");
        DinoObstacle first = obstacles[0], last = obstacles[obstacles.Count - 1];
        // A faster follower can leave the viewport first. Its predecessor has already created one follower.
        obstacles[0] = first.At(200);
        obstacles[obstacles.Count - 1] = last.At(-last.Width + 1);
        game.Step();
        Assert(game.Obstacles.Count == 1 && game.Obstacles[0].Id == first.Id, "an earlier obstacle must not create a second follower after its first follower exits");
    }

    internal static void WideHorizonDoesNotAllocate()
    {
        foreach (uint seed in new[] { 1u, 42u }) {
            var game = new DinoGame(new DinoRules(4096), seed); game.Jump();
            typeof(DinoGame).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, new DinoMotionState { Y = -100000 });
            long before = GC.GetAllocatedBytesForCurrentThread();
            int mostObstacles = 0;
            for (int tick = 0; tick < 20000; tick++) { game.Step(); mostObstacles = Math.Max(mostObstacles, game.Obstacles.Count); }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert(bytes == 0, "full widest-horizon runtime allocated " + bytes + " bytes for " + mostObstacles + " simultaneous obstacles seed=" + seed);
        }
    }

    internal static void TerminalTickFreezesStep()
    {
        var game = new DinoGame(new DinoRules(810), 17); game.Jump();
        for (int tick = 0; tick < 181; tick++) game.Step();
        Assert(game.Obstacles.Count > 0 && !game.IsGameOver, "terminal fixture includes a real obstacle");
        typeof(DinoGame).GetField("<Tick>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, long.MaxValue);
        typeof(DinoGame).GetField("<Speed>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, game.Rules.MaxSpeed);
        Assert(game.Jump(), "terminal fixture has an active jump to detect any motion update");
        var before = new List<DinoObstacle>(game.Obstacles);
        double elapsed = game.Elapsed, distance = game.Distance, presentation = game.PresentationDistance, ground = game.GroundDistance;
        double y = game.PlayerY, velocity = game.PlayerVelocity, speed = game.Speed;
        long score = game.Score;
        bool jumping = game.Jumping, ducking = game.Ducking, started = game.IsStarted, over = game.IsGameOver;
        for (int tick = 0; tick < 100; tick++) game.Step();
        Assert(game.Tick == long.MaxValue && game.Score == score, "terminal tick and score never overflow");
        Assert(game.Elapsed == elapsed && game.Distance == distance && game.PresentationDistance == presentation && game.GroundDistance == ground && game.Speed == speed,
            "terminal time and distances remain frozen");
        Assert(game.PlayerY == y && game.PlayerVelocity == velocity && game.Jumping == jumping && game.Ducking == ducking
            && game.IsStarted == started && game.IsGameOver == over, "terminal motion and lifecycle remain frozen");
        Assert(game.Obstacles.Count == before.Count, "terminal step neither generates nor removes obstacles");
        for (int i = 0; i < before.Count; i++) {
            DinoObstacle actual = game.Obstacles[i], expected = before[i];
            Assert(actual.Id == expected.Id && actual.Kind == expected.Kind && actual.GroupSize == expected.GroupSize
                && actual.SpawnTick == expected.SpawnTick && actual.FollowingObstacleCreated == expected.FollowingObstacleCreated,
                "terminal obstacle identity and generation state remain frozen");
            Assert(actual.X == expected.X && actual.Y == expected.Y && actual.Width == expected.Width && actual.Height == expected.Height
                && actual.Gap == expected.Gap && actual.SpeedOffset == expected.SpeedOffset, "terminal obstacle geometry remains frozen");
        }
        Assert(!double.IsNaN(game.Elapsed) && !double.IsInfinity(game.Elapsed)
            && !double.IsNaN(game.Distance) && !double.IsInfinity(game.Distance), "terminal counters remain finite");
    }

    private sealed class ReferenceJump
    {
        internal double Y = 93, Velocity;
        internal bool Jumping = true, SpeedDrop, Minimum;
        internal ReferenceJump(double speed) { Velocity = -10 - speed / 10; }
        internal void End() { if (Minimum && Velocity < -5) Velocity = -5; }
        internal void Drop() { if (Jumping) { SpeedDrop = true; Velocity = 1; } }
        internal void Advance()
        {
            if (!Jumping) return;
            Y += Math.Floor(Velocity * (SpeedDrop ? 3 : 1) + .5);
            Velocity += .6;
            if (Y < 63 || SpeedDrop) Minimum = true;
            if (Y < 30 || SpeedDrop) End();
            if (Y > 93) { Y = 93; Velocity = 0; Jumping = false; SpeedDrop = false; }
        }
    }
    internal static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Near(double actual, double expected, string message) { Assert(Math.Abs(actual - expected) < 1e-7, message + ": " + actual + " != " + expected); }
}
