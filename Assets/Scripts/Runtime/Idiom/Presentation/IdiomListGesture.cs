using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace XanaduYonder.Runtime.Idiom
{
    internal sealed class IdiomListGesture : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        public bool IsDragging { get; private set; }
        public Action Changed;
        public void OnBeginDrag(PointerEventData eventData) { IsDragging = true; Changed?.Invoke(); }
        public void OnEndDrag(PointerEventData eventData) { IsDragging = false; Changed?.Invoke(); }
        private void OnDisable() { IsDragging = false; }
    }
}
