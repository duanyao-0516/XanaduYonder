using System;
using System.Reflection;
using UnityEngine;
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.Two;
using XanaduYonder.Runtime.AgeOfWar;
using Object=UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Replays pointer events through the same Page path used by Unity Input, with real original UI assets.
    internal static class AgeOfWarSliderChecks
    {
        private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static int Run()
        {
            int count=0;var owner=new GameObject("Original pause slider pointer checks");int oldFps=Application.targetFrameRate;
            try
            {
                var page=owner.AddComponent<AgeOfWarPage>();page.enabled=false;page.Initialize(()=>{},()=>true,_=>{});
                var assets=AgeOfWarAssets.Load(2);Set(page,"assets",assets);Set(page,"edition",2);Set(page,"visible",true);Set(page,"oldTargetFps",oldFps);
                Call(page,"BuildCanvas");var audio=owner.AddComponent<AgeOfWarAudio>();audio.Initialize(assets,()=>true);Set(page,"audioPlayer",audio);
                page.StartBattle(1,123);var view=Get<AgeOfWarView>(page,"view");view.Advance(.6f);view.Advance(.01f);
                Require(!view.Transitioning,"Original transition releases input on its following manager update",ref count);
                Pointer(page,20,100,true,true,false);Pointer(page,20,100,false,false,true);
                page.Command(AgeOfWarAction.Pause);
                view.PauseValues(audio.MusicVolume,audio.SoundVolume,150,"HIGH",false);
                var battle=(AgeOfWarTwoBattle)Get<IAgeOfWarBattle>(page,"battle");
                Require(battle.Capture().Paused,"Fixture reaches the actual paused battle",ref count);
                var stage=Get<RectTransform>(page,"stage");
                var background=stage.Find("World/Background/Original pixels").GetComponent<UnityEngine.UI.Image>();
                var highTexture=background.sprite.texture;
                var originalTransform=background.rectTransform.parent.localPosition;

                Pointer(page,350,176.45f,true,true,false);
                Require(Get<string>(page,"slider")=="","Track click cannot grab the original 12.5px knob",ref count);
                Pointer(page,350,176.45f,false,false,true);
                Pointer(page,446,176.45f,true,true,false);Pointer(page,350,176.45f,false,true,false);
                RefreshValues(page,view,audio);
                Require(Get<string>(page,"quality")=="LOW","Quality pending value follows the source .33 threshold",ref count);
                Require(Math.Abs(SliderX(view,"sliderQual")-350)<.001,"Quality knob follows pointer continuously before release",ref count);
                Pointer(page,350,176.45f,false,false,true);RefreshValues(page,view,audio);
                Require(Math.Abs(SliderX(view,"sliderQual")-350)<.001,"Quality knob retains release position until the next pause opening",ref count);
                Require(assets.Quality=="Low"&&assets.Revision==1,"Mouse up switches to the actual LOW resource generation",ref count);
                Require(background.sprite.texture!=null&&background.sprite.texture!=highTexture,"Visible original sprites rebind before old resources are released",ref count);
                foreach(var binding in stage.GetComponentsInChildren<AgeOfWarImageBinding>(true))
                    if(binding.Owner!=null&&binding.Owner.Image.sprite!=null)
                        Require(binding.Owner.Image.sprite.texture!=highTexture,"All live and hidden Images release the previous background atlas reference",ref count);
                Require(background.rectTransform.parent.localPosition==originalTransform,"Quality preserves live stage transforms",ref count);

                float fpsX=306+(150-25)/275f*140;
                Pointer(page,fpsX,231.5f,true,true,false);Pointer(page,306,231.5f,false,true,false);
                Require(Get<int>(page,"requestedFps")==25,"FPS text updates to pending 25",ref count);
                Require(battle.TickRate==150&&Application.targetFrameRate==150,"Dragging FPS preserves applied native clock until mouse up",ref count);
                Pointer(page,306,231.5f,false,false,true);
                Require(battle.TickRate==25&&Application.targetFrameRate==25,"Mouse up applies requested FPS to both core and host",ref count);

                Pointer(page,446,340.5f,true,true,false);Pointer(page,376,340.5f,false,true,false);
                Require(audio.MusicVolume==1,"Music volume remains applied value while dragging",ref count);
                Pointer(page,376,340.5f,false,false,true);
                Require(Math.Abs(audio.MusicVolume-.5f)<.001,"Mouse up applies the displayed music volume",ref count);
                Pointer(page,446,286,true,true,false);Pointer(page,306,286,false,true,false);
                Require(audio.SoundVolume==1,"Sound volume remains applied value while dragging",ref count);
                Pointer(page,306,286,false,false,true);
                Require(audio.SoundVolume==0,"Mouse up applies the displayed sound volume",ref count);

                page.Command(AgeOfWarAction.Pause);page.Command(AgeOfWarAction.Pause);RefreshValues(page,view,audio);
                Require(Math.Abs(SliderX(view,"sliderQual")-306)<.001,"Opening pause snaps quality knob to the applied LOW detent",ref count);
                return count;
            }
            finally {Object.DestroyImmediate(owner);Application.targetFrameRate=oldFps;}
        }
        private static void Pointer(AgeOfWarPage page,float x,float y,bool down,bool held,bool up)
        {Call(page,"ProcessPointer",new Vector2(x,y),true,down,held,up,0,Get<IAgeOfWarBattle>(page,"battle").Capture());}
        private static void RefreshValues(AgeOfWarPage page,AgeOfWarView view,AgeOfWarAudio audio)
        {
            var method=typeof(AgeOfWarPage).GetMethod("RenderPauseValues",Fields);
            if(method!=null)method.Invoke(page,null);
            else view.PauseValues(audio.MusicVolume,audio.SoundVolume,Get<int>(page,"requestedFps"),Get<string>(page,"quality"),false);
        }
        private static float SliderX(AgeOfWarView view,string name)
        {
            var pause=Get<AgeOfWarSceneView>(view,"pause");
            for(int i=0;i<pause.Images.Count;i++)if(pause.Definition.layers[i].name==name)return pause.Images[i].Root.anchoredPosition.x;
            throw new InvalidOperationException("Missing original slider "+name);
        }
        private static void Require(bool value,string message,ref int count){count++;if(!value)throw new InvalidOperationException(message);}
        private static T Get<T>(object target,string name){return (T)target.GetType().GetField(name,Fields).GetValue(target);}
        private static void Set(object target,string name,object value){target.GetType().GetField(name,Fields).SetValue(target,value);}
        private static object Call(object target,string name,params object[] args)
        {try{return target.GetType().GetMethod(name,Fields).Invoke(target,args);}catch(TargetInvocationException e){throw e.InnerException??e;}}
    }
}
