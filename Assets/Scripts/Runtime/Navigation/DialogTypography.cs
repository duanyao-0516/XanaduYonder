using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // The games author their interfaces on different canvases. Use the rendered short edge,
    // including any local text supersampling, to compare typography on the same 360-unit scale.
    [ExecuteAlways]
    internal sealed class DialogTypography : MonoBehaviour
    {
        internal const float Title = 15, Body = 13, Note = 12, Action = 13;
        internal const float ReadingInset = 20;
        private static int activeScopes;
        private bool queued, applying;
        private readonly List<Text> labels = new List<Text>();
        private readonly Dictionary<Text, Original> originals = new Dictionary<Text, Original>();
        private readonly HashSet<Text> current = new HashSet<Text>();
        private readonly List<Text> retired = new List<Text>();
        private readonly Vector3[] corners = new Vector3[4];

        private sealed class Original
        {
            public int size, min, max, appliedSize = -1, appliedMin = -1, appliedMax = -1;
            public Vector2 initialLow, initialHigh, appliedLow, appliedHigh;
            public bool inset;
        }

        internal static void Attach(Graphic shade)
        {
            if (!shade.TryGetComponent<DialogTypography>(out _)) shade.gameObject.AddComponent<DialogTypography>();
        }

        internal static void TextCreated(Text label)
        {
            if (activeScopes > 0) label.GetComponentInParent<DialogTypography>()?.Queue();
        }

        internal static int FitSize(Transform local, int authored, float limit)
        {
            var canvas = local.GetComponentInParent<Canvas>();
            if (canvas == null) return authored;
            var boundary = (RectTransform)canvas.rootCanvas.transform;
            float dp = Unit(boundary, local, Vector3.up);
            return dp <= .0001f ? authored : Mathf.Clamp(Mathf.FloorToInt(limit / dp + .001f), 1, authored);
        }

        private static float Unit(RectTransform boundary, Transform local, Vector3 axis)
        {
            float edge = Mathf.Min(boundary.rect.width, boundary.rect.height);
            return edge <= 0 ? 0 : boundary.InverseTransformVector(local.TransformVector(axis)).magnitude * 360f / edge;
        }

        private void OnEnable() { activeScopes++; Queue(); }
        private void OnDisable()
        {
            activeScopes--;
            Canvas.preWillRenderCanvases -= Apply;
            queued = false;
            foreach (var item in originals)
                if (item.Key != null) item.Key.UnregisterDirtyLayoutCallback(Queue);
        }
        private void OnRectTransformDimensionsChange() { Queue(); }
        private void OnTransformChildrenChanged() { Queue(); }

        private void Queue()
        {
            if (queued || applying || !isActiveAndEnabled) return;
            queued = true;
            Canvas.preWillRenderCanvases += Apply;
        }

        private void Apply()
        {
            Canvas.preWillRenderCanvases -= Apply;
            queued = false;
            var canvas = GetComponentInParent<Canvas>();
            if (!isActiveAndEnabled || canvas == null) return;
            var boundary = (RectTransform)canvas.rootCanvas.transform;
            if (Mathf.Min(boundary.rect.width, boundary.rect.height) <= 0) return;
            applying = true;
            try
            {
                GetComponentsInChildren(true, labels);
                current.Clear();
                current.UnionWith(labels);
                retired.Clear();
                foreach (var old in originals.Keys)
                    if (!current.Contains(old)) retired.Add(old);
                foreach (var old in retired)
                {
                    if (old != null) old.UnregisterDirtyLayoutCallback(Queue);
                    originals.Remove(old);
                }
                Text heading = null;
                float largest = 0;
                foreach (var label in labels)
                {
                    if (!originals.TryGetValue(label, out var style))
                        originals.Add(label, style = new Original());
                    label.UnregisterDirtyLayoutCallback(Queue);
                    label.RegisterDirtyLayoutCallback(Queue);
                    if (label.fontSize != style.appliedSize) style.size = label.fontSize;
                    if (label.resizeTextMinSize != style.appliedMin) style.min = label.resizeTextMinSize;
                    if (label.resizeTextMaxSize != style.appliedMax) style.max = label.resizeTextMaxSize;
                    if (!label.isActiveAndEnabled || label.GetComponentInParent<Selectable>() != null || string.IsNullOrWhiteSpace(label.text) || label.text.Length > 40) continue;
                    float score = style.size * Unit(boundary, label.transform, Vector3.up);
                    if (label.name.IndexOf("title", System.StringComparison.OrdinalIgnoreCase) >= 0) score += 64;
                    else if (label.fontStyle == FontStyle.Bold || label.fontStyle == FontStyle.BoldAndItalic) score += 12;
                    if (score > largest) { largest = score; heading = label; }
                }
                var sheet = GetComponentInChildren<DialogSheetLayout>(true);
                foreach (var label in labels)
                {
                    var style = originals[label];
                    bool action = label.GetComponentInParent<Selectable>() != null;
                    float cap = label == heading ? Title : label.color == UiFactory.Muted ? Note : action ? Action : Body;
                    var local = label.rectTransform;
                    int size = FitSize(local, style.size, cap);
                    if (label.fontSize != size) label.fontSize = size;
                    style.appliedSize = size;
                    int max = Mathf.Min(style.max, size), min = Mathf.Min(style.min, max);
                    if (label.resizeTextForBestFit)
                    {
                        if (label.resizeTextMinSize != min) label.resizeTextMinSize = min;
                        if (label.resizeTextMaxSize != max) label.resizeTextMaxSize = max;
                    }
                    style.appliedMin = label.resizeTextMinSize;
                    style.appliedMax = label.resizeTextMaxSize;
                    if (!action) Pad(local, sheet, boundary, label == heading, style);
                }
            }
            finally { applying = false; }
        }

        private void Pad(RectTransform text, DialogSheetLayout sheet, RectTransform boundary, bool heading, Original style)
        {
            if (style.inset && text.offsetMin == style.appliedLow && text.offsetMax == style.appliedHigh)
            {
                text.offsetMin = style.initialLow;
                text.offsetMax = style.initialHigh;
            }
            style.inset = false;
            var parent = text.parent as RectTransform;
            if (parent == null) return;
            RectTransform surface = null;
            if (sheet != null && sheet.Content != null && text.IsChildOf(sheet.Content)) surface = sheet.Content;
            else
                for (var node = parent; node != null && node != transform; node = node.parent as RectTransform)
                    if (node.TryGetComponent<Graphic>(out var paint) && paint.color.a > .5f)
                    { surface = node; break; }
            if (surface == null) return;
            float dp = Unit(boundary, surface, Vector3.right);
            if (dp <= .0001f || surface.rect.width * dp < 145) return;
            text.GetWorldCorners(corners);
            float left = surface.InverseTransformPoint(corners[0]).x - surface.rect.xMin;
            float right = surface.rect.xMax - surface.InverseTransformPoint(corners[3]).x;
            if (!heading && surface.rect.width - left - right < surface.rect.width * .65f) return;
            float inset = ReadingInset / dp;
            float start = Mathf.Max(0, inset - left), end = Mathf.Max(0, inset - right);
            float units = surface.InverseTransformVector(parent.TransformVector(Vector3.right)).magnitude;
            if (units <= .0001f || start + end < .01f || text.rect.width * Mathf.Abs(text.localScale.x) < (start + end) / units + 8) return;
            style.initialLow = text.offsetMin;
            style.initialHigh = text.offsetMax;
            // Supersampled labels have a scaled rect and an unscaled anchored position.
            float localScale = Mathf.Abs(text.localScale.x);
            float shrink = (start + end) / (units * localScale);
            float shift = start / units - text.pivot.x * (start + end) / units;
            text.sizeDelta -= new Vector2(shrink, 0);
            text.anchoredPosition += new Vector2(shift, 0);
            style.appliedLow = text.offsetMin;
            style.appliedHigh = text.offsetMax;
            style.inset = true;
        }
    }
}
