using System;
using UnityEngine;

namespace XanaduYonder.Runtime
{
    // All distances are in the owning page's layout units, measured from the top left.
    internal readonly struct GameNavigationLayout
    {
        private readonly float first, last, top, size, step;

        public GameNavigationLayout(float width, float inset, float top, float size, float gap, float? leftInset = null)
        {
            first = leftInset ?? inset;
            last = width - inset - size;
            this.top = top;
            this.size = size;
            step = size + gap;
        }

        public Rect Bounds(UiIconKind icon)
        {
            float x;
            switch (icon)
            {
                case UiIconKind.Home:
                case UiIconKind.Back: x = first; break;
                case UiIconKind.Restart: x = first + step; break;
                case UiIconKind.Notebook: x = last - 2 * step; break;
                case UiIconKind.Book: x = last - step; break;
                case UiIconKind.Settings: x = last; break;
                default: throw new ArgumentOutOfRangeException(nameof(icon), icon, "No navigation slot for this icon.");
            }
            return new Rect(x, top, size, size);
        }
    }
}
