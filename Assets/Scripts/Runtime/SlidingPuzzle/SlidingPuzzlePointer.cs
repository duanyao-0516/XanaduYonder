using System;
using UnityEngine;
using UnityEngine.EventSystems;
using XanaduYonder.Core.SlidingPuzzle;

namespace XanaduYonder.Runtime.SlidingPuzzle
{
    // One pointer-up submission path for both tap and tile-directed swipe. No onClick fallback.
    internal sealed class SlidingPuzzlePointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform surface;
        private SlidingPuzzleBoardView board;
        private Func<SlidingPuzzleGame> game;
        private Func<bool> allowed;
        private Action<int> move;
        private int? owner;
        private int startIndex, value;
        private Vector2 start;
        private float threshold;
        public void Initialize(RectTransform parent, SlidingPuzzleBoardView view, Func<SlidingPuzzleGame> model, Func<bool> canInput, Action<int> onMove, float scale)
        { surface = parent; board = view; game = model; allowed = canInput; move = onMove; threshold = 16 * scale; CancelGesture(); }
        public void OnPointerDown(PointerEventData data)
        {
            if (owner.HasValue) { CancelGesture(); return; }
            if (data.button != PointerEventData.InputButton.Left || !allowed() || Input.touchCount > 1) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, data.position, data.pressEventCamera, out start)) return;
            startIndex = board.IndexAt(start); if (startIndex < 0) return;
            value = game().GetCells()[startIndex]; if (value == 0) return;
            owner = data.pointerId;
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (!owner.HasValue || owner.Value != data.pointerId) return;
            CancelGesture();
            if (!allowed() || WasCancelled(data.pointerId) || Input.touchCount > 1) return;
            Vector2 end;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, data.position, data.pressEventCamera, out end)) return;
            var current = game(); var cells = current.GetCells();
            if (cells[startIndex] != value) return;
            Vector2 delta = end - start;
            if (delta.magnitude < threshold)
            { if (board.IndexAt(end) == startIndex) move(value); return; }
            int blank = Array.IndexOf(cells, 0), n = current.Size;
            int expected = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x > 0 ? 1 : -1) : (delta.y > 0 ? -n : n);
            if (startIndex + expected == blank && Math.Abs(startIndex / n - blank / n) + Math.Abs(startIndex % n - blank % n) == 1) move(value);
        }
        internal void CancelGesture() { owner = null; }
        private void Update() { if (owner.HasValue && (Input.touchCount > 1 || !allowed())) CancelGesture(); }
        private void OnDisable() { CancelGesture(); }
        private static bool WasCancelled(int pointerId)
        {
            if (pointerId < 0) return false;
            for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).fingerId == pointerId) return Input.GetTouch(i).phase == TouchPhase.Canceled;
            return false;
        }
    }
}
