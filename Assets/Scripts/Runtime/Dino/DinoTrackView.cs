using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Dino;

namespace XanaduYonder.Runtime.Dino
{
    // Model coordinates are logical units, measured right/up from the player's ground line.
    // This renderer never advances the game or uses UI rectangles for collision decisions.
    internal sealed class DinoTrackView : MonoBehaviour
    {
        public struct PresentationState
        {
            internal readonly double AnimationClock;
            internal readonly float NightAmount;

            internal PresentationState(double animationClock, float nightAmount)
            {
                AnimationClock = animationClock;
                NightAmount = nightAmount;
            }
        }

        private const int ObstacleCapacity = 32;
        private const float GroundInset = 30;
        private const float GroundTileWidth = 600 * (float)DinoRules.SpriteScale;
        private const float GroundTileHeight = 12 * (float)DinoRules.SpriteScale;
        private const float DayNightSeconds = .8f;
        private static readonly Color DayBackground = Color.white;
        private static readonly Color NightBackground = Color.black;
        private static readonly Color DayForeground = new Color32(83, 83, 83, 255);
        private static readonly Color NightForeground = new Color32(218, 218, 218, 255);
        private static readonly int TintProperty = Shader.PropertyToID("_Color");

        private RectTransform root;
        private RectTransform groundRoot;
        private Image background;
        private Texture2D atlas;
        private Material daySpriteMaterial, nightSpriteMaterial;
        private RawImage player;
        private DinoCelebrationHat celebrationHat;
        private RawImage moon;
        private readonly RawImage[] clouds = new RawImage[5];
        private readonly RawImage[] stars = new RawImage[5];
        private readonly RawImage[] obstacles = new RawImage[ObstacleCapacity];
        private readonly List<RawImage> ground = new List<RawImage>(8);
        private DinoGame lastGame;
        private double previousDistance;
        private double animationClock;
        private double worldWidth = 600;
        private float pixelWidth = 600;
        private float pixelHeight = 300;
        private float scale = 1;
        private float nightAmount;
        private bool brightSprites;
        private int groundCount;
        private int visibleObstacleCount;

        public Color BackgroundColor { get { return Color.Lerp(DayBackground, NightBackground, nightAmount); } }
        public Color ForegroundColor { get { return brightSprites ? NightForeground : DayForeground; } }

        // A resized page may replace its UI tree while keeping the exact same paused run.
        // Do not carry the old run's animation phase or palette into a newly created game.
        public PresentationState Capture(DinoGame game)
        {
            return ReferenceEquals(game, lastGame)
                ? new PresentationState(animationClock, nightAmount)
                : default(PresentationState);
        }

        public void Restore(DinoGame game, PresentationState state)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            lastGame = game;
            previousDistance = game.Distance;
            animationClock = state.AnimationClock;
            nightAmount = state.NightAmount;
        }

        public void Initialize(RectTransform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (root != null)
            {
                root.SetParent(parent, false);
                return;
            }
            atlas = Resources.Load<Texture2D>("Dino/Sprites");
            if (atlas == null || atlas.width != 2446 || atlas.height != 194)
                throw new InvalidOperationException("Dino/Sprites must be the verified 2446 x 194 Chromium atlas.");
            var shader = Shader.Find("UI/Default");
            if (shader == null) throw new InvalidOperationException("Dino requires Unity's UI/Default shader.");
            daySpriteMaterial = new Material(shader) { name = "Dino day sprites" };
            nightSpriteMaterial = new Material(shader) { name = "Dino night sprites" };
            float nightGain = 218f / 83f;
            nightSpriteMaterial.SetColor(TintProperty, new Color(nightGain, nightGain, nightGain, 1));

            root = CreateRect(parent, "Dino Track");
            root.gameObject.AddComponent<RectMask2D>();
            background = CreateRect(root, "Sky").gameObject.AddComponent<Image>();
            background.raycastTarget = false;
            background.color = DayBackground;
            for (var i = 0; i < stars.Length; i++)
            {
                stars[i] = CreateSprite(root, "Star " + i);
                SetSlice(stars[i], 1276, 2 + (i % 2) * 18, 18, 18);
            }
            moon = CreateSprite(root, "Moon");
            SetSlice(moon, 954 + 120, 2, 80, 80);
            for (var i = 0; i < clouds.Length; i++)
            {
                clouds[i] = CreateSprite(root, "Cloud " + i);
                SetSlice(clouds[i], 166, 2, 92, 28);
            }
            groundRoot = CreateRect(root, "Ground");
            for (var i = 0; i < obstacles.Length; i++)
            {
                obstacles[i] = CreateSprite(root, "Obstacle " + i);
                obstacles[i].enabled = false;
            }
            player = CreateSprite(root, "Dinosaur");
            celebrationHat = CreateRect(player.transform, "Holiday hat").gameObject.AddComponent<DinoCelebrationHat>();
            celebrationHat.raycastTarget = false;
            celebrationHat.gameObject.SetActive(false);
            SetLayout(pixelWidth, pixelHeight, worldWidth);
            ApplyPalette();
        }

        public void SetCelebration(DinoCelebration celebration)
        {
            if (celebrationHat == null) return;
            celebrationHat.Kind = celebration;
            celebrationHat.gameObject.SetActive(celebration != DinoCelebration.None);
        }

        // Sizes are in the parent's actual canvas units; only one scale maps both world axes.
        public void SetLayout(float width, float height, double logicalWidth)
        {
            if (width <= 0 || height <= 0 || logicalWidth <= 0 || float.IsNaN(width) || float.IsNaN(height) ||
                float.IsInfinity(width) || float.IsInfinity(height) || double.IsNaN(logicalWidth) || double.IsInfinity(logicalWidth))
                throw new ArgumentOutOfRangeException(nameof(logicalWidth), "Dino layout dimensions must be finite and positive.");
            pixelWidth = width;
            pixelHeight = height;
            worldWidth = logicalWidth;
            scale = (float)(pixelWidth / worldWidth);
            if (root == null) return;
            root.sizeDelta = new Vector2(pixelWidth, pixelHeight);
            background.rectTransform.sizeDelta = root.sizeDelta;
            groundRoot.sizeDelta = root.sizeDelta;
            groundCount = (int)Math.Ceiling(worldWidth / GroundTileWidth) + 1;
            // Resizing may grow the pool. Render never allocates or creates hierarchy objects.
            while (ground.Count < groundCount)
            {
                var tile = CreateSprite(groundRoot, "Ground tile " + ground.Count);
                SetSlice(tile, 2, 104, 1200, 24);
                ground.Add(tile);
            }
            for (var i = 0; i < ground.Count; i++) ground[i].enabled = i < groundCount;
        }

        public void Render(DinoGame game, float dt)
        {
            if (root == null || game == null) return;
            if (!ReferenceEquals(game, lastGame) || game.Distance < previousDistance)
            {
                lastGame = game;
                animationClock = 0;
                nightAmount = 0;
            }
            previousDistance = game.Distance;
            // Zero is a deliberate frozen presentation frame, including during countdowns.
            var delta = dt > 0 && !float.IsInfinity(dt) && !game.IsGameOver ? dt : 0;
            animationClock += delta;
            // The run clock is fixed-step gameplay time: pauses and resume countdowns do not
            // move this boundary, and narrow viewports see the same cycle as wide ones.
            var targetNight = ((game.Tick / 1800) & 1) != 0 ? 1f : 0f;
            nightAmount = Mathf.MoveTowards(nightAmount, targetNight, delta / DayNightSeconds);
            ApplyPalette();
            RenderSky(game.Distance);
            RenderGround(game.Distance);
            RenderObstacles(game);
            RenderPlayer(game);
        }

        private void ApplyPalette()
        {
            background.color = BackgroundColor;
            // The source pixels are #535353 with a white outline. Interpolating both foreground
            // and sky would make their luminance cross. Switch foreground at the sky midpoint,
            // while the sky, moon and stars complete their continuous .8 second transition.
            bool nextBright = nightAmount >= .5f;
            if (brightSprites == nextBright) return;
            brightSprites = nextBright;
            // Rounded UGUI masks cache stencil copies of a base material. Switch immutable
            // materials so the cached copy receives the correct palette in both directions.
            var material = brightSprites ? nightSpriteMaterial : daySpriteMaterial;
            player.material = moon.material = material;
            foreach (var sprite in clouds) sprite.material = material;
            foreach (var sprite in stars) sprite.material = material;
            foreach (var sprite in obstacles) sprite.material = material;
            foreach (var sprite in ground) sprite.material = material;
        }

        private void RenderSky(double distance)
        {
            var logicalHeight = pixelHeight / scale;
            var skyBottom = Math.Min(GroundInset + 85, Math.Max(GroundInset + 20, logicalHeight - 55));
            var skyHeight = Math.Max(0, logicalHeight - 42 - skyBottom);
            for (var i = 0; i < clouds.Length; i++)
            {
                var cycle = worldWidth + 180;
                var x = Wrap(cycle * (i + .35) / clouds.Length - distance * (.12 + (i % 2) * .025), cycle) - 90;
                var y = skyBottom + skyHeight * (.15 + (i % 3) * .23);
                Place(clouds[i], x, y, 46, 14);
                clouds[i].color = new Color(1, 1, 1, 1 - nightAmount * .7f);
            }
            var moonX = Wrap(worldWidth * .78 - distance * .015 + 50, worldWidth + 100) - 50;
            Place(moon, moonX, skyBottom + skyHeight * .62, 40, 40);
            moon.color = new Color(1, 1, 1, nightAmount);
            for (var i = 0; i < stars.Length; i++)
            {
                var x = Wrap(worldWidth * (.12 + i * .19) - distance * .02 + 12, worldWidth + 24) - 12;
                var y = skyBottom + skyHeight * (.22 + ((i * 3) % 5) * .13);
                Place(stars[i], x, y, 9, 9);
                stars[i].color = new Color(1, 1, 1, nightAmount);
            }
        }

        private void RenderGround(double distance)
        {
            var offset = Wrap(distance, GroundTileWidth);
            long firstTile = (long)Math.Floor(distance / GroundTileWidth);
            for (var i = 0; i < groundCount; i++)
            {
                // Both complete 2x strips share the same endpoints: flat, then small hills/dips.
                // Absolute world tile IDs keep each choice stable while scrolling or rebuilding.
                SetSlice(ground[i], IsBumpyGround(firstTile + i) ? 1202 : 2, 104, 1200, 24);
                // Chromium places its 12px ground strip one pixel above the sprite bottom.
                // Its line at source rows 8..9 crosses the lower feet, rather than sitting
                // underneath them. Scale the whole strip like the character, purely visually.
                Place(ground[i], i * GroundTileWidth - offset, GroundInset + DinoRules.SpriteScale,
                    GroundTileWidth, GroundTileHeight);
            }
        }

        private static bool IsBumpyGround(long tileIndex)
        {
            if (tileIndex == 0) return false;
            unchecked
            {
                uint hash = (uint)tileIndex ^ (uint)(tileIndex >> 32) ^ 0x9E3779B9u;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                return (hash & 1u) != 0;
            }
        }

        private void RenderPlayer(DinoGame game)
        {
            var ducking = game.Ducking && !game.Jumping && !game.IsGameOver;
            int frame;
            if (game.IsGameOver) frame = 220;
            else if (ducking) frame = game.IsStarted && ((long)(animationClock * 8) & 1) != 0 ? 323 : 264;
            else if (!game.IsStarted) frame = animationClock % 3.7 > 3.53 ? 44 : 0;
            else if (game.Jumping) frame = 0;
            else frame = ((long)(animationClock * 12) & 1) == 0 ? 88 : 132;
            SetSlice(player, 1678 + frame * 2, 2, ducking ? 118 : 88, 94);
            // Keep the full 47-row duck source: its head, tail and outline extend above the
            // nominal 25-unit crouch body, and its feet share the standing frame's baseline.
            var width = ducking ? game.Rules.DuckWidth : game.Rules.PlayerWidth;
            var height = ducking ? game.Rules.DuckHeight * 47 / 25 : game.Rules.PlayerHeight;
            Place(player, game.Rules.PlayerX, GroundInset + game.PlayerY, width, height);
            RenderHat(game, ducking);
        }

        private void RenderHat(DinoGame game, bool ducking)
        {
            if (celebrationHat == null || !celebrationHat.gameObject.activeSelf) return;
            float sourceScale = (float)DinoRules.SpriteScale * scale;
            float hatWidth = DinoCelebrationHat.PixelWidth * sourceScale;
            // Size for the maximum held jump, so the decoration does not shrink mid-flight.
            // On unusually short windows only the hat gets shorter; physics and body stay fixed.
            double maxJump = game.Rules.MaximumJumpHeight;
            float available = pixelHeight - (float)(GroundInset + game.Rules.PlayerHeight + maxJump) * scale - 2;
            float hatHeight = Mathf.Min(DinoCelebrationHat.PixelHeight * sourceScale, Mathf.Max(1, available));
            var rect = celebrationHat.rectTransform;
            rect.sizeDelta = new Vector2(hatWidth, hatHeight);
            rect.anchoredPosition = new Vector2((ducking ? 47 : 32) * sourceScale - hatWidth * .5f,
                (ducking ? 28 : 46) * sourceScale);
        }

        private void RenderObstacles(DinoGame game)
        {
            var count = game.Obstacles.Count;
            if (count > obstacles.Length)
                throw new InvalidOperationException("Dino obstacle count exceeded the presentation pool contract.");
            var birdFrame = (int)((long)(animationClock * 6) & 1);
            for (var i = 0; i < count; i++)
            {
                var obstacle = game.Obstacles[i];
                var image = obstacles[i];
                if (!image.enabled) image.enabled = true;
                if (obstacle.Kind == DinoObstacleKind.SmallCactus || obstacle.Kind == DinoObstacleKind.LargeCactus)
                {
                    var small = obstacle.Kind == DinoObstacleKind.SmallCactus;
                    var unitWidth = small ? 34 : 50;
                    var group = obstacle.GroupSize;
                    // Atlas groups are laid out as one, two, then three plants.
                    var groupOffset = group * (group - 1) / 2 * unitWidth;
                    SetSlice(image, (small ? 446 : 652) + groupOffset, 2, unitWidth * group, small ? 70 : 100);
                }
                else SetSlice(image, 260 + birdFrame * 92, 2, 92, 80);
                Place(image, obstacle.X, GroundInset + obstacle.Y, obstacle.Width, obstacle.Height);
            }
            for (var i = count; i < visibleObstacleCount; i++) obstacles[i].enabled = false;
            visibleObstacleCount = count;
        }

        private static double Wrap(double value, double period)
        {
            var result = value % period;
            return result < 0 ? result + period : result;
        }

        private void Place(RawImage image, double x, double y, double width, double height)
        {
            var rect = image.rectTransform;
            rect.anchoredPosition = new Vector2((float)(x * scale), (float)(y * scale));
            rect.sizeDelta = new Vector2((float)(width * scale), (float)(height * scale));
        }

        private void SetSlice(RawImage image, int x, int top, int width, int height)
        {
            var uv = new Rect(x / (float)atlas.width, (atlas.height - top - height) / (float)atlas.height,
                width / (float)atlas.width, height / (float)atlas.height);
            if (image.uvRect != uv) image.uvRect = uv;
        }

        private RawImage CreateSprite(Transform parent, string name)
        {
            var image = CreateRect(parent, name).gameObject.AddComponent<RawImage>();
            image.texture = atlas;
            image.material = brightSprites ? nightSpriteMaterial : daySpriteMaterial;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private void OnEnable() { if (root != null) root.gameObject.SetActive(true); }
        private void OnDisable() { if (root != null) root.gameObject.SetActive(false); }

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
            if (daySpriteMaterial != null) Destroy(daySpriteMaterial);
            if (nightSpriteMaterial != null) Destroy(nightSpriteMaterial);
            root = null;
            daySpriteMaterial = nightSpriteMaterial = null;
            atlas = null;
        }
    }
}
