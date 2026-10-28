using System;
using System.Reflection;
using XanaduYonder.Core.Dino;

internal static class CollisionRegressionTests
{
    internal static void Run()
    {
        int failures = 0;
        Action<string, Action> check = delegate(string name, Action test) {
            try { test(); Console.WriteLine("PASS collision: " + name); }
            catch (Exception error) { failures++; Console.WriteLine("FAIL collision: " + name + ": " + error.Message); }
        };
        check("official fine boxes include the outer one-pixel translation", OfficialPlayerBoxes);
        check("atlas body excludes opaque white border", AtlasGeometry.Check);
        check("formerly invisible body gaps are clear", FormerBodyGaps);
        check("strict positive-area swept contact", StrictContact);
        check("official outer gate and fine boxes agree across poses and groups", OfficialStaticParity);
        check("earliest contact combines every fine pair with the outer interval", EarliestContact);
        check("airborne duck and landing transition preserve pose", DuckTransition);
        if (failures != 0) throw new Exception(failures + " collision regressions failed");
    }

    private static void OfficialPlayerBoxes()
    {
        CheckBoxes("Standing", new double[,] { { 23, 30, 17, 16 }, { 2, 19, 30, 9 }, { 11, 3, 14, 8 }, { 2, 17, 29, 5 }, { 6, 12, 21, 4 }, { 10, 8, 15, 4 } });
        // Chromium draws duck frames in the same 47px canvas, not a cropped 25px canvas.
        CheckBoxes("Duck", new double[,] { { 2, 3, 55, 25 } });
    }

    private static void CheckBoxes(string name, double[,] expected)
    {
        var actual = (DinoRect[])typeof(DinoCollision).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        Assert(actual.Length == expected.GetLength(0), name + " part count");
        for (int i = 0; i < actual.Length; i++) {
            Near(actual[i].X, expected[i, 0] * DinoRules.SpriteScale, name + " x " + i);
            Near(actual[i].Y, expected[i, 1] * DinoRules.SpriteScale, name + " y " + i);
            Near(actual[i].Width, expected[i, 2] * DinoRules.SpriteScale, name + " width " + i);
            Near(actual[i].Height, expected[i, 3] * DinoRules.SpriteScale, name + " height " + i);
        }
    }

    private static void FormerBodyGaps()
    {
        var rules = new DinoRules(656 / 1.2);
        double[,] cases = { { 0, 0, 49.94, 1.8 }, { 0, 20, 44.54, 8.0 }, { 1, 0, 56.69, 1.2 }, { 1, 35, 44.54, 9.0 } };
        for (int i = 0; i < cases.GetLength(0); i++) {
            var kind = (DinoObstacleKind)(int)cases[i, 0];
            double y = cases[i, 1], x = rules.PlayerX + cases[i, 2];
            var obstacle = new DinoObstacle(1, kind, 1, x, default(DinoPlan));
            double gap = AtlasGeometry.DarkBodyGap("Standing", rules.PlayerX, y, kind, 1, x, 0);
            Assert(gap > cases[i, 3], "diagnosed dark-body gap is still present: " + gap);
            Assert(!DinoCollision.Hits(rules, y, y, false, obstacle, x), "separated body was hit: " + kind + " y=" + y + " gap=" + gap);
        }
    }

    private static void StrictContact()
    {
        Assert(!DinoCollision.SweptRect(0, 0, 10, 10, 0, 10, 0, 2, 10, 0), "stationary edge touch");
        Assert(!DinoCollision.SweptRect(0, 0, 10, 10, 0, 25, 10, 2, 10, -40), "moving edge grazing");
        Assert(!DinoCollision.SweptRect(0, 0, 10, 10, 0, 20, 0, 2, 10, -10), "touch reached only at segment end");
        Assert(!DinoCollision.SweptRect(0, 0, 10, 10, 0, 10, 0, 2, 10, 10), "touch departing at segment start");
        Assert(!DinoCollision.SweptRect(0, 0, 10, 10, 20, 20, 0, 10, 10, -20), "diagonal corner contact lasts zero time");
        Assert(DinoCollision.SweptRect(0, 0, 10, 10, 0, 25, 0, 2, 10, -40), "fast thin obstacle remains detected");
        Assert(DinoCollision.SweptRect(0, 0, 10, 10, 0, 10, 0, 2, 10, -1), "entering from an initial touch");
    }

    private static void EarliestContact()
    {
        var rules = new DinoRules(600);
        var obstacle = new DinoObstacle(1, DinoObstacleKind.SmallCactus, 1, rules.PlayerX + 100, default(DinoPlan));
        double fraction;
        Assert(DinoCollision.TryHitsMotion(rules, 0, 0, false, obstacle, rules.PlayerX - 100, out fraction), "fast pass through cactus");
        Near(fraction, (100 - 35 * DinoRules.SpriteScale) / 200, "minimum across fine pairs");
        double impactX = obstacle.X - 200 * fraction;
        Assert(!DinoCollision.Hits(rules, 0, 0, false, obstacle.At(impactX + .0001), impactX + .0001), "before earliest contact");
        Assert(DinoCollision.Hits(rules, 0, 0, false, obstacle.At(impactX - .0001), impactX - .0001), "after earliest contact");
        obstacle = obstacle.At(rules.PlayerX + 78);
        Assert(DinoCollision.TryHitsMotion(rules, 0, 0, true, obstacle, rules.PlayerX + 30, out fraction), "duck outer gate enters");
        Near(fraction, (78 - 42 * DinoRules.SpriteScale) / 48, "duck fine extension waits for official 44px outer gate");
        Assert(!DinoCollision.TryHitsMotion(rules, 0, 0, true, obstacle, rules.PlayerX + 60, out fraction), "fine boxes outside disjoint outer bounds cannot collide");
        Near(fraction, 1, "no-hit fraction is endpoint");
    }

    private static void DuckTransition()
    {
        var rules = new DinoRules(600);
        var bird = new DinoObstacle(1, DinoObstacleKind.MidBird, 1, rules.PlayerX + 20, default(DinoPlan));
        double fraction;
        Assert(DinoCollision.TryHitsMotion(rules, 2, 0, true, bird, bird.X, out fraction), "landing retains airborne standing collision");
        Near(fraction, 0, "overlapping airborne pose is immediate");
        Assert(!DinoCollision.TryHitsMotion(rules, 0, 0, true, bird, bird.X, out fraction), "grounded duck passes middle bird");
        Assert(DinoCollision.TryHitsMotion(rules, 2, 2, true, bird, bird.X, out fraction), "airborne duck uses standing silhouette");
    }

    // Independent source fixture: coordinates are copied in Chromium's top-left space. This keeps
    // the reference calculation separate from the production bottom-left mapping and sweep code.
    private static readonly double[,] Running = { {22, 0, 17, 16}, {1, 18, 30, 9}, {10, 35, 14, 8}, {1, 24, 29, 5}, {5, 30, 21, 4}, {9, 34, 15, 4} };
    private static readonly double[,] Ducking = { {1, 18, 55, 25} };
    private static readonly double[,] Small = { {0, 7, 5, 27}, {4, 0, 6, 34}, {10, 4, 7, 14} };
    private static readonly double[,] Large = { {0, 12, 7, 38}, {8, 0, 7, 49}, {13, 10, 10, 38} };
    private static readonly double[,] Bird = { {15, 15, 16, 5}, {18, 21, 24, 6}, {2, 14, 4, 3}, {6, 10, 4, 7}, {10, 8, 6, 9} };

    private static void OfficialStaticParity()
    {
        var rules = new DinoRules(600);
        foreach (DinoObstacleKind kind in Enum.GetValues(typeof(DinoObstacleKind)))
        for (int group = 1; group <= (kind <= DinoObstacleKind.LargeCactus ? 3 : 1); group++)
        foreach (bool duck in new[] { false, true })
        foreach (double y in new[] { 0.0, 2, 20, 35, 46, 60, 80, 110 })
        for (double relativeX = -100.25; relativeX <= 100; relativeX += 1.5) {
            double x = rules.PlayerX + relativeX;
            var obstacle = new DinoObstacle(1, kind, group, x, default(DinoPlan));
            bool expected = OfficialHit(rules, y, duck, obstacle);
            bool actual = DinoCollision.Hits(rules, y, y, duck, obstacle, x);
            Assert(actual == expected, kind + " group=" + group + " duck=" + duck + " y=" + y + " dx=" + relativeX + " expected=" + expected);
        }
    }

    private static bool OfficialHit(DinoRules rules, double playerY, bool duck, DinoObstacle obstacle)
    {
        double scale = DinoRules.SpriteScale;
        double px = rules.PlayerX / scale + 1, py = -playerY / scale - 47 + 1;
        double ox = obstacle.X / scale + 1, oy = -obstacle.Y / scale - obstacle.Height / scale + 1;
        if (!Overlap(px, py, 42, 45, ox, oy, obstacle.Width / scale - 2, obstacle.Height / scale - 2)) return false;
        double[,] player = duck ? Ducking : Running;
        double[,] parts = obstacle.Kind == DinoObstacleKind.SmallCactus ? Small : obstacle.Kind == DinoObstacleKind.LargeCactus ? Large : Bird;
        for (int i = 0; i < player.GetLength(0); i++) for (int j = 0; j < parts.GetLength(0); j++) {
            double x = parts[j, 0], w = parts[j, 2];
            if (obstacle.GroupSize > 1) {
                if (j == 1) w = obstacle.Width / scale - parts[0, 2] - parts[2, 2];
                if (j == 2) x = obstacle.Width / scale - parts[2, 2];
            }
            if (Overlap(px + player[i, 0], py + player[i, 1], player[i, 2], player[i, 3], ox + x, oy + parts[j, 1], w, parts[j, 3])) return true;
        }
        return false;
    }

    private static bool Overlap(double ax, double ay, double aw, double ah, double bx, double by, double bw, double bh)
    { return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by; }
    private static void Near(double actual, double expected, string message) { Assert(Math.Abs(actual - expected) < 1e-8, message + ": " + actual + " != " + expected); }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
}
