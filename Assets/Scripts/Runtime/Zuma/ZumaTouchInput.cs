using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace XanaduYonder.Runtime.Zuma
{
    internal sealed class ZumaTouchInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        private Action<int,Vector2> down,move;
        private Action<int,Vector2,bool,bool> up;
        private readonly List<RaycastResult> hits=new List<RaycastResult>();
        public void Initialize(Action<int,Vector2> begin,Action<int,Vector2> drag,Action<int,Vector2,bool,bool> end)
        { down=begin; move=drag; up=end; }
        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold=false; }
        public void OnPointerDown(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left) down?.Invoke(e.pointerId,e.position); }
        public void OnDrag(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left) move?.Invoke(e.pointerId,e.position); }
        public void OnPointerUp(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left) return;
            var cancelled=false;
            for(var i=0;i<Input.touchCount;i++) { var t=Input.GetTouch(i); if(t.fingerId==e.pointerId && t.phase==TouchPhase.Canceled) cancelled=true; }
            hits.Clear(); EventSystem.current?.RaycastAll(e,hits);
            var onField=hits.Count>0 && hits[0].gameObject==gameObject;
            up?.Invoke(e.pointerId,e.position,cancelled,onField);
        }
    }
}
