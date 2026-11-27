using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Runtime;

namespace XanaduYonder.EditorTools
{
    internal static class DialogTypographyChecks
    {
        internal static void Run()
        {
            CheckWindow(new Vector2(720, 1320), 720);
            CheckWindow(new Vector2(540, 990), 540);
            CheckWindow(new Vector2(800, 360), 540);
        }

        internal static void CheckProductionSheet(Button action, string heading, bool lobby = false)
        {
            Canvas.ForceUpdateCanvases();
            var sheet = action.GetComponentInParent<DialogSheetLayout>();
            var surface = (RectTransform)sheet.transform;
            var title = surface.GetComponentsInChildren<Text>().Single(label => label.text == heading);
            var caption = action.GetComponentInChildren<Text>();
            Require(Em(title) <= 15.6f && Em(caption) <= 13.6f, heading + " uses the shared mobile heading and action sizes");
            if (lobby) Require(caption.fontSize < 25, "The lobby primary action is smaller than its original 25-unit label");
            var content = (RectTransform)title.transform.parent;
            Require(Inset(content, title.rectTransform) >= 19.2f, heading + " keeps at least twenty logical units of title margin");
            Require(Length((RectTransform)action.transform, action.image.rectTransform.rect.height) >= 43,
                heading + " preserves the original 44-unit action target");
        }

        private static void CheckWindow(Vector2 size, float designWidth)
        {
            var window = new GameObject("Dialog typography fixture", typeof(RectTransform), typeof(Canvas));
            try
            {
                window.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var root = (RectTransform)window.transform;
                root.sizeDelta = size;
                var hud = UiFactory.Label(root, "积分 128", 0, 0, 240, 70, 43, UiFactory.Ink);
                var shade = UiFactory.Panel(root, "Type fixture", 0, 0, size.x, size.y, DialogUi.Scrim, false);
                DialogTypography.Attach(shade);
                var surface = UiFactory.Rect(shade.transform, "Surface", 0, 0, designWidth, 590);
                var body = UiFactory.Rect(surface, "Sheet content", 0, 0, designWidth, 590);
                DialogSheetLayout.Attach(surface, body, designWidth, 590);
                var title = UiFactory.Label(body, "对局设置", 8, 12, designWidth - 16, 72, 38, UiFactory.Ink,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                var reading = UiFactory.Label(body, "这里显示当前对局的说明。", 6, 100, designWidth - 12, 78, 35, UiFactory.Ink, TextAnchor.UpperLeft);
                var note = UiFactory.Label(body, "设置会自动保存", 30, 173, designWidth - 60, 32, 29, UiFactory.Muted, TextAnchor.MiddleLeft);
                float target = 44 * designWidth / 360;
                var toggle = DialogUi.SwitchRow(body, "声音", 26, 205, designWidth - 52, target, true, _ => {}, fontSize: 33);
                var primary = DialogUi.ActionButton(body, "完成", 26, 325, designWidth - 52, target, () => {}, true, fontSize: 33);
                Canvas.ForceUpdateCanvases();
                Require(Em(title) <= 15.6f && Em(reading) <= 13.6f, "Sheet heading and wide paragraph respect their individual type limits");
                Require(Em(note) <= 12.6f, "Secondary instructions retain their own smaller size");
                Require(Inset(body, title.rectTransform) >= 19.2f && Inset(body, reading.rectTransform) >= 15.2f,
                    "Full-width headings and paragraphs preserve both inner margins");
                foreach (var control in new[] { toggle, primary })
                {
                    var rect = (RectTransform)control.transform;
                    var label = control.GetComponentInChildren<Text>();
                    Require(Em(label) <= 13.6f && (!label.resizeTextForBestFit || label.resizeTextMaxSize <= label.fontSize),
                        "Best fit cannot enlarge a switch or button above its action size");
                    Require(Mathf.Abs(Length(rect, rect.rect.height) - 44) <= 1,
                        "Typography preserves authored button and switch hit targets through layout scaling");
                }
                Require(Inset((RectTransform)primary.transform, primary.GetComponentInChildren<Text>().rectTransform) >= 7.2f,
                    "Primary action labels keep space inside their touch targets");
                if (size.x > size.y) Require(body.GetComponentInParent<ScrollRect>()?.enabled == true,
                    "A short landscape menu scrolls to its original-size actions");
                var oldTitle = title.fontSize;
                var message = DialogUi.ScrollText(body, "使用较小字号时，两行说明完整显示。", 38, 430, designWidth * .6f,
                    designWidth * .145f, 42, fitHeight: true);
                var viewport = (RectTransform)message.transform.parent;
                Canvas.ForceUpdateCanvases();
                Require(Em(message) <= 13.6f && Mathf.Abs(viewport.rect.height - Mathf.Ceil(message.preferredHeight)) <= 1,
                    "The first reading area measures the capped font before positioning its footer");
                float reserved = viewport.rect.height;
                message.text = string.Concat(Enumerable.Repeat("A translated explanation with additional detail. ", 9));
                Canvas.ForceUpdateCanvases();
                Canvas.ForceUpdateCanvases();
                var textScroll = viewport.GetComponent<ScrollRect>();
                Require(textScroll != null && textScroll.enabled && textScroll.content.rect.height > reserved &&
                    Mathf.Abs(viewport.rect.height - reserved) <= 1,
                    "Longer localized text scrolls inside the original reading space and keeps the actions reachable");
                var rebuilt = UiFactory.Label(body, "新说明", 6, 465, designWidth - 12, 68, 35, UiFactory.Ink, TextAnchor.UpperLeft);
                Canvas.ForceUpdateCanvases();
                Require(Em(rebuilt) <= 13.6f, "A label created after first presentation receives the same typography");
                rebuilt.text = "A longer translated description of the same settings";
                Canvas.ForceUpdateCanvases();
                Canvas.ForceUpdateCanvases();
                Require(Em(rebuilt) <= 13.6f && title.fontSize == oldTitle && hud.fontSize == 43,
                    "Changing language or rendering repeatedly preserves existing type sizes and unrelated gameplay text");
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        private static float Inset(RectTransform parent, RectTransform child)
        {
            var bounds = child.rect;
            float left = parent.InverseTransformPoint(child.TransformPoint(bounds.xMin, bounds.center.y, 0)).x;
            float right = parent.InverseTransformPoint(child.TransformPoint(bounds.xMax, bounds.center.y, 0)).x;
            return Length(parent, Mathf.Min(left - parent.rect.xMin, parent.rect.xMax - right));
        }

        private static float Em(Text label) => Length(label.rectTransform, label.fontSize);
        private static float Length(RectTransform owner, float localUnits)
        {
            var root = (RectTransform)owner.GetComponentInParent<Canvas>().rootCanvas.transform;
            float factor = root.InverseTransformVector(owner.TransformVector(Vector3.up)).magnitude;
            return localUnits * factor * 360 / Mathf.Min(root.rect.width, root.rect.height);
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }
    }
}
