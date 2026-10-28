using System;

namespace XanaduYonder.Runtime.Zuma
{
    public struct ZumaViewportRect
    {
        public float Left, Bottom, Width, Height;
        public bool ToDesign(float screenX, float screenY, out float x, out float y)
        {
            x = y = 0;
            if (Width <= 0 || Height <= 0 || !Finite(screenX) || !Finite(screenY)) return false;
            x = (screenX - Left) * 1280 / Width;
            y = (Bottom + Height - screenY) * 720 / Height;
            return x >= 0 && x <= 1280 && y >= 0 && y <= 720;
        }
        private static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
    }
    public static class ZumaViewportMath
    {
        public static ZumaViewportRect Fit(float left, float bottom, float width, float height)
        {
            if (width <= 0 || height <= 0 || float.IsNaN(width) || float.IsNaN(height) || float.IsInfinity(width) || float.IsInfinity(height)) return new ZumaViewportRect();
            var scale = Math.Min(width / 1280f, height / 720f);
            var w = 1280 * scale; var h = 720 * scale;
            return new ZumaViewportRect { Left=left+(width-w)/2, Bottom=bottom+(height-h)/2, Width=w, Height=h };
        }
    }
}
