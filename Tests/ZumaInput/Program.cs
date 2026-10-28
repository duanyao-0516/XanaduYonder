using System;
using XanaduYonder.Runtime.Zuma;

internal static class Program
{
    private static int count;
    private static void Check(bool value, string description) { if (!value) throw new Exception(description); count++; }
    private static void Main()
    {
        var g = new ZumaAimGesture();
        Check(!g.Begin(1, false, true), "UI/letterbox down must not start aiming");
        Check(!g.Begin(1, true, false), "Hidden/paused page rejects down");
        Check(g.Begin(7, true, true) && g.IsAiming, "Down begins immediately without long-press delay");
        Check(!g.Begin(8, true, true) && !g.Owns(8) && g.Owns(7), "Second finger cannot steal aim");
        Check(!g.Release(8, false, true, true) && g.IsAiming, "Unrelated UP must preserve owner");
        Check(g.Release(7, false, true, true) && !g.IsAiming, "Owner UP fires once");
        Check(!g.Release(7, false, true, true), "Duplicate UP does not fire");
        g.Begin(2, true, true); Check(!g.Release(2, true, true, true), "Canceled touch never fires");
        g.Begin(2, true, true); Check(!g.Release(2, false, false, true), "Outside release cancels");
        g.Begin(2, true, true); Check(!g.Release(2, false, true, false) && !g.IsAiming, "Cooldown release is rejected without queue");
        g.Begin(2, true, true); g.Cancel(); Check(!g.Release(2, false, true, true), "Suspend/rotate/swap cancels stale UP");
        var v = ZumaViewportMath.Fit(100, 20, 2200, 1080);
        Check(Math.Abs(v.Width - 1920) < .001 && Math.Abs(v.Left - 240) < .001, "Wide display fits16:9 inside safearea");
        float x,y;
        Check(v.ToDesign(1200, 560, out x, out y) && Math.Abs(x-640)<.001 && Math.Abs(y-360)<.001, "Center coordinates invert with top-left origin");
        Check(!v.ToDesign(120, 560, out x, out y), "Letterbox rejects input");
        Check(v.ToDesign(240, 1100, out x, out y) && x==0 && y==0, "Top-left origin maps exactly");
        Check(!v.ToDesign(float.NaN, 560, out x, out y), "Nonfinite input is rejected");
        var portrait = ZumaViewportMath.Fit(0, 80, 1080, 2200);
        Check(Math.Abs(portrait.Width-1080)<.001 && Math.Abs(portrait.Height-607.5f)<.001, "Fallback maintains aspect when orientation request fails");
        var empty = ZumaViewportMath.Fit(0,0,0,0);
        Check(!empty.ToDesign(0,0,out x,out y), "Zero-size transition never receives input");
        Console.WriteLine("Zuma input checks: " + count + " passed, 0 failed.");
    }
}
