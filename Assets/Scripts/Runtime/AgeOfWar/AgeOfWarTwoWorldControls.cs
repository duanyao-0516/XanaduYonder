using System;
using System.Collections.Generic;
using UnityEngine;
using XanaduYonder.Core.AgeOfWar;

namespace XanaduYonder.Runtime.AgeOfWar
{
    /// <summary>Original build/sell MovieClip controls, with independent Moon cross animation.</summary>
    internal sealed class AgeOfWarTwoWorldControls : IDisposable
    {
        private sealed class Control : IDisposable
        {
            internal readonly RectTransform Root;
            internal readonly AgeOfWarSceneView[] States=new AgeOfWarSceneView[3];
            internal readonly string Action;
            internal readonly int Slot,TurretId;
            internal Rect Bounds;
            internal bool Visible;
            internal int Frame,State;
            internal Control(Transform parent,AgeOfWarAssets assets,string prefix,string action,int slot,int turretId)
            {
                Action=action;Slot=slot;TurretId=turretId;
                Root=AgeOfWarDrawing.Rect(parent,action+" "+slot);
                string[] names={"out","over","down"};
                for(int index=0;index<names.Length;index++)
                {
                    States[index]=new AgeOfWarSceneView(Root,assets,prefix+"/"+names[index]);
                    bool cross=false;foreach(var layer in States[index].Definition.layers)if(layer.name=="cross")cross=true;
                    if(!cross)throw new InvalidOperationException("Missing original cross layer: "+prefix+"/"+names[index]);
                    States[index].Root.gameObject.SetActive(index==0);
                }
                Root.gameObject.SetActive(false);
            }
            internal void Display(int state)
            {
                Root.gameObject.SetActive(Visible);if(!Visible)return;
                for(int index=0;index<States.Length;index++)
                {
                    States[index].Root.gameObject.SetActive(index==state);
                    if(index==state)States[index].Named("cross",true,Frame);
                }
            }
            public void Dispose(){Root.gameObject.SetActive(false);UnityEngine.Object.Destroy(Root.gameObject);}
        }

        internal readonly RectTransform Root;
        private readonly AgeOfWarAssets assets;
        private readonly AgeOfWarScene buildLayout;
        private readonly Control[] builds=new Control[4],sells=new Control[4];
        private readonly List<Control> active=new List<Control>();
        private Vector2 pointer;
        private Control hoveredControl;
        private float cameraX,cameraY;
        private bool pointerPresent,down,paused,disposed;

        internal AgeOfWarTwoWorldControls(Transform parent,AgeOfWarAssets source)
        {
            if(source==null||source.Edition!=2)throw new ArgumentException("Second-edition assets are required.","source");
            assets=source;Root=AgeOfWarDrawing.Rect(parent,"Original turret controls");
            buildLayout=RequireScene("build-slots");
            for(int slot=1;slot<=4;slot++)builds[slot-1]=new Control(Root,assets,"build-slot-control","build-slot",slot,0);
        }
        private AgeOfWarScene RequireScene(string key)
        {var scene=assets.GetScene(key);if(scene==null)throw new InvalidOperationException("Missing original scene: "+key);return scene;}
        private static AgeOfWarSceneLayer Layer(AgeOfWarScene scene,string name)
        {foreach(var layer in scene.layers)if(layer.name==name)return layer;throw new InvalidOperationException("Missing original control placement: "+scene.key+"/"+name);}
        private static AgeOfWarHotspot Hotspot(AgeOfWarScene scene,string name)
        {foreach(var hotspot in scene.hotspots)if(hotspot.name==name)return hotspot;throw new InvalidOperationException("Missing original control hit zone: "+scene.key+"/"+name);}
        private static Rect TransformBounds(AgeOfWarHotspot hit,float x,float y,float scale)
        {return new Rect(x+hit.x*scale,y+hit.y*scale,hit.w*scale,hit.h*scale);}

        internal void Render(AgeOfWarFrame frame,bool selling,float x,float y)
        {
            if(disposed)return;
            cameraX=x;cameraY=y;paused=frame.Paused||frame.Ended;
            Root.anchoredPosition=new Vector2(x,-y);active.Clear();
            foreach(var control in builds)control.Visible=false;
            foreach(var control in sells)if(control!=null)control.Visible=false;
            foreach(var slot in frame.TurretSlots)
            {
                if(slot.Enemy||slot.Slot<1||slot.Slot>4)continue;
                int index=slot.Slot-1;
                var build=builds[index];var placement=Layer(buildLayout,"btnBuildTurret"+slot.Slot);
                build.Root.anchoredPosition=new Vector2(placement.x,-placement.y);
                build.Root.localScale=new Vector3(placement.scaleX,placement.scaleY,1);
                var buildHit=Hotspot(buildLayout,"btnBuildTurret"+slot.Slot);
                build.Bounds=new Rect(buildHit.x,buildHit.y,buildHit.w,buildHit.h);
                build.Frame=(int)frame.BuildTurretFrame;
                build.Visible=frame.PendingTurretId!=0&&slot.Purchased&&slot.TurretId==0;
                if(build.Visible)active.Add(build);
                if(slot.TurretId==0)continue;
                var sell=sells[index];
                if(sell==null||sell.TurretId!=slot.TurretId)
                {
                    if(sell!=null)sell.Dispose();
                    sell=new Control(Root,assets,"turret-sell-control/"+slot.TurretId,"sell-slot",slot.Slot,slot.TurretId);sells[index]=sell;
                }
                var layout=RequireScene("turret-sell/"+slot.TurretId);var sellPlacement=Layer(layout,"btnSell");
                float scale=(float)slot.Scale;
                sell.Root.anchoredPosition=new Vector2((float)slot.X+sellPlacement.x*scale,-((float)slot.Y+sellPlacement.y*scale));
                sell.Root.localScale=new Vector3(sellPlacement.scaleX*scale,sellPlacement.scaleY*scale,1);
                sell.Bounds=TransformBounds(Hotspot(layout,"btnSell"),(float)slot.X,(float)slot.Y,scale);
                sell.Frame=(int)slot.SellFrame;sell.Visible=selling;
                if(sell.Visible)active.Add(sell);
            }
            RefreshPointer();
        }
        private Control Find(Vector2 stagePoint)
        {
            if(paused)return null;
            var worldPoint=stagePoint-new Vector2(cameraX,cameraY);
            for(int index=active.Count-1;index>=0;index--)if(active[index].Bounds.Contains(worldPoint))return active[index];
            return null;
        }
        internal AgeOfWarHotspot Hit(Vector2 stagePoint)
        {
            if(disposed)return null;var hit=Find(stagePoint);
            return hit==null?null:new AgeOfWarHotspot{action=hit.Action,name=hit.Action,id=hit.Slot,x=hit.Bounds.x+cameraX,y=hit.Bounds.y+cameraY,w=hit.Bounds.width,h=hit.Bounds.height};
        }
        internal void Pointer(Vector2 stagePoint,bool isDown)
        {
            pointer=stagePoint;pointerPresent=true;RefreshPointer();
            // A drag entering a control sends MOUSE_OVER, not a fresh MOUSE_DOWN.
            if(hoveredControl!=null&&isDown!=down)hoveredControl.State=isDown?2:1;
            down=isDown;DisplayControls();
        }
        internal void HidePointer(){pointerPresent=false;down=false;RefreshPointer();}
        private void RefreshPointer()
        {
            if(disposed)return;
            var hovered=pointerPresent?Find(pointer):null;
            if(hovered!=hoveredControl)
            {
                if(hoveredControl!=null)hoveredControl.State=0;
                if(hovered!=null)hovered.State=1;
                hoveredControl=hovered;
            }
            DisplayControls();
        }
        private void DisplayControls()
        {
            if(disposed)return;
            foreach(var control in builds)control.Display(control.State);
            foreach(var control in sells)if(control!=null)control.Display(control.State);
        }
        public void Dispose()
        {if(disposed)return;disposed=true;Root.gameObject.SetActive(false);UnityEngine.Object.Destroy(Root.gameObject);active.Clear();}
    }
}
