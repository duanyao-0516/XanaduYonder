using UnityEngine;
using XanaduYonder.Core.GoldMiner;

namespace XanaduYonder.Runtime.GoldMiner
{
    internal sealed class GoldMinerLayout
    {
        public Rect Safe { get; private set; }
        public Rect Mine { get; private set; }
        public Rect SourceCanvas { get; private set; }
        public Rect Hud { get; private set; }
        public bool Cooperative { get; private set; }
        public float Scale { get; private set; }
        public float Width { get; private set; }
        public float Height { get; private set; }
        public bool SourceCampaign { get; private set; }
        private readonly GoldMinerMode mode;

        // All page coordinates are top-left anchored design units, not physical pixels.
        public GoldMinerLayout(float width, float height, Rect safe, GoldMinerMode mode,
            GoldMinerBalanceProfile profile = GoldMinerBalanceProfile.Classic)
        {
            this.mode = mode;
            SourceCampaign = profile == GoldMinerBalanceProfile.Source;
            Width = width;
            Height = height;
            Safe = safe;
            Cooperative = mode == GoldMinerMode.Coop;
            Hud = new Rect(safe.x + 14, safe.y + 12, safe.width - 28, Cooperative ? 64 : 82);
            var mineTop = safe.y + (Cooperative ? 174 : Mathf.Clamp(safe.height * .17f, 208, 270));
            var available = new Rect(safe.x + 22, mineTop, safe.width - 44,
                Mathf.Max(120, safe.yMax - 96 - mineTop));
            float worldWidth = (float)(SourceCampaign ? GoldMinerSourceRules.Width(mode) : GoldMinerRules.Width(mode));
            float worldHeight = (float)(SourceCampaign ? GoldMinerSourceRules.Height(mode) : GoldMinerRules.Height(mode));
            Scale = Mathf.Min(available.width / worldWidth, available.height / worldHeight);
            var mineWidth = Scale * worldWidth;
            Mine = new Rect(available.center.x - mineWidth * .5f, available.y,
                mineWidth, Scale * worldHeight);
            // The source canvas includes sky above the hook origin (46/55 source pixels).
            float sky = SourceCampaign ? (mode == GoldMinerMode.Solo ? .46f : .55f) * Scale : 0;
            SourceCanvas = new Rect(Mine.x, Mine.y - sky, Mine.width, Mine.height + sky);
        }

        public double OriginX(int player)
        {
            return SourceCampaign ? GoldMinerSourceRules.OriginX(mode, player) : GoldMinerRules.OriginX(mode, player);
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
