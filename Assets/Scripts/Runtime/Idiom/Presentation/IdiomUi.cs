using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Idiom
{
    internal static class IdiomUi
    {
        public static Text Label(Transform parent, string text, float x, float y, float width, float height,
            int size = 24, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft, bool bold = false)
        {
            var label = UiFactory.Label(parent, text, x, y, width, height, size, color ?? UiFactory.Ink,
                align, bold ? FontStyle.Bold : FontStyle.Normal);
            label.supportRichText = false;
            return label;
        }

        public static Button Button(Transform parent, string title, float x, float y, float width, float height,
            Action action, bool primary = false, int size = 24)
        {
            var button = UiFactory.Button(parent, title, x, y, width, height, action, primary);
            var label = button.GetComponentInChildren<Text>();
            label.supportRichText = false;
            label.fontSize = size;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, Mathf.Max(1, height));
        }

        public static ScrollRect Scroll(Transform parent, string name, out RectTransform viewport, out RectTransform content)
        {
            viewport = UiFactory.Rect(parent, name, 0, 0, 640, 400);
            var image = viewport.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = UiFactory.Rect(viewport, name + " content", 0, 0, 640, 400);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.decelerationRate = .12f;
            scroll.scrollSensitivity = 42;
            return scroll;
        }

        public static void ScrollText(Transform parent, string text, float x, float y, float width, float height)
        {
            RectTransform viewport, content;
            Scroll(parent, "Scrollable explanation", out viewport, out content);
            Place(viewport, x, y, width, height);
            var label = Label(content, text, 2, 0, width - 12, height, 24, UiFactory.Ink, TextAnchor.UpperLeft);
            var fullHeight = Mathf.Max(height, label.preferredHeight + 20);
            Place(content, 0, 0, width, fullHeight);
            Place(label.rectTransform, 2, 0, width - 12, fullHeight);
        }

        public static void Caption(Button button, string text)
        {
            button.GetComponentInChildren<Text>().text = text;
        }
    }
}
