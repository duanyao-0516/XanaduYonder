using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core.GoldMiner;

namespace XanaduYonder.Runtime.GoldMiner
{
    /// <summary>
    /// Attach to the full-window mining input Image below all raycastable UI. The callbacks
    /// own command sequencing and model legality; this adapter never queues a rejected command.
    /// </summary>
    internal sealed class GoldMinerInputRouter : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ICancelHandler
    {
        private const int MousePointer = -1;
        private const int P1LaunchKey = -101;
        private const int P1DynamiteKey = -102;
        private const int P2LaunchKey = -103;
        private const int P2DynamiteKey = -104;
        private const int SoloLaunchKey = -105;
        private readonly Dictionary<GameObject, EventTrigger.Entry> dynamiteBindings = new Dictionary<GameObject, EventTrigger.Entry>();
        private GoldMinerInputState state;
        private Func<bool> canAct;
        private Action<int> launch;
        private Action<int> dynamite;
        private bool cooperative;
        private bool gateWasOpen;
        private int lastWidth;
        private int lastHeight;
        private Rect lastSafeArea;
        private long generation;

        public long Generation { get { return generation; } }

        public void Initialize(bool cooperative, Func<bool> canAct, Action<int> launch, Action<int> dynamite)
        {
            if (canAct == null) throw new ArgumentNullException("canAct");
            if (launch == null) throw new ArgumentNullException("launch");
            if (dynamite == null) throw new ArgumentNullException("dynamite");
            ClearDynamiteBindings();
            this.cooperative = cooperative;
            this.canAct = canAct;
            this.launch = launch;
            this.dynamite = dynamite;
            generation = checked(generation + 1);
            state = new GoldMinerInputState(cooperative, generation);
            QuarantineActiveTouches();
            gateWasOpen = canAct();
            RememberWindow();
        }

        /// <summary>Call on a new page/mode, modal, orientation, safe-area change or lifecycle gate.</summary>
        public void ResetGeneration()
        {
            if (state == null) return;
            generation = checked(generation + 1);
            state.ResetGeneration(generation, ActivePointerIds());
        }

        /// <summary>Call immediately before Start/Continue opens its gate, including from a UI Down.</summary>
        public void QuarantineActiveTouches()
        {
            if (state != null) state.QuarantineActivePointers(ActivePointerIds());
        }

        /// <summary>Use this instead of wiring dynamite to Button.onClick (which fires on Up).</summary>
        public void BindDynamite(GameObject button, int playerId)
        {
            if (state == null) throw new InvalidOperationException("Initialize the input router first.");
            if (button == null) throw new ArgumentNullException("button");
            if (playerId < 0 || playerId >= (cooperative ? 2 : 1)) throw new ArgumentOutOfRangeException("playerId");
            var trigger = button.GetComponent<EventTrigger>() ?? button.AddComponent<EventTrigger>();
            EventTrigger.Entry previous;
            if (dynamiteBindings.TryGetValue(button, out previous)) trigger.triggers.Remove(previous);
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(value =>
            {
                var data = value as PointerEventData;
                if (!isActiveAndEnabled || data == null || !AcceptPointer(data) || !button.activeInHierarchy) return;
                var selectable = button.GetComponent<Selectable>();
                bool enabled = selectable == null || (selectable.IsActive() && selectable.IsInteractable());
                bool allowed = GateIsOpen() && enabled;
                if (state.TryConsumeUiDown(data.pointerId, generation, allowed)) dynamite(playerId);
            });
            trigger.triggers.Add(down);
            dynamiteBindings[button] = down;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (state == null || !AcceptPointer(data)) return;
            // A parent's handler may receive a child graphic's event through ExecuteHierarchy.
            // Only this exact input Image is the mining surface; every other graphic consumes it.
            bool uiConsumed = data.pointerCurrentRaycast.gameObject != gameObject
                || !Screen.safeArea.Contains(data.position);
            int playerId = cooperative && data.position.x >= Screen.width * 0.5f ? 1 : 0;
            var intent = state.Process(new GoldMinerPointerEvent(GoldMinerPointerPhase.Down,
                data.pointerId, playerId, uiConsumed, GateIsOpen(), generation));
            if (intent.ShouldLaunch) launch(intent.PlayerId);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (state == null) return;
            bool canceled = false;
            if (data.pointerId >= 0)
            {
                foreach (var touch in Input.touches)
                    if (touch.fingerId == data.pointerId) canceled = touch.phase == TouchPhase.Canceled;
            }
            Release(data.pointerId, canceled);
        }

        public void OnCancel(BaseEventData data) { ResetGeneration(); }

        private void Update()
        {
            if (state == null) return;
            if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafeArea)
            {
                ResetGeneration();
                RememberWindow();
            }
            GateIsOpen();
            // UI captures need not bubble Up to this Image. Native end/cancel is authoritative.
            foreach (var touch in Input.touches)
            {
                if (touch.phase == TouchPhase.Canceled) Release(touch.fingerId, true);
                else if (touch.phase == TouchPhase.Ended) Release(touch.fingerId, false);
            }
            if (!Input.GetMouseButton(0)) Release(MousePointer, false);
#if UNITY_EDITOR
            HandleKey(KeyCode.S, P1LaunchKey, 0, false);
            HandleKey(KeyCode.W, P1DynamiteKey, 0, true);
            if (cooperative)
            {
                HandleKey(KeyCode.DownArrow, P2LaunchKey, 1, false);
                HandleKey(KeyCode.UpArrow, P2DynamiteKey, 1, true);
            }
            else HandleKey(KeyCode.Space, SoloLaunchKey, 0, false);
#endif
        }

        private bool GateIsOpen()
        {
            bool open = canAct != null && canAct();
            if (!open && gateWasOpen) ResetGeneration();
            gateWasOpen = open;
            return open;
        }

        private static bool AcceptPointer(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return false;
            // StandaloneInputModule can synthesize mouse events from a touch. Never count both.
            return data.pointerId >= 0 || (data.pointerId == MousePointer && Input.touchCount == 0);
        }

        private void Release(int pointerId, bool canceled)
        {
            state.Process(new GoldMinerPointerEvent(canceled ? GoldMinerPointerPhase.Cancel : GoldMinerPointerPhase.Up,
                pointerId, 0, false, false, generation));
            if (canceled) QuarantineActiveTouches();
        }

        private List<int> ActivePointerIds()
        {
            var active = new List<int>();
            foreach (var touch in Input.touches)
                if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled) active.Add(touch.fingerId);
            if (Input.GetMouseButton(0)) active.Add(MousePointer);
#if UNITY_EDITOR
            AddHeldKey(active, KeyCode.S, P1LaunchKey);
            AddHeldKey(active, KeyCode.W, P1DynamiteKey);
            AddHeldKey(active, KeyCode.DownArrow, P2LaunchKey);
            AddHeldKey(active, KeyCode.UpArrow, P2DynamiteKey);
            AddHeldKey(active, KeyCode.Space, SoloLaunchKey);
#endif
            return active;
        }

#if UNITY_EDITOR
        private static void AddHeldKey(List<int> pointers, KeyCode key, int pointerId)
        {
            if (Input.GetKey(key)) pointers.Add(pointerId);
        }

        private void HandleKey(KeyCode key, int pointerId, int playerId, bool isDynamite)
        {
            if (Input.GetKeyUp(key)) Release(pointerId, false);
            if (!Input.GetKeyDown(key)) return;
            bool allowed = GateIsOpen();
            if (isDynamite)
            {
                if (state.TryConsumeUiDown(pointerId, generation, allowed)) dynamite(playerId);
                return;
            }
            var intent = state.Process(new GoldMinerPointerEvent(GoldMinerPointerPhase.Down,
                pointerId, playerId, false, allowed, generation));
            if (intent.ShouldLaunch) launch(intent.PlayerId);
        }
#endif

        private void RememberWindow()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            lastSafeArea = Screen.safeArea;
        }

        private void ClearDynamiteBindings()
        {
            foreach (var binding in dynamiteBindings)
            {
                if (binding.Key == null) continue;
                var trigger = binding.Key.GetComponent<EventTrigger>();
                if (trigger != null) trigger.triggers.Remove(binding.Value);
            }
            dynamiteBindings.Clear();
        }

        private void OnApplicationFocus(bool focused) { ResetGeneration(); }
        private void OnApplicationPause(bool paused) { ResetGeneration(); }
        private void OnEnable() { QuarantineActiveTouches(); }
        private void OnDisable() { ResetGeneration(); }
        private void OnDestroy() { ClearDynamiteBindings(); }
    }
}
