using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.AgeOfWar
{
    /// <summary>Original opening display lists. The host owns audio, transitions and navigation.</summary>
    internal sealed class AgeOfWarOpening : IDisposable
    {
        private readonly AgeOfWarAssets assets;
        private readonly AgeOfWarOpeningState state;
        private readonly Action<AgeOfWarOpeningCue> cue;
        private readonly RectTransform root;
        private readonly Surface main,overlay;
        private AgeOfWarHotspot hover,pressed;
        private bool disposed,suspended,validated;
        private double activeTime;
        internal bool Done {get{return state.Done;}}
        internal bool CanPressPlay {get{return state.CanPressPlay;}}
        internal int SourceFrame {get{return state.SourceFrame;}}

        internal AgeOfWarOpening(Transform stage,AgeOfWarAssets assets,Func<int,int> randomInteger,Action<AgeOfWarOpeningCue> cue)
        {
            if(stage==null)throw new ArgumentNullException("stage");
            if(assets==null)throw new ArgumentNullException("assets");
            this.assets=assets;this.cue=cue??throw new ArgumentNullException("cue");
            state=new AgeOfWarOpeningState(assets.Edition,randomInteger);
            // This root stays at its original sibling index, below later transition snapshots.
            root=AgeOfWarDrawing.Rect(stage,"Original opening",0,0,assets.Stage.width,assets.Stage.height);
            main=new Surface(root,assets,"Opening root");overlay=new Surface(root,assets,"Opening movie");
            state.FrameAdvanced+=Present;
            Present();
        }
        internal void ResourceProgress(long loadedBytes,long totalBytes,bool ready)
        {
            if(disposed)return;
            if(ready&&!validated){ValidateResources();validated=true;}
            state.ResourceProgress(loadedBytes,totalBytes,ready);
        }
        private void ValidateResources()
        {
            var keys=new List<string>{"preloader"};
            if(assets.Edition==1)
            {
                keys.AddRange(new[]{"preloader-progress","loaded-lead","loaded","intro-lead","intro-background"});
                for(int i=1;i<=160;i++)keys.Add("intro/"+i);
            }
            else
            {
                keys.AddRange(new[]{"sponsor-initial","sponsor"});
                for(int i=1;i<=66;i++)keys.Add("author/"+i);
            }
            var checkedClips=new HashSet<string>();
            foreach(string key in keys)
            {
                var scene=assets.GetScene(key);if(scene==null)throw new InvalidOperationException("Missing original opening scene: "+key);
                if(scene.layers==null)throw new InvalidOperationException("Missing original opening layers: "+key);
                foreach(var layer in scene.layers)
                {
                    string identity=layer.key+"\n"+layer.action;if(!checkedClips.Add(identity))continue;
                    var clip=assets.GetClip(layer.key,layer.action);
                    if(clip==null||clip.frames==null||clip.frames.Length==0)throw new InvalidOperationException("Missing original opening clip: "+identity);
                    int tick=0;
                    foreach(var frame in clip.frames)
                    {
                        AgeOfWarSpriteFrame output;
                        if(!assets.TryGetFrame(layer.key,layer.action,tick,out output))throw new InvalidOperationException("Missing opening frame: "+identity);
                        tick+=frame.ticks;
                    }
                }
            }
            foreach(string sound in assets.Edition==1?new[]{"sound/1002","intro-stream"}:new[]{"music/2","sponsor-stream"})
                if(assets.LoadSound(sound)==null)throw new InvalidOperationException("Missing original opening audio: "+sound);
        }
        internal void Advance(double activeSeconds)
        {
            if(disposed||suspended)return;
            if(!double.IsNaN(activeSeconds)&&!double.IsInfinity(activeSeconds)&&activeSeconds>0)activeTime+=Math.Min(activeSeconds,.25);
            state.Advance(activeSeconds);
            main.Hover(hover,pressed,activeTime);overlay.Hover(hover,pressed,activeTime);
        }
        internal void TransitionComplete(){if(!disposed)state.TransitionComplete();}
        internal void Suspend(bool value)
        {
            if(disposed)return;suspended=value;state.Suspend(value);pressed=null;hover=null;
            main.Hover(null,null,activeTime);overlay.Hover(null,null,activeTime);
        }
        private void Dispatch(){foreach(var item in state.DrainCues())cue(item);}
        private void Present()
        {
            // A transition cue captures the old display list before any surface is changed.
            Dispatch();if(disposed)return;
            main.Render(state.SceneKey,state,activeTime);
            if(string.IsNullOrEmpty(state.OverlaySceneKey))overlay.Hide();
            else
            {
                overlay.Root.anchoredPosition=new Vector2(321.15f,-228.45f);
                overlay.Render(state.OverlaySceneKey,state,activeTime);
            }
            main.Hover(hover,pressed,activeTime);overlay.Hover(hover,pressed,activeTime);
        }
        internal AgeOfWarHotspot Hit(Vector2 originalStagePoint)
        {
            if(disposed||suspended||Done)return null;
            return overlay.Hit(originalStagePoint)??main.Hit(originalStagePoint);
        }
        private static bool Same(AgeOfWarHotspot a,AgeOfWarHotspot b){return a!=null&&b!=null&&a.id==b.id&&a.action==b.action;}
        internal bool Pointer(AgeOfWarHotspot hit,bool down,bool release)
        {
            if(disposed||suspended||Done)return false;
            hover=hit;if(hit==null)pressed=null;
            if(down)pressed=hit;
            bool activate=assets.Edition==1?(down||release):release&&Same(hit,pressed);
            if(release)pressed=null;
            main.Hover(hit,pressed,activeTime);overlay.Hover(hit,pressed,activeTime);
            if(!activate||hit==null)return false;
            if(assets.Edition==1&&hit.id==996)return down&&state.PressPlay();
            if(assets.Edition==2&&hit.action=="btnPlay")return state.PressPlay();
            // The offline edition has no external destinations.
            return false;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;state.FrameAdvanced-=Present;state.Dispose();Dispatch();
            root.gameObject.SetActive(false);UnityEngine.Object.Destroy(root.gameObject);
        }

        private sealed class Surface
        {
            private sealed class Node
            {
                internal AgeOfWarImage Image;
                internal AgeOfWarSceneLayer Layer;
                internal Mask Mask;
                internal RectTransform Plane;
                internal Surface Progress;
                internal AgeOfWarExposureImage Exposure;
                internal readonly AgeOfWarOpeningHover Tween=new AgeOfWarOpeningHover();
                internal int Frame;
                internal bool Visible;
            }
            internal readonly RectTransform Root;
            private readonly AgeOfWarAssets assets;
            private readonly Dictionary<int,Node> nodes=new Dictionary<int,Node>();
            private AgeOfWarScene scene;
            private AgeOfWarOpeningState state;
            internal Surface(Transform parent,AgeOfWarAssets assets,string name)
            {this.assets=assets;Root=AgeOfWarDrawing.Rect(parent,name,0,0,assets.Stage.width,assets.Stage.height);}
            internal void Hide(){Root.gameObject.SetActive(false);}
            internal void Render(string key,AgeOfWarOpeningState state,double now)
            {
                this.state=state;scene=assets.GetScene(key);if(scene==null)throw new InvalidOperationException("Missing original opening scene: "+key);
                Root.gameObject.SetActive(true);
                foreach(var old in nodes.Values){old.Visible=false;old.Image.Root.gameObject.SetActive(false);}
                Node activeMask=null;
                foreach(var layer in scene.layers)
                {
                    if(activeMask!=null&&layer.depth>activeMask.Layer.clipDepth)activeMask=null;
                    Node node;if(!nodes.TryGetValue(layer.depth,out node))
                    {node=new Node{Image=new AgeOfWarImage(Root,assets,"Source depth "+layer.depth)};nodes.Add(layer.depth,node);}
                    node.Layer=layer;node.Visible=true;
                    if(assets.Edition==2&&key=="preloader")
                    {
                        if(layer.name=="btnPlay")node.Visible=state.CanPressPlay;
                        if(layer.name=="loadingBar"||layer.name=="loadingFrame")node.Visible=state.IsLoading;
                    }
                    var parent=activeMask==null?Root:activeMask.Plane;
                    node.Image.Root.SetParent(parent,false);node.Image.Root.SetAsLastSibling();node.Image.Root.gameObject.SetActive(node.Visible);
                    node.Frame=state.ResolveFrame(layer.key,layer.clock,layer.frame,layer.birthFrame);
                    if(assets.Edition==2&&key=="sponsor"&&layer.key.EndsWith("/58",StringComparison.Ordinal))node.Frame=state.SourceFrame-1;
                    double scaleX=layer.scaleX;
                    if((key=="preloader-progress"&&layer.name=="lb")||(assets.Edition==2&&key=="preloader"&&layer.name=="loadingBar"))scaleX*=state.Progress;
                    node.Image.Set(layer.key,layer.action,node.Frame,layer.x,layer.y,scaleX,layer.scaleY,layer.rotation,layer.alpha);
                    if(layer.clipDepth>layer.depth)
                    {
                        if(node.Mask==null){node.Mask=node.Image.Image.gameObject.AddComponent<Mask>();node.Mask.showMaskGraphic=false;node.Plane=AgeOfWarDrawing.Rect(node.Image.Image.transform,"Masked source coordinates");}
                        node.Mask.enabled=true;
                        // Preserve the scene's coordinate plane inside the mask's cropped bitmap.
                        node.Plane.position=Root.position;node.Plane.rotation=Root.rotation;
                        var a=Root.lossyScale;var b=node.Image.Image.transform.lossyScale;
                        node.Plane.localScale=new Vector3(b.x==0?0:a.x/b.x,b.y==0?0:a.y/b.y,1);
                        node.Plane.gameObject.SetActive(node.Image.Image.enabled);activeMask=node;
                    }
                    else if(node.Mask!=null)node.Mask.enabled=false;
                    bool progress=assets.Edition==1&&key=="preloader"&&layer.key.EndsWith("/984",StringComparison.Ordinal);
                    if(progress)
                    {
                        node.Image.Image.enabled=false;
                        if(node.Progress==null)node.Progress=new Surface(node.Image.Root,assets,"Original loading bar");
                        node.Progress.Render("preloader-progress",state,now);
                    }
                    else if(node.Progress!=null)node.Progress.Hide();
                }
            }
            internal AgeOfWarHotspot Hit(Vector2 point)
            {
                if(!Root.gameObject.activeSelf||scene==null)return null;
                point-=new Vector2(Root.anchoredPosition.x,-Root.anchoredPosition.y);
                var hits=scene.hotspots??new AgeOfWarHotspot[0];
                for(int i=hits.Length-1;i>=0;--i)
                {
                    var hit=hits[i];bool visible=true;
                    foreach(var node in nodes.Values)if(Matches(node.Layer,hit)){visible=node.Visible;break;}
                    if(visible&&new Rect(hit.x,hit.y,hit.w,hit.h).Contains(point))return hit;
                }
                return null;
            }
            private static bool Matches(AgeOfWarSceneLayer layer,AgeOfWarHotspot hit)
            {return layer!=null&&hit!=null&&((hit.name.Length>0&&layer.name==hit.name)||layer.key.EndsWith("/"+hit.id,StringComparison.Ordinal));}
            internal void Hover(AgeOfWarHotspot hit,AgeOfWarHotspot pressed,double now)
            {
                if(scene==null||!Root.gameObject.activeSelf)return;
                foreach(var node in nodes.Values)
                {
                    if(!node.Visible)continue;var layer=node.Layer;bool button=false;
                    foreach(var candidate in scene.hotspots??new AgeOfWarHotspot[0])if(Matches(layer,candidate)){button=true;break;}
                    if(!button)continue;
                    bool selected=Matches(layer,hit);string action=selected?(Matches(layer,pressed)?"down":"over"):"out";
                    if(assets.GetClip(layer.key,action)==null)action=layer.action;
                    double scaleX=layer.scaleX,scaleY=layer.scaleY;
                    if(assets.Edition==2&&scene.key=="preloader")
                    {
                        node.Tween.Hover(selected,now);double amount=node.Tween.Sample(now);
                        // Preloader sets absolute scale1..1.02, unlike the later menu's exposure-only tween.
                        scaleX=scaleY=1+.02*amount;
                        if(node.Exposure==null)node.Exposure=node.Image.Image.gameObject.AddComponent<AgeOfWarExposureImage>();
                        node.Exposure.SetExposure((float)(.3*amount));
                    }
                    else if(node.Exposure!=null)node.Exposure.SetExposure(0);
                    node.Image.Set(layer.key,action,node.Frame,layer.x,layer.y,scaleX,scaleY,layer.rotation,layer.alpha);
                }
            }
        }
    }
}
