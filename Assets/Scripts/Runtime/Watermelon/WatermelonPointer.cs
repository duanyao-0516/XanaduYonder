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
            Vector2 local;
            float x;
            if (!TryPosition(data, out local, out x) || local.x < area.rect.xMin || local.x > area.rect.xMax
                || !WithinHeight(local)) return;
            pointer = data.pointerId;
            Aim?.Invoke(x);
        }

        public void OnDrag(PointerEventData data)
        {
            if (pointer != data.pointerId) return;
            Vector2 local;
            float x;
            if (CanAim?.Invoke() != true || !TryPosition(data, out local, out x)) { Cancel(); return; }
            Aim?.Invoke(x);
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
                    if (touch.fingerId == data.pointerId) { ended = touch.phase == TouchPhase.Ended; break; }
                if (!ended) return;
            }
            Vector2 local;
            float x;
            // The captured gesture may finish beyond either side, but not above or below the board.
            if (CanAim?.Invoke() == true && TryPosition(data, out local, out x) && WithinHeight(local))
                Drop?.Invoke(x);
        }

        private bool TryPosition(PointerEventData data, out Vector2 local, out float x)
        {
            local = Vector2.zero;
            x = 0;
            if (area == null || !Finite(data.position.x) || !Finite(data.position.y)) return false;
            var rect = area.rect;
            if (!Finite(rect.width) || !Finite(rect.height) || rect.width <= 0 || rect.height <= 0
                || !Finite(rect.xMin) || !Finite(rect.xMax) || !Finite(rect.yMin) || !Finite(rect.yMax)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, data.position, data.pressEventCamera, out local)
                || !Finite(local.x) || !Finite(local.y)) return false;
            float normalized = (local.x - rect.xMin) / rect.width;
            if (!Finite(normalized)) return false;
            x = Mathf.Clamp01(normalized);
            return true;
        }

        private bool WithinHeight(Vector2 local) { return local.y >= area.rect.yMin && local.y <= area.rect.yMax; }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        public void Cancel() { pointer = null; }
        public void OnCancel(BaseEventData data) { Cancel(); }
        private void OnDisable() { Cancel(); }
    }
}
