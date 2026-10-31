using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace XanaduYonder.Runtime.AircraftFire
{
    // Relative drag keeps the aircraft visible above the finger and never teleports
    // it to a new contact. Only the contact that began this gesture can move it.
    internal sealed class AircraftPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, ICancelHandler
    {
        internal Func<bool> AcceptsInput;
        internal Action<Vector2> Moved;
        private int? owner;
        private Vector2 previous;
        internal bool HasOwner => owner.HasValue;

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || owner.HasValue || AcceptsInput == null || !AcceptsInput()) return;
            if (!Local(data, out previous)) return;
            owner = data.pointerId;
        }
        public void OnBeginDrag(PointerEventData data) { data.useDragThreshold = false; }
        public void OnDrag(PointerEventData data)
        {
            if (owner != data.pointerId) return;
            if (AcceptsInput == null || !AcceptsInput() || IsCanceled(data.pointerId)) { Cancel(); return; }
            if (!Local(data, out var point)) return;
            var delta = point - previous;
            previous = point;
            Moved?.Invoke(delta);
        }
        public void OnPointerUp(PointerEventData data) { if (owner == data.pointerId) Cancel(); }
        public void OnEndDrag(PointerEventData data) { if (owner == data.pointerId) Cancel(); }
        public void OnCancel(BaseEventData data) { Cancel(); }
        internal void Cancel() { owner = null; }
        private void OnDisable() { Cancel(); }
        private bool Local(PointerEventData data, out Vector2 point) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, data.position, data.pressEventCamera, out point);
        private static bool IsCanceled(int id)
        {
            if (id < 0) return false;
            for (var i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.fingerId == id) return touch.phase == TouchPhase.Canceled;
            }
            return false;
        }
    }
}
