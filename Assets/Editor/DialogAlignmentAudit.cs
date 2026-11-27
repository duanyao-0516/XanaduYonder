using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime;

namespace XanaduYonder.EditorTools
{
    // Sample the active UGUI tree at the same render callback that writes the PNG.
    internal static class DialogAlignmentAudit
    {
        [Serializable]
        internal sealed class Label
        {
            public string text, role, alignment, control, card;
            public int controlId, group;
            public float left, width, top, controlLeft, controlWidth;
            public int lines;
            public bool direct, reading, compact;
        }

        public static Label[] Measure(Transform scope, Text heading, DialogSheetLayout sheet)
        {
            var surface = sheet != null ? sheet.Content : heading?.transform.parent as RectTransform;
            if (surface == null) surface = scope as RectTransform;
            if (surface == null || surface.rect.width < 1) return Array.Empty<Label>();
            var result = new List<Label>();
            var corners = new Vector3[4];
            var scale = 360f / surface.rect.width;

            void Bounds(RectTransform rect, out float left, out float width, out float top)
            {
                rect.GetWorldCorners(corners);
                var first = surface.InverseTransformPoint(corners[0]);
                float lo = first.x, hi = first.x, y = first.y;
                for (int index = 1; index < corners.Length; index++)
                {
                    var pt = surface.InverseTransformPoint(corners[index]);
                    lo = Mathf.Min(lo, pt.x); hi = Mathf.Max(hi, pt.x); y = Mathf.Max(y, pt.y);
                }
                left = (lo - surface.rect.xMin) * scale;
                width = (hi - lo) * scale;
                top = (surface.rect.yMax - y) * scale;
            }
            foreach (var text in scope.GetComponentsInChildren<Text>())
            {
                if (!text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text) || text.color.a <= .01f) continue;
                var owner = text.GetComponentInParent<DialogFocusScope>();
                if (owner != null && owner.transform != scope && owner.transform.IsChildOf(scope)) continue;
                var control = text.GetComponentInParent<Selectable>();
                var target = text.rectTransform;
                var label = new Label { text = text.text, lines = text.cachedTextGenerator.lineCount,
                    alignment = text.alignment.ToString(), direct = target.parent == surface };
                Bounds(target, out label.left, out label.width, out label.top);
                if (control != null)
                {
                    label.control = control.name;
                    label.controlId = control.GetInstanceID();
                    label.group = control.transform.parent != null ? control.transform.parent.GetInstanceID() : 0;
                    Bounds((RectTransform)control.transform, out label.controlLeft, out label.controlWidth, out _);
                    label.role = control.TryGetComponent<DialogSwitch>(out _) ? "switch" :
                        control.transform.Find("Disclosure") != null ? "disclosure" : "control";
                    label.compact = label.controlWidth < 220;
                }
                else
                {
                    label.role = text == heading ? "heading" : text.name.StartsWith("Section ", StringComparison.Ordinal) ? "section" : "label";
                    var scroll = text.GetComponentInParent<ScrollRect>();
                    label.reading = text.GetComponentInParent<DialogReadingArea>() != null ||
                        (scroll != null && scroll.content != null && target.IsChildOf(scroll.content) &&
                        (sheet == null || scroll.content != sheet.Content));
                    // An inset reading card inside a scroll view has its own text column.
                    if (label.reading && scroll != null && scroll.content != null)
                        for (var ancestor = target.parent; ancestor != null && ancestor != scroll.content && ancestor != surface; ancestor = ancestor.parent)
                        {
                            if (!ancestor.TryGetComponent<Image>(out var background) || !background.isActiveAndEnabled ||
                                background.color.a < .5f || ancestor.GetComponent<Mask>() != null) continue;
                            label.card = ancestor.GetInstanceID().ToString();
                            break;
                        }
                }
                result.Add(label);
            }
            return result.ToArray();
        }
    }
}
