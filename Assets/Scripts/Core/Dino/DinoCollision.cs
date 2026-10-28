// Portions derived from Chromium, Copyright 2024 The Chromium Authors.
// Use of this source code is governed by the BSD-style license in docs/chromium-dino-reference/source/LICENSE.
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
        // Chromium trex.ts / offline_sprite_definitions.ts inner boxes. offline.ts translates
        // these by the outer box's (1, 1) inset before testing. Keep that translation when
        // converting its top-left canvas coordinates into this model's bottom-left coordinates.
        private static readonly DinoRect[] Standing = {
            Inner(22, 0, 17, 16, 47), Inner(1, 18, 30, 9, 47), Inner(10, 35, 14, 8, 47),
            Inner(1, 24, 29, 5, 47), Inner(5, 30, 21, 4, 47), Inner(9, 34, 15, 4, 47)
        };
        // Duck frames use the full 47px canvas, including transparent space above the body.
        private static readonly DinoRect[] Duck = { Inner(1, 18, 55, 25, 47) };
        private static readonly DinoRect[] Small = {
            Inner(0, 7, 5, 27, 35), Inner(4, 0, 6, 34, 35), Inner(10, 4, 7, 14, 35)
        };
        private static readonly DinoRect[] Large = {
            Inner(0, 12, 7, 38, 50), Inner(8, 0, 7, 49, 50), Inner(13, 10, 10, 38, 50)
        };
        private static readonly DinoRect[] Bird = {
            Inner(15, 15, 16, 5, 40), Inner(18, 21, 24, 6, 40), Inner(2, 14, 4, 3, 40),
            Inner(6, 10, 4, 7, 40), Inner(10, 8, 6, 9, 40)
        };
        private static readonly DinoRect[][] SmallGroups = { Small, Group(Small, 34), Group(Small, 51) };
        private static readonly DinoRect[][] LargeGroups = { Large, Group(Large, 50), Group(Large, 75) };

        private static DinoRect Inner(double x, double y, double width, double height, double canvasHeight)
        { return new DinoRect(x + 1, canvasHeight - 1 - y - height, width, height); }

        private static DinoRect[] Group(DinoRect[] single, double canvasWidth)
        {
            // obstacle.ts expands the central box and moves the final box. It does not tile
            // single-cactus hitboxes, even though the atlas depicts several distinct plants.
            var result = (DinoRect[])single.Clone();
            result[1].Width = canvasWidth * DinoRules.SpriteScale - result[0].Width - result[2].Width;
            result[2].X = (canvasWidth + 1) * DinoRules.SpriteScale - result[2].Width;
            return result;
        }

        internal static DinoRect[] ObstacleParts(DinoObstacleKind kind, int group)
        {
            if (kind == DinoObstacleKind.SmallCactus) return SmallGroups[group - 1];
            if (kind == DinoObstacleKind.LargeCactus) return LargeGroups[group - 1];
            return Bird;
        }

        internal static bool Hits(DinoRules rules, double oldY, double newY, bool duck, DinoObstacle obstacle, double newX)
        {
            double fraction;
            return TryHits(rules, oldY, newY, duck, obstacle, newX, out fraction);
        }

        private static bool TryHits(DinoRules rules, double oldY, double newY, bool duck, DinoObstacle obstacle, double newX, out double earliestFraction)
        {
            earliestFraction = 1;
            double scale = DinoRules.SpriteScale, dy = newY - oldY, dx = newX - obstacle.X;
            double outerEnter = 0, outerExit = 1;
            // The original outer gate always uses standing width 44, including ducking. It is
            // a simultaneous broadphase condition, not a clip of each fine box to that outer
            // rectangle: some official fine boxes intentionally extend beyond those bounds.
            if (!RectInterval(rules.PlayerX + scale, oldY + scale, 42 * scale, 45 * scale, dy,
                obstacle.X + scale, obstacle.Y + scale, obstacle.Width - 2 * scale, obstacle.Height - 2 * scale, dx,
                ref outerEnter, ref outerExit)) return false;

            DinoRect[] player = duck ? Duck : Standing;
            DinoRect[] parts = ObstacleParts(obstacle.Kind, obstacle.GroupSize);
            bool hit = false;
            for (int i = 0; i < parts.Length; i++) for (int j = 0; j < player.Length; j++) {
                DinoRect b = parts[i], p = player[j];
                double enter = outerEnter, exit = outerExit;
                if (!RectInterval(rules.PlayerX + p.X, oldY + p.Y, p.Width, p.Height, dy,
                    obstacle.X + b.X, obstacle.Y + b.Y, b.Width, b.Height, dx, ref enter, ref exit)) continue;
                if (!hit || enter < earliestFraction) earliestFraction = enter;
                hit = true;
            }
            return hit;
        }

        internal static bool HitsMotion(DinoRules rules, double oldY, double newY, bool duckRequested, DinoObstacle obstacle, double newX)
        {
            double fraction;
            return TryHitsMotion(rules, oldY, newY, duckRequested, obstacle, newX, out fraction);
        }

        internal static bool TryHitsMotion(DinoRules rules, double oldY, double newY, bool duckRequested, DinoObstacle obstacle, double newX, out double earliestFraction)
        {
            // Air duck requests change motion, while the silhouette remains standing. On a
            // landing segment that silhouette is swept first; the new duck pose starts at t=1.
            bool groundedDuck = duckRequested && oldY == 0 && newY == 0;
            if (TryHits(rules, oldY, newY, groundedDuck, obstacle, newX, out earliestFraction)) return true;
            if (duckRequested && oldY > 0 && newY == 0 && Hits(rules, 0, 0, true, obstacle.At(newX), newX)) {
                earliestFraction = 1;
                return true;
            }
            return false;
        }

        // Each motion update is a linear segment. Require a nonempty interval of positive-area
        // overlap, so an edge graze or endpoint-only touch does not kill the runner.
        internal static bool SweptRect(double ax, double ay, double aw, double ah, double playerDy,
            double bx, double by, double bw, double bh, double obstacleDx)
        {
            double enter = 0, exit = 1;
            return RectInterval(ax, ay, aw, ah, playerDy, bx, by, bw, bh, obstacleDx, ref enter, ref exit);
        }

        private static bool RectInterval(double ax, double ay, double aw, double ah, double playerDy,
            double bx, double by, double bw, double bh, double obstacleDx, ref double enter, ref double exit)
        {
            return Axis(ax, ax + aw, bx, bx + bw, obstacleDx, ref enter, ref exit)
                && Axis(ay, ay + ah, by, by + bh, -playerDy, ref enter, ref exit);
        }

        private static bool Axis(double amin, double amax, double bmin, double bmax, double delta, ref double enter, ref double exit)
        {
            if (delta == 0) return bmax > amin && bmin < amax;
            double first = (amin - bmax) / delta, last = (amax - bmin) / delta;
            if (first > last) { double swap = first; first = last; last = swap; }
            enter = Math.Max(enter, first); exit = Math.Min(exit, last);
            return enter < exit;
        }
    }
}
