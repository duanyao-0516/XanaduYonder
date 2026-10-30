using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.Two;

namespace XanaduYonder.Runtime.AgeOfWar
{
    /// <summary>Original Hud.as overlays and Windows/*Info fields; all artwork comes from the embedded HUD SWF.</summary>
    internal sealed class AgeOfWarTwoHud : IDisposable
    {
        private sealed class UnitStatus
        {
            internal AgeOfWarImage Lock;
            internal AgeOfWarSceneView Progress,Queue;
        }
        private sealed class Window
        {
            internal RectTransform Root;
            internal AgeOfWarSceneView Scene;
            internal CanvasGroup Group;
            internal string Kind,Variant;
            internal readonly AgeOfWarTwoWindowFade Fade=new AgeOfWarTwoWindowFade();
        }
        internal readonly RectTransform Root,TooltipRoot;
        private readonly Dictionary<string,Window> windows=new Dictionary<string,Window>();
        private Vector2 pointer;
        private float viewportWidth=720;
        private readonly RectTransform statuses;
        private readonly AgeOfWarAssets assets;
        private readonly Dictionary<string,AgeOfWarSceneLayer> placements=new Dictionary<string,AgeOfWarSceneLayer>();
        private readonly Dictionary<int,UnitStatus> units=new Dictionary<int,UnitStatus>();
        private readonly Dictionary<string,AgeOfWarImage> unavailable=new Dictionary<string,AgeOfWarImage>();
        private readonly AgeOfWarImage cooldown;
        private readonly AgeOfWarSceneView error;
        private readonly CanvasGroup errorAlpha;
        private bool disposed;
        private double errorElapsed=double.PositiveInfinity,errorOpacity;
        // Cancel's original full-width sprite covers the button panel in both placement and sale modes.
        internal bool CoveredByCancel;

        internal AgeOfWarTwoHud(Transform parent,AgeOfWarAssets source,Transform tooltipParent=null)
        {
            assets=source;
            if(source.Edition!=2)throw new ArgumentException("The second-edition HUD requires second-edition assets.","source");
            Root=AgeOfWarDrawing.Rect(parent,"Age of War 2 dynamic HUD");
            TooltipRoot=AgeOfWarDrawing.Rect(tooltipParent==null?parent:tooltipParent,"Original seven tooltip windows");
            statuses=AgeOfWarDrawing.Rect(Root,"Button status overlays",10,19);
            var buttons=source.GetScene("buttons/1");
            if(buttons==null)throw new InvalidOperationException("Missing original HUD button layout.");
            foreach(var layer in buttons.layers)if(!string.IsNullOrEmpty(layer.name))placements[layer.name]=layer;
            cooldown=new AgeOfWarImage(statuses,assets,"Special cooldown");
            foreach(string name in new[]{"btnSellTurret","btnAddTower","btnEvolve"})
            {
                var image=new AgeOfWarImage(statuses,assets,name+" unavailable");var p=Placement(name);
                image.Set("ui/hud/347","idle",0,p.x,p.y,p.scaleX,p.scaleY,0,.5);unavailable.Add(name,image);
            }
            error=new AgeOfWarSceneView(Root,assets,"error-popup",3,80);
            error.Root.localScale=new Vector3(1,.9982757568359375f,1);
            errorAlpha=error.Root.gameObject.AddComponent<CanvasGroup>();errorAlpha.alpha=0;errorAlpha.blocksRaycasts=false;errorAlpha.interactable=false;
            // Hud.init adds all seven windows in this source order; they keep independent alpha states.
            foreach(string kind in new[]{"unitInfo","upgradeInfo","evolveInfo","specialInfo","turretInfo","sellTurretInfo","addTowerInfo"})
            {
                var window=new Window {Kind=kind,Variant=kind,Root=AgeOfWarDrawing.Rect(TooltipRoot,kind)};
                window.Group=window.Root.gameObject.AddComponent<CanvasGroup>();window.Group.alpha=0;window.Group.blocksRaycasts=false;window.Group.interactable=false;
                window.Scene=new AgeOfWarSceneView(window.Root,assets,"window/"+kind);windows.Add(kind,window);
            }
        }
        private AgeOfWarSceneLayer Placement(string name)
        {
            AgeOfWarSceneLayer value;
            if(!placements.TryGetValue(name,out value))throw new InvalidOperationException("Missing original HUD placement: "+name);
            return value;
        }
        private float PlacementX(AgeOfWarSceneLayer placement)
        {return placement.x+(placement.x>=230?viewportWidth-720:0);}
        internal void SetViewportWidth(float width)
        {
            if(disposed)return;
            viewportWidth=Mathf.Max(720,width);
            foreach(var item in unavailable)
            {
                var placement=Placement(item.Key);
                item.Value.Root.anchoredPosition=new Vector2(PlacementX(placement),-placement.y);
            }
            foreach(var item in units)
            {
                var placement=Placement("btnU"+item.Key);var position=new Vector2(PlacementX(placement),-placement.y);
                item.Value.Lock.Root.anchoredPosition=position;
                item.Value.Progress.Root.anchoredPosition=position;
                item.Value.Queue.Root.anchoredPosition=position;
            }
            TooltipRoot.sizeDelta=new Vector2(viewportWidth,TooltipRoot.sizeDelta.y);
        }
        private UnitStatus GetUnit(int id)
        {
            UnitStatus value;if(units.TryGetValue(id,out value))return value;
            var p=Placement("btnU"+id);
            value=new UnitStatus {
                Lock=new AgeOfWarImage(statuses,assets,"Unit "+id+" lock"),
                Progress=new AgeOfWarSceneView(statuses,assets,"status-progress",PlacementX(p),p.y),
                Queue=new AgeOfWarSceneView(statuses,assets,"status-queue",PlacementX(p),p.y)
            };
            value.Lock.Set("ui/hud/349","idle",0,PlacementX(p),p.y,p.scaleX,p.scaleY,0,102/256d);
            units.Add(id,value);return value;
        }
        internal void Render(AgeOfWarTwoBattle battle,AgeOfWarFrame frame)
        {
            if(disposed)return;
            statuses.gameObject.SetActive(!CoveredByCancel&&frame.PendingTurretId==0);
            foreach(var item in units.Values)
            {
                item.Lock.Root.gameObject.SetActive(false);item.Progress.Root.gameObject.SetActive(false);item.Queue.Root.gameObject.SetActive(false);
            }
            int[] waiting=battle.WaitingUnitIds;
            foreach(var offer in frame.Units)
            {
                // Future age retains Tank (24), but ACS (28) occupies its same rectangle at a higher source depth.
                var placement=Placement("btnU"+offer.Id);bool obscured=false;
                foreach(var other in frame.Units)if(other.Id!=offer.Id)
                {
                    var top=Placement("btnU"+other.Id);
                    if(top.depth>placement.depth&&top.x==placement.x&&top.y==placement.y){obscured=true;break;}
                }
                if(obscured)continue;
                var status=GetUnit(offer.Id);status.Lock.Root.gameObject.SetActive(offer.LockedAppearance);
                bool building=battle.BuildingUnitId==offer.Id;
                status.Progress.Root.gameObject.SetActive(building);
                if(building)
                {
                    for(int index=0;index<status.Progress.Images.Count;index++)
                    {
                        var layer=status.Progress.Definition.layers[index];
                        if(layer.name!="bar")continue;
                        status.Progress.Layer(index,true);
                        status.Progress.Images[index].Root.localScale=new Vector3(layer.scaleX,layer.scaleY*(float)battle.TrainingRemainingFraction,1);
                    }
                }
                int count=0;foreach(int unit in waiting)if(unit==offer.Id)count++;
                status.Queue.Root.gameObject.SetActive(count>0);
                if(count>0)status.Queue.Named("queue",true,count);
            }
            bool hasTurret=false;int spots=0;
            foreach(var slot in frame.TurretSlots)if(!slot.Enemy){if(slot.Purchased)spots++;if(slot.TurretId!=0)hasTurret=true;}
            unavailable["btnSellTurret"].Root.gameObject.SetActive(!hasTurret);
            unavailable["btnAddTower"].Root.gameObject.SetActive(spots>=4);
            unavailable["btnEvolve"].Root.gameObject.SetActive(frame.Age>=7);
            var special=Placement("btnS"+frame.Age);
            cooldown.Set("ui/hud/74","idle",battle.SpecialDelayFrame-1,PlacementX(special),special.y,special.scaleX,special.scaleY,0,.5);
            errorAlpha.alpha=(float)errorOpacity;error.Root.gameObject.SetActive(errorOpacity>0);
            foreach(var window in windows.Values)
            {
                window.Group.alpha=(float)window.Fade.Alpha;window.Root.gameObject.SetActive(window.Fade.Alpha>0);
                // Pointer is already in the full logical stage; do not clamp it to the old720px width.
                window.Root.anchoredPosition=new Vector2((float)Math.Floor(pointer.x+10+.5),-(float)Math.Floor(pointer.y+20+.5));
            }
            TooltipRoot.SetAsLastSibling();
        }
        internal void Pointer(Vector2 point){pointer=point;}
        internal void Advance(double elapsed,int speed)
        {
            if(disposed)return;
            foreach(var window in windows.Values)window.Fade.Advance(elapsed*speed);
            errorElapsed+=elapsed;
            double age=errorElapsed;
            double alpha=age<.2?1-Math.Pow(1-age/.2,2):age<1.2?1:age<1.4?Math.Pow(1-(age-1.2)/.2,2):0;
            errorOpacity=Math.Truncate(alpha*256)/256;
        }
        internal void ShowError(string message)
        {
            if(disposed)return;
            error.Value("errorMsg",message);errorElapsed=0;errorOpacity=0;errorAlpha.alpha=0;error.Root.gameObject.SetActive(true);
            Text messageField=null;
            for(int i=0;i<error.Texts.Count;i++)if(error.Definition.texts[i].name=="errorMsg")messageField=error.Texts[i];
            if(messageField==null)throw new InvalidOperationException("Original error popup is missing errorMsg.");
            for(int i=0;i<error.Images.Count;i++)
            {
                var layer=error.Definition.layers[i];if(layer.name!="errorBG")continue;
                var image=error.Images[i];
                float width=image.Image.rectTransform.sizeDelta.x;
                if(width>0)image.Root.localScale=new Vector3((90+messageField.preferredWidth)/width,layer.scaleY,1);
            }
        }
        internal void ShowTooltip(AgeOfWarTwoBattle battle,AgeOfWarAction action,int id,Vector2 point)
        {
            if(disposed)return;pointer=point;string kind=Kind(action);HideTooltip();if(kind==null)return;
            var window=windows[kind];window.Fade.Visible=true;
            bool resale=action==AgeOfWarAction.SellTurret&&id>0;string variant=resale?kind+"/onT":kind;
            if(window.Variant!=variant)
            {
                window.Scene.Dispose();window.Scene=new AgeOfWarSceneView(window.Root,assets,"window/"+variant,0,resale?-1:0);window.Variant=variant;
            }
            foreach(var pair in battle.CaptureTooltip(action,id))window.Scene.Value(resale&&pair.Key=="onT.costTXT"?"costTXT":pair.Key,pair.Value);
            if(action==AgeOfWarAction.SellTurret&&!resale){window.Scene.Named("onB",true);window.Scene.Named("onT",false);}
        }
        private static string Kind(AgeOfWarAction action)
        {
            switch(action)
            {
                case AgeOfWarAction.Train:return "unitInfo";
                case AgeOfWarAction.BuyTurret:return "turretInfo";
                case AgeOfWarAction.Upgrade:return "upgradeInfo";
                case AgeOfWarAction.Special:return "specialInfo";
                case AgeOfWarAction.Evolve:return "evolveInfo";
                case AgeOfWarAction.BuyTurretSlot:return "addTowerInfo";
                case AgeOfWarAction.SellTurret:return "sellTurretInfo";
                default:return null;
            }
        }
        internal void HideTooltip(){foreach(var window in windows.Values)window.Fade.Visible=false;}
        public void Dispose()
        {
            if(disposed)return;disposed=true;Root.gameObject.SetActive(false);TooltipRoot.gameObject.SetActive(false);UnityEngine.Object.Destroy(Root.gameObject);UnityEngine.Object.Destroy(TooltipRoot.gameObject);units.Clear();unavailable.Clear();placements.Clear();windows.Clear();
        }
    }
}
