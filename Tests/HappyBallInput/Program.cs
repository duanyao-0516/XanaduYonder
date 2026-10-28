using System;
using XanaduYonder.Runtime.HappyBall;

internal static class Program
{
    private static int passed, failed;
    private static int Main()
    {
        Check("Continuous moves and final UP normalize to viewport width", Continuous);
        Check("Release stops rotation without inertia", Release);
        Check("Rejected rotation does not accumulate and reversal is immediate", RejectedDelta);
        Check("Second and third contacts cancel and lock until all up", MultiTouch);
        Check("Owner UP plus second DOWN cancels in either order", Arbitration);
        Check("A UI began finger cannot slide into play", UiBegin);
        Check("Canceled, outside and lost owner discard final displacement", InvalidRounds);
        Check("Modal, focus and disable cancel cannot resume an old pointer", ExternalCancel);
        Check("Duplicate begin, id reuse and bad coordinates cancel", InvalidFrames);
        Check("Cancellation discards queued deltas but normal UP preserves its endpoint", CancellationSignal);
        Check("Screen gesture edges reject only initial acquisition", ScreenEdges);
        Console.WriteLine("HappyBall input checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }
    private static HappyBallPointer P(int id, double x, HappyBallPointerPhase phase, bool inside = true, bool eligible = true)
    { return new HappyBallPointer { Id = id, X = x, Phase = phase, InPlayArea = inside, CanBegin = eligible }; }
    private static double Frame(HappyBallDragGesture g, params HappyBallPointer[] p) { return g.ProcessFrame(p, true, 100); }
    private static HappyBallDragGesture Started()
    { var g = new HappyBallDragGesture(); Near(0, Frame(g, P(7, 10, HappyBallPointerPhase.Began))); Assert(g.IsDragging, "begin must acquire"); return g; }
    private static void Continuous()
    {
        var g = Started(); Near(Math.PI * .3, Frame(g, P(7, 30, HappyBallPointerPhase.Moved)));
        Near(-Math.PI * .15, Frame(g, P(7, 20, HappyBallPointerPhase.Moved)));
        Near(0, Frame(g, P(7, 20, HappyBallPointerPhase.Stationary)));
        Near(Math.PI * 1.5, Frame(g, P(7, 120, HappyBallPointerPhase.Ended)));
        Assert(!g.IsDragging && !g.IsLocked, "normal release ends ownership");
        var wide = new HappyBallDragGesture(); wide.ProcessFrame(new[] { P(1, 0, HappyBallPointerPhase.Began) }, true, 200);
        Near(Math.PI * 1.5, wide.ProcessFrame(new[] { P(1, 200, HappyBallPointerPhase.Moved) }, true, 200));
    }
    private static void Release()
    {
        var g = Started(); Frame(g, P(7, 20, HappyBallPointerPhase.Ended));
        for (int i = 0; i < 5; i++) Near(0, Frame(g));
        Near(0, Frame(g, P(8, 99, HappyBallPointerPhase.Began)));
        Near(Math.PI * .015, Frame(g, P(8, 100, HappyBallPointerPhase.Moved)));
    }
    private static void RejectedDelta()
    {
        var g = Started(); double accepted = Frame(g, P(7, 20, HappyBallPointerPhase.Moved));
        double rejected = Frame(g, P(7, 80, HappyBallPointerPhase.Moved)); Assert(rejected > 0, "emit candidate once");
        Near(-Math.PI * .015, Frame(g, P(7, 79, HappyBallPointerPhase.Moved)));
        Near(Math.PI * .15, accepted);
    }
    private static void MultiTouch()
    {
        for (int count = 2; count <= 3; count++)
        {
            var g = Started(); var p = new HappyBallPointer[count]; p[0] = P(7, 80, HappyBallPointerPhase.Moved);
            for (int i = 1; i < count; i++) p[i] = P(i, 10, HappyBallPointerPhase.Began);
            Near(0, Frame(g, p)); Assert(g.IsLocked && !g.IsDragging, "multi touch cancels");
            Near(0, Frame(g, P(7, 90, HappyBallPointerPhase.Moved))); Assert(g.IsLocked, "remaining finger cannot take over");
            Near(0, Frame(g, P(7, 100, HappyBallPointerPhase.Ended))); Assert(!g.IsLocked, "all-up unlocks");
            Near(0, Frame(g, P(8, 10, HappyBallPointerPhase.Began))); Assert(g.IsDragging, "fresh down resumes");
        }
    }
    private static void Arbitration()
    {
        for (int order = 0; order < 2; order++)
        {
            var g = Started(); var up = P(7, 100, HappyBallPointerPhase.Ended); var down = P(9, 100, HappyBallPointerPhase.Began);
            Near(0, Frame(g, order == 0 ? new[] { up, down } : new[] { down, up }));
            Assert(g.IsLocked, "same-frame secondary owns cancellation");
            Near(0, Frame(g, P(9, 200, HappyBallPointerPhase.Moved)));
        }
    }
    private static void UiBegin()
    {
        var g = new HappyBallDragGesture(); Near(0, Frame(g, P(1, 0, HappyBallPointerPhase.Began, true, false)));
        Near(0, Frame(g, P(1, 80, HappyBallPointerPhase.Moved))); Assert(!g.IsDragging && g.IsLocked, "UI touch stays excluded");
        Frame(g, P(1, 80, HappyBallPointerPhase.Ended)); Frame(g, P(2, 10, HappyBallPointerPhase.Began)); Assert(g.IsDragging, "new round can start");
    }
    private static void InvalidRounds()
    {
        var g = Started(); Near(0, Frame(g, P(7, 100, HappyBallPointerPhase.Canceled))); Assert(!g.IsDragging, "cancel ends");
        g = Started(); Near(0, Frame(g, P(7, 100, HappyBallPointerPhase.Moved, false))); Assert(g.IsLocked, "leave locks");
        Near(0, Frame(g, P(7, 10, HappyBallPointerPhase.Moved))); Frame(g, P(7, 10, HappyBallPointerPhase.Ended));
        g = Started(); Near(0, Frame(g)); Assert(!g.IsDragging, "missing owner canceled");
        Near(0, Frame(g, P(7, 100, HappyBallPointerPhase.Moved))); Assert(!g.IsDragging, "missing owner never restored");
        g = Started(); Near(0, Frame(g, P(8, 100, HappyBallPointerPhase.Began))); Assert(g.IsLocked, "new id cannot replace lost owner");
    }
    private static void ExternalCancel()
    {
        var g = Started(); Near(0, g.ProcessFrame(new[] { P(7, 100, HappyBallPointerPhase.Moved) }, false, 100));
        Assert(g.IsLocked, "modal locks"); Near(0, Frame(g, P(7, 120, HappyBallPointerPhase.Moved)));
        Frame(g, P(7, 120, HappyBallPointerPhase.Ended)); Frame(g, P(8, 10, HappyBallPointerPhase.Began));
        g.Cancel(); Assert(g.IsLocked && !g.IsDragging, "explicit cancel clears");
        Near(0, Frame(g, P(8, 100, HappyBallPointerPhase.Moved))); Frame(g);
        Frame(g, P(9, 0, HappyBallPointerPhase.Began)); Near(Math.PI * .15, Frame(g, P(9, 10, HappyBallPointerPhase.Moved)));
    }
    private static void InvalidFrames()
    {
        var g = Started(); Near(0, Frame(g, P(7, 30, HappyBallPointerPhase.Began))); Assert(g.IsLocked, "repeated begin");
        g = Started(); Near(0, Frame(g, P(7, double.NaN, HappyBallPointerPhase.Moved))); Assert(!g.IsDragging, "nonfinite x");
        g = Started(); Near(0, g.ProcessFrame(new[] { P(7, 30, HappyBallPointerPhase.Moved) }, true, 0)); Assert(!g.IsDragging, "zero viewport");
        g = Started(); Near(0, Frame(g, P(7, 30, (HappyBallPointerPhase)99))); Assert(!g.IsDragging, "unknown phase");
    }
    private static void CancellationSignal()
    {
        var g = Started(); double queued = Frame(g, P(7, 20, HappyBallPointerPhase.Moved));
        Assert(queued > 0 && !g.WasCanceled, "legal move unexpectedly canceled");
        Frame(g, P(7, 40, HappyBallPointerPhase.Moved), P(8, 50, HappyBallPointerPhase.Began));
        if (g.WasCanceled) queued = 0;
        Near(0, queued); Assert(g.WasCanceled, "second finger must clear uncommitted queue");
        Frame(g, P(7, 80, HappyBallPointerPhase.Moved)); Assert(!g.WasCanceled, "do not repeat cancellation while locked");
        g = Started(); Frame(g); Assert(g.WasCanceled, "lost owner must signal cancellation even with no live contact");
        Frame(g); Assert(!g.WasCanceled, "empty idle frame is not canceled");
        g = Started(); Frame(g, P(7, 60, HappyBallPointerPhase.Moved, false)); Assert(g.WasCanceled, "outside cancels queue");
        g = Started(); Frame(g, P(7, 60, HappyBallPointerPhase.Canceled)); Assert(g.WasCanceled, "platform cancellation clears queue");
        g = Started(); Near(Math.PI * .15, Frame(g, P(7, 20, HappyBallPointerPhase.Ended)));
        Assert(!g.WasCanceled, "normal release must retain final displacement");
        g = Started(); g.Cancel(); Assert(g.WasCanceled, "explicit cancel clears queue");
    }
    private static void ScreenEdges()
    {
        foreach (double width in new[] { 360d, 720d, 1440d })
        {
            double edge = width * 16 / 720;
            Assert(!HappyBallDragGesture.AllowsScreenStart(0, width), "left edge admitted");
            Assert(!HappyBallDragGesture.AllowsScreenStart(edge - .001, width), "inside left band admitted");
            Assert(HappyBallDragGesture.AllowsScreenStart(edge, width), "left boundary excluded");
            Assert(HappyBallDragGesture.AllowsScreenStart(width - edge, width), "right boundary excluded");
            Assert(!HappyBallDragGesture.AllowsScreenStart(width - edge + .001, width), "inside right band admitted");
            Assert(!HappyBallDragGesture.AllowsScreenStart(width, width), "right edge admitted");
        }
        Assert(!HappyBallDragGesture.AllowsScreenStart(double.NaN, 720) && !HappyBallDragGesture.AllowsScreenStart(20, 0), "invalid screen geometry admitted");
        var g = new HappyBallDragGesture(); Frame(g, P(1, 30, HappyBallPointerPhase.Began));
        Near(-Math.PI * .42, Frame(g, P(1, 2, HappyBallPointerPhase.Moved, true, HappyBallDragGesture.AllowsScreenStart(2, 720))));
        Assert(g.IsDragging, "owned drag canceled at system-edge band");
    }
    private static void Check(string name, Action check) { try { check(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Near(double expected, double actual) { Assert(Math.Abs(expected - actual) < 1e-10, "Expected " + expected + ", got " + actual); }
}
