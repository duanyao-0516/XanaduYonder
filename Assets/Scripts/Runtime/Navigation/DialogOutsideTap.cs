using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    internal sealed class DialogOutsideTap : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IDragHandler
    {
        private Action dismiss;
#if UNITY_EDITOR
        private void OnEnable()
        {
            if (!TryGetComponent<DialogFocusScope>(out _)) DialogFocusScope.VisibilityChanged?.Invoke(transform, true);
        }
        private void OnDisable()
        {
            if (!TryGetComponent<DialogFocusScope>(out _)) DialogFocusScope.VisibilityChanged?.Invoke(transform, false);
        }
#endif

        internal static void Bind(Graphic shade, Action onDismiss)
        {
            DialogTypography.Attach(shade);
            shade.raycastTarget = true;
            if (!shade.TryGetComponent<DialogOutsideTap>(out var tap))
                tap = shade.gameObject.AddComponent<DialogOutsideTap>();
            tap.dismiss = onDismiss;
        }

        // An art panel can consist entirely of non-interactive children. Give its whitespace a hit target.
        internal static void Protect(RectTransform panel)
        {
            if (!panel.TryGetComponent<Graphic>(out var surface))
            {
                surface = panel.gameObject.AddComponent<Image>();
                surface.color = Color.clear;
            }
            surface.raycastTarget = true;
        }

        // Keep pointerPress on the shade when the underlying game handles presses on an ancestor.
        public void OnPointerDown(PointerEventData eventData) { }

        // Registering a drag handler lets the input module apply its normal movement threshold
        // even to empty backdrop space. A swipe ending on the backdrop is not a dismissal.
        public void OnDrag(PointerEventData eventData) { }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging ||
                !IsShade(eventData.pointerPressRaycast.gameObject) || !IsShade(eventData.pointerCurrentRaycast.gameObject)) return;
#if UNITY_EDITOR
            DialogFocusScope.BeforeInput?.Invoke(transform);
#endif
            dismiss?.Invoke();
        }

        private bool IsShade(GameObject hit) => hit == gameObject ||
            (hit != null && hit.transform.parent == transform && hit.TryGetComponent<DialogBackdrop>(out _));
    }
}
