using System;
using System.Diagnostics;
using XanaduYonder.Core.HappyBall;
internal static class Program
{
    private static int Main()
    {
        var timer = Stopwatch.StartNew();
        ReferenceChecks.Run();
        HighWallChecks.Run();
        Check(Math.Abs(HappyBallRules.BallRadius - .22) < 1e-12, "approved visual scale uses the same .22 physical radius");
        var startup = Stopwatch.StartNew();
        var game = new HappyBallGame(123);
        Console.WriteLine("Seeded initial 12 layers: " + startup.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
        var snapshot = game.GetSnapshot();
        string reason;
        Check(snapshot.TryValidate(out reason), "generated snapshot valid: " + reason);
        var copy = snapshot.Copy();
        copy.Layers[0].Sectors[0].StartAngle += 0.1;
        Check(copy.Layers[0].Sectors[0].StartAngle != snapshot.Layers[0].Sectors[0].StartAngle, "copy owns geometry");
        TestBounceAndRed();
        TestHeavyAndPass();
        TestWallAndSweeps();
        TestUnderside();
        TestSideAndTransaction();
        TestGapRadius();
        TestSnapshotBoundary();
        TestDeterminismAndRoutes();
        GeneratorChecks.Run();
        GeneratorChecks.RunLongRun();
        TestContactOrderingAndSpentRestore();
        TestFrameCadence();
        TestScoreOverflowAndSpentBoundary();
        TestDescendingRedEdgeWithSmallDrag();
        Console.WriteLine("HappyBall tests passed in " + timer.Elapsed.TotalSeconds.ToString("F2") + " s"); return 0;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static HappyBallLayer Layer(int index, bool red = false)
    {
        return new HappyBallLayer { Index = index, Y = -index * HappyBallRules.LayerSpacing,
            Sectors = new[] { new HappyBallSector { StartAngle = 0, SweepAngle = Math.PI * 2, Dangerous = red } }, Walls = new HappyBallWall[0] };
    }
    private static HappyBallSnapshot State(params HappyBallLayer[] layers)
    {
        return new HappyBallSnapshot { RandomState = 123, NextLayerIndex = layers[layers.Length - 1].Index + 1,
            Layers = layers, BallY = .3, BallVelocity = -4 };
    }
    private static void TestBounceAndRed()
    {
        var game = new HappyBallGame(State(Layer(0)));
        var result = game.Step(.1, 0);
        Check(Count(result, HappyBallEventKind.Bounce) == 1 && game.GetSnapshot().BallVelocity > 0, "black bounces once");
        Check(game.Score == 0, "bounce gives no points");
        game = new HappyBallGame(State(Layer(0, true)));
        Check(Count(game.Step(.1, 0), HappyBallEventKind.Died) == 1 && game.GameOver, "normal red dies");
        Check(game.Step(.1, 3).Events.Length == 0, "death frozen and once only");
    }
    private static void TestHeavyAndPass()
    {
        var layers = new[] { Layer(0), Layer(1), Layer(2), Layer(3, true), Layer(4, true) };
        for (int i = 0; i < 3; i++) layers[i].Sectors = new HappyBallSector[0];
        var state = State(layers); state.BallY = .2; state.BallVelocity = -10;
        var game = new HappyBallGame(state);
        int broken = 0, armed = 0, passed = 0;
        for (int i = 0; i < 150 && !game.GameOver; i++)
        {
            var events = game.Step(HappyBallRules.FixedStep, 0);
            broken += Count(events, HappyBallEventKind.LayerBroken); armed += Count(events, HappyBallEventKind.Armed);
            passed += Count(events, HappyBallEventKind.LayerPassed);
        }
        Check(game.GameOver && broken == 1 && armed == 1 && passed == 4, "three gaps arm; one red breaks then next red kills");
        Check(game.Score == 10 && game.GetSnapshot().NaturalStreak == 0, "1+2+3+4 impact scoring and spent reset");
    }
    private static void TestWallAndSweeps()
    {
        var layer = Layer(0); layer.Sectors = new HappyBallSector[0];
        layer.Walls = new[] { new HappyBallWall { Angle = 1, WidthAngle = .06, Height = .4 } };
        var state = State(layer); state.BallY = .2; state.BallVelocity = 0;
        var game = new HappyBallGame(state);
        var result = game.Step(HappyBallRules.FixedStep, -Math.PI * 4);
        Check(Count(result, HappyBallEventKind.Blocked) == 1, "full two turn sweep hits thin wall");
        Check(Math.Abs(game.GetSnapshot().TowerAngle) < 1e-9 && game.GetSnapshot().BallY < .2, "reject angle but advance gravity");
        state.BallY = .4 + HappyBallRules.BallRadius + .06; game = new HappyBallGame(state);
        Check(Count(game.Step(HappyBallRules.FixedStep, -2), HappyBallEventKind.Blocked) == 0, "whole ball clears wall");
        state.BallY = .5; game = new HappyBallGame(state);
        Check(Count(game.Step(HappyBallRules.FixedStep, -2), HappyBallEventKind.Blocked) == 1, "center above wall insufficient");
    }
    private static void TestUnderside()
    {
        var state = State(Layer(0)); state.BallY = -.6; state.BallVelocity = 5;
        var game = new HappyBallGame(state); game.Step(.1, 0);
        Check(game.GetSnapshot().BallY <= -HappyBallRules.PlateThickness - HappyBallRules.BallRadius && game.GetSnapshot().BallVelocity < 0, "black underside stops upward flight y=" + game.GetSnapshot().BallY + " v=" + game.GetSnapshot().BallVelocity);
    }
    private static int Count(HappyBallStepResult result, HappyBallEventKind kind)
    { int n = 0; foreach (var e in result.Events) if (e.Kind == kind) n++; return n; }

    private static void TestSideAndTransaction()
    {
        var layer = Layer(0);
        layer.Walls = new[] { new HappyBallWall { Angle = .5, WidthAngle = .08, Height = .4 } };
        var state = State(layer); state.BallY = HappyBallRules.BallRadius + .01; state.BallVelocity = -4;
        var game = new HappyBallGame(state);
        var result = game.Step(HappyBallRules.FixedStep, -1);
        Check(Count(result, HappyBallEventKind.Blocked) == 1 && Count(result, HappyBallEventKind.Bounce) == 1,
            "blocked step recomputes exactly one bounce on original floor");
        Check(game.GetSnapshot().BallY >= HappyBallRules.BallRadius && game.GetSnapshot().TowerAngle == 0,
            "recomputed angle and height form legal state");
        foreach (var e in result.Events) if (e.Kind == HappyBallEventKind.Bounce)
            Check(e.HasContact && e.ContactTowerAngle == 0 && Math.Abs(e.ContactBallY - HappyBallRules.BallRadius) < 1e-5,
                "blocked transaction discards speculative impact pose along with speculative events");
        layer = Layer(3); layer.Sectors = new[] { new HappyBallSector { StartAngle = .5, SweepAngle = 1 } };
        state = State(layer); state.PassedLayers = 3; state.Score = 6; state.NaturalStreak = 3;
        state.Power = HappyBallPower.Armed; state.BallY = layer.Y - .1; state.BallVelocity = 0;
        game = new HappyBallGame(state); result = game.Step(HappyBallRules.FixedStep, -1);
        Check(Count(result, HappyBallEventKind.Blocked) == 1 && Count(result, HappyBallEventKind.LayerBroken) == 0 &&
            game.GetSnapshot().Power == HappyBallPower.Armed, "armed side contact blocks without smash");
        layer.Sectors[0].Dangerous = true; game = new HappyBallGame(state);
        Check(Count(game.Step(HappyBallRules.FixedStep, -1), HappyBallEventKind.Died) == 1, "armed red side is fatal");

        layer = Layer(0); layer.Sectors = new HappyBallSector[0];
        layer.Walls = new[] { new HappyBallWall { Angle = 1, WidthAngle = .08, Height = .4 } };
        state = State(layer); state.BallY = .5; state.BallVelocity = -.1;
        game = new HappyBallGame(state); result = game.Step(HappyBallRules.FixedStep, -1);
        Check(Count(result, HappyBallEventKind.Blocked) == 1 && Count(result, HappyBallEventKind.Bounce) == 0,
            "lateral wall corner blocks rather than grants a top bounce");
    }

    private static void TestGapRadius()
    {
        double diameterAngle = 2 * Math.Asin(HappyBallRules.BallRadius / HappyBallRules.OrbitRadius);
        foreach (double gap in new[] { diameterAngle - .03, diameterAngle + .04 })
        {
            var layer = Layer(0); layer.Sectors = new[] { new HappyBallSector { StartAngle = gap / 2, SweepAngle = Math.PI * 2 - gap } };
            var game = new HappyBallGame(State(layer));
            int bounce = 0;
            for (int i = 0; i < 40; i++) bounce += Count(game.Step(HappyBallRules.FixedStep, 0), HappyBallEventKind.Bounce);
            Check(gap < diameterAngle ? bounce > 0 && game.Score == 0 : game.GetSnapshot().PassedLayers > 0,
                "gap clearance uses sphere radius: " + gap);
        }
        var mixed = Layer(0); mixed.Sectors = new[] {
            new HappyBallSector { StartAngle = 0, SweepAngle = .5, Dangerous = true },
            new HappyBallSector { StartAngle = .5, SweepAngle = Math.PI * 2 - .5 } };
        var edge = new HappyBallGame(State(mixed)); edge.Step(.1, 0);
        Check(edge.GameOver, "shared red/black edge gives red priority");
    }

    private static void TestSnapshotBoundary()
    {
        var good = new HappyBallGame(7).GetSnapshot(); string reason;
        var bad = good.Copy(); bad.Layers[2].State = HappyBallLayerState.Broken;
        Check(!bad.TryValidate(out reason), "reject future broken layer");
        bad = good.Copy(); bad.PassedLayers = bad.NextLayerIndex; bad.Score = bad.PassedLayers;
        foreach (var layer in bad.Layers) layer.State = HappyBallLayerState.Passed;
        Check(!bad.TryValidate(out reason), "reject missing current layer");
        bad = good.Copy(); bad.Layers[0].Sectors[0] = null;
        Check(!bad.TryValidate(out reason), "reject null sector");
        bad = good.Copy(); bad.BallVelocity = double.NaN;
        Check(!bad.TryValidate(out reason), "reject nonfinite velocity");
        bad = State(Layer(0)); bad.BallY = -.1;
        Check(!bad.TryValidate(out reason), "reject snapshot embedded in active geometry");
        bad = State(Layer(3)); bad.PassedLayers = 3; bad.Score = 6; bad.NaturalStreak = 3;
        bad.BallY = bad.Layers[0].Y + HappyBallRules.BallRadius + .1;
        Check(!bad.TryValidate(out reason), "normal power cannot retain a fully charged natural streak");
    }

    private static void TestDeterminismAndRoutes()
    {
        for (uint seed = 1; seed <= 16; seed++)
        {
            var game = new HappyBallGame(seed); var restored = new HappyBallGame(game.GetSnapshot());
            for (int step = 0; step < 160; step++)
            {
                double delta = step < 90 ? -.035 : .012;
                game.Step(HappyBallRules.FixedStep, delta); restored.Step(HappyBallRules.FixedStep, delta);
                var a = game.GetSnapshot(); var b = restored.GetSnapshot();
                Check(a.BallY == b.BallY && a.BallVelocity == b.BallVelocity && a.TowerAngle == b.TowerAngle &&
                    a.RandomState == b.RandomState && a.Score == b.Score && a.Power == b.Power, "restore replay deterministic");
            }
            var snapshot = new HappyBallGame(seed).GetSnapshot(); double entry = 0;
            foreach (var layer in snapshot.Layers)
            {
                Check(HappyBallGenerator.VerifyRoute(layer, entry), "generated route replayable");
                entry = HappyBallGenerator.GapCenter(layer);
            }
        }
    }

    private static void TestContactOrderingAndSpentRestore()
    {
        var layer = Layer(0);
        layer.Sectors = new[] { new HappyBallSector { StartAngle = 3, SweepAngle = .2, Dangerous = true } };
        layer.Walls = new[] { new HappyBallWall { Angle = 1, WidthAngle = .08, Height = .4 } };
        var state = State(layer); state.BallY = .1; state.BallVelocity = -1;
        var game = new HappyBallGame(state); var result = game.Step(HappyBallRules.FixedStep, -2);
        Check(!game.GameOver && Count(result, HappyBallEventKind.Blocked) == 1, "first wall rejects before any later hazard");
        game = new HappyBallGame(state); result = game.Step(HappyBallRules.FixedStep, Math.PI * 2 - 2);
        Check(game.GameOver && Count(result, HappyBallEventKind.Died) == 1, "same endpoint retains long arc direction and hits red");

        var first = Layer(3, true); var second = Layer(4, true);
        state = State(first, second); state.PassedLayers = 3; state.NaturalStreak = 3; state.Score = 6;
        state.Power = HappyBallPower.Armed; state.BallY = first.Y + HappyBallRules.BallRadius + .01; state.BallVelocity = -4;
        game = new HappyBallGame(state); result = game.Step(HappyBallRules.FixedStep, 0);
        Check(Count(result, HappyBallEventKind.LayerBroken) == 1 && game.GetSnapshot().Power == HappyBallPower.Spent,
            "impact enters spent before clearing underside");
        string reason; Check(game.GetSnapshot().TryValidate(out reason), "spent snapshot validates: " + reason);
        var restored = new HappyBallGame(game.GetSnapshot()); int passed = 0; int broken = 0;
        for (int i = 0; i < 100 && !restored.GameOver; i++)
        {
            result = restored.Step(HappyBallRules.FixedStep, 0);
            passed += Count(result, HappyBallEventKind.LayerPassed); broken += Count(result, HappyBallEventKind.LayerBroken);
        }
        Check(restored.GameOver && passed == 1 && broken == 0 && restored.Score == 10,
            "restoring spent cannot rebreak or double-score and next red kills");

        layer = Layer(3); layer.Sectors = new HappyBallSector[0];
        layer.Walls = new[] { new HappyBallWall { Angle = 0, WidthAngle = .2, Height = .4 } };
        state = State(layer); state.PassedLayers = 3; state.Score = 6; state.NaturalStreak = 3;
        state.Power = HappyBallPower.Armed; state.BallY = layer.Y + .4 + HappyBallRules.BallRadius + .06; state.BallVelocity = -4;
        game = new HappyBallGame(state); result = game.Step(.1, 0);
        Check(Count(result, HappyBallEventKind.Bounce) == 1 && Count(result, HappyBallEventKind.LayerBroken) == 0 &&
            game.GetSnapshot().Power == HappyBallPower.Armed, "charged wall top safely bounces without consuming horizontal smash");
        Check(game.GetSnapshot().TryValidate(out reason), "wall-top armed snapshot valid: " + reason);
    }

    private static void TestFrameCadence()
    {
        var thirty = new HappyBallGame(432); var sixty = new HappyBallGame(432); var oneTwenty = new HappyBallGame(432);
        for (int frame = 0; frame < 90; frame++)
        {
            double delta = frame < 60 ? -.04 : .016;
            thirty.Step(1.0 / 30, delta);
            for (int i = 0; i < 2; i++) sixty.Step(1.0 / 60, delta / 2);
            for (int i = 0; i < 4; i++) oneTwenty.Step(1.0 / 120, delta / 4);
        }
        var a = thirty.GetSnapshot(); var b = sixty.GetSnapshot(); var c = oneTwenty.GetSnapshot();
        Check(a.BallY == b.BallY && b.BallY == c.BallY && a.TowerAngle == c.TowerAngle && a.Score == c.Score &&
            a.RandomState == c.RandomState, "30/60/120 Hz with identical fixed-step input are bitwise equivalent");
    }

    private static void TestScoreOverflowAndSpentBoundary()
    {
        var layer = Layer(0); layer.Sectors = new HappyBallSector[0];
        var state = State(layer); state.Score = long.MaxValue;
        state.BallY = -HappyBallRules.PlateThickness - HappyBallRules.BallRadius + .01; state.BallVelocity = -4;
        var game = new HappyBallGame(state); var result = game.Step(HappyBallRules.FixedStep, 0);
        Check(game.Score == long.MaxValue && Count(result, HappyBallEventKind.LayerPassed) == 1, "score saturates instead of wrapping");
        foreach (var e in result.Events) if (e.Kind == HappyBallEventKind.LayerPassed) Check(e.ScoreDelta == 0, "event delta equals actual saturated score change");
        layer = Layer(3); layer.State = HappyBallLayerState.Broken;
        state = State(layer); state.PassedLayers = 3; state.Score = 6; state.Power = HappyBallPower.Spent;
        state.ImpactLayerIndex = 3; state.BallY = layer.Y - 1;
        string reason; Check(!state.TryValidate(out reason), "spent cannot already be below impact layer");
    }

    private static void TestDescendingRedEdgeWithSmallDrag()
    {
        foreach (double delta in new[] { 0.0, 1e-8, .01, -.01 })
        {
            var layer = Layer(3, true);
            layer.Sectors = new[] { new HappyBallSector { StartAngle = .05, SweepAngle = 1, Dangerous = true } };
            var state = State(layer); state.Power = HappyBallPower.Armed; state.NaturalStreak = 3;
            state.Score = 6; state.PassedLayers = 3; state.BallY = layer.Y + HappyBallRules.BallRadius + .05; state.BallVelocity = -4;
            var game = new HappyBallGame(state); var result = game.Step(.05, delta);
            Check(!game.GameOver && Count(result, HappyBallEventKind.LayerBroken) == 1 && game.GetSnapshot().Power == HappyBallPower.Spent,
                "downward red top-edge impact stays a smash with small drag " + delta);
        }
    }
}
