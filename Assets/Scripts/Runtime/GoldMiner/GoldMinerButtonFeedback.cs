using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.GoldMiner
{
    internal sealed class GoldMinerButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public void OnPointerDown(PointerEventData data)
        {
            var button = GetComponent<Button>();
            if (button != null && button.IsInteractable()) transform.localScale = Vector3.one * .97f;
        }
        public void OnPointerUp(PointerEventData data) { ResetScale(); }
        public void OnPointerExit(PointerEventData data) { ResetScale(); }
        private void OnDisable() { ResetScale(); }
        private void ResetScale() { transform.localScale = Vector3.one; }
    }
}
