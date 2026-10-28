using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace XanaduYonder.Runtime.Dino
{
    // A control owns only its pointer records. DinoPage owns Jump/Duck arbitration.
    internal sealed class DinoTouchControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, ICancelHandler
    {
        private readonly List<int> pointers = new List<int>(10);
        private Action<int> down;
        private Action<int> up;

        public void Initialize(Action<int> onDown, Action<int> onUp)
        {
            Cancel();
            down = onDown;
            up = onUp;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (!isActiveAndEnabled || data.button != PointerEventData.InputButton.Left ||
                pointers.Contains(data.pointerId) || !CanBegin(data.pointerId)) return;
            pointers.Add(data.pointerId);
            down?.Invoke(data.pointerId);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) Release(data.pointerId);
        }

        public void OnPointerExit(PointerEventData data) { Release(data.pointerId); }
        public void OnCancel(BaseEventData data) { Cancel(); }

        private static bool CanBegin(int pointerId)
        {
            if (pointerId < 0) return true;
            for (var i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.fingerId == pointerId)
                    // UGUI dispatches Down then Up for a newly observed Ended touch.
                    // That is a valid short press; only cancellation must not begin a jump.
                    return touch.phase != TouchPhase.Canceled;
            }
            return false;
        }

        private void Update()
        {
            // Some native cancellation paths do not dispatch a matching UI PointerUp.
            for (var i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.Ended)
                    Release(touch.fingerId);
            }
        }

        private void Release(int pointerId)
        {
            if (!pointers.Remove(pointerId)) return;
            up?.Invoke(pointerId);
        }

        public void Cancel()
        {
            // Remove before calling outward: a release can hide the page and reenter Cancel.
            while (pointers.Count > 0)
            {
                var index = pointers.Count - 1;
                var pointerId = pointers[index];
                pointers.RemoveAt(index);
                up?.Invoke(pointerId);
            }
        }

        // Lifecycle suspension preserves the model's action intent before clearing real pointers.
        public void ClearWithoutRelease() { pointers.Clear(); }

        private void OnDisable() { Cancel(); }
        private void OnDestroy() { Cancel(); down = up = null; }
    }
}
