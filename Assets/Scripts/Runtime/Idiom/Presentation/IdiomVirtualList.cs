using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Idiom
{
    // Fixed row heights let the transcript retain tens of thousands of IDs while
    // only the viewport's rows own GameObjects. Notebook uses the same viewport.
    internal sealed class IdiomVirtualList
    {
        internal sealed class Row
        {
            public RectTransform Root;
            public Button Button;
            public Text Word, Pinyin, Note, Actor;
            public IdiomTapGuard Guard;
            public CanvasGroup Appearance;
            public int Index = -1;
        }

        private readonly List<Row> rows = new List<Row>();
        private readonly RectTransform content;
        private readonly ScrollRect scroll;
        private readonly Action<Row, int> bind;
        private readonly Action<int> clicked;
        private readonly float rowHeight;
        private int count, revision, renderedRevision = -1, renderedStart = -1;
        private float lastWidth, lastHeight;
        private int appearingIndex = -1;
        private float appearedAt;
        public RectTransform Viewport { get; private set; }
        public bool NearBottom => count == 0 || MaximumOffset - Offset < 90;
        public float Offset => Mathf.Clamp(content.anchoredPosition.y, 0, MaximumOffset);
        private float MaximumOffset => Mathf.Max(0, count * rowHeight - Viewport.rect.height);

        public IdiomVirtualList(Transform parent, string name, float height, Action<Row, int> binder, Action<int> onClick)
        {
            RectTransform viewport, body;
            scroll = IdiomUi.Scroll(parent, name, out viewport, out body);
            Viewport = viewport; content = body;
            rowHeight = height; bind = binder; clicked = onClick;
            scroll.onValueChanged.AddListener(_ => Render());
        }

        public void SetCount(int value, bool bottom)
        {
            count = value; revision++;
            ResizeContent();
            if (bottom) GoBottom(); else SetOffset(Offset);
            Render();
        }

        public void SetOffset(float offset)
        {
            scroll.StopMovement();
            content.anchoredPosition = new Vector2(0, Mathf.Clamp(offset, 0, MaximumOffset));
            renderedStart = -1;
            Render();
        }

        public void GoBottom() { SetOffset(MaximumOffset); }

        public void BeginAppearance(int index)
        {
            appearingIndex = index; appearedAt = Time.unscaledTime;
            UpdateAppearance();
        }

        public void FinishAppearance()
        {
            appearingIndex = -1; UpdateAppearance();
        }

        private void UpdateAppearance()
        {
            var progress = Mathf.Clamp01((Time.unscaledTime - appearedAt) / .18f);
            foreach (var row in rows) row.Appearance.alpha = row.Index == appearingIndex ? progress : 1;
            if (progress >= 1) appearingIndex = -1;
        }

        public void Tick()
        {
            if (Mathf.Abs(lastWidth - Viewport.rect.width) > .1f || Mathf.Abs(lastHeight - Viewport.rect.height) > .1f)
            {
                var follow = lastHeight > 0 && count * rowHeight - lastHeight - content.anchoredPosition.y < 90;
                lastWidth = Viewport.rect.width; lastHeight = Viewport.rect.height;
                ResizeContent(); revision++;
                if (follow) GoBottom(); else SetOffset(Offset);
            }
            Render();
            UpdateAppearance();
        }

        private void ResizeContent()
        {
            content.sizeDelta = new Vector2(Viewport.rect.width, Mathf.Max(Viewport.rect.height, count * rowHeight));
        }

        private Row CreateRow()
        {
            var row = new Row();
            row.Root = UiFactory.Rect(content, "Reused idiom row", 0, 0, Viewport.rect.width, rowHeight);
            row.Appearance = row.Root.gameObject.AddComponent<CanvasGroup>();
            row.Button = IdiomUi.Button(row.Root, "", 0, 22, 470, 116, () =>
            {
                if (row.Guard.CanClick && row.Index >= 0 && row.Index < count) clicked(row.Index);
            });
            row.Guard = row.Button.gameObject.AddComponent<IdiomTapGuard>();
            row.Actor = IdiomUi.Label(row.Root, "", 8, 0, 150, 24, 17, UiFactory.Muted);
            row.Word = IdiomUi.Label(row.Button.transform, "", 20, 7, 430, 58, 36, UiFactory.Ink, bold: true);
            row.Pinyin = IdiomUi.Label(row.Button.transform, "", 20, 65, 430, 37, 21, UiFactory.Accent);
            row.Pinyin.horizontalOverflow = HorizontalWrapMode.Overflow;
            row.Note = IdiomUi.Label(row.Button.transform, "", 20, 85, 430, 35, 19, UiFactory.Muted);
            row.Note.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.Note.verticalOverflow = VerticalWrapMode.Truncate;
            rows.Add(row);
            return row;
        }

        private void Render()
        {
            if (!Viewport.gameObject.activeInHierarchy) return;
            var needed = Mathf.Min(count, Mathf.Max(1, Mathf.CeilToInt(Viewport.rect.height / rowHeight) + 3));
            while (rows.Count < needed) CreateRow();
            var start = Mathf.Max(0, Mathf.FloorToInt(Offset / rowHeight) - 1);
            if (renderedStart == start && renderedRevision == revision) return;
            renderedStart = start; renderedRevision = revision;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var index = start + i;
                var active = i < needed && index < count;
                row.Root.gameObject.SetActive(active);
                if (!active) { row.Index = -1; continue; }
                row.Index = index; row.Guard.BoundIndex = index;
                IdiomUi.Place(row.Root, 0, index * rowHeight, Viewport.rect.width, rowHeight);
                bind(row, index);
            }
        }
    }
}
