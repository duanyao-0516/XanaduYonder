using System;
using System.Collections.Generic;
using System.Diagnostics;
using XanaduYonder.Core.HappyBall;

internal static class GeneratorChecks
{
    internal static void Run()
    {
        TestReadableHazards();
        var counts = new HashSet<int>(); var freefallLengths = new HashSet<int>();
        int positive = 0, negative = 0, variedWidths = 0, multipleRed = 0, wallCount = 0;
        double earlyWidth = 0, lateWidth = 0; int earlyCount = 0, lateCount = 0;
        var generation = Stopwatch.StartNew();
        for (uint seed = 100; seed < 104; seed++)
        {
            uint random = seed; HappyBallLayer previous = null; double fallingAngle = 0; int freefallLength = 0;
            for (int i = 0; i < 180; i++)
            {
                var layer = HappyBallGenerator.Next(i, i / 64 * 64, previous, ref random);
                var gaps = Gaps(layer); counts.Add(gaps.Count);
                var publicGaps = HappyBallGenerator.Gaps(layer);
                Check(publicGaps.Length == gaps.Count, "public route gaps enumerate the geometry complement");
                Check(gaps.Count >= 1 && gaps.Count <= 3, "generated layer has one to three actual gaps");
                foreach (var gap in gaps)
                {
                    double opening = 2 * HappyBallRules.OrbitRadius * Math.Sin(gap.Item2 / 2);
                    Check(opening >= HappyBallRules.BallRadius * 6 - 1e-9, "every gap admits at least three physical ball diameters");
                    if (i < 16) { earlyWidth += gap.Item2; earlyCount++; }
                    if (i >= 150) { lateWidth += gap.Item2; lateCount++; }
                }
                if (gaps.Count > 1 && Math.Abs(gaps[0].Item2 - gaps[1].Item2) > .02) variedWidths++;
                double center = HappyBallGenerator.GapCenter(layer);
                Check(IsInGap(layer, center), "compatibility gap center is inside a real gap");
                bool falling = true;
                foreach (var sector in layer.Sectors)
                    if (HappyBallGeometry.SectorDistance(fallingAngle, sector.StartAngle, sector.SweepAngle) < HappyBallRules.BallRadius) falling = false;
                if (falling) freefallLength++;
                else { if (freefallLength > 0) freefallLengths.Add(freefallLength); freefallLength = 0; fallingAngle = center; }
                if (previous != null)
                {
                    double delta = Difference(center, HappyBallGenerator.GapCenter(previous));
                    if (delta > .08) positive++; if (delta < -.08) negative++;
                }
                int red = 0;
                foreach (var sector in layer.Sectors) if (sector.Dangerous) red++;
                if (red > 1) multipleRed++;
                wallCount += layer.Walls.Length;
                foreach (var wall in layer.Walls)
                {
                    Check(wall.Height + HappyBallRules.BallRadius * 2 < HappyBallRules.LayerSpacing - HappyBallRules.PlateThickness,
                        "later fins preserve sphere height clearance beneath preceding layer");
                    Check(!IsInGap(layer, wall.Angle), "vertical fins stay on solid plate geometry");
                }
                previous = layer;
            }
        }
        Check(counts.Count == 3, "generator exercises one, two and three gaps instead of a single-gap template");
        Check(positive > 100 && negative > 100, "adjacent primary gap changes use both rotation directions");
        Check(variedWidths > 150, "multi-gap floors vary their gap widths");
        Check(earlyWidth / earlyCount > lateWidth / lateCount + .2, "depth makes the average gap narrower without violating minimum clearance");
        Check(multipleRed > 100, "later plates contain several separated red and black intervals");
        Check(wallCount > 200, "late route geometry retains vertical fins");
        Check(freefallLengths.Contains(1) && freefallLengths.Contains(2) && freefallLengths.Contains(3) && freefallLengths.Count >= 5,
            "actual unrotated freefall opportunities vary across short and long runs");
        Console.WriteLine("Generated 720 varied layers in " + generation.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
        Console.WriteLine("Generation coverage: " + multipleRed + " floors with multiple red intervals; " + wallCount +
            " fins; " + freefallLengths.Count + " freefall lengths; mean gap " + (earlyWidth / earlyCount).ToString("F3") +
            " early / " + (lateWidth / lateCount).ToString("F3") + " late radians");
        TestRoutes();
        TestGeometryAndRandomRestore();
        TestGapUnion();
    }

    private static void TestReadableHazards()
    {
        double minimum = double.PositiveInfinity; int narrow = 0, hazards = 0;
        for (uint seed = 100; seed < 104; seed++)
        {
            uint random = seed; HappyBallLayer previous = null;
            for (int index = 0; index < 180; index++)
            {
                var layer = HappyBallGenerator.Next(index, index / 64 * 64, previous, ref random);
                foreach (var sector in layer.Sectors)
                    if (sector.Dangerous)
                    {
                        minimum = Math.Min(minimum, sector.SweepAngle); hazards++;
                        if (sector.SweepAngle < .18 - 1e-10) narrow++;
                    }
                previous = layer;
            }
        }
        Console.WriteLine("Danger readability: " + hazards + " intervals; minimum " + minimum.ToString("F6") +
            " radians; " + narrow + " below 0.18 radians");
        Check(hazards > 500 && narrow == 0, "incoming protection must not leave hairline danger remnants below 0.18 radians");
    }

    private static void TestGapUnion()
    {
        var layer = new HappyBallLayer { Sectors = new[] {
            new HappyBallSector { StartAngle = 5.8, SweepAngle = 1.0 },
            new HappyBallSector { StartAngle = .2, SweepAngle = .8, Dangerous = true },
            new HappyBallSector { StartAngle = 2, SweepAngle = .3 },
            new HappyBallSector { StartAngle = 2.3, SweepAngle = .7, Dangerous = true }
        }, Walls = new HappyBallWall[0] };
        var gaps = HappyBallGenerator.Gaps(layer);
        Check(gaps.Length == 2, "overlapping wraparound sectors and touching colors create exactly two gaps");
        double width = 0;
        foreach (var gap in gaps) { width += gap.SweepAngle; Check(IsInGap(layer, gap.CenterAngle), "every union gap has a real open center"); }
        Check(Math.Abs(width - 3.8) < 1e-9, "gap union measures complement without double-counting overlap");
    }

    private static void TestRoutes()
    {
        int entries = 0;
        for (uint seed = 100; seed < 104; seed++)
        {
            uint random = seed; HappyBallLayer previous = null;
            for (int i = 0; i < 180; i++)
            {
                var layer = HappyBallGenerator.Next(i, i / 64 * 64, previous, ref random);
                var entryGaps = previous == null ? new List<Tuple<double, double>> { Tuple.Create(0.0, 0.0) } : Gaps(previous);
                foreach (var gap in entryGaps)
                {
                    double edge = Math.Max(0, gap.Item2 / 2 - Math.Asin(HappyBallRules.BallRadius / HappyBallRules.OrbitRadius) - .04);
                    var offsets = previous == null ? new[] { 0.0 } : new[] { -edge, 0.0, edge };
                    foreach (double offset in offsets)
                    {
                        double entry = HappyBallGeometry.NormalizeAngle(gap.Item1 + gap.Item2 / 2 + offset);
                        Check(HappyBallGenerator.VerifyRoute(layer, entry), "multi-gap replay seed=" + seed + " layer=" + i +
                            " previous gap start=" + gap.Item1 + " width=" + gap.Item2 + " offset=" + offset + " entry=" + entry);
                        entries++;
                    }
                }
                previous = layer;
            }
        }
        Console.WriteLine("Replayed 720 layer routes across " + entries + " distinct gap entries");
    }

    private static void TestGeometryAndRandomRestore()
    {
        for (uint seed = 1; seed <= 8; seed++)
        {
            var initial = new HappyBallGame(seed).GetSnapshot();
            var restored = new HappyBallGame(initial).GetSnapshot();
            uint a = initial.RandomState, b = restored.RandomState;
            var left = initial.Layers[initial.Layers.Length - 1];
            var right = restored.Layers[restored.Layers.Length - 1];
            for (int i = initial.NextLayerIndex; i < initial.NextLayerIndex + 100; i++)
            {
                left = HappyBallGenerator.Next(i, 0, left, ref a);
                right = HappyBallGenerator.Next(i, 0, right, ref b);
                Check(a == b && Geometry(left) == Geometry(right), "restored random cursor reproduces every gap, color and fin");
            }
        }
    }

    internal static void RunLongRun()
    {
        var game = new HappyBallGame(159);
        var passed = new HashSet<int>(); var broken = new HashSet<int>();
        bool rebased = false, restored = false; double sinceBounce = -1;
        long totalTicks = 0, maxTicks = 0; int steps = 0;
        for (int step = 0; step < 60000 && !game.GameOver; step++)
        {
            var before = game.GetSnapshot(); if (before.PassedLayers >= 220) break;
            HappyBallLayer layer = null;
            foreach (var candidate in before.Layers) if (candidate.Index == before.PassedLayers) layer = candidate;
            double local = HappyBallGeometry.NormalizeAngle(-before.TowerAngle);
            double target = NearestGap(layer, local);
            double difference = Difference(target, local);
            double rotation = 0;
            if (sinceBounce >= HappyBallGenerator.RouteReactionTime || Math.Abs(difference) < .1)
                rotation = -Math.Sign(difference) * Math.Min(Math.Abs(difference), HappyBallGenerator.RouteAngularSpeed * HappyBallRules.FixedStep);
            long start = Stopwatch.GetTimestamp();
            var result = game.Step(HappyBallRules.FixedStep, rotation);
            long ticks = Stopwatch.GetTimestamp() - start;
            totalTicks += ticks; maxTicks = Math.Max(maxTicks, ticks); steps++;
            var after = game.GetSnapshot();
            foreach (var e in result.Events)
            {
                if (e.Kind == HappyBallEventKind.Bounce || e.Kind == HappyBallEventKind.LayerBroken) sinceBounce = 0;
                if (e.Kind == HappyBallEventKind.LayerPassed) { Check(passed.Add(e.LayerIndex), "long run passage unique"); sinceBounce = -1; }
                if (e.Kind == HappyBallEventKind.LayerBroken) Check(broken.Add(e.LayerIndex), "long run smash unique");
                if (e.Kind == HappyBallEventKind.LayerPassed || e.Kind == HappyBallEventKind.LayerBroken)
                {
                    bool retained = false;
                    foreach (var retainedLayer in after.Layers) if (retainedLayer.Index == e.LayerIndex) retained = true;
                    Check(retained, "removed geometry remains available through the event step");
                }
            }
            if (sinceBounce >= 0) sinceBounce += HappyBallRules.FixedStep;
            string reason;
            Check(after.TryValidate(out reason), "varied long-run snapshot remains valid: " + reason);
            Check(after.Layers.Length <= 17, "long run retains bounded geometry");
            Check(after.BallVelocity >= -HappyBallRules.TerminalFallSpeed, "long run obeys terminal speed");
            rebased |= after.OriginLayer > 0;
            if (step == 2000) { game = new HappyBallGame(after); restored = true; }
        }
        var final = game.GetSnapshot();
        Check(!game.GameOver && final.PassedLayers >= 220 && rebased && restored,
            "multi-gap long run survives 220 layers with rebase and restore; passed=" + final.PassedLayers);
        Console.WriteLine("Varied long-run fixed steps " + steps + ": mean " +
            (totalTicks * 1000.0 / Stopwatch.Frequency / steps).ToString("F3") + " ms, max " +
            (maxTicks * 1000.0 / Stopwatch.Frequency).ToString("F3") + " ms");
    }

    private static string Geometry(HappyBallLayer layer)
    {
        var result = new System.Text.StringBuilder();
        foreach (var s in layer.Sectors) result.Append(s.StartAngle.ToString("R")).Append('/').Append(s.SweepAngle.ToString("R")).Append('/').Append(s.Dangerous).Append(';');
        foreach (var w in layer.Walls) result.Append(w.Angle.ToString("R")).Append('/').Append(w.WidthAngle.ToString("R")).Append('/').Append(w.Height.ToString("R")).Append(';');
        return result.ToString();
    }

    private static double NearestGap(HappyBallLayer layer, double angle)
    {
        double target = 0, distance = double.PositiveInfinity;
        foreach (var gap in Gaps(layer))
        {
            double center = HappyBallGeometry.NormalizeAngle(gap.Item1 + gap.Item2 / 2);
            double d = Math.Abs(Difference(center, angle));
            if (d < distance) { distance = d; target = center; }
        }
        return target;
    }

    // Independent union/complement extraction verifies real geometry, rather than
    // trusting the generator's own gap bookkeeping in its regression tests.
    private static List<Tuple<double, double>> Gaps(HappyBallLayer layer)
    {
        var intervals = new List<Tuple<double, double>>();
        foreach (var sector in layer.Sectors)
        {
            double start = sector.StartAngle, end = start + sector.SweepAngle;
            intervals.Add(Tuple.Create(start, Math.Min(end, HappyBallGeometry.Tau)));
            if (end > HappyBallGeometry.Tau) intervals.Add(Tuple.Create(0.0, end - HappyBallGeometry.Tau));
        }
        intervals.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        var merged = new List<Tuple<double, double>>();
        foreach (var interval in intervals)
        {
            if (merged.Count == 0 || interval.Item1 > merged[merged.Count - 1].Item2 + 1e-8) merged.Add(interval);
            else { var last = merged[merged.Count - 1]; merged[merged.Count - 1] = Tuple.Create(last.Item1, Math.Max(last.Item2, interval.Item2)); }
        }
        var gaps = new List<Tuple<double, double>>();
        if (merged.Count == 0) { gaps.Add(Tuple.Create(0.0, HappyBallGeometry.Tau)); return gaps; }
        for (int i = 0; i < merged.Count; i++)
        {
            double start = merged[i].Item2, end = merged[(i + 1) % merged.Count].Item1 + (i + 1 == merged.Count ? HappyBallGeometry.Tau : 0);
            if (end - start > 1e-8) gaps.Add(Tuple.Create(HappyBallGeometry.NormalizeAngle(start), end - start));
        }
        return gaps;
    }

    private static bool IsInGap(HappyBallLayer layer, double angle)
    { foreach (var s in layer.Sectors) if (HappyBallGeometry.SectorDistance(angle, s.StartAngle, s.SweepAngle) < 1e-8) return false; return true; }
    private static double Difference(double a, double b) { return HappyBallGeometry.NormalizeAngle(a - b + Math.PI) - Math.PI; }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
