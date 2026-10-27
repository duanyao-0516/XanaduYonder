using System;
using XanaduYonder.Runtime.Minesweeper;

internal static class MinesweeperControlLayoutChecks
{
    internal static void Run(Action<string, Action> check)
    {
        check("360dp controls require 88 design units and fit one row", () => {
            double minimum = MinesweeperControlLayout.TargetSize(160, 360.0 / 720);
            Near(88, minimum);
            var layout = MinesweeperControlLayout.Create(648, minimum);
            Near(88, layout.Height); Near(648, layout.Width); Near(layout.Reveal.Width, layout.Flag.Width);
            Verify(layout, minimum); Near(0, layout.Overview.Y);
        });
        check("320dp controls require 99 design units and fall back to two rows", () => {
            double minimum = MinesweeperControlLayout.TargetSize(160, 320.0 / 720);
            Near(99, minimum);
            var layout = MinesweeperControlLayout.Create(648, minimum);
            True(layout.Height > minimum * 2); True(layout.ZoomOut.Y >= minimum);
            Near(0, layout.Reveal.Y); Near(0, layout.Flag.Y); Near(layout.Reveal.Width, layout.Flag.Width);
            Near(layout.ZoomOut.Y, layout.Overview.Y); Verify(layout, minimum);
        });
        check("short safe area enlarges targets without clipping or overlap", () => {
            double minimum = MinesweeperControlLayout.TargetSize(160, 500.0 / 1320);
            Near(117, minimum);
            var layout = MinesweeperControlLayout.Create(844.8 - 72, minimum);
            Verify(layout, minimum); Near(117, layout.Height);
            True(minimum * (500.0 / 1320) >= 44);
        });
        check("DPI and physical pixels produce the same dp target", () => {
            Near(88, MinesweeperControlLayout.TargetSize(480, 1080.0 / 720));
            Near(99, MinesweeperControlLayout.TargetSize(480, 960.0 / 720));
            Near(88, MinesweeperControlLayout.TargetSize(0, .5));
            Near(88, MinesweeperControlLayout.TargetSize(-1, .5));
            Near(88, MinesweeperControlLayout.TargetSize(160, 3));
        });
        check("invalid control-layout numeric inputs are rejected", () => {
            foreach (double unit in new[] { 0, -1, double.NaN, double.PositiveInfinity })
                Throws(() => MinesweeperControlLayout.TargetSize(160, unit));
            foreach (double width in new[] { 0, -1, double.NaN, double.PositiveInfinity })
                Throws(() => MinesweeperControlLayout.Create(width, 88));
            foreach (double minimum in new[] { 0, -1, double.NaN, double.PositiveInfinity })
                Throws(() => MinesweeperControlLayout.Create(648, minimum));
            Throws(() => MinesweeperControlLayout.Create(395, 99));
        });
        check("overview minimum width is never sacrificed to force a row", () => {
            Throws(() => MinesweeperControlLayout.Create(4 * 88, 88));
            var layout = MinesweeperControlLayout.Create(3 * 88 + 120, 88);
            Verify(layout, 88); Near(120, layout.Overview.Width); True(layout.Overview.Y > 0);
        });
        check("exact four-target width fits when overview already meets 120", () => {
            var layout = MinesweeperControlLayout.Create(4 * 130, 130);
            Verify(layout, 130); Near(130, layout.Overview.Width); True(layout.Overview.Y > 0);
        });
        check("single-row breakpoint has deterministic two-row fallback", () => {
            // 2 mode targets + 3 zoom targets + 120 overview + five 8-unit gaps.
            double breakpoint = 5 * 99 + 120 + 5 * 8;
            var single = MinesweeperControlLayout.Create(breakpoint, 99);
            var two = MinesweeperControlLayout.Create(breakpoint - .01, 99);
            Near(99, single.Height); True(two.Height > 198);
            Verify(single, 99); Verify(two, 99);
        });
        check("layout invariants hold across phone sizes and target scales", () => {
            foreach (double minimum in new[] { 88.0, 99, 117, 120, 132, 180 })
            for (int extra = 0; extra <= 500; extra += 7)
            {
                double width = 3 * minimum + Math.Max(120, minimum) + extra;
                Verify(MinesweeperControlLayout.Create(width, minimum), minimum);
            }
        });
    }

    private static void Verify(MinesweeperControlLayout layout, double minimum)
    {
        var rects = new[] { layout.Reveal, layout.Flag, layout.ZoomOut, layout.ZoomValue, layout.ZoomIn, layout.Overview };
        True(layout.Overview.Width >= 120);
        for (int i = 0; i < rects.Length; i++)
        {
            var a = rects[i];
            True(a.Width + 1e-8 >= minimum && a.Height + 1e-8 >= minimum);
            True(a.X >= 0 && a.Y >= 0 && a.X + a.Width <= layout.Width + 1e-8 && a.Y + a.Height <= layout.Height + 1e-8);
            for (int j = 0; j < i; j++)
            {
                var b = rects[j];
                bool overlaps = a.X < b.X + b.Width - 1e-8 && a.X + a.Width > b.X + 1e-8 &&
                    a.Y < b.Y + b.Height - 1e-8 && a.Y + a.Height > b.Y + 1e-8;
                True(!overlaps);
            }
        }
    }
    private static void True(bool value) { if (!value) throw new Exception("control layout invariant failed"); }
    private static void Near(double expected, double actual) { if (double.IsNaN(actual) || Math.Abs(expected - actual) > 1e-8) throw new Exception("expected " + expected + ", got " + actual); }
    private static void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("expected ArgumentException"); }
}
