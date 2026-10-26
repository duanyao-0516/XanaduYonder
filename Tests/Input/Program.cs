using System;
using XanaduYonder.Core;
using XanaduYonder.Runtime;

internal static class Program
{
    private static int Main()
    {
        try
        {
            var gesture = new SwipeGesture();
            Direction direction;
            Assert(!gesture.End(0, 100, 0, false, out direction), "release without press");
            gesture.Begin(0, 0, 0);
            Assert(!gesture.End(0, 100, 0, true, out direction), "cancelled swipe must never commit");
            Assert(!gesture.End(0, 100, 0, false, out direction), "cancel consumes press");
            gesture.Begin(0, 0, 0);
            Assert(!gesture.End(1, 100, 0, false, out direction), "another finger cannot commit");
            Assert(gesture.End(0, 100, 0, false, out direction) && direction == Direction.Right, "original finger retains ownership");
            gesture.Begin(0, 0, 0);
            gesture.Begin(1, 1000, 1000);
            Assert(gesture.End(0, -80, 2, false, out direction) && direction == Direction.Left, "second finger cannot replace start");
            gesture.Begin(0, 0, 0);
            Assert(!gesture.End(0, 27, 3, false, out direction), "tap stays below swipe threshold");
            gesture.Begin(0, 0, 0);
            Assert(gesture.End(0, 3, 90, false, out direction) && direction == Direction.Up, "vertical up");
            gesture.Begin(0, 0, 0);
            Assert(gesture.End(0, 5, -100, false, out direction) && direction == Direction.Down, "vertical down");
            gesture.Begin(0, 0, 0);
            gesture.Cancel();
            Assert(!gesture.End(0, 100, 0, false, out direction), "pause or disable cancels press");
            Console.WriteLine("Input checks: 10 passed, 0 failed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
