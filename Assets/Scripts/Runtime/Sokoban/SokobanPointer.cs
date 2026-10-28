using System;
using UnityEngine;
using UnityEngine.EventSystems;
using XanaduYonder.Core;

namespace XanaduYonder.Runtime.Sokoban
{
    internal sealed class SokobanPointer : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IDragHandler
    {
        private RectTransform surface;
        private RectTransform[] controls;
        private Direction[] directions;
        private SokobanInput input;
        private Func<bool> allowed;
        private Action<SokobanInputResult> deliver;
        private Camera eventCamera;
        private bool mouse,hadTouch;
        private int mouseId;
        internal bool HasOwner { get { return input!=null&&input.HasOwner; } }
        internal void Initialize(RectTransform board,SokobanViewport viewport,RectTransform[] buttons,Direction[] values,Func<bool> canInput,Action<SokobanInputResult> handle)
        {
            CancelGesture(); surface=board; controls=buttons; directions=values; allowed=canInput; deliver=handle;
            input=new SokobanInput();
        }
        internal void CancelGesture()
        { if(input==null) return; input.Cancel(); if(Input.touchCount==0&&!Input.GetMouseButton(0)) input.Process(false); }
        private SokobanContact Contact(int id,Vector2 screen,SokobanContactPhase phase)
        {
            Vector2 p; RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,screen,eventCamera,out p);
            var region=RectTransformUtility.RectangleContainsScreenPoint(surface,screen,eventCamera)?SokobanContactRegion.Board:SokobanContactRegion.Other;
            Direction direction=Direction.Up;
            if(controls!=null) for(int i=0;i<controls.Length;i++) if(SokobanHitArea.Contains(controls[i],screen,eventCamera))
            { region=SokobanContactRegion.Control; direction=directions[i]; break; }
            return new SokobanContact(id,p.x-surface.rect.xMin,surface.rect.yMax-p.y,phase,region,direction);
        }
        private void Process(params SokobanContact[] contacts) { if(input!=null) deliver(input.Process(allowed(),contacts)); }
        private void LateUpdate()
        {
            if(input==null) return;
            var canvas=surface.GetComponentInParent<Canvas>(); eventCamera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            if(Input.touchCount>0)
            {
                if(mouse) { input.Cancel(); mouse=false; }
                hadTouch=true; var contacts=new SokobanContact[Input.touchCount];
                for(int i=0;i<contacts.Length;i++)
                {
                    var touch=Input.GetTouch(i); var phase=touch.phase==TouchPhase.Began?SokobanContactPhase.Began:touch.phase==TouchPhase.Ended?SokobanContactPhase.Ended:
                        touch.phase==TouchPhase.Canceled?SokobanContactPhase.Canceled:touch.phase==TouchPhase.Moved?SokobanContactPhase.Moved:SokobanContactPhase.Stationary;
                    contacts[i]=Contact(touch.fingerId,touch.position,phase);
                }
                Process(contacts);
            }
            else if(hadTouch) { hadTouch=false; Process(); }
            else if(mouse)
            {
                // A lost up event is cancellation, never a synthetic successful release.
                if(!Input.GetMouseButton(0)) { Process(Contact(mouseId,Input.mousePosition,SokobanContactPhase.Canceled)); mouse=false; }
            }
            else Process();
        }
        public void OnPointerDown(PointerEventData data)
        {
            if(input==null||Input.touchCount>0||data.pointerId>=0||data.button!=PointerEventData.InputButton.Left) return;
            if(mouse) { input.Cancel(); return; }
            mouse=true; mouseId=data.pointerId; eventCamera=data.pressEventCamera; Process(Contact(mouseId,data.position,SokobanContactPhase.Began));
        }
        public void OnPointerUp(PointerEventData data)
        {
            if(!mouse||Input.touchCount>0||data.pointerId!=mouseId) return;
            mouse=false; Process(Contact(mouseId,data.position,SokobanContactPhase.Ended)); Process();
        }
        public void OnDrag(PointerEventData data)
        { if(mouse&&Input.touchCount==0&&data.pointerId==mouseId) Process(Contact(mouseId,data.position,SokobanContactPhase.Moved)); }
        private void OnDisable() { CancelGesture(); mouse=false; hadTouch=false; }
    }
    // Each direction button routes press ownership through the same arbiter as the board.
    internal sealed class SokobanDirectionTarget : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IDragHandler
    {
        internal SokobanPointer Pointer;
        public void OnPointerDown(PointerEventData data) { Pointer.OnPointerDown(data); }
        public void OnPointerUp(PointerEventData data) { Pointer.OnPointerUp(data); }
        public void OnDrag(PointerEventData data) { Pointer.OnDrag(data); }
    }
}
