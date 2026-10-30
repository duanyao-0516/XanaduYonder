using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // Native UGUI rendering of the approved 390-unit lobby. No game sessions or save data live here.
    internal sealed class LobbyView : MonoBehaviour
    {
        private const float LogicalWidth = 390;
        private const int TextSampling = 2;
        private const float CardsTop = 201.4f;
        private static readonly Color SecondaryText = UiFactory.Hex("5D7468");
        private static readonly Color ControlColor = UiFactory.Hex("E7EDE4");
        private static readonly string[] Categories = { "逻辑策略", "合成消除", "敏捷挑战", "文字语言" };
        private static readonly string[] ModeTitles = { "双列卡片", "分类目录", "单列大卡" };
        private static readonly string[] ModeNotes = {
            "图标与名称同行 · 一行纪录", "图标与名称 · 紧凑排列", "大 Logo · 玩法介绍与纪录"
        };

        private readonly List<LobbyEntry> entries = new List<LobbyEntry>();
        private readonly Dictionary<string, LobbyEntry> entriesById = new Dictionary<string, LobbyEntry>();
        private readonly Dictionary<string, Text> recordLabels = new Dictionary<string, Text>();
        private RectTransform logicalFrame;
        private RectTransform content;
        private RectTransform cards;
        private RectTransform header;
        private RectTransform toolbar;
        private Text footer;
        private Text modeLabel;
        private CanvasGroup backgroundInteraction;
        private GameObject displayOptions;
        private GameObject previousSelection;
        private Button displayButton;
        private Action<LobbyDisplayMode> modeChanged;
        private bool initialized;
        private bool scrollEnabledBeforeOptions;

        public ScrollRect Scroll { get; private set; }
        public Text StatusLabel { get; private set; }
        public LobbyDisplayMode Mode { get; private set; }
        public bool HasDisplayOptions { get { return displayOptions != null && displayOptions.activeSelf; } }
        public IReadOnlyList<LobbyEntry> Entries { get; private set; }

        public void Initialize(IReadOnlyList<LobbyEntry> source, Action openSettings,
            LobbyDisplayMode initialMode, Action<LobbyDisplayMode> onModeChanged)
        {
            if (initialized) throw new InvalidOperationException("LobbyView can only be initialized once.");
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (openSettings == null) throw new ArgumentNullException(nameof(openSettings));
            foreach (var entry in source)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Id) || entry.Enter == null)
                    throw new ArgumentException("Every lobby entry must have an id and its real enter action.", nameof(source));
                if (Array.IndexOf(Categories, entry.Category) < 0)
                    throw new ArgumentException("Unknown lobby category: " + entry.Category, nameof(source));
                entriesById.Add(entry.Id, entry);
                entries.Add(entry);
            }
            Entries = entries.AsReadOnly();
            modeChanged = onModeChanged;
            Mode = LobbyDisplayPreferences.Normalize(initialMode);

            logicalFrame = UiFactory.Rect(transform, "Lobby logical frame", 0, 0, LogicalWidth, 715);
            UpdateLogicalFrame();
            var viewport = UiFactory.Rect(logicalFrame, "Lobby list viewport", 0, 0, LogicalWidth, 715);
            UiFactory.Stretch(viewport);
            var surface = viewport.gameObject.AddComponent<Image>();
            surface.color = UiFactory.Paper;
            surface.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            backgroundInteraction = viewport.gameObject.AddComponent<CanvasGroup>();
            content = UiFactory.Rect(viewport, "Lobby list content", 0, 0, LogicalWidth, 900);
            Scroll = viewport.gameObject.AddComponent<ScrollRect>();
            Scroll.viewport = viewport;
            Scroll.content = content;
            Scroll.horizontal = false;
            Scroll.vertical = true;
            Scroll.movementType = ScrollRect.MovementType.Elastic;
            Scroll.elasticity = .12f;
            Scroll.decelerationRate = .10f;
            Scroll.scrollSensitivity = 20;

            BuildHeader(openSettings);
            footer = Label(content, "Lobby footer", "全部游戏 · 无需联网 · 无广告", 18, 0, 354, 18.15f,
                11, SecondaryText, TextAnchor.MiddleCenter);
            StatusLabel = Label(content, "Lobby status", "", 18, 0, 354, 18, 11,
                UiFactory.Accent, TextAnchor.MiddleCenter);
            initialized = true;
            RebuildCards();
        }

        public void SetRecord(string id, string detailed, string compact)
        {
            LobbyEntry entry;
            if (!entriesById.TryGetValue(id, out entry))
                throw new ArgumentException("Unknown lobby game: " + id, nameof(id));
            entry.Record = detailed ?? string.Empty;
            entry.CompactRecord = compact ?? string.Empty;
            Text label;
            if (recordLabels.TryGetValue(id, out label))
                SetRecordLabel(label, entry);
        }

        // Programmatic changes never write preferences. Only the user's option action calls modeChanged.
        public void SetMode(LobbyDisplayMode mode)
        {
            mode = LobbyDisplayPreferences.Normalize(mode);
            if (Mode == mode) return;
            Mode = mode;
            if (initialized) RebuildCards();
        }

        private void BuildHeader(Action openSettings)
        {
            header = UiFactory.Rect(content, "Lobby header", 18, 20, 354, 116.4f);
            Label(header, "Lobby brand", "XANADU / YONDER", 0, 0, 300, 40, 14,
                UiFactory.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            var settings = RoundedPanel(header, "Lobby settings", 314, 0, 40, 40, ControlColor, 12);
            UiFactory.ConfigureButton(settings, openSettings);
            Icon(settings.transform, "Settings icon", "settings", 10, 10, 20);
            Label(header, "Lobby title", "小小一局，慢慢向前。", 0, 60, 354, 32.2f, 23,
                UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            Label(header, "Lobby subtitle", "把片刻空闲，留给一点简单的快乐。", 0, 97.2f, 354, 19.2f,
                12, SecondaryText);

            toolbar = UiFactory.Rect(content, "Lobby toolbar", 18, 145.4f, 354, 44);
            Label(toolbar, "Lobby game count", "全部游戏 · " + entries.Count + " 款", 0, 0, 210, 36,
                12, SecondaryText);
            var selector = RoundedPanel(toolbar, "Display options button", 266, 0, 88, 36, ControlColor, 9);
            displayButton = UiFactory.ConfigureButton(selector, OpenDisplayOptions);
            Icon(selector.transform, "Display mode icon", "sliders-horizontal", 9, 10, 16);
            modeLabel = Label(selector.transform, "Display mode label", ModeTitles[(int)Mode], 31, 0, 48, 36,
                12, UiFactory.Accent);
            UiFactory.Panel(toolbar, "Toolbar rule", 0, 43, 354, 1, UiFactory.Hex("D6DFD2"), false);
        }

        private void RebuildCards()
        {
            var inset = Mode == LobbyDisplayMode.Directory ? 16 : 18;
            var width = LogicalWidth - inset * 2;
            header.anchoredPosition = new Vector2(inset, -20);
            header.sizeDelta = new Vector2(width, 116.4f);
            var settings = (RectTransform)header.Find("Lobby settings");
            settings.anchoredPosition = new Vector2(width - 40, 0);
            toolbar.anchoredPosition = new Vector2(inset, -145.4f);
            toolbar.sizeDelta = new Vector2(width, 44);
            displayButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(width - 88, 0);
            toolbar.Find("Toolbar rule").GetComponent<RectTransform>().sizeDelta = new Vector2(width, 1);
            modeLabel.text = ModeTitles[(int)Mode];

            recordLabels.Clear();
            if (cards != null)
            {
                // Deactivate first: Destroy is deferred in play mode, so old input targets must stop immediately.
                cards.gameObject.SetActive(false);
                Destroy(cards.gameObject);
            }
            cards = UiFactory.Rect(content, "Lobby cards", inset, CardsTop, width, 0);
            float top = 0;
            bool single = Mode == LobbyDisplayMode.Single;
            foreach (var category in Categories)
            {
                var group = entries.FindAll(entry => entry.Category == category);
                if (group.Count == 0) continue;
                if (single)
                {
                    foreach (var entry in group)
                    {
                        BuildCard(cards, entry, 0, top, width, 143);
                        top += 156;
                    }
                    continue;
                }
                var section = UiFactory.Rect(cards, "Category " + category, 0, top, width, 0);
                Label(section, "Group title", category, 0, 0, width - 45, 18.85f,
                    13, UiFactory.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
                Label(section, "Group count", group.Count + " 款", width - 45, 0, 45, 18.85f,
                    11, SecondaryText, TextAnchor.MiddleRight);
                const float headingHeight = 28.85f;
                var cardHeight = Mode == LobbyDisplayMode.Directory ? 48 : 66;
                const float gap = 10;
                var cardWidth = (width - gap) * .5f;
                for (int i = 0; i < group.Count; i++)
                {
                    var x = i % 2 * (cardWidth + gap);
                    var y = headingHeight + i / 2 * (cardHeight + gap);
                    BuildCard(section, group[i], x, y, cardWidth, cardHeight);
                }
                var rows = (group.Count + 1) / 2;
                var sectionHeight = headingHeight + rows * cardHeight + (rows - 1) * gap;
                section.sizeDelta = new Vector2(width, sectionHeight);
                top += sectionHeight + 17;
            }
            if (top > 0) top -= single ? 13 : 17;
            cards.sizeDelta = new Vector2(width, top);
            // The last section's margin collapses with the larger footer margin in the approved CSS.
            float footerTop = CardsTop + top + 20;
            footer.rectTransform.anchoredPosition = new Vector2(inset, -footerTop);
            footer.rectTransform.sizeDelta = new Vector2(width, 18.15f) * TextSampling;
            StatusLabel.rectTransform.anchoredPosition = new Vector2(inset, -footerTop - 27.15f);
            StatusLabel.rectTransform.sizeDelta = new Vector2(width, 18) * TextSampling;
            content.sizeDelta = new Vector2(LogicalWidth, footerTop + 65.15f);
            Scroll.StopMovement();
            content.anchoredPosition = Vector2.zero;
        }

        private void BuildCard(RectTransform parent, LobbyEntry entry, float x, float y, float width, float height)
        {
            var color = entry.Category == "合成消除" ? UiFactory.Hex("EEE7D3")
                : entry.Category == "敏捷挑战" ? UiFactory.Hex("E4E9DC") : UiFactory.Hex("DFE8DE");
            var card = RoundedPanel(parent, entry.ObjectName + " game card", x, y, width, height, color,
                Mode == LobbyDisplayMode.Directory ? 12 : 14);
            // Native UGUI hands card drags to the ancestor ScrollRect and cancels the pending click.
            UiFactory.ConfigureButton(card, entry.Enter);
            if (Mode == LobbyDisplayMode.Single)
            {
                Label(card.transform, "Game caption", entry.Number + " / " + entry.Category + " / " + entry.Tagline,
                    17, 15, width - 34, 14.48f, 10, UiFactory.Accent);
                var titleWidth = width - 34 - (entry.BuildLogo != null ? 77 : 0);
                Label(card.transform, "Game name", entry.Name, 17, 38.48f, titleWidth, 36,
                    24, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                Label(card.transform, "Game description", entry.Description, 17, 82.48f, width - 34, 20.15f,
                    13, UiFactory.Ink);
                var record = Label(card.transform, "Game record", "", 17, 110.63f, width - 34, 16.5f,
                    11, SecondaryText);
                SetRecordLabel(record, entry);
                recordLabels.Add(entry.Id, record);
                if (entry.BuildLogo != null)
                {
                    var slot = UiFactory.Rect(card.transform, "Game logo", width - 93, 38, 63, 63);
                    entry.BuildLogo(slot);
                    foreach (var graphic in slot.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                }
                return;
            }

            var directory = Mode == LobbyDisplayMode.Directory;
            var headTop = directory ? (height - 20) * .5f : (height - 42.5f) * .5f;
            Icon(card.transform, "Game icon", entry.Id, 10, headTop + 1, 20);
            Label(card.transform, "Game name", entry.Name, 38, headTop, width - 48, 20,
                directory ? 13 : 14, UiFactory.Ink, TextAnchor.MiddleLeft,
                directory ? FontStyle.Normal : FontStyle.Bold);
            if (!directory)
            {
                var record = Label(card.transform, "Game record", "", 38, headTop + 26, width - 48, 16.5f,
                    11, SecondaryText);
                SetRecordLabel(record, entry);
                recordLabels.Add(entry.Id, record);
            }
        }

        private void OpenDisplayOptions()
        {
            if (HasDisplayOptions) return;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            backgroundInteraction.interactable = false;
            backgroundInteraction.blocksRaycasts = false;
            Scroll.StopMovement();
            scrollEnabledBeforeOptions = Scroll.enabled;
            Scroll.enabled = false;
            var overlay = UiFactory.Panel(logicalFrame, "Display options overlay", 0, 0, LogicalWidth, 715,
                new Color(23 / 255f, 61 / 255f, 53 / 255f, .30f), false);
            UiFactory.Stretch(overlay.rectTransform);
            displayOptions = overlay.gameObject;
            UiFactory.ConfigureButton(overlay, CloseDisplayOptions);
            var sheet = RoundedPanel(overlay.transform, "Display options sheet", 20, 0, 350, 391,
                UiFactory.Paper, 22);
            sheet.rectTransform.anchorMin = sheet.rectTransform.anchorMax = new Vector2(.5f, .5f);
            sheet.rectTransform.pivot = new Vector2(.5f, .5f);
            sheet.rectTransform.anchoredPosition = Vector2.zero;
            sheet.raycastTarget = true;
            sheet.gameObject.AddComponent<LobbyModalSurface>();
            Label(sheet.transform, "Display options title", "展示方式", 20, 20, 240, 40,
                20, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var close = RoundedPanel(sheet.transform, "Close display options", 290, 20, 40, 40, ControlColor, 12);
            var closeButton = UiFactory.ConfigureButton(close, CloseDisplayOptions);
            Icon(close.transform, "Close icon", "x", 10, 10, 20);
            for (int i = 0; i < ModeTitles.Length; i++)
            {
                var mode = (LobbyDisplayMode)i;
                bool selected = Mode == mode;
                var option = RoundedPanel(sheet.transform, "Display mode " + mode, 20, 84 + i * 81, 310, 65,
                    selected ? UiFactory.Accent : ControlColor, 14);
                if (selected)
                    RoundedPanel(option.transform, "Selected option fill", 1, 1, 308, 63, UiFactory.Hex("DFE8DE"), 13);
                UiFactory.ConfigureButton(option, () => ChooseMode(mode));
                Label(option.transform, "Display mode name", ModeTitles[i], 14, 10, 240, 23, 16, UiFactory.Ink);
                Label(option.transform, "Display mode description", ModeNotes[i], 14, 37, 265, 16,
                    11, SecondaryText);
                if (selected) Icon(option.transform, "Selected mode check", "check", 276, 22.5f, 20);
            }
            var done = RoundedPanel(sheet.transform, "Done display options", 20, 329, 310, 42, UiFactory.Accent, 12);
            UiFactory.ConfigureButton(done, CloseDisplayOptions);
            Label(done.transform, "Done label", "完成", 0, 0, 310, 42, 14, Color.white, TextAnchor.MiddleCenter);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        private void ChooseMode(LobbyDisplayMode mode)
        {
            var changed = Mode != mode;
            SetMode(mode);
            CloseDisplayOptions();
            if (changed) modeChanged?.Invoke(mode);
        }

        public void CloseDisplayOptions()
        {
            if (displayOptions == null) return;
            displayOptions.SetActive(false);
            Destroy(displayOptions);
            displayOptions = null;
            backgroundInteraction.interactable = true;
            backgroundInteraction.blocksRaycasts = true;
            Scroll.enabled = scrollEnabledBeforeOptions;
            if (EventSystem.current != null)
            {
                var selection = previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null;
                // A card may have been rebuilt during selection. Fall back to the stable trigger.
                if (selection == null && displayButton.gameObject.activeInHierarchy) selection = displayButton.gameObject;
                EventSystem.current.SetSelectedGameObject(selection);
            }
            previousSelection = null;
        }

        private void OnDisable()
        {
            CloseDisplayOptions();
            if (Scroll != null) Scroll.StopMovement();
        }

        private void OnRectTransformDimensionsChange()
        {
            UpdateLogicalFrame();
        }

        private void UpdateLogicalFrame()
        {
            if (logicalFrame == null) return;
            var root = (RectTransform)transform;
            if (root.rect.width <= 0 || root.rect.height <= 0) return;
            var scale = root.rect.width / LogicalWidth;
            logicalFrame.sizeDelta = new Vector2(LogicalWidth, root.rect.height / scale);
            logicalFrame.localScale = Vector3.one * scale;
        }

        private static Image RoundedPanel(Transform parent, string name, float x, float y, float width,
            float height, Color color, float radius)
        {
            var image = UiFactory.Panel(parent, name, x, y, width, height, color);
            // UiFactory's sliced sprite has 16-pixel borders; preserve the CSS radius in logical units.
            image.pixelsPerUnitMultiplier = 16 / radius;
            return image;
        }

        private static Text Label(Transform parent, string name, string value, float x, float y, float width,
            float height, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Normal)
        {
            // The logical frame is enlarged to the 720-unit host. Rasterize text at 2x first so
            // its atlas glyphs are not magnified from a 14-pixel source while preserving geometry.
            var label = UiFactory.Label(parent, value ?? string.Empty, x, y,
                width * TextSampling, height * TextSampling, fontSize * TextSampling, color, alignment, style);
            label.rectTransform.localScale = Vector3.one / TextSampling;
            label.name = name;
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return label;
        }

        private void SetRecordLabel(Text label, LobbyEntry entry)
        {
            var single = Mode == LobbyDisplayMode.Single;
            SetSingleLine(label, single ? entry.Record : entry.CompactRecord, single && entry.Id == "sudoku");
        }

        private static void SetSingleLine(Text label, string value, bool fitCompleteRecord)
        {
            value = (value ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
            label.fontSize = 11 * TextSampling;
            var generation = label.GetGenerationSettings(Vector2.zero);
            var generator = label.cachedTextGeneratorForLayout;
            var available = label.rectTransform.rect.width * label.pixelsPerUnit;
            if (fitCompleteRecord)
            {
                // Sudoku exposes all four difficulty totals. Its full record must survive narrow widths.
                while (label.fontSize > 9 * TextSampling && generator.GetPreferredWidth(value, generation) > available)
                {
                    label.fontSize -= TextSampling;
                    generation = label.GetGenerationSettings(Vector2.zero);
                }
                label.text = value;
                return;
            }
            if (generator.GetPreferredWidth(value, generation) <= available)
            {
                label.text = value;
                return;
            }
            const string ellipsis = "…";
            while (value.Length > 0 && generator.GetPreferredWidth(value + ellipsis, generation) > available)
                value = value.Substring(0, value.Length - 1);
            label.text = value + ellipsis;
        }

        private static void Icon(Transform parent, string name, string resource, float x, float y, float size)
        {
            var image = UiFactory.Rect(parent, name, x, y, size, size).gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("Lobby/Icons/" + resource)
                ?? Resources.Load<Sprite>("Lobby/Icons/fallback-controller");
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white; // Exported assets already carry the approved accent color.
        }
    }

    // Consume sheet whitespace so a click cannot bubble to the dismissing backdrop.
    internal sealed class LobbyModalSurface : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        public void OnPointerClick(PointerEventData eventData) { }
        public void OnSubmit(BaseEventData eventData) { }
    }
}
