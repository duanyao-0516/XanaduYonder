using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.AgeOfWar
{
    internal sealed class AgeOfWarMenuDialog : IDisposable
    {
        private readonly RectTransform parent;
        private readonly int edition;
        private readonly Func<bool> sound;
        private readonly Action<bool> setSound;
        private readonly Func<bool> haptics;
        private readonly Action<bool> setHaptics;
        private readonly Action closed;
        private RectTransform overlay;
        private ScrollRect scroll;
        private Text soundState, hapticsState;
        private float width, height;
        private bool help, disposed;

        public bool IsOpen { get { return overlay != null && overlay.gameObject.activeSelf; } }

        public AgeOfWarMenuDialog(RectTransform parent, int edition, Func<bool> sound, Action<bool> setSound,
            Func<bool> haptics, Action<bool> setHaptics, Action closed)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (edition != 1 && edition != 2) throw new ArgumentOutOfRangeException(nameof(edition));
            this.parent = parent;
            this.edition = edition;
            this.sound = sound ?? throw new ArgumentNullException(nameof(sound));
            this.setSound = setSound ?? throw new ArgumentNullException(nameof(setSound));
            this.haptics = haptics ?? throw new ArgumentNullException(nameof(haptics));
            this.setHaptics = setHaptics ?? throw new ArgumentNullException(nameof(setHaptics));
            this.closed = closed;
            width = parent.rect.width > 0 ? parent.rect.width : (edition == 1 ? 650 : 720);
            height = parent.rect.height > 0 ? parent.rect.height : (edition == 1 ? 450 : 480);
        }

        public void ShowHelp()
        {
            if (disposed) return;
            help = true;
            Build(1);
        }

        public void ShowSettings()
        {
            if (disposed) return;
            help = false;
            Build(1);
        }

        public void SetSize(float width, float height)
        {
            if (disposed || float.IsNaN(width) || float.IsInfinity(width) || float.IsNaN(height) || float.IsInfinity(height) || width <= 0 || height <= 0) return;
            if (Mathf.Approximately(this.width, width) && Mathf.Approximately(this.height, height)) return;
            this.width = width; this.height = height;
            if (IsOpen) Build(scroll == null ? 1 : scroll.verticalNormalizedPosition);
        }

        public void Close()
        {
            if (!IsOpen) return;
            DestroyVisuals();
            // Notify only after the dialog stops intercepting input. Repeated Close is harmless.
            closed?.Invoke();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // Owner teardown is not a user dismissal and must not re-enter navigation callbacks.
            DestroyVisuals();
        }

        private void Build(float position)
        {
            DestroyVisuals();
            overlay = UiFactory.Rect(parent, "Age of War menu dialog", 0, 0, width, height);
            overlay.SetAsLastSibling();
            var shade = overlay.gameObject.AddComponent<Image>();
            shade.color = new Color(0, 0, 0, .62f);
            shade.raycastTarget = true;

            float margin = Mathf.Min(14, Mathf.Min(width, height) * .04f);
            float panelWidth = Mathf.Min(help ? 720 : 500, width - margin * 2);
            float panelHeight = Mathf.Min(help ? 430 : 310, height - margin * 2);
            var paper = UiFactory.Panel(overlay, "Dialog paper", (width - panelWidth) * .5f,
                (height - panelHeight) * .5f, panelWidth, panelHeight, UiFactory.Paper);
            paper.raycastTarget = true;
            var panel = paper.rectTransform;

            var icon = UiFactory.Rect(panel, "Dialog icon", 18, 17, 25, 25).gameObject.AddComponent<UiIconGraphic>();
            icon.Kind = help ? UiIconKind.Book : UiIconKind.Settings;
            icon.color = UiFactory.Accent; icon.raycastTarget = false;
            var title = UiFactory.Label(panel, help ? "战争进化史" + (edition == 1 ? "一代" : "二代") + " · 玩法" : "设置",
                52, 11, Mathf.Max(1, panelWidth - 116), 40, 20, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.supportRichText = false;
            UiFactory.IconButton(panel, UiIconKind.Close, panelWidth - 54, 10, 40, 40, Close);

            float viewportWidth = Mathf.Max(1, panelWidth - 44);
            float viewportHeight = Mathf.Max(1, panelHeight - 114);
            var viewport = UiFactory.Rect(panel, "Scrollable dialog viewport", 20, 62, viewportWidth, viewportHeight);
            var surface = viewport.gameObject.AddComponent<Image>(); surface.color = Color.clear; surface.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiFactory.Rect(viewport, "Dialog content", 0, 0, viewportWidth, 0);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true; scroll.decelerationRate = .135f; scroll.scrollSensitivity = 24;
            float bodyHeight = help ? BuildHelp(content, viewportWidth) : BuildSettings(content, viewportWidth);
            content.sizeDelta = new Vector2(viewportWidth, Mathf.Max(viewportHeight, bodyHeight));
            BuildScrollbar(panel, panelWidth - 15, 62, viewportHeight);
            scroll.verticalNormalizedPosition = Mathf.Clamp01(position);

            var hint = UiFactory.Label(panel, help ? "上下滑动查看" : "设置在所有游戏中共用", 20, panelHeight - 42,
                Mathf.Max(1, panelWidth - 138), 30, 13, UiFactory.Muted);
            hint.supportRichText = false;
            SmallButton(panel, help ? "知道了" : "完成", panelWidth - 106, panelHeight - 43, 86, 32, Close);
        }

        private float BuildHelp(RectTransform content, float contentWidth)
        {
            float top = 0;
            foreach (var section in AgeOfWarHelpContent.ForEdition(edition))
            {
                var heading = UiFactory.Label(content, section.Title, 0, top, contentWidth, 1, 17,
                    UiFactory.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
                heading.supportRichText = false;
                float headingHeight = Mathf.Ceil(heading.preferredHeight);
                heading.rectTransform.sizeDelta = new Vector2(contentWidth, headingHeight);
                top += headingHeight + 6;
                var body = UiFactory.Label(content, section.Body, 0, top, contentWidth, 1, 16, UiFactory.Muted, TextAnchor.UpperLeft);
                body.supportRichText = false;
                float bodyHeight = Mathf.Ceil(body.preferredHeight);
                body.rectTransform.sizeDelta = new Vector2(contentWidth, bodyHeight);
                top += bodyHeight + 22;
            }
            return top;
        }

        private float BuildSettings(RectTransform content, float contentWidth)
        {
            soundState = SettingRow(content, "声音", "游戏音乐与音效", 0, contentWidth, () =>
            {
                setSound(!sound());
                RefreshSettings();
            });
            hapticsState = SettingRow(content, "触感", "共用触感反馈开关", 72, contentWidth, () =>
            {
                setHaptics(!haptics());
                RefreshSettings();
            });
            RefreshSettings();
            return 140;
        }

        private static Text SettingRow(RectTransform content, string title, string description, float y, float rowWidth, Action toggle)
        {
            var background = UiFactory.Panel(content, title, 0, y, rowWidth, 62, UiFactory.Hex("E7EDE4"));
            UiFactory.ConfigureButton(background, toggle);
            var name = UiFactory.Label(background.transform, title, 14, 5, Mathf.Max(1, rowWidth - 112), 28, 18, UiFactory.Ink);
            name.supportRichText = false;
            var explanation = UiFactory.Label(background.transform, description, 14, 33, Mathf.Max(1, rowWidth - 112), 23, 12, UiFactory.Muted);
            explanation.supportRichText = false;
            var state = UiFactory.Label(background.transform, "", rowWidth - 88, 8, 74, 46, 17, UiFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            state.supportRichText = false;
            return state;
        }

        private void RefreshSettings()
        {
            if (soundState != null) SetState(soundState, sound());
            if (hapticsState != null) SetState(hapticsState, haptics());
        }

        private static void SetState(Text label, bool enabled)
        { label.text = enabled ? "开启" : "关闭"; label.color = enabled ? UiFactory.Accent : UiFactory.Muted; }

        private void BuildScrollbar(RectTransform panel, float x, float y, float barHeight)
        {
            var track = UiFactory.Panel(panel, "Scroll track", x, y, 5, barHeight, UiFactory.Hex("E1E6DC"));
            var slide = UiFactory.Rect(track.transform, "Sliding area", 0, 0, 5, barHeight);
            UiFactory.Stretch(slide);
            var handle = UiFactory.Panel(slide, "Scroll handle", 0, 0, 5, 30, UiFactory.Hex("90A88E"));
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.targetGraphic = handle; bar.handleRect = handle.rectTransform;
            track.raycastTarget = true; handle.raycastTarget = true;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private static void SmallButton(RectTransform parent, string title, float x, float y, float width, float height, Action action)
        {
            var image = UiFactory.Panel(parent, title, x, y, width, height, UiFactory.Accent);
            UiFactory.ConfigureButton(image, action);
            UiFactory.Label(image.transform, title, 0, 0, width, height, 16, Color.white, TextAnchor.MiddleCenter).supportRichText = false;
        }

        private void DestroyVisuals()
        {
            soundState = hapticsState = null;
            scroll = null;
            if (overlay == null) return;
            // Destroy is deferred; deactivate immediately so its raycasts cannot overlap the new dialog.
            overlay.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(overlay.gameObject);
            overlay = null;
        }
    }
}
