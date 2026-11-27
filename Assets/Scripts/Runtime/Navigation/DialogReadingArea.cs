using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // Measure again when the text, font or available width changes, including after orientation changes.
    [ExecuteAlways]
    internal sealed class DialogReadingArea : MonoBehaviour
    {
        private RectTransform viewport;
        private Text label;
        private ScrollRect scroll;
        private RectMask2D mask;
        private Image hitArea;
        private float height;
        private bool measuring, queued, centeredWhenShort;

        internal void Initialize(RectTransform frame, Text body, float maximumHeight, bool fitHeight)
        {
            viewport = frame;
            label = body;
            centeredWhenShort = fitHeight;
            // A compact message card places its footer just below this viewport. Later changes
            // to the message must scroll within this space to keep its actions reachable.
            height = fitHeight ? Mathf.Min(maximumHeight, Mathf.Ceil(body.preferredHeight)) : maximumHeight;
            viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            label.RegisterDirtyLayoutCallback(Queue);
            Measure();
        }
        private void OnEnable() { if (label != null) { label.RegisterDirtyLayoutCallback(Queue); Queue(); } }
        private void OnDisable()
        {
            if (label != null) label.UnregisterDirtyLayoutCallback(Queue);
            Canvas.preWillRenderCanvases -= Measure;
            queued = false;
        }
        private void OnRectTransformDimensionsChange() { Queue(); }
        private void Queue()
        {
            if (label == null || !isActiveAndEnabled || queued || measuring) return;
            queued = true;
            Canvas.preWillRenderCanvases += Measure;
        }

        private void Measure()
        {
            Canvas.preWillRenderCanvases -= Measure;
            queued = false;
            if (label == null || !isActiveAndEnabled) return;
            measuring = true;
            try
            {
                float measured = Mathf.Ceil(label.preferredHeight);
                bool overflow = measured > height + .01f;
                var rect = label.rectTransform;
                float contentHeight = Mathf.Max(height, measured);
                if (Mathf.Abs(rect.rect.height - contentHeight) > .01f)
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
                var alignment = overflow ? TextAnchor.UpperLeft :
                    centeredWhenShort && label.text.IndexOf('\n') < 0 && label.preferredWidth <= rect.rect.width ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
                if (label.alignment != alignment) label.alignment = alignment;
                if (overflow && scroll == null)
                {
                    hitArea = gameObject.AddComponent<Image>();
                    hitArea.color = Color.clear;
                    mask = gameObject.AddComponent<RectMask2D>();
                    scroll = gameObject.AddComponent<ScrollRect>();
                    scroll.viewport = viewport;
                    scroll.content = rect;
                    scroll.horizontal = false;
                    scroll.movementType = ScrollRect.MovementType.Clamped;
                    scroll.scrollSensitivity = label.fontSize;
                }
                if (scroll != null)
                {
                    scroll.enabled = overflow;
                    mask.enabled = overflow;
                    hitArea.raycastTarget = overflow;
                    if (!overflow) rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 0);
                }
            }
            finally { measuring = false; }
        }
    }
}
