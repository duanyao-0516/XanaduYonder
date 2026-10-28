using System;
using XanaduYonder.Runtime.Tetris;

internal static class Program
{
    private static int assertions;

    private static void Require(bool condition, string description)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(description);
    }

    private static void Near(double actual, double expected, string description)
    { Require(Math.Abs(actual - expected) < .00001, description + ": " + actual + " != " + expected); }

    private static void Main()
    {
        var normal = new TetrisLayout(388, 784, false);
        Near(normal.BoardWidth, 237, "normal board width");
        Near(normal.BoardHeight, 474, "normal board height");
        var shortScreen = new TetrisLayout(388, 688, false);
        Near(shortScreen.BoardWidth, 194, "short board width");
        Near(shortScreen.BoardHeight, 388, "short board height");

        foreach (double width in new[] { 320d, 360, 388, 412, 720 })
        foreach (double cssHeight in new[] { 600d, 688, 784, 900 })
        foreach (bool warning in new[] { false, true })
        {
            double scale = width / 388;
            var layout = new TetrisLayout(width, cssHeight * scale, warning);
            string context = width + " x " + cssHeight + "; warning=" + warning + ": ";
            Near(layout.BoardHeight, layout.BoardWidth * 2, context + "square cells over 20 rows");
            Require(layout.BoardTop >= 12 * scale - .00001, context + "board top padding");
            Require(layout.BoardTop + layout.BoardHeight <= layout.ArenaHeight - 12 * scale + .00001,
                context + "board bottom padding");
            Require(layout.BoardLeft - 48 * scale >= 0, context + "Hold target within arena");
            Require(layout.BoardLeft + layout.BoardWidth + 44 * scale <= layout.ContentWidth,
                context + "Next preview within arena");
            Require(layout.ArenaTop + layout.ArenaHeight + (warning ? 26 * scale : 0) + 16 * scale <=
                layout.ControlsTop + .00001, context + "warning and arena separated from controls");
            Require((layout.ContentWidth - 32 * scale) / 5 >= layout.MinimumTarget,
                context + "five action targets retain minimum width");
            Near(layout.ControlsTop + layout.ControlsHeight + 12 * scale, cssHeight * scale,
                context + "bottom margin");
        }
        Console.WriteLine("PASS Tetris layout: " + assertions + " assertions, 40 responsive cases + 2 prototype sizes.");
    }
}
