using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // Menu and dialog controls. Gameplay touch surfaces keep their own input and layout.
    internal static class DialogUi
    {
        internal static readonly Color Surface = UiFactory.Hex("FAFAF5");
        internal static readonly Color Secondary = UiFactory.Hex("E5EDE6");
        internal static readonly Color Row = UiFactory.Hex("F0F3EC");
        internal static readonly Color Scrim = new Color(0f, 0f, 0f, .40f);
        internal static readonly Color TutorialScrim = new Color(0f, 0f, 0f, .16f);
        internal static readonly Color Destructive = UiFactory.Hex("9E403C");

        public static Image ModalOverlay(RectTransform parent, string name, Color? scrim = null)
        {
            var overlay = UiFactory.Panel(parent, name, 0, 0, 0, 0, scrim ?? Scrim, false);
            UiFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            if (!parent.TryGetComponent<Canvas>(out _)) DialogBackdrop.CoverCanvas(overlay);
            overlay.gameObject.AddComponent<DialogFocusScope>().BlockBackground(parent);
            return overlay;
        }

        public static Button ActionButton(Transform parent, string title, float x, float y, float w,
            float h, Action action, bool primary = false, int fontSize = 25, bool destructive = false)
        {
            var fill = destructive ? Destructive : primary ? UiFactory.Accent : Secondary;
            var surface = UiFactory.Panel(parent, title, x, y, w, h, fill);
            var button = MenuButton(surface, action);
            float inset = Mathf.Min(fontSize * .48f, w * .08f);
            Caption(surface.transform, title, inset, w - 2 * inset, h, fontSize,
                primary || destructive ? Color.white : UiFactory.Ink, TextAnchor.MiddleCenter);
            return button;
        }

        public static Button Disclosure(Transform parent, string title, float x, float y, float w,
            float h, Action action, int fontSize = 25)
        {
            var row = UiFactory.Panel(parent, title, x, y, w, h, Row);
            var button = MenuButton(row, action);
            float inset = Mathf.Min(fontSize * .75f, w * .08f);
            Caption(row.transform, title, inset, w - 2 * inset - fontSize, h, fontSize,
                UiFactory.Ink, TextAnchor.MiddleLeft);
            var chevron = UiFactory.Rect(row.transform, "Disclosure", w - inset - fontSize,
                (h - fontSize) * .5f, fontSize, fontSize).gameObject.AddComponent<UiIconGraphic>();
            chevron.Kind = UiIconKind.Chevron;
            chevron.color = UiFactory.Muted;
            chevron.raycastTarget = false;
            return button;
        }

        public static Button SwitchRow(Transform parent, string title, float x, float y, float w, float h,
            bool value, Action<bool> changed, bool interactable = true, int fontSize = 25)
        {
            var row = UiFactory.Panel(parent, title, x, y, w, h, Row);
            float inset = Mathf.Min(fontSize * .75f, w * .08f);
            float diameter = Mathf.Min(fontSize * 1.45f, h * .7f, w * .18f);
            float trackWidth = diameter * 1.85f;
            Caption(row.transform, title, inset, w - trackWidth - 3 * inset, h, fontSize,
                interactable ? UiFactory.Ink : UiFactory.Muted, TextAnchor.MiddleLeft);
            var state = row.gameObject.AddComponent<DialogSwitch>();
            state.Initialize(w - inset - trackWidth, (h - diameter) * .5f, diameter, trackWidth, value);
            var button = MenuButton(row, () =>
            {
                bool next = !state.Value;
                state.SetValueWithoutNotify(next);
                changed(next);
            });
            button.interactable = interactable;
            return button;
        }

        private static Button MenuButton(Image surface, Action action)
        {
            var button = UiFactory.ConfigureButton(surface, action);
            var colors = button.colors;
            colors.selectedColor = UiFactory.Hex("BED8CC");
            button.colors = colors;
            return button;
        }

        public static Text ScrollText(Transform parent, string message, float x, float y, float w,
            float h, int fontSize)
        {
            var viewport = UiFactory.Rect(parent, "Reading area", x, y, w, h);
            var label = UiFactory.Label(viewport, message, 0, 0, w, h, fontSize, UiFactory.Ink,
                TextAnchor.MiddleLeft);
            label.supportRichText = false;
            float measured = Mathf.Ceil(label.preferredHeight);
            if (measured <= h) return label;

            var touchSurface = viewport.gameObject.AddComponent<Image>();
            touchSurface.color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            label.alignment = TextAnchor.UpperLeft;
            label.rectTransform.sizeDelta = new Vector2(w, measured);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = label.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = fontSize;
            return label;
        }

        private static Text Caption(Transform parent, string title, float x, float w, float h,
            int size, Color color, TextAnchor alignment)
        {
            var text = UiFactory.Label(parent, title, x, 0, Mathf.Max(1, w), h, size, color, alignment);
            text.supportRichText = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(size * .8f));
            text.resizeTextMaxSize = size;
            return text;
        }
    }
}
