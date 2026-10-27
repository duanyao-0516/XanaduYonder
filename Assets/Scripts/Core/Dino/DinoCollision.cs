using System;

namespace XanaduYonder.Core.Dino
{
    internal struct DinoRect
    {
        internal double X, Y, Width, Height;
        internal DinoRect(double x, double y, double width, double height)
        { X = x * DinoRules.SpriteScale; Y = y * DinoRules.SpriteScale; Width = width * DinoRules.SpriteScale; Height = height * DinoRules.SpriteScale; }
    }

    internal static class DinoCollision
    {
        // Pixel-space silhouette parts, with bottom-left coordinates. Legs cover both run frames;
        // wing coverage is the union of both animation frames. Transparent sprite corners are excluded.
        private static readonly DinoRect[] Standing = {
            new DinoRect(12, 0, 7, 12), new DinoRect(22, 2, 6, 10),
            new DinoRect(9, 10, 25, 23), new DinoRect(1, 21, 12, 7), new DinoRect(21, 30, 22, 16)
        };
        private static readonly DinoRect[] Duck = {
            new DinoRect(16, 0, 9, 9), new DinoRect(29, 5, 8, 7), new DinoRect(11, 6, 32, 15),
            new DinoRect(3, 17, 12, 7), new DinoRect(40, 12, 18, 14)
        };
        private static readonly DinoRect[] Small = {
            new DinoRect(6, 0, 5, 35), new DinoRect(1, 10, 5, 17), new DinoRect(11, 14, 5, 15)
        };
        private static readonly DinoRect[] Large = {
            new DinoRect(9, 0, 7, 50), new DinoRect(1, 15, 8, 23), new DinoRect(16, 20, 8, 22)
        };
        // The atlas's two/three-plant slices have distinct silhouettes. Their cells are not copies
        // of the single sprite: the second double has a raised arm, and the middle triple is offset.
        private static readonly DinoRect[] SmallDoubleSecond = {
            new DinoRect(6, 0, 5, 35), new DinoRect(1, 19, 4, 10),
            new DinoRect(3, 17, 5, 5), new DinoRect(11, 14, 5, 15)
        };
        private static readonly DinoRect[] LargeDoubleSecond = {
            new DinoRect(9, 0, 7, 50), new DinoRect(1, 27, 5, 16),
            new DinoRect(4, 24, 7, 6), new DinoRect(16, 20, 8, 22)
        };
        private static readonly DinoRect[] LargeTripleMiddle = {
            new DinoRect(6, 2, 5, 44), new DinoRect(0, 18, 4, 14), new DinoRect(3, 15, 5, 17),
            new DinoRect(13, 33, 4, 8), new DinoRect(10, 28, 5, 5),
            new DinoRect(19, 2, 3, 27), new DinoRect(14, 14, 10, 10)
        };
        private static readonly DinoRect[] Bird = {
            new DinoRect(10, 10, 26, 14), new DinoRect(33, 12, 11, 11), new DinoRect(14, 4, 12, 32)
        };

        internal static bool Hits(DinoRules rules, double oldY, double newY, bool duck, DinoObstacle obstacle, double newX)
        {
            DinoRect[] player = duck ? Duck : Standing;
            double stride = obstacle.Kind == DinoObstacleKind.SmallCactus ? 17 * DinoRules.SpriteScale : 25 * DinoRules.SpriteScale;
            for (int g = 0; g < obstacle.GroupSize; g++) {
                DinoRect[] parts = ObstacleParts(obstacle.Kind, obstacle.GroupSize, g);
                for (int i = 0; i < parts.Length; i++) {
                    DinoRect b = parts[i];
                    for (int j = 0; j < player.Length; j++) {
                        DinoRect p = player[j];
                        if (SweptRect(rules.PlayerX + p.X, oldY + p.Y, p.Width, p.Height, newY - oldY,
                            obstacle.X + g * stride + b.X, obstacle.Y + b.Y, b.Width, b.Height, newX - obstacle.X)) return true;
                    }
                }
            }
            return false;
        }

        internal static DinoRect[] ObstacleParts(DinoObstacleKind kind, int group, int plant)
        {
            if (kind == DinoObstacleKind.SmallCactus) return group == 2 && plant == 1 ? SmallDoubleSecond : Small;
            if (kind == DinoObstacleKind.LargeCactus) {
                if (group == 2 && plant == 1) return LargeDoubleSecond;
                if (group == 3 && plant == 1) return LargeTripleMiddle;
                return Large;
            }
            return Bird;
        }

        internal static bool HitsMotion(DinoRules rules, double oldY, double newY, bool duckRequested, DinoObstacle obstacle, double newX)
        {
            // An airborne duck request changes acceleration, not the airborne silhouette.
            // If this segment lands, sweep the standing body first, then check the new duck pose.
            bool groundedDuck = duckRequested && oldY == 0 && newY == 0;
            if (Hits(rules, oldY, newY, groundedDuck, obstacle, newX)) return true;
            return duckRequested && oldY > 0 && newY == 0 && Hits(rules, 0, 0, true, obstacle.At(newX), newX);
        }

        // Relative swept AABB. Each physical substep is a linear segment, so this tests every instant
        // on it, including a thin obstacle crossed between endpoints or during downward acceleration.
        internal static bool SweptRect(double ax, double ay, double aw, double ah, double playerDy,
            double bx, double by, double bw, double bh, double obstacleDx)
        {
            double enter = 0, exit = 1;
            return Axis(ax, ax + aw, bx, bx + bw, obstacleDx, ref enter, ref exit)
                && Axis(ay, ay + ah, by, by + bh, -playerDy, ref enter, ref exit);
        }

        private static bool Axis(double amin, double amax, double bmin, double bmax, double delta, ref double enter, ref double exit)
        {
            if (Math.Abs(delta) < 1e-12) return bmax >= amin && bmin <= amax;
            double first = (amin - bmax) / delta, last = (amax - bmin) / delta;
            if (first > last) { double swap = first; first = last; last = swap; }
            enter = Math.Max(enter, first); exit = Math.Min(exit, last);
            return enter <= exit;
        }
    }

    internal static class DinoMotion
    {
        internal static void Advance(ref double y, ref double velocity, bool held, bool duck)
        {
            if (y <= 0 && velocity <= 0) { y = 0; velocity = 0; return; }
            if (duck) velocity = Math.Min(velocity, -220);
            else if (!held && y >= DinoRules.MinimumJumpHeight && velocity > DinoRules.ReleasedVelocity) velocity = DinoRules.ReleasedVelocity;
            double dt = DinoRules.StepSeconds / DinoRules.Substeps;
            velocity -= DinoRules.Gravity * (duck ? 2.8 : 1) * dt;
            y += velocity * dt;
            if (y <= 0) { y = 0; velocity = 0; }
        }
    }
}
