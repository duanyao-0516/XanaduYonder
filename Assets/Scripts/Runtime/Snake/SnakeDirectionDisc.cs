using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core.Snake;

namespace XanaduYonder.Runtime.Snake
{
    // Four sectors share the board's pointer ownership, cancellation and turn queue.
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class SnakeDirectionDisc : MaskableGraphic, IPointerDownHandler,
        IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler, ICanvasRaycastFilter
    {
        private SnakeInput inputTarget;
        public SnakeInput InputTarget
        {
            get { return inputTarget; }
            set
            {
                if(inputTarget==value)return;
                DetachInput();ClearPointer();inputTarget=value;AttachInput();
            }
        }
        private bool subscribed;
        private int? pointer;
        private SnakeDirection? lastDirection;
        private SnakeDirection? pressedDirection;
        private const float DeadZone = .25f;

        protected override void OnEnable() { base.OnEnable();AttachInput(); }
        private void AttachInput()
        {
            if(subscribed||inputTarget==null||!isActiveAndEnabled)return;
            inputTarget.PointerReleased+=ClearPointer;subscribed=true;
        }
        private void DetachInput()
        { if(subscribed&&inputTarget!=null)inputTarget.PointerReleased-=ClearPointer;subscribed=false; }
        private void ClearPointer() { pointer=null;lastDirection=null;SetPressed(null); }
        private void SetPressed(SnakeDirection? direction)
        { if(pressedDirection==direction)return;pressedDirection=direction;SetVerticesDirty(); }
        private void LateUpdate()
        { if(pointer.HasValue&&(InputTarget==null||!InputTarget.OwnsPointer(pointer.Value)))ClearPointer(); }

        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold=false; }
        public void OnPointerDown(PointerEventData e)
        {
            if(InputTarget==null||!InputTarget.BeginDirectionPointer(e))return;
            pointer=e.pointerId;lastDirection=null;Move(e);
        }
        public void OnDrag(PointerEventData e) { if(pointer==e.pointerId)Move(e); }
        private void Move(PointerEventData e)
        {
            if(InputTarget==null||!InputTarget.OwnsPointer(e.pointerId)){ClearPointer();return;}
            Vector2 point;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,e.position,e.pressEventCamera,out point))
            { SetPressed(null);return; }
            point-=rectTransform.rect.center;
            float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;
            if(point.sqrMagnitude<radius*radius*DeadZone*DeadZone||point.sqrMagnitude>radius*radius)
            { lastDirection=null;SetPressed(null);return; }
            var direction=Mathf.Abs(point.x)>=Mathf.Abs(point.y)
                ?(point.x>0?SnakeDirection.Right:SnakeDirection.Left)
                :(point.y>0?SnakeDirection.Up:SnakeDirection.Down);
            SetPressed(direction);
            if(lastDirection==direction)return;
            if(InputTarget.MoveDirectionPointer(e,direction))lastDirection=direction;
            else ClearPointer();
        }
        public void OnPointerUp(PointerEventData e)
        { if(pointer!=e.pointerId)return;InputTarget?.Release(e.pointerId);ClearPointer(); }
        protected override void OnDisable()
        { if(pointer.HasValue)InputTarget?.Cancel();DetachInput();ClearPointer();base.OnDisable(); }
        public bool IsRaycastLocationValid(Vector2 screenPoint,Camera eventCamera)
        {
            Vector2 local;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screenPoint,eventCamera,out local))return false;
            float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;
            return (local-rectTransform.rect.center).sqrMagnitude<=radius*radius;
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=GetPixelAdjustedRect();float radius=Mathf.Min(rect.width,rect.height)*.5f;
            for(int sector=0;sector<4;sector++)
            {
                var direction=sector==0?SnakeDirection.Right:sector==1?SnakeDirection.Up:sector==2?SnakeDirection.Left:SnakeDirection.Down;
                // Same tint as UiFactory's pressed buttons; only the owned sector darkens.
                var tint=pressedDirection==direction&&pointer.HasValue&&InputTarget!=null&&InputTarget.OwnsPointer(pointer.Value)
                    ?color*new Color(.78f,.85f,.78f,1):color;
                for(int step=0;step<20;step++)
                {
                    float a=(sector*90-43+step*86f/20)*Mathf.Deg2Rad;
                    float b=(sector*90-43+(step+1)*86f/20)*Mathf.Deg2Rad;
                    var v1=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var v2=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                    int start=mesh.currentVertCount;
                    mesh.AddVert(rect.center+v1*radius*DeadZone,tint,Vector2.zero);
                    mesh.AddVert(rect.center+v1*radius,tint,Vector2.zero);
                    mesh.AddVert(rect.center+v2*radius,tint,Vector2.zero);
                    mesh.AddVert(rect.center+v2*radius*DeadZone,tint,Vector2.zero);
                    mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
                }
            }
        }
    }
}
