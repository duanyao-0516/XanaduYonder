using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace XanaduYonder.Runtime.LinkMatch
{
    internal sealed class LinkMatchPointer : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IDragHandler,IInitializePotentialDragHandler
    {
        private readonly LinkMatchInput input=new LinkMatchInput();
        internal RectTransform Surface;
        internal Func<LinkMatchLayout> Layout;
        internal Func<bool> Allowed;
        internal Action<int> Submit;
        private int Tile(PointerEventData e)
        {
            if(Surface==null||Layout==null||!RectTransformUtility.ScreenPointToLocalPointInRectangle(Surface,e.position,e.pressEventCamera,out var p)) return -1;
            return Layout().Hit(p.x-Surface.rect.xMin,Surface.rect.yMax-p.y);
        }
        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold=false; }
        public void OnPointerDown(PointerEventData e)
        { if(e.button==PointerEventData.InputButton.Left) input.Begin(e.pointerId,Tile(e),Allowed()!=false&&Input.touchCount<=1); }
        public void OnDrag(PointerEventData e) { input.Move(e.pointerId,Tile(e)); }
        public void OnPointerUp(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left) return;
            int tile=input.End(e.pointerId,Tile(e),Allowed()!=false&&Input.touchCount<=1); if(tile>=0) Submit(tile);
        }
        private void Update() { if(Input.touchCount>1||Allowed==null||!Allowed()) input.Cancel(); }
        internal void Cancel() { input.Cancel(); }
        private void OnDisable() { Cancel(); }
    }
}
