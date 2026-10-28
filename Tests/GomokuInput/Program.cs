using System;
using XanaduYonder.Runtime.Gomoku;

internal static class Program
{
    private static int passed, failed;
    private const GomokuInputPhase B = GomokuInputPhase.Began, M = GomokuInputPhase.Moved,
        S = GomokuInputPhase.Stationary, E = GomokuInputPhase.Ended, X = GomokuInputPhase.Canceled;

    private static int Main()
    {
        Check("all four corners and H8/H10 match displayed coordinates", () => {
            Equal(0, Hit(52, 52)); Equal(14, Hit(596, 52));
            Equal(210, Hit(52, 596)); Equal(224, Hit(596, 596));
            Equal(112, Hit(324, 324)); Equal(82, Hit(324, 52 + 5 * Pitch));
            Equal("A", GomokuBoardGeometry.ColumnLabel(0)); Equal("O", GomokuBoardGeometry.ColumnLabel(14));
            Equal("15", GomokuBoardGeometry.RowLabel(0)); Equal("1", GomokuBoardGeometry.RowLabel(14));
        });
        Check("every rendered intersection maps back to the same cell", () => {
            for (int i = 0; i < 225; i++) Equal(i, Hit(GomokuBoardGeometry.XAt(i), GomokuBoardGeometry.YAt(i)));
        });
        Check("half-cell outer border is inclusive but coordinate gutters are inert", () => {
            double lower = 52 - Pitch / 2, upper = 596 + Pitch / 2;
            Equal(0, Hit(lower, lower)); Equal(224, Hit(upper, upper));
            Equal(-1, Hit(lower - .001, 324)); Equal(-1, Hit(upper + .001, 324));
            Equal(-1, Hit(324, lower - .001)); Equal(-1, Hit(324, upper + .001));
            Equal(-1, Hit(22, 324)); Equal(-1, Hit(626, 324)); Equal(-1, Hit(324, 22)); Equal(-1, Hit(324, 626));
            Equal(-1, Hit(double.NaN, 52)); Equal(-1, Hit(52, double.PositiveInfinity));
        });
        Check("single tap selects once on release only", () => {
            var g = new GomokuInput(); None(F(g, C(1, B))); None(F(g, C(1, S)));
            Equal(112, F(g, C(1, E))); None(F(g, C(1, E))); None(F(g));
        });
        Check("touch must originate on this board and in its grid", () => {
            var g = new GomokuInput(); None(F(g, C(1, B, 324, 324, false))); None(F(g, C(1, E)));
            None(F(g, C(2, B, 22, 324))); None(F(g, C(2, M))); None(F(g, C(2, E)));
        });
        Check("exactly 12 design units allowed; farther movement cancels", () => {
            var g = new GomokuInput(); F(g, C(1, B)); Equal(112, F(g, C(1, E, 336, 324)));
            F(g, C(2, B)); None(F(g, C(2, E, 336.001, 324)));
        });
        Check("drag out and back cannot select", () => {
            var g = new GomokuInput(); F(g, C(1, B)); None(F(g, C(1, M, 350, 324))); None(F(g, C(1, E)));
        });
        Check("crossing a cell boundary invalidates even a short motion", () => {
            double edge = 324 + Pitch / 2; var g = new GomokuInput();
            F(g, C(1, B, edge - 1, 324)); None(F(g, C(1, M, edge + 1, 324))); None(F(g, C(1, E, edge - 1, 324)));
        });
        Check("final release position alone can cancel", () => {
            var g = new GomokuInput(); F(g, C(1, B)); None(F(g, C(1, E, 22, 324)));
        });
        Check("a different pointer cannot release the originating pointer", () => {
            var g = new GomokuInput(); F(g, C(1, B)); None(F(g, C(2, E))); None(F(g, C(1, E)));
        });
        Check("second finger anywhere cancels until every finger lifts", () => {
            var g = new GomokuInput(); F(g, C(1, B)); None(F(g, C(1, S), C(2, B, -100, -100, false)));
            None(F(g, C(1, E), C(2, S))); None(F(g, C(2, M))); None(F(g, C(2, E)));
            F(g, C(3, B)); Equal(112, F(g, C(3, E)));
        });
        Check("release plus second down is rejected in either contact order", () => {
            for (int order = 0; order < 2; order++) {
                var g = new GomokuInput(); F(g, C(1, B));
                None(order == 0 ? F(g, C(1, E), C(2, B)) : F(g, C(2, B), C(1, E)));
                None(F(g, C(2, E)));
            }
        });
        Check("system canceled phase never becomes a release selection", () => {
            var g = new GomokuInput(); F(g, C(1, B)); None(F(g, C(1, X))); None(F(g, C(1, E)));
            F(g, C(2, B)); Equal(112, F(g, C(2, E)));
        });
        Check("explicit cancel suppresses held pointer across modal dismissal", () => {
            var g = new GomokuInput(); F(g, C(1, B)); g.Cancel();
            None(F(g, C(1, S))); None(F(g, C(1, E))); F(g, C(2, B)); Equal(112, F(g, C(2, E)));
        });
        Check("disabled and resumed input needs a fresh down", () => {
            var g = new GomokuInput(); F(g, C(1, B)); None(g.ProcessFrame(false, new[] { C(1, S) }));
            None(F(g, C(1, S))); None(F(g, C(1, E))); F(g, C(2, B)); Equal(112, F(g, C(2, E)));
        });
        Check("new view cannot inherit stationary or moving fingers", () => {
            var g = new GomokuInput(); None(F(g, C(1, S))); None(F(g, C(1, E)));
            None(F(g, C(2, M))); None(F(g, C(2, E))); F(g, C(3, B)); Equal(112, F(g, C(3, E)));
        });
        Check("missing contact cancels and next physical down can begin", () => {
            var g = new GomokuInput(); F(g, C(1, B)); None(F(g)); None(F(g, C(1, E)));
            F(g, C(2, B)); Equal(112, F(g, C(2, E)));
        });
        Check("replacement down without release remains locked", () => {
            var g = new GomokuInput(); F(g, C(1, B)); None(F(g, C(2, B))); None(F(g, C(2, E)));
            F(g, C(3, B)); Equal(112, F(g, C(3, E)));
        });
        Check("nonfinite input cancels a pending selection", () => {
            var g = new GomokuInput(); F(g, C(1, B)); None(F(g, C(1, M, double.NaN, 324))); None(F(g, C(1, E)));
        });
        Console.WriteLine("Gomoku input: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    private static double Pitch { get { return 544.0 / 14; } }
    private static int Hit(double x, double y) { return GomokuBoardGeometry.HitTest(x, y); }
    private static GomokuContact C(int id, GomokuInputPhase phase, double x = 324, double y = 324, bool beganOnBoard = true)
    { return new GomokuContact(id, x, y, phase, beganOnBoard); }
    private static int F(GomokuInput input, params GomokuContact[] contacts) { return input.ProcessFrame(true, contacts); }
    private static void None(int actual) { Equal(-1, actual); }
    private static void Equal<T>(T expected, T actual)
    { if (!Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual); }
    private static void Check(string name, Action run)
    { try { run(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
}
