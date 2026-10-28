using System;
using XanaduYonder.Runtime.Dino;

internal static class Program
{
    private static int count;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; }
    private static int Main()
    {
        try
        {
            var owners = new DinoActionOwners();
            Check(owners.PressJump(3), "first jump owns action");
            Check(!owners.PressJump(4), "stage cannot take button ownership");
            Check(!owners.ReleaseJump(4), "nonowner release cannot shorten jump");
            Check(owners.PressDuck(4), "second finger independently ducks");
            Check(owners.ReleaseJump(3), "owner release emits once");
            Check(!owners.ReleaseJump(3), "duplicate up ignored");
            Check(owners.DuckHeld, "jump release does not release duck");
            owners.Clear();
            Check(!owners.DuckHeld && !owners.JumpHeld, "lifecycle clears physical owners");
            Check(!owners.ReleaseDuck(4), "late pointer up after pause does not affect resumed pose");
            Check(owners.PressJump(-101) && !owners.PressJump(-102), "keyboard keys share action");
            Check(!owners.ReleaseJump(-102) && owners.ReleaseJump(-101), "keyboard nonowner cannot release");
            var clock = new DinoRunClock();
            Check(clock.AddFrame(.05), "normal delta accepted");
            int ticks = 0; while (clock.ConsumeStep()) ticks++;
            Check(ticks == 3, "50ms consumes three ticks");
            Check(!clock.AddFrame(.101), "long stall requires pause");
            Check(!clock.ConsumeStep(), "long stall discards backlog");
            Check(!clock.AddFrame(double.NaN) && !clock.AddFrame(-1), "invalid dt rejected");
            clock.BeginCountdown();
            Check(clock.CountingDown && clock.CountdownNumber == 3, "resume begins at three");
            Check(!clock.AdvanceCountdown(1) && clock.CountdownNumber == 2, "one second leaves two");
            clock.Reset();
            Check(!clock.CountingDown && !clock.ConsumeStep(), "background cancels countdown and clock");
            clock.BeginCountdown();
            Check(!clock.AdvanceCountdown(2.9), "not early resume");
            Check(clock.AdvanceCountdown(.1) && !clock.CountingDown, "three second countdown finishes once");
            Check(!clock.AdvanceCountdown(1), "does not finish twice");
            foreach (int fps in new[] {30,60,120})
            {
                clock.Reset(); ticks=0;
                for (int frame=0; frame<fps*10; frame++) { Check(clock.AddFrame(1.0/fps), "frame accepted"); while(clock.ConsumeStep()) ticks++; }
                Check(ticks==600, "ten seconds always 600 steps at " + fps);
            }
            Console.WriteLine("Dino session checks: " + count + " passed, 0 failed.");
            return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
