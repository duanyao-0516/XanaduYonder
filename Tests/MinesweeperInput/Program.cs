using System;
using System.Collections.Generic;
using XanaduYonder.Runtime.Minesweeper;

internal static class Program
{
    private static int passed;
    private static int failed;
    private static int Main()
    {
        Check("new board starts at top left, small axes centered", () => {
            var v = V(); Near(0, v.OffsetX); Near(0, v.OffsetY);
            var small = new MinesweeperViewport(9, 9, 648, 760); Near(0, small.OffsetX); Near(56, small.OffsetY);
        });
        Check("continuous scale and center survive layout clamp", () => {
            var v = V(); v.RestoreNormal(1.38, 6.2, 7.3);
            v.SetLayout(1800, 1600, 72); Near(6.2, v.CenterColumn); Near(7.3, v.CenterRow);
            v.SetLayout(648, 760, 72); Near(6.2, v.RenderedCenterColumn); Near(7.3, v.RenderedCenterRow); Near(1.38, v.ZoomScale);
        });
        Check("zoom buttons add 25 points and reset continuous scale", () => {
            var v = V(); v.RestoreNormal(1.38, 15, 8); v.StepZoom(1); Near(1.63, v.ZoomScale);
            v.RestoreNormal(.61, 15, 8); v.StepZoom(-1); Near(.5, v.ZoomScale); v.StepZoom(-1); Near(.5, v.ZoomScale);
            v.RestoreNormal(1.9, 15, 8); v.StepZoom(1); Near(2, v.ZoomScale); v.ResetZoom(); Near(1, v.ZoomScale);
        });
        Check("pinch combines midpoint movement and scale around actual transform", () => {
            var v = V(); v.RestoreNormal(1, 15, 8); var old = P(250, 200); var anchor = v.BoardPosition(old);
            v.Transform(old, P(280, 230), 1.4); Near(1.4, v.ZoomScale);
            var rendered = v.BoardToViewport(anchor); Near(280, rendered.X); Near(230, rendered.Y);
        });
        Check("clamps board without extra edge whitespace", () => {
            var v = V(); v.Pan(10000, 10000); Near(0, v.OffsetX); Near(0, v.OffsetY);
            v.Pan(-10000, -10000); Near(v.Width - 30 * v.CellSize, v.OffsetX); Near(v.Height - 16 * v.CellSize, v.OffsetY);
        });
        Check("small-scale hit test follows actual rendered cells and whitespace", () => {
            var v = new MinesweeperViewport(9, 9, 648, 760); v.RestoreNormal(.5, 0, 0);
            Equal(-1, v.HitTest(P(100, 300))); Equal(0, v.HitTest(P(162, 218))); Equal(1, v.HitTest(P(198, 218)));
            Equal(-1, v.HitTest(P(486, 218))); Equal(-1, v.HitTest(P(162, 542))); Equal(-1, v.HitTest(P(648, 760)));
            Near(4.5, v.BoardPosition(P(324, 380)).X); Near(0, v.CenterColumn);
        });
        Check("overview fit is independent of normal bounds and preserves desired center", () => {
            var v = V(); v.RestoreNormal(1.38, 25, 10); v.EnterOverview(); Near(.3, v.RenderScale); Near(1.38, v.ZoomScale);
            v.Pan(50, 50); v.StepZoom(-1); v.ResetZoom(); Near(1.38, v.ZoomScale); Near(25, v.CenterColumn);
            v.ExitOverview(); Near(1.38, v.RenderScale); Near(25, v.CenterColumn);
            var tiny = new MinesweeperViewport(1, 1, 648, 760); tiny.EnterOverview(); Near(9, tiny.RenderScale);
        });
        Check("overview whitespace does not exit and location is continuous", () => {
            var v = V(); v.RestoreNormal(.75, 15, 8); v.EnterOverview();
            False(v.LocateFromOverview(P(300, 5))); True(v.IsOverview);
            var target = P(20.25, 8.125); True(v.LocateFromOverview(v.BoardToViewport(target)));
            False(v.IsOverview); Near(.75, v.ZoomScale); Near(20.25, v.CenterColumn); Near(8.125, v.CenterRow);
        });
        Check("viewport rejects invalid external numeric state", () => {
            Throws(() => new MinesweeperViewport(0, 9, 648, 760)); var v = V();
            Throws(() => v.RestoreNormal(.49, 1, 1)); Throws(() => v.RestoreNormal(2.01, 1, 1));
            Throws(() => v.RestoreNormal(double.NaN, 1, 1)); Throws(() => v.RestoreNormal(1, -1, 1));
            Throws(() => v.SetLayout(double.PositiveInfinity, 760, 72));
        });
        Check("short press commits once only on UP and captures mode", () => {
            var g = G(); None(F(g, 0, C(1, 36, 36, B))); var r = F(g, 399, C(1, 36, 36, E));
            Equal(MinesweeperGestureAction.ShortPress, r.Action); Equal(0, r.CellIndex); True(r.GestureCompleted); False(r.FlagMode);
            None(F(g, 400));
            r = g.ProcessFrame(Frame(500, true, true, true, C(2, 36, 36, B)), Flag); None(r);
            r = g.ProcessFrame(Frame(510, true, true, true, C(2, 36, 36, E)), Flag); True(r.FlagMode);
        });
        Check("400 ms previews then UP flags, 399 ms still taps", () => {
            var g = G(); F(g, 0, C(1, 36, 36, B)); var r = F(g, 400, C(1, 36, 36, S)); None(r); Equal(0, r.PreviewCellIndex);
            r = F(g, 420, C(1, 36, 36, E)); Equal(MinesweeperGestureAction.ToggleFlag, r.Action); Equal(-1, r.PreviewCellIndex);
        });
        Check("long hold with no intermediate frames flags", () => {
            var g = G(); F(g, 0, C(1, 36, 36, B)); Equal(MinesweeperGestureAction.ToggleFlag, F(g, 400, C(1, 36, 36, E)).Action);
        });
        Check("long hold opened cell does not fall through to short press", () => {
            var g = G(); g.ProcessFrame(Frame(0, true, true, false, C(1, 36, 36, B)), NoFlag);
            var r = g.ProcessFrame(Frame(400, true, true, false, C(1, 36, 36, S)), NoFlag); Equal(-1, r.PreviewCellIndex);
            None(g.ProcessFrame(Frame(410, true, true, false, C(1, 36, 36, E)), NoFlag));
        });
        Check("right mouse release flags and opened target is inert", () => {
            var g = G(); F(g, 0, C(1, 36, 36, B, true)); Equal(MinesweeperGestureAction.ToggleFlag, F(g, 20, C(1, 36, 36, E, true)).Action);
            g.ProcessFrame(Frame(100, true, true, false, C(1, 36, 36, B, true)), NoFlag);
            None(g.ProcessFrame(Frame(110, true, true, false, C(1, 36, 36, E, true)), NoFlag));
        });
        Check("exactly 12 design units can tap, above 12 cannot", () => {
            var g = G(); F(g, 0, C(1, 30, 30, B)); Equal(MinesweeperGestureAction.ShortPress, F(g, 20, C(1, 42, 30, E)).Action);
            F(g, 30, C(1, 30, 30, B)); None(F(g, 40, C(1, 42.01, 30, E)));
        });
        Check("final UP position cancels without a MOVE event", () => {
            var g = G(); F(g, 0, C(1, 36, 36, B)); None(F(g, 500, C(1, 70, 36, E)));
            F(g, 600, C(1, 71, 36, B)); None(F(g, 610, C(1, 73, 36, E)));
        });
        Check("moving across a cell and back loses click eligibility", () => {
            var g = G(); F(g, 0, C(1, 71, 36, B)); F(g, 20, C(1, 73, 36, M)); None(F(g, 30, C(1, 71, 36, E)));
        });
        Check("drag and return cannot tap or hold", () => {
            var g = G(); F(g, 0, C(1, 36, 36, B)); F(g, 500, C(1, 60, 36, M)); None(F(g, 600, C(1, 36, 36, E)));
        });
        Check("single drag pans including final UP delta", () => {
            var v = V(); v.RestoreNormal(1, 15, 8); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 300, 300, B)); True(F(g, 20, C(1, 320, 300, M)).ViewChanged); Near(15 - 20.0 / 72, v.CenterColumn);
            F(g, 30, C(1, 340, 300, E)); Near(15 - 40.0 / 72, v.CenterColumn);
        });
        Check("second pointer cancels hold preview", () => {
            var g = G(); F(g, 0, C(1, 36, 36, B)); Equal(0, F(g, 400, C(1, 36, 36, S)).PreviewCellIndex);
            var r = F(g, 410, C(1, 36, 36, S), C(2, 120, 36, B)); Equal(-1, r.PreviewCellIndex); None(r);
            None(F(g, 420, C(1, 36, 36, E), C(2, 120, 36, E)));
        });
        Check("UP plus second DOWN is order-independent and never clicks", () => {
            for (int order = 0; order < 2; order++) {
                var g = G(); F(g, 0, C(1, 36, 36, B)); var up = C(1, 36, 36, E); var down = C(2, 120, 36, B);
                None(order == 0 ? F(g, 100, up, down) : F(g, 100, down, up)); None(F(g, 120, C(2, 120, 36, E)));
            }
        });
        Check("pinch threshold does not backfill and resets below 12", () => {
            var v = V(); v.RestoreNormal(1, 15, 8); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 300, 300, B), C(2, 305, 300, B)); F(g, 10, C(1, 300, 300, S), C(2, 312, 300, M)); Near(1, v.ZoomScale);
            F(g, 20, C(1, 300, 300, S), C(2, 324, 300, M)); Near(2, v.ZoomScale);
            F(g, 30, C(1, 300, 300, S), C(2, 305, 300, M)); Near(2, v.ZoomScale);
            F(g, 40, C(1, 300, 300, S), C(2, 320, 300, M)); Near(2, v.ZoomScale);
            F(g, 50, C(1, 300, 300, S), C(2, 315, 300, M)); Near(1.5, v.ZoomScale);
        });
        Check("pinch at upper bound updates baseline for immediate reversal", () => {
            var v = V(); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 100, 100, B), C(2, 200, 100, B));
            F(g, 10, C(1, 100, 100, S), C(2, 400, 100, M)); Near(2, v.ZoomScale);
            F(g, 20, C(1, 100, 100, S), C(2, 500, 100, M)); Near(2, v.ZoomScale);
            F(g, 30, C(1, 100, 100, S), C(2, 460, 100, M)); Near(1.8, v.ZoomScale);
        });
        Check("pinch at lower bound updates baseline for immediate reversal", () => {
            var v = V(); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 100, 100, B), C(2, 300, 100, B)); F(g, 10, C(1, 100, 100, S), C(2, 150, 100, M)); Near(.5, v.ZoomScale);
            F(g, 20, C(1, 100, 100, S), C(2, 125, 100, M)); Near(.5, v.ZoomScale);
            F(g, 30, C(1, 100, 100, S), C(2, 130, 100, M)); Near(.6, v.ZoomScale);
        });
        Check("pinch threshold stays 12 at half scale", () => {
            var v = V(); v.RestoreNormal(.5, 15, 8); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 100, 100, B), C(2, 106, 100, B)); F(g, 20, C(1, 100, 100, S), C(2, 110, 100, M)); Near(.5, v.ZoomScale);
        });
        Check("two to one rebases pan and rejoining establishes fresh pinch", () => {
            var v = V(); v.RestoreNormal(1, 15, 8); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 200, 300, B), C(2, 300, 300, B)); F(g, 20, C(1, 200, 300, E), C(2, 300, 300, S)); Near(15, v.CenterColumn);
            F(g, 30, C(2, 320, 300, M)); Near(15 - 20.0 / 72, v.CenterColumn);
            F(g, 40, C(2, 320, 300, S), C(3, 600, 300, B)); Near(1, v.ZoomScale);
            None(F(g, 50, C(2, 320, 300, E), C(3, 600, 300, E)));
            F(g, 100, C(4, 36, 36, B)); Equal(MinesweeperGestureAction.ShortPress, F(g, 120, C(4, 36, 36, E)).Action);
        });
        Check("legitimate ended frame applies one final pair transform", () => {
            var v = V(); v.RestoreNormal(1, 15, 8); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 200, 300, B), C(2, 300, 300, B)); var r = F(g, 10, C(2, 330, 300, E), C(1, 210, 300, E));
            Near(1.2, v.ZoomScale); True(r.ViewChanged); True(r.GestureCompleted); None(r);
        });
        Check("third pointer freezes last legal view through remaining releases", () => {
            var v = V(); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 100, 100, B), C(2, 200, 100, B));
            F(g, 20, C(1, 100, 100, S), C(2, 300, 100, M), C(3, 400, 100, B)); Near(1, v.ZoomScale); True(g.IsLocked);
            F(g, 30, C(1, 100, 100, S), C(2, 400, 100, M), C(3, 400, 100, E)); Near(1, v.ZoomScale);
            F(g, 40, C(1, 100, 100, E), C(2, 400, 100, E)); False(g.IsLocked);
        });
        Check("outside start cannot join by sliding into viewport", () => {
            var g = G(); F(g, 0, C(1, 36, 36, B), C(2, -10, 100, B)); True(g.IsLocked);
            None(F(g, 20, C(1, 36, 36, E), C(2, 100, 100, M))); True(g.IsLocked); None(F(g, 30, C(2, 100, 100, E)));
        });
        Check("leaving viewport locks and return cannot click", () => {
            var g = G(); F(g, 0, C(1, 36, 36, B)); None(F(g, 10, C(1, -1, 36, M))); True(g.IsLocked);
            None(F(g, 20, C(1, 36, 36, E))); False(g.IsLocked);
        });
        Check("canceled phase wins over a simultaneous normal UP", () => {
            var g = G(); F(g, 0, C(1, 36, 36, B), C(2, 120, 36, B));
            None(F(g, 500, C(1, 36, 36, E), C(2, 120, 36, X)));
        });
        Check("missing tracked point cancels entire round", () => {
            var g = G(); F(g, 0, C(1, 36, 36, B), C(2, 120, 36, B)); None(F(g, 10, C(1, 36, 36, S))); True(g.IsLocked);
            None(F(g, 20, C(1, 36, 36, E)));
        });
        Check("cancel and re-enable require all contacts released", () => {
            var g = G(); F(g, 0, C(1, 36, 36, B)); g.Cancel(); True(g.IsLocked);
            None(F(g, 10, C(1, 36, 36, S))); None(F(g, 20, C(1, 36, 36, E))); False(g.IsLocked);
            F(g, 30, C(2, 36, 36, B)); Equal(MinesweeperGestureAction.ShortPress, F(g, 40, C(2, 36, 36, E)).Action);
        });
        Check("new instance never treats a stationary contact as DOWN", () => {
            var g = G(); None(F(g, 100, C(1, 36, 36, S))); True(g.IsLocked); None(F(g, 110, C(1, 36, 36, E)));
        });
        Check("paused frames cancel all transforms and pending clicks", () => {
            var v = V(); var g = new MinesweeperGesture(v); F(g, 0, C(1, 36, 36, B));
            None(g.ProcessFrame(Frame(500, false, false, false, C(1, 60, 36, M)), Flag)); True(g.IsLocked); Near(1, v.ZoomScale);
            None(F(g, 510, C(1, 36, 36, E)));
        });
        Check("terminal allows view transformations but no cell actions", () => {
            var v = V(); v.RestoreNormal(1, 15, 8); var g = new MinesweeperGesture(v);
            g.ProcessFrame(Frame(0, true, false, false, C(1, 300, 300, B)), Flag);
            var r = g.ProcessFrame(Frame(100, true, false, false, C(1, 320, 300, E)), Flag); None(r); True(r.ViewChanged);
        });
        Check("mode or game identity changes invalidate pending hand", () => {
            var g = G(); F(g, 0, C(1, 36, 36, B)); None(g.ProcessFrame(Frame(100, true, true, true, C(1, 36, 36, E)), Flag));
            F(g, 200, C(1, 36, 36, B)); None(g.ProcessFrame(new MinesweeperInputFrame(210, "different", true, true, false, new[] {C(1,36,36,E)}), Flag));
        });
        Check("overview short tap locates once without a cell action", () => {
            var v = V(); v.EnterOverview(); var target = v.BoardToViewport(P(20.25, 8.25)); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, target.X, target.Y, B)); var r = F(g, 30, C(1, target.X, target.Y, E));
            Equal(MinesweeperGestureAction.OverviewLocated, r.Action); False(v.IsOverview); Near(20.25, v.CenterColumn); Near(8.25, v.CenterRow);
        });
        Check("overview long hold stays overview without preview", () => {
            var v = V(); v.EnterOverview(); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 300, 300, B)); Equal(-1, F(g, 400, C(1, 300, 300, S)).PreviewCellIndex);
            None(F(g, 420, C(1, 300, 300, E))); True(v.IsOverview);
        });
        Check("overview drag and two pointers never transform or locate", () => {
            var v = V(); v.EnterOverview(); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 300, 300, B)); None(F(g, 100, C(1, 330, 300, E))); True(v.IsOverview);
            F(g, 200, C(1, 300, 300, B), C(2, 400, 300, B)); None(F(g, 210, C(1, 250, 300, E), C(2, 450, 300, E)));
            True(v.IsOverview); Near(1, v.ZoomScale);
        });
        Check("overview whitespace press stays overview", () => {
            var v = V(); v.EnterOverview(); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 300, 5, B)); None(F(g, 100, C(1, 300, 5, E))); True(v.IsOverview);
        });
        Check("whitespace supports two-point pan and zoom", () => {
            var v = new MinesweeperViewport(9, 9, 648, 760); v.RestoreNormal(.5, 4.5, 4.5); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 100, 100, B), C(2, 200, 100, B)); F(g, 20, C(1, 100, 100, S), C(2, 300, 100, M)); Near(1, v.ZoomScale);
        });
        Check("nonfinite contacts never mutate view", () => {
            var v = V(); var g = new MinesweeperGesture(v); F(g, 0, C(1, 36, 36, B)); None(F(g, 30, C(1, double.NaN, 36, E))); Near(1, v.ZoomScale);
        });
        Check("stationary touches preserve layout-clamped desired center", () => {
            var v = V(); v.RestoreNormal(1, 2, 2); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 100, 100, B), C(2, 200, 100, B));
            F(g, 10, C(1, 100, 100, S), C(2, 200, 100, S)); Near(2, v.CenterColumn); Near(2, v.CenterRow);
            F(g, 20, C(1, 100, 100, E), C(2, 200, 100, S));
            F(g, 30, C(2, 200, 100, S)); Near(2, v.CenterColumn); Near(2, v.CenterRow);
        });
        Check("direct zero view transforms preserve desired position", () => {
            var v = V(); v.RestoreNormal(1, 2, 2); v.Pan(0, 0); Near(2, v.CenterColumn);
            v.Transform(P(100, 100), P(100, 100), 1); Near(2, v.CenterRow);
        });
        Check("final UP outside cancels an armed hold with no MOVE", () => {
            var g = G(); F(g, 0, C(1, 36, 36, B)); F(g, 400, C(1, 36, 36, S));
            None(F(g, 410, C(1, -1, 36, E))); False(g.IsLocked);
        });
        Check("view transformation is invariant to contact iteration order", () => {
            var v1 = V(); var v2 = V(); v1.RestoreNormal(1, 15, 8); v2.RestoreNormal(1, 15, 8);
            var g1 = new MinesweeperGesture(v1); var g2 = new MinesweeperGesture(v2);
            F(g1, 0, C(1, 200, 200, B), C(2, 300, 200, B)); F(g2, 0, C(2, 300, 200, B), C(1, 200, 200, B));
            F(g1, 10, C(1, 220, 210, M), C(2, 340, 230, M)); F(g2, 10, C(2, 340, 230, M), C(1, 220, 210, M));
            Near(v1.ZoomScale, v2.ZoomScale); Near(v1.CenterColumn, v2.CenterColumn); Near(v1.CenterRow, v2.CenterRow);
        });
        Check("terminal overview still permits short-tap location", () => {
            var v = V(); v.EnterOverview(); var p = v.BoardToViewport(P(15, 8)); var g = new MinesweeperGesture(v);
            g.ProcessFrame(Frame(0, true, false, false, C(1, p.X, p.Y, B)), NoFlag);
            var r = g.ProcessFrame(Frame(100, true, false, false, C(1, p.X, p.Y, E)), NoFlag);
            Equal(MinesweeperGestureAction.OverviewLocated, r.Action); False(v.IsOverview);
        });
        Check("small-scale movement threshold does not shrink with cells", () => {
            var v = V(); v.RestoreNormal(.5, 15, 8); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 300, 370, B)); Equal(MinesweeperGestureAction.ShortPress, F(g, 100, C(1, 310, 370, E)).Action);
        });
        Check("duplicate contact IDs cancel safely before any transform", () => {
            var v = V(); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 36, 36, B)); None(F(g, 10, C(1, 36, 36, E), C(1, 100, 100, B))); True(g.IsLocked); Near(1, v.ZoomScale);
        });
        Check("overview secondary-button tap cannot locate", () => {
            var v = V(); v.EnterOverview(); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 300, 300, B, true)); None(F(g, 100, C(1, 300, 300, E, true))); True(v.IsOverview);
        });
        Check("normal small-board blank area cannot tap or flag", () => {
            var v = new MinesweeperViewport(9, 9, 648, 760); v.RestoreNormal(.5, 4.5, 4.5); var g = new MinesweeperGesture(v);
            F(g, 0, C(1, 100, 100, B)); None(F(g, 100, C(1, 100, 100, E)));
            F(g, 200, C(1, 100, 100, B)); None(F(g, 700, C(1, 100, 100, E)));
        });
        Check("overview visible rectangle uses remembered normal rendering", () => {
            var v = V(); v.RestoreNormal(1, 15, 8); var expected = v.NormalVisibleBounds; v.EnterOverview();
            var actual = v.NormalVisibleBounds; Near(expected.X, actual.X); Near(expected.Y, actual.Y);
            Near(9, actual.Width); Near(760.0 / 72, actual.Height);
        });
        Check("input frame owns an immutable contact snapshot", () => {
            var contacts = new[] { C(1, 36, 36, B) }; var frame = Frame(0, true, true, false, contacts);
            contacts[0] = C(1, 100, 100, E); Equal(B, frame.Contacts[0].Phase); Near(36, frame.Contacts[0].Position.X);
        });
        MinesweeperControlLayoutChecks.Run(Check);
        Console.WriteLine("Minesweeper input checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }
    private const MinesweeperInputPhase B = MinesweeperInputPhase.Began, M = MinesweeperInputPhase.Moved, S = MinesweeperInputPhase.Stationary, E = MinesweeperInputPhase.Ended, X = MinesweeperInputPhase.Canceled;
    private static MinesweeperViewport V() { return new MinesweeperViewport(30, 16, 648, 760); }
    private static MinesweeperGesture G() { return new MinesweeperGesture(V()); }
    private static MinesweeperInputPosition P(double x, double y) { return new MinesweeperInputPosition(x, y); }
    private static MinesweeperInputContact C(int id, double x, double y, MinesweeperInputPhase phase, bool secondary = false) { return new MinesweeperInputContact(id, x, y, phase, secondary); }
    private static MinesweeperInputFrame Frame(long t, bool view, bool cell, bool flagMode, params MinesweeperInputContact[] contacts) { return new MinesweeperInputFrame(t, "game", view, cell, flagMode, contacts); }
    private static MinesweeperGestureResult F(MinesweeperGesture g, long t, params MinesweeperInputContact[] contacts) { return g.ProcessFrame(Frame(t, true, true, false, contacts), Flag); }
    private static bool Flag(int index) { return true; }
    private static bool NoFlag(int index) { return false; }
    private static void Check(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); } }
    private static void None(MinesweeperGestureResult r) { Equal(MinesweeperGestureAction.None, r.Action); }
    private static void True(bool value) { if (!value) throw new Exception("expected true"); }
    private static void False(bool value) { True(!value); }
    private static void Near(double expected, double actual) { if (Math.Abs(expected - actual) > 0.0000001 || double.IsNaN(actual)) throw new Exception("expected " + expected + ", got " + actual); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception("expected " + expected + ", got " + actual); }
    private static void Throws(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("expected ArgumentException"); }
}
