using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // Draw all preferences in one vertical reading order. A custom control can use Reserve.
    internal sealed class DialogSheetSections
    {
        private readonly RectTransform content;
        private readonly float rowHeight, gap, unit;
        public float Top { get; private set; }
        public float Inset { get; }
        public float Width => content.rect.width - 2 * Inset;

        public DialogSheetSections(RectTransform content, float top = 94, float inset = 32, float rowHeight = 0, float gap = 12)
        {
            this.content = content;
            this.gap = gap;
            unit = content.rect.width / 720f;
            this.rowHeight = Mathf.Max(rowHeight, Mathf.Ceil(88 * unit));
            Inset = Mathf.Max(inset, DialogTypography.ReadingInset * 2 * unit);
            Top = top;
        }

        public float Reserve(float height)
        {
            float start = Top;
            Top += height + gap;
            return start;
        }

        public void Heading(string title)
        {
            Top += gap;
            float height = 48 * unit;
            var label = UiFactory.Label(content, title, Inset, Reserve(height), Width, height,
                Mathf.RoundToInt(24 * unit), UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            label.name = "Section " + title;
            label.supportRichText = false;
        }

        public Text Body(string message, int fontSize = 24)
        {
            int size = DialogTypography.FitSize(content, fontSize, DialogTypography.Body);
            var text = UiFactory.Label(content, message, Inset, Top, Width, rowHeight, size, UiFactory.Ink, TextAnchor.UpperLeft);
            text.supportRichText = false;
            float height = Mathf.Ceil(text.preferredHeight + 4 * unit);
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            Reserve(height);
            return text;
        }

        public Button Switch(string title, bool value, Action<bool> changed, bool enabled = true)
            => DialogUi.SwitchRow(content, title, Inset, Reserve(rowHeight), Width, rowHeight, value, changed, enabled,
                Mathf.RoundToInt(23 * unit));

        public Button Entry(string title, Action select)
        {
            int size = Mathf.RoundToInt(23 * unit);
            float padding = DialogUi.DisclosureInset(Width, size);
            return DialogUi.Disclosure(content, title, Inset - padding, Reserve(rowHeight),
                Width + 2 * padding, rowHeight, select, size);
        }

        public Button Action(string title, Action tap, bool primary = false, bool enabled = true)
        {
            var button = DialogUi.ActionButton(content, title, Inset, Reserve(rowHeight), Width, rowHeight, tap, primary,
                Mathf.RoundToInt(23 * unit));
            button.interactable = enabled;
            return button;
        }

        public void Finish(float bottomPadding = 28) => DialogSheetLayout.SetContentHeight(content, Top - gap + bottomPadding);
    }
}
