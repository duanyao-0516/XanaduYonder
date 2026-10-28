using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Snake
{
    // Same UGUI template and warm selected-row treatment as the Sudoku picker.
    internal sealed class SnakeDropdown : Dropdown
    {
        private GameObject expandedList;
        public Func<bool> CanOpen;
        public Action Opening;
        public Action Closed;
        public bool IsExpanded { get { return expandedList != null; } }
        public override void OnPointerClick(PointerEventData eventData)
        {
            if (CanOpen == null || !CanOpen()) return;
            Opening?.Invoke(); base.OnPointerClick(eventData);
        }
        public override void OnSubmit(BaseEventData eventData)
        {
            if (CanOpen == null || !CanOpen()) return;
            Opening?.Invoke(); base.OnSubmit(eventData);
        }
        protected override GameObject CreateDropdownList(GameObject template)
        { expandedList = base.CreateDropdownList(template); return expandedList; }
        protected override void DestroyDropdownList(GameObject list)
        {
            // UGUI can enqueue Hide more than once for the same selection. Each delayed task
            // targets its current list, so retire them all before a preview can reopen the picker.
            StopAllCoroutines();
            base.DestroyDropdownList(list); expandedList = null;
            try { Closed?.Invoke(); }
            finally
            {
                // The base class clears its private list reference after this override returns.
                // A Closed callback showing a pause dialog can call Hide again in that interval.
                StopAllCoroutines();
            }
        }
        public void SetWidth(float width)
        {
            var root = (RectTransform)transform;
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            captionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width-58);
            ((RectTransform)transform.Find("Down icon")).anchoredPosition = new Vector2(width-42, -(root.rect.height-26)/2);
            template.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            var viewport = (RectTransform)template.Find("Viewport");
            viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width-16);
            var content = (RectTransform)viewport.Find("Content");
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width-16);
            var item = (RectTransform)content.Find("Option");
            item.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width-16);
            ((RectTransform)item.Find("Selected band")).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width-16);
            itemText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width-44);
        }
        public static SnakeDropdown Create(Transform parent, float x, float y, float width, float height)
        {
            var panel = UiFactory.Panel(parent, "Snake level dropdown", x, y, width, height, UiFactory.Hex("E7EDE4")); panel.raycastTarget = true;
            var dropdown = panel.gameObject.AddComponent<SnakeDropdown>(); dropdown.targetGraphic = panel;
            dropdown.captionText = UiFactory.Label(panel.transform, "", 18, 0, width-58, height, 26, UiFactory.Ink);
            dropdown.captionText.horizontalOverflow = HorizontalWrapMode.Overflow;
            SnakeIconGraphic.Add(panel.transform, SnakeIconKind.Down, width-42, (height-26)/2, 26, UiFactory.Ink);
            var template = UiFactory.Panel(panel.transform, "Difficulty options", 0, height+6, width, 272, UiFactory.Hex("D6C9AE")).rectTransform;
            var viewport = UiFactory.Rect(template, "Viewport", 8, 0, width-16, 272); viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiFactory.Rect(viewport, "Content", 0, 0, width-16, 80);
            var item = UiFactory.Panel(content, "Option", 0, 8, width-16, 64, UiFactory.Hex("F7F0E2"), false); item.raycastTarget = true;
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = item;
            toggle.graphic = UiFactory.Panel(item.transform, "Selected band", 0, 0, width-16, 64, UiFactory.Hex("DBC7A3"), false);
            dropdown.itemText = UiFactory.Label(item.transform, "", 18, 0, width-44, 64, 26, UiFactory.Ink);
            dropdown.itemText.resizeTextForBestFit = true; dropdown.itemText.resizeTextMinSize = 20; dropdown.itemText.resizeTextMaxSize = 26;
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            dropdown.template = template; template.gameObject.SetActive(false);
            return dropdown;
        }
    }
}
