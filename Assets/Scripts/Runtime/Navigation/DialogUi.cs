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
            DialogTypography.Attach(overlay);
            return overlay;
        }

        public static Button ActionButton(Transform parent, string title, float x, float y, float w,
            float h, Action action, bool primary = false, int fontSize = 25, bool destructive = false,
            float unitScale = 1f)
        {
            var fill = destructive ? Destructive : primary ? UiFactory.Accent : Secondary;
            var surface = DialogCorners.Panel(parent, title, x, y, w, h, fill, DialogCorners.Action * unitScale);
            var button = MenuButton(surface, action);
            float inset = Mathf.Min(fontSize * .48f, w * .08f);
            Caption(surface.transform, title, inset, w - 2 * inset, h, fontSize,
                primary || destructive ? Color.white : UiFactory.Ink, TextAnchor.MiddleCenter);
            return button;
        }

        public static Button Disclosure(Transform parent, string title, float x, float y, float w,
            float h, Action action, int fontSize = 25, float unitScale = 1f, string description = null)
        {
            var row = DialogCorners.Panel(parent, title, x, y, w, h, Row, DialogCorners.Row * unitScale);
            var button = MenuButton(row, action);
            float inset = DisclosureInset(w, fontSize);
            float textWidth = w - 2 * inset - fontSize;
            if (string.IsNullOrEmpty(description))
                Caption(row.transform, title, inset, textWidth, h, fontSize, UiFactory.Ink, TextAnchor.MiddleLeft);
            else
            {
                var label = Caption(row.transform, title, inset, textWidth, h, fontSize, UiFactory.Ink, TextAnchor.MiddleLeft);
                int noteSize = DialogTypography.FitSize(row.transform, Mathf.RoundToInt(fontSize * .84f), DialogTypography.Note);
                var note = UiFactory.Label(row.transform, description, inset, 0, textWidth, h, noteSize, UiFactory.Muted);
                note.name = "Row description";
                note.supportRichText = false;
                float titleHeight = Mathf.Ceil(label.preferredHeight);
                float noteHeight = Mathf.Ceil(note.preferredHeight);
                float top = (h - titleHeight - noteHeight) * .5f;
                label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, titleHeight);
                label.rectTransform.anchoredPosition = new Vector2(inset, -top);
                note.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, noteHeight);
                note.rectTransform.anchoredPosition = new Vector2(inset, -top - titleHeight);
            }
            var chevron = UiFactory.Rect(row.transform, "Disclosure", w - inset - fontSize,
                (h - fontSize) * .5f, fontSize, fontSize).gameObject.AddComponent<UiIconGraphic>();
            chevron.Kind = UiIconKind.Chevron;
            chevron.color = UiFactory.Muted;
            chevron.raycastTarget = false;
            return button;
        }

        internal static float DisclosureInset(float width, int fontSize) => Mathf.Min(fontSize * .75f, width * .08f);

        // Called on a dialog whose first label is its heading. Keep the trailing clearance for a close button.
        internal static void AlignReadingTitle(RectTransform panel, float inset = 32)
        {
            var title = panel.GetComponentInChildren<Text>();
            var rect = title.rectTransform;
            float right = Mathf.Max(inset, panel.rect.width - rect.anchoredPosition.x - rect.rect.width);
            rect.anchoredPosition = new Vector2(inset, rect.anchoredPosition.y);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panel.rect.width - inset - right);
            title.alignment = TextAnchor.MiddleLeft;
        }

        public static Button SwitchRow(Transform parent, string title, float x, float y, float w, float h,
            bool value, Action<bool> changed, bool interactable = true, int fontSize = 25)
        {
            return SwitchControl(parent, title, x, y, w, h, value, changed, interactable, fontSize, false);
        }

        public static void FeedbackSwitches(Transform parent, float x, float y, float width, float height,
            float gap, bool sound, bool haptics, Action<bool> changeSound, Action<bool> changeHaptics, int fontSize = 25)
        {
            float cell = (width - gap) * .5f;
            SwitchControl(parent, "声音", x, y, cell, height, sound, changeSound, true, fontSize, true);
            SwitchControl(parent, "触感", x + cell + gap, y, cell, height, haptics, changeHaptics, true, fontSize, true);
        }

        private static Button SwitchControl(Transform parent, string title, float x, float y, float w,
            float h, bool value, Action<bool> changed, bool interactable, int fontSize, bool compact)
        {
            var row = DialogCorners.Panel(parent, title, x, y, w, h, Row, DialogCorners.Row);
            float inset = Mathf.Min(fontSize * .75f, w * .08f);
            float diameter = Mathf.Min(fontSize * 1.45f, h * .7f, w * .18f);
            float trackWidth = diameter * 1.85f;
            float gap = compact ? Mathf.Min(fontSize, w * .1f) : inset;
            float labelWidth = Mathf.Max(1, w - trackWidth - 2 * inset - gap);
            var label = Caption(row.transform, title, inset, labelWidth, h, fontSize,
                interactable ? UiFactory.Ink : UiFactory.Muted, TextAnchor.MiddleLeft);
            float trackX = w - inset - trackWidth;
            if (compact)
            {
                labelWidth = Mathf.Min(labelWidth, Mathf.Ceil(label.preferredWidth));
                float left = (w - labelWidth - gap - trackWidth) * .5f;
                label.rectTransform.sizeDelta = new Vector2(labelWidth, h);
                label.rectTransform.anchoredPosition = new Vector2(left, 0);
                trackX = left + labelWidth + gap;
            }
            var state = row.gameObject.AddComponent<DialogSwitch>();
            state.Initialize(trackX, (h - diameter) * .5f, diameter, trackWidth, value);
            var button = MenuButton(row, () =>
            {
                bool next = !state.Value;
                state.SetValueWithoutNotify(next);
                changed(next);
            });
            button.interactable = interactable;
            return button;
        }

        internal static Button MenuButton(Image surface, Action action)
        {
            var button = UiFactory.ConfigureButton<DialogMenuButton>(surface, action);
            var colors = button.colors;
            colors.selectedColor = UiFactory.Hex("BED8CC");
            button.colors = colors;
            return button;
        }

        public static Text ScrollText(Transform parent, string message, float x, float y, float w,
            float h, int fontSize, bool fitHeight = false, Text heading = null)
        {
            var viewport = UiFactory.Rect(parent, "Reading area", x, y, w, h);
            if (parent.GetComponentInParent<DialogTypography>() != null)
                fontSize = DialogTypography.FitSize(viewport, fontSize, DialogTypography.Body);
            var label = UiFactory.Label(viewport, message, 0, 0, w, h, fontSize, UiFactory.Ink,
                TextAnchor.MiddleLeft);
            label.supportRichText = false;
            viewport.gameObject.AddComponent<DialogReadingArea>().Initialize(viewport, label, h, fitHeight, heading);
            return label;
        }

        private static Text Caption(Transform parent, string title, float x, float w, float h,
            int size, Color color, TextAnchor alignment)
        {
            if (parent.GetComponentInParent<DialogTypography>() != null)
                size = DialogTypography.FitSize(parent, size, DialogTypography.Action);
            var text = UiFactory.Label(parent, title, x, 0, Mathf.Max(1, w), h, size, color, alignment);
            text.supportRichText = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(size * .8f));
            text.resizeTextMaxSize = size;
            return text;
        }
    }
}
