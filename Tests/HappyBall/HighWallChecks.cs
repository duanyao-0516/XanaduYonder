using System;
using XanaduYonder.Core.HappyBall;

internal static class HighWallChecks
{
    internal static void Run()
    {
        GeneratedHeightProgresses();
        HighWallContactUsesFullSphere();
        PreviousLayerPassesBeforeHighWallContact();
        Console.WriteLine("PASS progressive high fins, side rollback, whole-ball clearance, top bounce and passage ordering");
    }

    private static void GeneratedHeightProgresses()
    {
        double earlyTotal = 0, lateTotal = 0, minimum = double.PositiveInfinity, maximum = 0;
        int earlyCount = 0, lateCount = 0;
        for (uint seed = 100; seed < 104; seed++)
        {
            uint random = seed; HappyBallLayer previous = null;
            for (int index = 0; index < 180; index++)
            {
                var layer = HappyBallGenerator.Next(index, index / 64 * 64, previous, ref random);
                foreach (var wall in layer.Walls)
                {
                    minimum = Math.Min(minimum, wall.Height); maximum = Math.Max(maximum, wall.Height);
                    Check(wall.Height >= .85 && wall.Height <= 1.50, "generated fins use the visibly taller .85 to 1.50 range");
                    Check(HappyBallRules.LayerSpacing - HappyBallRules.PlateThickness - 2 * HappyBallRules.BallRadius - wall.Height >= .219999,
                        "every high fin leaves at least .22 units to finish passing the preceding slab");
                    if (index < 36) { earlyTotal += wall.Height; earlyCount++; Check(wall.Height <= 1.05, "first fins remain .85 to 1.05 high"); }
                    if (index >= 150) { lateTotal += wall.Height; lateCount++; Check(wall.Height >= 1.25, "deep fins reach 1.25 to 1.50 high"); }
                }
                previous = layer;
            }
        }
        Check(earlyCount > 30 && lateCount > 100 && lateTotal / lateCount > earlyTotal / earlyCount + .30,
            "actual generated fin heights become significantly taller with depth");
        Console.WriteLine("Fin height coverage: " + minimum.ToString("F3") + " to " + maximum.ToString("F3") +
            "; mean " + (earlyTotal / earlyCount).ToString("F3") + " early / " + (lateTotal / lateCount).ToString("F3") + " late");
    }

    private static HappyBallLayer Floor(int index, double angle)
    {
        return new HappyBallLayer { Index = index, Y = -index * HappyBallRules.LayerSpacing,
            Sectors = new[] { new HappyBallSector { SweepAngle = HappyBallGeometry.Tau } },
            Walls = new[] { new HappyBallWall { Angle = angle, WidthAngle = .08, Height = 1.5 } } };
    }

    private static HappyBallSnapshot State(HappyBallLayer floor, double relativeY)
    {
        return new HappyBallSnapshot { RandomState = 1, NextLayerIndex = floor.Index + 1,
            PassedLayers = floor.Index, Score = floor.Index, BallY = floor.Y + relativeY, Layers = new[] { floor } };
    }

    private static void HighWallContactUsesFullSphere()
    {
        foreach (double centerHeight in new[] { .9, 1.55, 1.74 })
        {
            var game = new HappyBallGame(State(Floor(0, 1), centerHeight), false);
            var result = game.Step(HappyBallRules.FixedStep, -2);
            bool shouldBlock = centerHeight < 1.72;
            Check(!game.GameOver && Count(result, HappyBallEventKind.Blocked) == (shouldBlock ? 1 : 0),
                "high fin blocks low and center-only clearance without killing the ball");
            Check(shouldBlock ? game.GetSnapshot().TowerAngle == 0 : Math.Abs(game.GetSnapshot().TowerAngle - (HappyBallGeometry.Tau - 2)) < 1e-9,
                "high fin rejects full rotation until the ball bottom clears the wall top");
            Check(game.GetSnapshot().BallY < centerHeight, "blocked rotation still advances vertical physics");
        }
        foreach (bool armed in new[] { false, true })
        {
            var floor = Floor(3, 0); var state = State(floor, 1.77); state.BallVelocity = -4;
            if (armed) { state.Power = HappyBallPower.Armed; state.NaturalStreak = 3; state.Score = 6; }
            var game = new HappyBallGame(state, false); var result = game.Step(.05, 0);
            Check(Count(result, HappyBallEventKind.Bounce) == 1 && Count(result, HappyBallEventKind.LayerBroken) == 0 &&
                game.GetSnapshot().BallVelocity > 0 && game.Score == state.Score, "high fin top safely rebounds without smashing or scoring");
            Check(!armed || game.GetSnapshot().Power == HappyBallPower.Armed, "high fin top cannot consume armed smash");
            foreach (var e in result.Events) if (e.Kind == HappyBallEventKind.Bounce)
                Check(e.HasContact && Math.Abs(e.ContactBallY - (floor.Y + 1.5 + HappyBallRules.BallRadius)) < 1e-5,
                    "high fin bounce event uses real wall top height");
        }
    }

    private static void PreviousLayerPassesBeforeHighWallContact()
    {
        var upper = Floor(0, 1); upper.Sectors = new HappyBallSector[0]; upper.Walls = new HappyBallWall[0];
        var lower = Floor(1, 0);
        var state = new HappyBallSnapshot { RandomState = 1, NextLayerIndex = 2,
            BallY = -HappyBallRules.PlateThickness - HappyBallRules.BallRadius + .0001,
            BallVelocity = -HappyBallRules.TerminalFallSpeed, Layers = new[] { upper, lower } };
        var game = new HappyBallGame(state, false); var result = game.Step(.02, 0);
        Check(result.Events.Length == 2 && result.Events[0].Kind == HappyBallEventKind.LayerPassed && result.Events[0].LayerIndex == 0 &&
            result.Events[1].Kind == HappyBallEventKind.Bounce && result.Events[1].LayerIndex == 1,
            "preceding layer is physically passed before a maximum-height next fin can bounce the ball");
        Check(game.GetSnapshot().PassedLayers == 1 && game.Score == 1 && !game.GameOver,
            "high fin sequence preserves one physical passage and its score");
    }

    private static int Count(HappyBallStepResult result, HappyBallEventKind kind)
    { int count = 0; foreach (var e in result.Events) if (e.Kind == kind) count++; return count; }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
