using System;
using XanaduYonder.Core;

namespace XanaduYonder.Runtime
{
    internal sealed class SwipeGesture
    {
        private bool tracking;
        private int pointerId;
        private float startX;
        private float startY;

        public void Begin(int id, float x, float y)
        {
            if (tracking) return;
            tracking = true;
            pointerId = id;
            startX = x;
            startY = y;
        }

        public bool End(int id, float x, float y, bool cancelled, out Direction direction)
        {
            direction = Direction.Left;
            if (!tracking || id != pointerId) return false;
            tracking = false;
            if (cancelled) return false;
            var dx = x - startX;
            var dy = y - startY;
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < 28) return false;
            direction = Math.Abs(dx) > Math.Abs(dy)
                ? dx > 0 ? Direction.Right : Direction.Left
                : dy > 0 ? Direction.Up : Direction.Down;
            return true;
        }

        public void Cancel() { tracking = false; }
    }
}
