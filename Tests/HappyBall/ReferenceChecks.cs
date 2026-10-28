using System;
using XanaduYonder.Core.HappyBall;

internal static class ReferenceChecks
{
    internal static void Run()
    {
        BounceRhythm();
        SmashScoresAndRebounds();
        SpentRestoreBoundaries();
        FallAcceleratesAcrossSeveralLayers();
        ContactEventsFreezeImpactCoordinates();
        LegacySpentNormalizesOnce();
        LegacySpacingKeepsRelativeMotion();
        Console.WriteLine("PASS reference bounce, smash score/rebound/restore, accelerating fall");
    }

    private static HappyBallLayer Floor(int index, bool dangerous = false)
    {
        return new HappyBallLayer { Index = index, Y = -index * HappyBallRules.LayerSpacing,
            Sectors = new[] { new HappyBallSector { SweepAngle = HappyBallGeometry.Tau, Dangerous = dangerous } },
            Walls = new HappyBallWall[0] };
    }

    private static HappyBallSnapshot Armed()
    {
        var floor = Floor(3, true);
        return new HappyBallSnapshot { RandomState = 77, NextLayerIndex = 5, PassedLayers = 3,
            NaturalStreak = 3, Score = 6, Power = HappyBallPower.Armed,
            BallY = floor.Y + HappyBallRules.BallRadius + .01, BallVelocity = -4,
            Layers = new[] { floor, Floor(4, true) } };
    }

    private static void BounceRhythm()
    {
        var state = new HappyBallSnapshot { RandomState = 1, NextLayerIndex = 1,
            BallY = HappyBallRules.BallRadius + .00001, BallVelocity = -1, Layers = new[] { Floor(0) } };
        var game = new HappyBallGame(state, false);
        int first = -1, second = -1; double apex = 0;
        for (int step = 0; step < 100; step++)
        {
            var result = game.Step(HappyBallRules.FixedStep, 0);
            apex = Math.Max(apex, game.GetSnapshot().BallY - HappyBallRules.BallRadius);
            foreach (var e in result.Events) if (e.Kind == HappyBallEventKind.Bounce)
            { if (first < 0) first = step; else { second = step; break; } }
            if (second >= 0) break;
        }
        Check(second >= 0 && Math.Abs((second - first) * HappyBallRules.FixedStep - .667) < .025,
            "reference bounce cycle is approximately 0.67 seconds");
        Check(apex / (2 * HappyBallRules.BallRadius) > 4.8 && apex / (2 * HappyBallRules.BallRadius) < 5.1,
            "reference apex is approximately five ball diameters; actual=" + apex / (2 * HappyBallRules.BallRadius));
    }

    private static void SmashScoresAndRebounds()
    {
        foreach (bool red in new[] { false, true })
        {
            var state = Armed(); state.Layers[0].Sectors[0].Dangerous = red;
            var game = new HappyBallGame(state, false);
            var result = game.Step(HappyBallRules.FixedStep, 0);
            var after = game.GetSnapshot();
            Check(!game.GameOver && after.Power == HappyBallPower.Spent && after.BallVelocity > 0,
                "first armed horizontal contact rebounds and spends smash, including red");
            Check(after.PassedLayers == 3 && after.Score == 10 && after.NaturalStreak == 0,
                "smash awards current natural streak plus one immediately, before passage");
            int impacts = 0;
            foreach (var e in result.Events) if (e.Kind == HappyBallEventKind.LayerBroken)
            { impacts++; Check(e.ScoreDelta == 4, "LayerBroken carries actual four-point impact score"); }
            Check(impacts == 1, "one impact event per contact");

            // The rebound originates at the same sphere/plate contact, with no layer-height teleport.
            double hitTime = (-4 + Math.Sqrt(16 + 2 * HappyBallRules.Gravity * .01)) / HappyBallRules.Gravity;
            double remaining = HappyBallRules.FixedStep - hitTime;
            double expected = state.Layers[0].Y + HappyBallRules.BallRadius +
                HappyBallRules.BounceSpeed * remaining - HappyBallRules.Gravity * remaining * remaining / 2;
            Check(Math.Abs(after.BallY - expected) < 1e-5, "smash rebounds at its exact contact height");

            var restored = new HappyBallGame(after, false); int passes = 0, breaks = 0;
            for (int step = 0; step < 150 && !restored.GameOver; step++)
            {
                result = restored.Step(HappyBallRules.FixedStep, 0);
                var current = restored.GetSnapshot(); string reason;
                Check(current.TryValidate(out reason), "spent rebound snapshot remains legal: " + reason);
                foreach (var e in result.Events)
                {
                    if (e.Kind == HappyBallEventKind.LayerBroken) breaks++;
                    if (e.Kind == HappyBallEventKind.LayerPassed)
                    { passes++; Check(e.LayerIndex == 3 && e.ScoreDelta == 0, "broken-layer passage cannot award twice"); }
                }
                if (current.Power == HappyBallPower.Spent && step % 10 == 0)
                    restored = new HappyBallGame(current, false);
            }
            Check(restored.GameOver && passes == 1 && breaks == 0 && restored.Score == 10,
                "spent restores cannot smash again; physically passing resets and next red kills");
            Check(restored.GetSnapshot().Power == HappyBallPower.Normal && restored.GetSnapshot().NaturalStreak == 0,
                "physical passage clears spent and streak");
        }
        var saturated = Armed(); saturated.Score = long.MaxValue - 2;
        var full = new HappyBallGame(saturated, false);
        foreach (var e in full.Step(HappyBallRules.FixedStep, 0).Events)
            if (e.Kind == HappyBallEventKind.LayerBroken) Check(e.ScoreDelta == 2, "impact delta saturates without overflow");
        Check(full.Score == long.MaxValue, "impact score saturates");
    }

    private static void SpentRestoreBoundaries()
    {
        var game = new HappyBallGame(Armed(), false); game.Step(.1, 0);
        var state = game.GetSnapshot(); string reason;
        Check(state.Power == HappyBallPower.Spent && state.BallVelocity > 0 && state.TryValidate(out reason),
            "ascending spent snapshot is a valid save state");
        var bad = state.Copy(); bad.BallVelocity = HappyBallRules.BounceSpeed + .1;
        Check(!bad.TryValidate(out reason), "spent cannot exceed rebound launch speed");
        bad = state.Copy(); bad.BallY = bad.Layers[0].Y + HappyBallRules.BallRadius +
            HappyBallRules.BounceSpeed * HappyBallRules.BounceSpeed / (2 * HappyBallRules.Gravity) + .01;
        Check(!bad.TryValidate(out reason), "spent cannot exceed rebound apex");
        bad = state.Copy(); bad.ImpactLayerIndex = 4;
        Check(!bad.TryValidate(out reason), "spent must retain its actual broken current layer");
        bad = state.Copy(); bad.BallY = bad.Layers[0].Y - HappyBallRules.PlateThickness - HappyBallRules.BallRadius - .01;
        Check(!bad.TryValidate(out reason), "spent cannot already have passed broken layer");
    }

    private static void FallAcceleratesAcrossSeveralLayers()
    {
        var layers = new HappyBallLayer[6];
        for (int i = 0; i < layers.Length; i++) { layers[i] = Floor(i); layers[i].Sectors = new HappyBallSector[0]; }
        var state = new HappyBallSnapshot { RandomState = 1, NextLayerIndex = layers.Length,
            BallY = HappyBallRules.BallRadius + .01, Layers = layers };
        var game = new HappyBallGame(state, false);
        double previousSpeed = 0; int passes = 0;
        for (int step = 0; step < 100 && passes < 4; step++)
        {
            foreach (var e in game.Step(HappyBallRules.FixedStep, 0).Events)
                if (e.Kind == HappyBallEventKind.LayerPassed)
                {
                    double speed = -game.GetSnapshot().BallVelocity;
                    Check(speed > previousSpeed + .5, "first four natural passes keep accelerating");
                    previousSpeed = speed; passes++;
                }
        }
        Check(passes == 4, "freefall crosses four layers in the expected interval");
    }

    private static void ContactEventsFreezeImpactCoordinates()
    {
        foreach (bool red in new[] { false, true })
        {
            var state = new HappyBallSnapshot { RandomState = 1, NextLayerIndex = 1,
                BallY = HappyBallRules.BallRadius + .01, BallVelocity = -4, Layers = new[] { Floor(0, red) } };
            var game = new HappyBallGame(state, false);
            double duration = .2, delta = 1.8;
            double hitTime = (-4 + Math.Sqrt(16 + 2 * HappyBallRules.Gravity * .01)) / HappyBallRules.Gravity;
            int contactCount = 0;
            foreach (var e in game.Step(duration, delta).Events)
                if (e.Kind == HappyBallEventKind.Bounce || e.Kind == HappyBallEventKind.Died)
                {
                    contactCount++;
                    Check(e.HasContact && Math.Abs(e.ContactBallY - HappyBallRules.BallRadius) < 1e-5,
                        "bounce/death event retains exact contact height across accumulated fixed ticks");
                    Check(Math.Abs(e.ContactTowerAngle - delta / duration * hitTime) < 1e-5 && e.ContactOriginLayer == 0,
                        "contact rotation is impact-time rotation, not frame-end rotation");
                }
            Check(contactCount == 1, "exactly one frozen contact event");
        }
        var armed = Armed();
        armed.PassedLayers = 63; armed.Score = 100; armed.OriginLayer = 0; armed.NextLayerIndex = 65;
        armed.Layers = new[] { Floor(63, true), Floor(64) };
        armed.BallY = armed.Layers[0].Y + HappyBallRules.BallRadius + .01;
        var rebase = new HappyBallGame(armed); HappyBallEvent impact = null;
        foreach (var e in rebase.Step(HappyBallRules.FixedStep, 0).Events)
            if (e.Kind == HappyBallEventKind.LayerBroken) impact = e;
        Check(impact != null && impact.HasContact && impact.ContactOriginLayer == 0,
            "smash event records contact before later world-origin shifts");
        for (int step = 0; step < 120 && rebase.GetSnapshot().OriginLayer == 0; step++) rebase.Step(HappyBallRules.FixedStep, 0);
        var after = rebase.GetSnapshot();
        Check(after.OriginLayer == 64 && Math.Abs(impact.ContactBallY +
            (after.OriginLayer - impact.ContactOriginLayer) * HappyBallRules.LayerSpacing -
            (HappyBallRules.LayerSpacing + HappyBallRules.BallRadius)) < 1e-5,
            "retained contact event converts correctly after an origin rebase");
        var empty = Floor(0); empty.Sectors = new HappyBallSector[0];
        double passHeight = -HappyBallRules.PlateThickness - HappyBallRules.BallRadius - HappyBallRules.ContactTolerance;
        var passing = new HappyBallGame(new HappyBallSnapshot { RandomState = 1, NextLayerIndex = 1,
            BallY = passHeight + .01, BallVelocity = -4, Layers = new[] { empty } }, false);
        int passCount = 0;
        foreach (var e in passing.Step(.1, .9).Events)
            if (e.Kind == HappyBallEventKind.LayerPassed)
            {
                passCount++;
                double hitTime = (-4 + Math.Sqrt(16 + 2 * HappyBallRules.Gravity * .01)) / HappyBallRules.Gravity;
                Check(e.HasContact && Math.Abs(e.ContactBallY - passHeight) < 1e-6 &&
                    Math.Abs(e.ContactTowerAngle - 9 * hitTime) < 1e-5,
                    "ordinary layer-removal event keeps passage-time pose across accumulated ticks");
            }
        Check(passCount == 1, "one passage pose event");
    }

    private static void LegacySpentNormalizesOnce()
    {
        var legacy = Armed(); legacy.Power = HappyBallPower.Spent; legacy.ImpactLayerIndex = 3;
        legacy.RulesVersion = 1;
        foreach (var layer in legacy.Layers) layer.Y = -layer.Index * 1.65;
        legacy.Layers[0].State = HappyBallLayerState.Broken;
        legacy.BallY = legacy.Layers[0].Y + .1; legacy.BallVelocity = -12;
        var game = new HappyBallGame(legacy, false); var current = game.GetSnapshot();
        Check(current.Score == 10 && current.NaturalStreak == 0 &&
            Math.Abs((current.BallY - current.Layers[0].Y) - (legacy.BallY - legacy.Layers[0].Y)) < 1e-9 &&
            current.BallVelocity == legacy.BallVelocity, "legacy spent save receives its pending impact score once without changing motion");
        Check(legacy.Score == 6 && legacy.NaturalStreak == 3, "legacy normalization does not mutate caller snapshot");
        var replay = new HappyBallGame(current, false);
        Check(replay.Score == 10, "normalizing an already upgraded spent save is idempotent");
        for (int step = 0; step < 30 && !replay.GameOver; step++) replay.Step(HappyBallRules.FixedStep, 0);
        Check(replay.Score == 10 && replay.GetSnapshot().PassedLayers == 4 && replay.GameOver,
            "legacy resumed passage cannot score twice and consumes power before next red");
    }

    private static void LegacySpacingKeepsRelativeMotion()
    {
        var legacy = Armed(); legacy.RulesVersion = 1;
        foreach (var layer in legacy.Layers) layer.Y = -layer.Index * 1.65;
        legacy.BallY = legacy.Layers[0].Y + .55; legacy.BallVelocity = 3;
        var migrated = new HappyBallGame(legacy, false).GetSnapshot();
        Check(migrated.RulesVersion == 2 && Math.Abs(migrated.Layers[0].Y + 7.2) < 1e-9 &&
            Math.Abs(migrated.BallY - (migrated.Layers[0].Y + .55)) < 1e-9 && migrated.BallVelocity == 3,
            "v1 constructor migration adopts 2.4 layer spacing while retaining relative ball motion");
        Check(Math.Abs(legacy.Layers[0].Y + 4.95) < 1e-9, "spacing migration does not mutate caller geometry");
        var repeated = new HappyBallGame(migrated, false).GetSnapshot();
        Check(repeated.BallY == migrated.BallY && repeated.Score == migrated.Score, "v2 migration is idempotent");

        var empty = Floor(0); empty.Sectors = new HappyBallSector[0];
        var future = Floor(1); future.Y = -1.65;
        var corrupt = new HappyBallSnapshot { RulesVersion = 1, RandomState = 1, NextLayerIndex = 2,
            BallY = -2.4, Layers = new[] { empty, future } };
        string reason; Check(corrupt.TryValidate(out reason), "fixture meets legacy geometry contract before migration");
        bool rejected = false;
        try { new HappyBallGame(corrupt, false); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "migration rejects a counter-inconsistent position that would penetrate future geometry instead of fabricating a repair");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
