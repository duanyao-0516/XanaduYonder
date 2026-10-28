using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace XanaduYonder.Runtime.Minesweeper
{
    internal sealed class MinesweeperPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private RectTransform surface;
        private MinesweeperGesture gesture;
        private Func<MinesweeperInputContact[],MinesweeperInputFrame> frame;
        private Func<int,bool> canFlag;
        private Action<MinesweeperGestureResult> deliver;
        private bool mouse, secondary, hadTouch;
        private int mouseId;
        private Camera eventCamera;
        public void Initialize(RectTransform rect,MinesweeperViewport viewport,
            Func<MinesweeperInputContact[],MinesweeperInputFrame> makeFrame,Func<int,bool> canToggleFlag,Action<MinesweeperGestureResult> completed)
        { CancelGesture(); surface=rect; gesture=new MinesweeperGesture(viewport); frame=makeFrame; canFlag=canToggleFlag; deliver=completed; }
        public MinesweeperGestureResult ProcessFrame(MinesweeperInputFrame input)
        {
            var result=gesture.ProcessFrame(input,canFlag); deliver(result); return result;
        }
        public void CancelGesture() { gesture?.Cancel(); }
        private MinesweeperInputPosition Position(Vector2 screen)
        {
            Vector2 point;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,screen,eventCamera,out point);
            return new MinesweeperInputPosition(point.x-surface.rect.xMin,surface.rect.yMax-point.y);
        }
        private void LateUpdate()
        {
            if(gesture==null) return;
            var canvas=surface.GetComponentInParent<Canvas>();
            if(canvas!=null) eventCamera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            if(Input.touchCount>0)
            {
                if(mouse) { CancelGesture(); mouse=false; }
                hadTouch=true;
                var contacts=new MinesweeperInputContact[Input.touchCount];
                for(int i=0;i<contacts.Length;i++)
                {
                    var touch=Input.GetTouch(i); var p=Position(touch.position);
                    var phase=touch.phase==TouchPhase.Began?MinesweeperInputPhase.Began:touch.phase==TouchPhase.Ended?MinesweeperInputPhase.Ended:
                        touch.phase==TouchPhase.Canceled?MinesweeperInputPhase.Canceled:touch.phase==TouchPhase.Moved?MinesweeperInputPhase.Moved:MinesweeperInputPhase.Stationary;
                    contacts[i]=new MinesweeperInputContact(touch.fingerId,p.X,p.Y,phase);
                }
                ProcessFrame(frame(contacts));
            }
            else if(hadTouch) { hadTouch=false; ProcessFrame(frame(new MinesweeperInputContact[0])); }
            else if(mouse)
            {
                var p=Position(Input.mousePosition);
                bool held=Input.GetMouseButton(secondary?1:0);
                ProcessFrame(frame(new[]{new MinesweeperInputContact(mouseId,p.X,p.Y,held?MinesweeperInputPhase.Moved:MinesweeperInputPhase.Canceled,secondary)}));
                if(!held) mouse=false;
            }
            else ProcessFrame(frame(new MinesweeperInputContact[0]));
        }
        public void OnPointerDown(PointerEventData data)
        {
            if(gesture==null||Input.touchCount>0||data.pointerId>=0) return;
            if(data.button!=PointerEventData.InputButton.Left&&data.button!=PointerEventData.InputButton.Right) return;
            if(mouse) { CancelGesture(); return; }
            eventCamera=data.pressEventCamera; mouse=true; secondary=data.button==PointerEventData.InputButton.Right; mouseId=data.pointerId;
            var p=Position(data.position);
            ProcessFrame(frame(new[]{new MinesweeperInputContact(mouseId,p.X,p.Y,MinesweeperInputPhase.Began,secondary)}));
        }
        public void OnPointerUp(PointerEventData data)
        {
            if(!mouse||gesture==null||Input.touchCount>0||data.pointerId!=mouseId) return;
            var p=Position(data.position); mouse=false;
            ProcessFrame(frame(new[]{new MinesweeperInputContact(mouseId,p.X,p.Y,MinesweeperInputPhase.Ended,secondary)}));
            ProcessFrame(frame(new MinesweeperInputContact[0]));
        }
        public void OnDrag(PointerEventData data)
        {
            if(!mouse||gesture==null||Input.touchCount>0||data.pointerId!=mouseId) return;
            var p=Position(data.position);
            ProcessFrame(frame(new[]{new MinesweeperInputContact(mouseId,p.X,p.Y,MinesweeperInputPhase.Moved,secondary)}));
        }
        private void OnDisable() { CancelGesture(); mouse=false; }
    }
}
