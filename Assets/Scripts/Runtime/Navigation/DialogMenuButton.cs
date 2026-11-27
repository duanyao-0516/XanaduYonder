using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // Keep UGUI selection for navigation and Submit. Only directional input paints that selection.
    internal sealed class DialogMenuButton : Button
    {
        private static EventSystem navigationSystem;
        private bool touchHover;

        // A game can open a shared dialog through its own control or a physical Submit key.
        internal static void ReadActivationInput(EventSystem events)
        {
            if (!(events.currentInputModule is StandaloneInputModule module)) return;
            var input = module.input;
            if (input.touchCount > 0 || input.GetMouseButtonDown(0) || input.GetMouseButtonUp(0))
                navigationSystem = null;
            else if (input.GetButtonDown(module.submitButton))
                navigationSystem = events;
        }

        public override void OnPointerEnter(PointerEventData data)
        {
            touchHover = data.pointerId >= 0;
            base.OnPointerEnter(data);
        }

        public override void OnPointerDown(PointerEventData data)
        {
#if UNITY_EDITOR
            DialogFocusScope.BeforeInput?.Invoke(transform);
#endif
            if (data.button == PointerEventData.InputButton.Left) navigationSystem = null;
            base.OnPointerDown(data);
        }

        public override void OnSelect(BaseEventData data)
        {
            if (data is AxisEventData) navigationSystem = EventSystem.current;
            else if (data is PointerEventData) navigationSystem = null;
            base.OnSelect(data);
        }

        public override void OnMove(AxisEventData data)
        {
            if (!IsActive() || !IsInteractable()) return;
            navigationSystem = EventSystem.current;
            base.OnMove(data);
            // Repaint even if already on the last button in this direction.
            DoStateTransition(currentSelectionState, false);
        }

        public override void OnSubmit(BaseEventData data)
        {
            if (!IsActive() || !IsInteractable()) return;
#if UNITY_EDITOR
            DialogFocusScope.BeforeInput?.Invoke(transform);
#endif
            navigationSystem = EventSystem.current;
            base.OnSubmit(data);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if ((state == SelectionState.Selected && (navigationSystem == null || navigationSystem != EventSystem.current)) ||
                (state == SelectionState.Highlighted && touchHover))
                state = SelectionState.Normal;
            base.DoStateTransition(state, instant);
        }
    }
}
