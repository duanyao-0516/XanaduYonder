using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    internal static class UiFactory
    {
        public const float DesignWidth = 720;
        public const float DesignHeight = 1320;
        public static readonly Color Ink = Hex("173D35");
        public static readonly Color Muted = Hex("718179");
        public static readonly Color Paper = Hex("F6F4EC");
        public static readonly Color Accent = Hex("276653");
        public static Font Font;
        private static Sprite rounded;

        public static Color Hex(string value)
        {
            Color result;
            ColorUtility.TryParseHtmlString("#" + value, out result);
            return result;
        }

        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // Preserve baseline dimensions while distributing extra height between content groups.
        // A share of 0 stays at the top; 1 stays at the same distance from the bottom.
        public static RectTransform Band(Transform parent, string name, float top, float height, float heightShare)
        {
            var rect = Rect(parent, name, 0, 0, DesignWidth, height);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1 - heightShare);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, DesignHeight * heightShare - top);
            return rect;
        }

        public static void Center(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
        }

        public static Image Panel(Transform parent, string name, float x, float y, float w, float h, Color color, bool round = true)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (round)
            {
                image.sprite = Rounded;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        public static Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var label = Rect(parent, text, x, y, w, h).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Noto CJK uses a 1.448 em line box; truncation can hide an entire large heading.
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Button Button(Transform parent, string title, float x, float y, float w, float h, Action action, bool primary = false)
        {
            var image = Panel(parent, title, x, y, w, h, primary ? Accent : Hex("E7EDE4"));
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(.94f, .96f, .92f);
            colors.pressedColor = new Color(.78f, .85f, .78f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            Label(image.transform, title, 12, 0, w - 24, h, 25, primary ? Color.white : Ink, TextAnchor.MiddleCenter);
            button.onClick.AddListener(() => action());
            return button;
        }

        private static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 48;
                const float radius = 16;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "UI rounded rectangle";
                texture.filterMode = FilterMode.Bilinear;
                var pixels = new Color[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius));
                    var dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius));
                    var distance = new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - distance + .5f));
                }
                texture.SetPixels(pixels);
                texture.Apply();
                rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
                    SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                return rounded;
            }
        }
    }

    // Fill the physical safe area, retaining a uniform content scale for short displays.
    internal sealed class SafeDesignFrame : MonoBehaviour
    {
        private Rect lastSafe;
        private Vector2 lastSize;
        private Vector2 lastCanvasSize;
        private RectTransform frame;
        private RectTransform canvas;
        private bool widthOnly;

        // Text input pages must reflow when Android resizes their window for IME,
        // instead of uniformly shrinking the entire page to a 1320-unit minimum.
        public bool WidthOnly
        {
            get { return widthOnly; }
            set { if (widthOnly == value) return; widthOnly = value; Refresh(); }
        }

        public void Initialize(RectTransform frameRect, RectTransform canvasRect)
        {
            frame = frameRect;
            canvas = canvasRect;
            Refresh();
        }

        private void Update()
        {
            if (canvas != null && (lastSafe != Screen.safeArea || lastSize != new Vector2(Screen.width, Screen.height)
                || lastCanvasSize != canvas.rect.size)) Refresh();
        }

        private void Refresh()
        {
            if (frame == null || canvas == null || Screen.width == 0 || Screen.height == 0) return;
            var safeArea = Screen.safeArea;
            var canvasSize = canvas.rect.size;
            if (safeArea.width <= 0 || safeArea.height <= 0 || canvasSize.x <= 0 || canvasSize.y <= 0) return;
            lastSafe = safeArea;
            lastSize = new Vector2(Screen.width, Screen.height);
            lastCanvasSize = canvasSize;
            var width = canvasSize.x * safeArea.width / Screen.width;
            var height = canvasSize.y * safeArea.height / Screen.height;
            var scale = widthOnly ? width / UiFactory.DesignWidth : Mathf.Min(width / UiFactory.DesignWidth, height / UiFactory.DesignHeight);
            frame.anchorMin = frame.anchorMax = new Vector2(.5f, .5f);
            frame.pivot = new Vector2(.5f, .5f);
            frame.sizeDelta = new Vector2(width / scale, height / scale);
            frame.localScale = Vector3.one * scale;
            var center = safeArea.center;
            frame.anchoredPosition = new Vector2((center.x / Screen.width - .5f) * canvasSize.x,
                (center.y / Screen.height - .5f) * canvasSize.y);
        }
    }
}
