using UnityEngine;
using XanaduYonder.Core.GoldMiner;

namespace XanaduYonder.Runtime.GoldMiner
{
    internal sealed class GoldMinerLayout
    {
        public Rect Safe { get; private set; }
        public Rect Mine { get; private set; }
        public Rect Hud { get; private set; }
        public bool Cooperative { get; private set; }
        public float Scale { get; private set; }
        public float Width { get; private set; }
        public float Height { get; private set; }

        // All page coordinates are top-left anchored design units, not physical pixels.
        public GoldMinerLayout(float width, float height, Rect safe, GoldMinerMode mode)
        {
            Width = width;
            Height = height;
            Safe = safe;
            Cooperative = mode == GoldMinerMode.Coop;
            Hud = new Rect(safe.x + 14, safe.y + 12, safe.width - 28, Cooperative ? 64 : 82);
            var mineTop = safe.y + (Cooperative ? 174 : Mathf.Clamp(safe.height * .17f, 208, 270));
            var available = new Rect(safe.x + 22, mineTop, safe.width - 44,
                Mathf.Max(120, safe.yMax - 96 - mineTop));
            Scale = Mathf.Min(available.width / (float)GoldMinerRules.Width(mode),
                available.height / (float)GoldMinerRules.Height(mode));
            var mineWidth = Scale * (float)GoldMinerRules.Width(mode);
            Mine = new Rect(available.center.x - mineWidth * .5f, available.y,
                mineWidth, Scale * (float)GoldMinerRules.Height(mode));
        }

        public Vector2 World(double x, double y)
        {
            return new Vector2(Mine.x + (float)x * Scale, Mine.y + (float)y * Scale);
        }

        public static Rect WindowSafeArea(RectTransform canvas)
        {
            var area = Screen.safeArea;
            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            return new Rect(area.x / width * canvas.rect.width,
                (height - area.yMax) / height * canvas.rect.height,
                area.width / width * canvas.rect.width,
                area.height / height * canvas.rect.height);
        }
    }
}
