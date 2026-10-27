using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace XanaduYonder.Runtime.Watermelon
{
    internal sealed class WatermelonPointer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICancelHandler
    {
        public Func<bool> CanAim;
        public Action<float> Aim;
        public Action<float> Drop;
        private int? pointer;
        private RectTransform area;

        public void OnPointerDown(PointerEventData data)
        {
            if (pointer.HasValue || data.button != PointerEventData.InputButton.Left || CanAim?.Invoke() != true) return;
            area = (RectTransform)transform;
            pointer = data.pointerId;
            Aim?.Invoke(Position(data));
        }

        public void OnDrag(PointerEventData data)
        {
            if (pointer != data.pointerId) return;
            if (CanAim?.Invoke() != true) { Cancel(); return; }
            Aim?.Invoke(Position(data));
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (pointer != data.pointerId) return;
            pointer = null;
            // StandaloneInputModule also sends PointerUp for a canceled native touch.
            if (data.pointerId >= 0)
            {
                bool ended = false;
                foreach (var touch in Input.touches)
                    if (touch.fingerId == data.pointerId) ended = touch.phase != TouchPhase.Canceled;
                if (!ended) return;
            }
            if (CanAim?.Invoke() == true && RectTransformUtility.RectangleContainsScreenPoint(area, data.position, data.pressEventCamera))
                Drop?.Invoke(Position(data));
        }

        private float Position(PointerEventData data)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, data.position, data.pressEventCamera, out local);
            return Mathf.Clamp01((local.x - area.rect.xMin) / area.rect.width);
        }

        public void Cancel() { pointer = null; }
        public void OnCancel(BaseEventData data) { Cancel(); }
        private void OnDisable() { Cancel(); }
    }
}
