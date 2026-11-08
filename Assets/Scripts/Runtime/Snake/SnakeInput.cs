using System;
using UnityEngine;
using UnityEngine.EventSystems;
using XanaduYonder.Core.Snake;

namespace XanaduYonder.Runtime.Snake
{
    internal struct SnakePointerFrame
    {
        public int TouchCount;
        public bool Canceled;
        public bool PrimaryPressed;
    }

    internal sealed class SnakeInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        public Func<bool> CanInput;
        public Action<SnakeDirection> Turn;
        public Action Canceled;
        private int? owner;
        private Vector2 anchor;
        private bool quarantined;
        public bool IsQuarantined { get { return quarantined; } }
        private RectTransform surface;
        private void Awake() { surface = (RectTransform)transform; }
        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }
        public void OnPointerDown(PointerEventData e)
        {
            if (!Acquire(e)) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, e.position, e.pressEventCamera, out anchor);
        }
        public void OnDrag(PointerEventData e)
        {
            if (owner != e.pointerId || !Allowed()) return;
            Vector2 point;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, e.position, e.pressEventCamera, out point)) return;
            Vector2 delta = point-anchor;
            // 0.8 cells responds during the drag, and can recognize another turn without lifting.
            if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) < surface.rect.width / 25f) return;
            anchor = point;
            Turn?.Invoke(Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? delta.x > 0 ? SnakeDirection.Right : SnakeDirection.Left : delta.y > 0 ? SnakeDirection.Up : SnakeDirection.Down);
        }
        public void OnPointerUp(PointerEventData e) { Release(e.pointerId); }
        public bool PressButton(PointerEventData e, SnakeDirection direction)
        { if (!Acquire(e)) return false; Turn?.Invoke(direction); return true; }
        public void Release(int pointerId) { if (owner == pointerId) owner = null; }
        private bool Acquire(PointerEventData e)
        {
            if (!Allowed() || (e.pointerId < 0 && e.button != PointerEventData.InputButton.Left)) return false;
            if (owner.HasValue && owner != e.pointerId) { Cancel(true); return false; }
            owner = e.pointerId; return true;
        }
        private bool Allowed()
        {
            if (Input.touchCount > 1) { Cancel(true); return false; }
            return !quarantined && CanInput != null && CanInput();
        }
        public static SnakePointerFrame CaptureFrame()
        {
            var frame = new SnakePointerFrame { TouchCount = Input.touchCount, PrimaryPressed = Input.GetMouseButton(0) };
            for (int i=0;i<frame.TouchCount;i++) if (Input.GetTouch(i).phase == TouchPhase.Canceled) frame.Canceled = true;
            return frame;
        }
        public void PollFrame(SnakePointerFrame frame)
        {
            if(frame.TouchCount>1||frame.Canceled)Cancel(true);
            if(quarantined&&frame.TouchCount==0&&!frame.PrimaryPressed)quarantined=false;
        }
        private void Update() { PollFrame(CaptureFrame()); }
        public void Cancel(bool waitForRelease = false) { owner = null; quarantined |= waitForRelease; Canceled?.Invoke(); }
        private void OnDisable() { Cancel(); }
    }
    internal sealed class SnakeDirectionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public SnakeInput InputTarget;
        public SnakeDirection Direction;
        public void OnPointerDown(PointerEventData e) { InputTarget?.PressButton(e, Direction); }
        public void OnPointerUp(PointerEventData e) { InputTarget?.Release(e.pointerId); }
    }
}
