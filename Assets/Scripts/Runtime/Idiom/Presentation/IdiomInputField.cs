using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Idiom
{
    // UGUI deselects on DOWN, before the send Button receives its UP/click.
    // Keep native ownership until Submit decides whether to dismiss the IME.
    internal sealed class IdiomInputField : InputField
    {
        public Button SendButton { private get; set; }
        private bool restoreSelection;

        public override void OnDeselect(BaseEventData eventData)
        {
            var pointer = eventData as PointerEventData;
            var hit = pointer == null ? null : pointer.pointerCurrentRaycast.gameObject;
            if (isFocused && IsInteractable() && SendButton != null && SendButton.IsActive() &&
                SendButton.IsInteractable() && hit != null && hit.transform.IsChildOf(SendButton.transform))
            {
                restoreSelection = true;
                return;
            }
            restoreSelection = false;
            base.OnDeselect(eventData);
        }

        protected override void LateUpdate()
        {
            // EventSystem rejects re-selection inside its OnDeselect guard.
            // Do not steal focus if another control selected itself meanwhile.
            if (restoreSelection)
            {
                restoreSelection = false;
                if (isFocused && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
                    EventSystem.current.SetSelectedGameObject(gameObject);
            }
            base.LateUpdate();
        }
    }
}
