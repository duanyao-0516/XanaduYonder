using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Watermelon
{
    internal sealed class WatermelonArtwork : IDisposable
    {
        public static readonly string[] ThemeNames = { "水果", "表情", "校徽" };
        private static readonly string[] Folders = { "Fruit", "Emoji", "Campus" };
        public static readonly string[] CampusNames = { "中国海洋", "湖南", "山东", "华东师范", "电子科大", "四川", "复旦", "上海交大", "浙江", "北京", "清华" };
        private readonly Texture2D[,] textures = new Texture2D[3, 11];
        private readonly Texture2D circleTexture;
        private readonly Sprite circle;

        public WatermelonArtwork()
        {
            const int size = 128;
            circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Watermelon circular mask", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(size * .5f - new Vector2(x + .5f - size * .5f, y + .5f - size * .5f).magnitude));
            circleTexture.SetPixels(pixels);
            circleTexture.Apply(false, true);
            circle = Sprite.Create(circleTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }

        public Texture2D Texture(int theme, int level)
        {
            if (textures[theme, level] == null)
                textures[theme, level] = Resources.Load<Texture2D>("Watermelon/" + Folders[theme] + "/" + (level + 1));
            if (textures[theme, level] == null) throw new InvalidOperationException("Missing Watermelon artwork " + theme + "/" + level);
            return textures[theme, level];
        }

        public WatermelonIcon Icon(Transform parent, string name, float x, float y, float size, int theme, int level)
        {
            var rect = UiFactory.Rect(parent, name, x, y, size, size);
            rect.pivot = new Vector2(.5f, .5f);
            var backing = rect.gameObject.AddComponent<Image>();
            backing.sprite = circle;
            backing.raycastTarget = false;
            var mask = rect.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var image = UiFactory.Rect(rect, "Artwork", 0, 0, size, size).gameObject.AddComponent<RawImage>();
            UiFactory.Stretch(image.rectTransform);
            image.raycastTarget = false;
            var icon = new WatermelonIcon(rect, backing, mask, image, this);
            icon.Set(theme, level);
            return icon;
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(circle);
            UnityEngine.Object.Destroy(circleTexture);
        }
    }

    internal sealed class WatermelonIcon
    {
        public readonly RectTransform Rect;
        private readonly Image backing;
        private readonly Mask mask;
        private readonly RawImage image;
        private readonly WatermelonArtwork artwork;
        public WatermelonIcon(RectTransform rect, Image backing, Mask mask, RawImage image, WatermelonArtwork artwork)
        { Rect = rect; this.backing = backing; this.mask = mask; this.image = image; this.artwork = artwork; }
        public void Set(int theme, int level)
        {
            backing.enabled = mask.enabled = theme == 2;
            image.texture = artwork.Texture(theme, level);
        }
        public void Tint(Color color) { image.color = color; backing.color = color; }
    }
}
