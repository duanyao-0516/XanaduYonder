using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.HappyBall
{
    public struct HappyBallGap
    {
        public double StartAngle;
        public double SweepAngle;
        public double CenterAngle { get { return HappyBallGeometry.NormalizeAngle(StartAngle + SweepAngle / 2); } }
    }

    public static class HappyBallGenerator
    {
        public const double RouteAngularSpeed = 4.5;
        public const double RouteReactionTime = .14;
        private const double MinimumGapAngle = 1.025;
        private const double MinimumDangerAngle = .18;

        internal static HappyBallLayer Next(int index, int origin, HappyBallLayer previous, ref uint random)
        {
            var incoming = previous == null ? new[] { new HappyBallGap { StartAngle = 0, SweepAngle = 0 } } : Gaps(previous);
            double entry = previous == null ? 0 : GapCenter(previous);
            // The independent turn decision creates variable freefall runs. A turn can
            // go either way; neither its period nor its magnitude follows a block index.
            bool turn = index == 0 || Unit(ref random) < .34;
            double direction = Unit(ref random) < .5 ? -1 : 1;
            double offset = direction * (turn ? .95 + Unit(ref random) * .65 : .09 + Unit(ref random) * .38);
            double center = HappyBallGeometry.NormalizeAngle(entry + offset);
            int count = 1 + (int)(Unit(ref random) * 3);
            double depth = Math.Min(1, index / 160.0);
            double baseWidth = 1.55 - depth * .42;
            var gaps = new HappyBallGap[count];
            for (int i = 0; i < count; i++)
            {
                double width = Math.Max(MinimumGapAngle, baseWidth + (Unit(ref random) - .5) * .28);
                double location = center + i * HappyBallGeometry.Tau / count;
                if (i > 0) location += (Unit(ref random) - .5) * .18;
                gaps[i] = new HappyBallGap { StartAngle = location - width / 2, SweepAngle = width };
            }
            var sectors = new List<HappyBallSector>();
            for (int i = 0; i < count; i++)
            {
                double start = gaps[i].StartAngle + gaps[i].SweepAngle;
                double end = gaps[(i + 1) % count].StartAngle + (i + 1 == count ? HappyBallGeometry.Tau : 0);
                AddSolid(sectors, start, end, index, incoming, ref random);
            }
            MakeSmallHazardsSafe(sectors);
            var walls = new List<HappyBallWall>();
            if (index >= 16)
            {
                int wanted = index >= 36 ? 2 : 1;
                double wallDepth = Math.Max(0, Math.Min(1, (index - 36) / 100.0));
                double minimumHeight = .85 + .40 * wallDepth;
                double maximumHeight = 1.05 + (HappyBallRules.MaximumWallHeight - 1.05) * wallDepth;
                // Fins use black plate interiors and avoid the full incoming opening
                // plus their own half-width. Their tops stay below the previous slab.
                foreach (var sector in sectors)
                {
                    double angle = HappyBallGeometry.NormalizeAngle(sector.StartAngle + sector.SweepAngle / 2);
                    if (!sector.Dangerous && sector.SweepAngle >= .24 && AwayFromEntries(angle, incoming, .05))
                    {
                        walls.Add(new HappyBallWall { Angle = angle, WidthAngle = .065 + Unit(ref random) * .035,
                            Height = minimumHeight + Unit(ref random) * (maximumHeight - minimumHeight) });
                        if (walls.Count == wanted) break;
                    }
                }
            }
            return new HappyBallLayer { Index = index, Y = (origin - index) * HappyBallRules.LayerSpacing,
                Sectors = sectors.ToArray(), Walls = walls.ToArray() };
        }

        private static void AddSolid(List<HappyBallSector> output, double start, double end, int index,
            HappyBallGap[] incoming, ref uint random)
        {
            // Split the solid into independently sized black/red intervals. Safe
            // edge bands permit a late approach, and safe incoming bands guarantee
            // that a preceding gap cannot drop directly onto a new red face.
            var cuts = new List<double> { start, end };
            var red = new List<HappyBallGap>();
            if (index >= 6 && end - start > .6)
            {
                int count = end - start > 2.3 ? 2 : 1;
                double innerStart = start + .27, innerEnd = end - .27;
                for (int i = 0; i < count; i++)
                {
                    double slot = (innerEnd - innerStart) / count;
                    double width = Math.Min(slot * .62, .22 + Math.Min(index, 150) * .003 + Unit(ref random) * .2);
                    double redStart = innerStart + i * slot + (slot - width) * (.25 + Unit(ref random) * .5);
                    red.Add(new HappyBallGap { StartAngle = redStart, SweepAngle = width });
                    cuts.Add(redStart); cuts.Add(redStart + width);
                }
                foreach (var gap in incoming)
                {
                    double protectedWidth = EntryHalfWidth(gap);
                    double local = start + HappyBallGeometry.NormalizeAngle(gap.CenterAngle - start);
                    foreach (double entry in new[] { local - HappyBallGeometry.Tau, local, local + HappyBallGeometry.Tau })
                    {
                        if (entry - protectedWidth > start && entry - protectedWidth < end) cuts.Add(entry - protectedWidth);
                        if (entry + protectedWidth > start && entry + protectedWidth < end) cuts.Add(entry + protectedWidth);
                    }
                }
            }
            cuts.Sort();
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                double a = cuts[i], b = cuts[i + 1]; if (b - a < 1e-10) continue;
                double middle = (a + b) / 2;
                bool dangerous = false;
                foreach (var stripe in red) if (middle > stripe.StartAngle && middle < stripe.StartAngle + stripe.SweepAngle) dangerous = true;
                dangerous &= AwayFromEntries(middle, incoming, 0);
                // Keeping adjacent same-color pieces merged avoids needless mesh and
                // collision work while retaining all genuine red/black boundaries.
                if (output.Count > 0)
                {
                    var last = output[output.Count - 1];
                    if (last.Dangerous == dangerous && Math.Abs(Difference(last.StartAngle + last.SweepAngle, a)) < 1e-8)
                    { last.SweepAngle += b - a; continue; }
                }
                output.Add(new HappyBallSector { StartAngle = HappyBallGeometry.NormalizeAngle(a), SweepAngle = b - a, Dangerous = dangerous });
            }
        }

        private static void MakeSmallHazardsSafe(List<HappyBallSector> sectors)
        {
            // Incoming safety bands can trim a readable red stripe down to a hairline.
            // Remove that hazard, then merge the neighboring black geometry; never
            // widen red back into the protected incoming opening or shrink a gap.
            for (int i = 0; i < sectors.Count; i++)
                if (sectors[i].Dangerous && sectors[i].SweepAngle < MinimumDangerAngle)
                    sectors[i].Dangerous = false;
            for (int i = 1; i < sectors.Count; i++)
            {
                var previous = sectors[i - 1]; var current = sectors[i];
                if (previous.Dangerous != current.Dangerous ||
                    Math.Abs(Difference(previous.StartAngle + previous.SweepAngle, current.StartAngle)) >= 1e-8) continue;
                previous.SweepAngle += current.SweepAngle;
                sectors.RemoveAt(i--);
            }
        }

        private static double EntryHalfWidth(HappyBallGap gap)
        { return Math.Max(.44, gap.SweepAngle / 2 + .02); }

        private static bool AwayFromEntries(double angle, HappyBallGap[] entries, double additionalMargin)
        {
            foreach (var gap in entries)
                if (Math.Abs(Difference(angle, gap.CenterAngle)) < EntryHalfWidth(gap) + additionalMargin) return false;
            return true;
        }

        // The union/complement also handles arbitrary saved sectors, including wrap
        // around zero and adjacent red/black pieces. Gap zero precedes sector zero,
        // preserving the generated primary route without adding a snapshot field.
        public static HappyBallGap[] Gaps(HappyBallLayer layer)
        {
            if (layer == null || layer.Sectors == null || layer.Sectors.Length == 0)
                return new[] { new HappyBallGap { StartAngle = 0, SweepAngle = HappyBallGeometry.Tau } };
            var intervals = new List<HappyBallGap>();
            foreach (var sector in layer.Sectors)
            {
                double end = sector.StartAngle + sector.SweepAngle;
                intervals.Add(new HappyBallGap { StartAngle = sector.StartAngle, SweepAngle = Math.Min(end, HappyBallGeometry.Tau) - sector.StartAngle });
                if (end > HappyBallGeometry.Tau) intervals.Add(new HappyBallGap { StartAngle = 0, SweepAngle = end - HappyBallGeometry.Tau });
            }
            intervals.Sort((a, b) => a.StartAngle.CompareTo(b.StartAngle));
            var merged = new List<HappyBallGap>();
            foreach (var interval in intervals)
            {
                if (merged.Count == 0) { merged.Add(interval); continue; }
                var last = merged[merged.Count - 1]; double lastEnd = last.StartAngle + last.SweepAngle;
                if (interval.StartAngle > lastEnd + 1e-8) merged.Add(interval);
                else { last.SweepAngle = Math.Max(lastEnd, interval.StartAngle + interval.SweepAngle) - last.StartAngle; merged[merged.Count - 1] = last; }
            }
            var gaps = new List<HappyBallGap>();
            for (int i = 0; i < merged.Count; i++)
            {
                double start = merged[i].StartAngle + merged[i].SweepAngle;
                double end = merged[(i + 1) % merged.Count].StartAngle + (i + 1 == merged.Count ? HappyBallGeometry.Tau : 0);
                if (end > start + 1e-8) gaps.Add(new HappyBallGap { StartAngle = HappyBallGeometry.NormalizeAngle(start), SweepAngle = end - start });
            }
            for (int i = 0; i < gaps.Count; i++)
            {
                if (Math.Abs(Difference(gaps[i].StartAngle + gaps[i].SweepAngle, layer.Sectors[0].StartAngle)) > 1e-8) continue;
                var primary = gaps[i]; gaps.RemoveAt(i); gaps.Insert(0, primary); break;
            }
            return gaps.ToArray();
        }

        public static double GapCenter(HappyBallLayer layer)
        { var gaps = Gaps(layer); return gaps.Length == 0 ? 0 : gaps[0].CenterAngle; }

        public static double NearestGapCenter(HappyBallLayer layer, double angle)
        {
            var gaps = Gaps(layer); double target = 0, distance = double.PositiveInfinity;
            foreach (var gap in gaps)
            {
                double candidate = Math.Abs(Difference(gap.CenterAngle, angle));
                if (candidate < distance) { target = gap.CenterAngle; distance = candidate; }
            }
            return target;
        }

        public static bool VerifyRoute(HappyBallLayer layer, double entryAngle)
        {
            if (layer == null) return false;
            var gaps = Gaps(layer);
            Array.Sort(gaps, (a, b) => Math.Abs(Difference(a.CenterAngle, entryAngle)).CompareTo(Math.Abs(Difference(b.CenterAngle, entryAngle))));
            // Try each real opening independently with exact swept collisions. This
            // is a bounded validation helper, never part of per-frame generation.
            foreach (var gap in gaps) if (ReplayRoute(layer, entryAngle, gap.CenterAngle)) return true;
            return false;
        }

        private static bool ReplayRoute(HappyBallLayer layer, double entryAngle, double target)
        {
            int origin = layer.Index + (int)Math.Round(layer.Y / HappyBallRules.LayerSpacing);
            var state = new HappyBallSnapshot { RandomState = 1, NextLayerIndex = layer.Index + 1, OriginLayer = origin,
                PassedLayers = layer.Index, Score = layer.Index, BallY = layer.Y + HappyBallRules.BallRadius + .05,
                BallVelocity = -HappyBallRules.TerminalFallSpeed, TowerAngle = HappyBallGeometry.NormalizeAngle(-entryAngle),
                Layers = new[] { layer.Copy() } };
            string reason; if (!state.TryValidate(out reason)) return false;
            var game = new HappyBallGame(state, false); double elapsedAfterBounce = -1;
            for (int i = 0; i < 240 && !game.GameOver; i++)
            {
                var current = game.GetSnapshot(); if (current.PassedLayers > layer.Index) return true;
                double difference = Difference(target, -current.TowerAngle);
                double rotation = 0;
                if (elapsedAfterBounce >= RouteReactionTime || elapsedAfterBounce < 0 && Math.Abs(difference) < .1)
                    rotation = -Math.Sign(difference) * Math.Min(Math.Abs(difference), RouteAngularSpeed * HappyBallRules.FixedStep);
                var result = game.Step(HappyBallRules.FixedStep, rotation);
                foreach (var e in result.Events) if (e.Kind == HappyBallEventKind.Bounce) elapsedAfterBounce = 0;
                if (elapsedAfterBounce >= 0) elapsedAfterBounce += HappyBallRules.FixedStep;
            }
            return false;
        }

        private static double Difference(double a, double b) { return HappyBallGeometry.NormalizeAngle(a - b + Math.PI) - Math.PI; }
        private static double Unit(ref uint state)
        { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return (state & 0xffffff) / 16777216.0; }
    }
}
