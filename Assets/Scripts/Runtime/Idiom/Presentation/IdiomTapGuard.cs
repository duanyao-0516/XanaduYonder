using UnityEngine;
using UnityEngine.EventSystems;

namespace XanaduYonder.Runtime.Idiom
{
    // A recycled row cannot activate a different entry after a drag/rebind.
    internal sealed class IdiomTapGuard : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private int pressedIndex = -1;
        private Vector2 pressedAt;
        private bool cleanRelease;
        public int BoundIndex { get; set; }
        public bool CanClick => cleanRelease && pressedIndex == BoundIndex;

        public void OnPointerDown(PointerEventData data)
        {
            pressedIndex = BoundIndex;
            pressedAt = data.position;
            cleanRelease = false;
        }

        public void OnPointerUp(PointerEventData data)
        {
            cleanRelease = !data.dragging && data.eligibleForClick &&
                Vector2.Distance(pressedAt, data.position) <= Mathf.Max(12, Screen.dpi * .06f);
        }
    }
}
