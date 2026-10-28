using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace XanaduYonder.Runtime.Gomoku
{
    // Touch selection comes from the native Ended phase, never UGUI OnPointerUp:
    // UGUI can dispatch OnPointerUp for a touch that the OS actually canceled.
    internal sealed class GomokuPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private static readonly GomokuContact[] NoContacts = new GomokuContact[0];
        private readonly GomokuInput input = new GomokuInput();
        private readonly HashSet<int> touchStarts = new HashSet<int>();
        private RectTransform surface;
        private Camera eventCamera;
        private Action<int> select;
        private bool interactable, mouse, mouseReleased, hadTouch;
        private int mouseId;
        private Vector2 mouseReleasePosition;

        public bool Interactable
        {
            get { return interactable; }
            set { if (interactable == value) return; interactable = value; if (!value) CancelInput(); }
        }

        public void Initialize(RectTransform value, Action<int> selected)
        { surface = value; select = selected; CancelInput(); }

        public void CancelInput()
        { input.Cancel(); touchStarts.Clear(); mouse = false; mouseReleased = false; }

        private GomokuContact Contact(int id, Vector2 screen, GomokuInputPhase phase, bool beganOnBoard)
        {
            Vector2 point;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(surface, screen, eventCamera, out point) ||
                surface.rect.width <= 0 || surface.rect.height <= 0)
                return new GomokuContact(id, double.NaN, double.NaN, phase, beganOnBoard);
            return new GomokuContact(id,
                (point.x - surface.rect.xMin) * GomokuBoardGeometry.Size / surface.rect.width,
                (surface.rect.yMax - point.y) * GomokuBoardGeometry.Size / surface.rect.height,
                phase, beganOnBoard);
        }

        private void Deliver(GomokuContact[] contacts)
        {
            int cell = input.ProcessFrame(interactable, contacts);
            if (cell >= 0) select?.Invoke(cell);
        }

        private void LateUpdate()
        {
            if (surface == null) return;
            var canvas = surface.GetComponentInParent<Canvas>();
            if (canvas != null) eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (Input.touchCount > 0)
            {
                if (mouse) { input.Cancel(); mouse = false; mouseReleased = false; }
                hadTouch = true;
                var contacts = new GomokuContact[Input.touchCount];
                for (int i = 0; i < contacts.Length; i++)
                {
                    var touch = Input.GetTouch(i);
                    var phase = touch.phase == TouchPhase.Began ? GomokuInputPhase.Began :
                        touch.phase == TouchPhase.Ended ? GomokuInputPhase.Ended :
                        touch.phase == TouchPhase.Canceled ? GomokuInputPhase.Canceled :
                        touch.phase == TouchPhase.Moved ? GomokuInputPhase.Moved : GomokuInputPhase.Stationary;
                    contacts[i] = Contact(touch.fingerId, touch.position, phase, touchStarts.Contains(touch.fingerId));
                }
                Deliver(contacts);
            }
            else if (hadTouch)
            {
                // Do not accept the simulated mouse release after a physical touch.
                hadTouch = false; mouse = false; mouseReleased = false; Deliver(NoContacts);
            }
            else if (mouse)
            {
                var phase = mouseReleased ? GomokuInputPhase.Ended :
                    Input.GetMouseButton(0) ? GomokuInputPhase.Moved : GomokuInputPhase.Canceled;
                var position = mouseReleased ? mouseReleasePosition : (Vector2)Input.mousePosition;
                if (phase == GomokuInputPhase.Ended || phase == GomokuInputPhase.Canceled) mouse = false;
                mouseReleased = false;
                Deliver(new[] { Contact(mouseId, position, phase, true) });
            }
            else
            {
                // A held mouse after CancelInput must drain before a fresh press.
                if (Input.GetMouseButton(0))
                    Deliver(new[] { Contact(-1, Input.mousePosition, GomokuInputPhase.Stationary, false) });
                else Deliver(NoContacts);
            }
            touchStarts.Clear();
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (surface == null || data.button != PointerEventData.InputButton.Left) return;
            eventCamera = data.pressEventCamera;
            if (data.pointerId >= 0) { touchStarts.Add(data.pointerId); return; }
            if (Input.touchCount > 0 || hadTouch) return;
            if (mouse) { CancelInput(); return; }
            mouse = true; mouseReleased = false; mouseId = data.pointerId;
            Deliver(new[] { Contact(mouseId, data.position, GomokuInputPhase.Began, true) });
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (!mouse || data.pointerId != mouseId || Input.touchCount > 0) return;
            mouseReleased = true; mouseReleasePosition = data.position;
        }

        public void OnDrag(PointerEventData data)
        {
            if (mouse && data.pointerId == mouseId && Input.touchCount == 0)
                Deliver(new[] { Contact(mouseId, data.position, GomokuInputPhase.Moved, true) });
        }

        private void OnDisable() { CancelInput(); }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelInput(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelInput(); }
    }
}
