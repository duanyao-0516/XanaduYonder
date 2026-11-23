using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace XanaduYonder.Runtime.Feihualing
{
    [DisallowMultipleComponent]
    internal sealed class FeihualingComputerLinePress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, ICancelHandler
    {
        private const double HoldSeconds = .55;
        private Func<bool> canHold;
        private Action onHold;
        private PointerEventData press;
        private Vector2 pressPosition;
        private Vector3 rowPosition;
        private double heldSince;

        internal void Bind(Func<bool> stillAvailable, Action onHeld)
        {
            Cancel();
            canHold = stillAvailable;
            onHold = onHeld;
            enabled = onHeld != null;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            if (press != null)
            {
                Cancel();
                return;
            }
            if (canHold?.Invoke() != true) return;
            press = data;
            pressPosition = data.position;
            rowPosition = transform.position;
            heldSince = Time.unscaledTimeAsDouble;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (press != data) return;
            if (!StillValid() || !TouchPhaseMatches(data.pointerId, true))
            {
                Cancel();
                return;
            }
            if (!FinishHold(Time.unscaledTimeAsDouble)) press = null;
        }

        public void OnPointerExit(PointerEventData data)
        {
            if (press == data) Cancel();
        }

        public void OnCancel(BaseEventData data) => Cancel();

        private void OnDisable() => Cancel();

        private void LateUpdate() => Advance(Time.unscaledTimeAsDouble);

        internal void Advance(double now)
        {
            if (press == null) return;
            if (!StillValid() || !TouchPhaseMatches(press.pointerId, false))
            {
                Cancel();
                return;
            }
            FinishHold(now);
        }

        private bool FinishHold(double now)
        {
            if (now - heldSince < HoldSeconds) return false;
            Cancel();
            onHold?.Invoke();
            return true;
        }

        private bool StillValid()
        {
            var movement = Mathf.Max(12, Screen.dpi * .06f);
            return press.eligibleForClick && !press.dragging && canHold?.Invoke() == true &&
                (press.position - pressPosition).sqrMagnitude <= movement * movement &&
                (transform.position - rowPosition).sqrMagnitude < .001f;
        }

        private static bool TouchPhaseMatches(int pointerId, bool released)
        {
            if (pointerId < 0) return true;
            for (var index = 0; index < Input.touchCount; index++)
            {
                var touch = Input.GetTouch(index);
                if (touch.fingerId != pointerId) continue;
                return released ? touch.phase == TouchPhase.Ended :
                    touch.phase != TouchPhase.Canceled && touch.phase != TouchPhase.Ended;
            }
            return false;
        }

        private void Cancel()
        {
            if (press == null) return;
            // Keep the ancestor ScrollRect as the drag handler, and consume the
            // Button click if the captured contact is canceled or becomes a hold.
            press.eligibleForClick = false;
            press = null;
        }
    }
}
