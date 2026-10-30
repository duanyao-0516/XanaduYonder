using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.One;
using XanaduYonder.Core.AgeOfWar.Two;
using XanaduYonder.Runtime.AgeOfWar;
using Object=UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    [InitializeOnLoad]
    public static class AgeOfWarUiChecks
    {
        private const string Pending="Xanadu.AgeOfWarUiChecks.Pending";
        private static int checks;
        private static readonly StringBuilder report=new StringBuilder();
        private static string output;
        private static readonly System.Collections.Generic.List<string> runtimeErrors=new System.Collections.Generic.List<string>();
        private static void Logged(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeErrors.Add(message);}
        static AgeOfWarUiChecks(){EditorApplication.playModeStateChanged+=StateChanged;}
        public static void Run()
        {
            SessionState.SetBool(Pending,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();
        }
        private static void StateChanged(PlayModeStateChange state)
        {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false)){SessionState.SetBool(Pending,false);new GameObject("Age of War verification").AddComponent<Runner>().StartCoroutine(AfterFrame());}}
        private sealed class Runner:MonoBehaviour{}
        private static IEnumerator AfterFrame(){yield return null;Execute();}
        private static void Require(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
        private static void Execute()
        {
            output=Path.GetFullPath(Path.Combine(Application.dataPath,"../artifacts/validation/age-of-war-ui"));Directory.CreateDirectory(output);int code=0;
            Application.logMessageReceived+=Logged;
            try
            {
                for(int edition=1;edition<=2;edition++)
                using(var assets=AgeOfWarAssets.Load(edition))
                using(var f=new Fixture(assets))
                {
                    foreach(var sound in assets.Manifest.sounds)
                    {
                        var audio=assets.LoadSound(sound.key);
                        Require(audio!=null&&audio.samples>0&&audio.frequency>0,"Every original sound imports: "+sound.key);
                        Require(audio.LoadAudioData(),"Every original sound decodes: "+sound.key);
                    }
                    f.Opening();
                    f.View.ShowScene("menu");var menuPixels=f.Capture("menu");
                    Require(f.View.Hit(new Vector2(edition==1?325:358,edition==1?217:174))!=null,"Original start hit region edition "+edition);
                    if(edition==2)
                    {
                        f.View.BeginTransition(false);f.View.ShowScene("difficulty");
                        Require(f.View.Transitioning&&f.View.Hit(new Vector2(358,174))==null,"Original transition blocks input");
                        var snapshotPixels=f.Capture("transition-start");Require(PixelError(menuPixels,snapshotPixels,assets.Stage.width,0,0,assets.Stage.width,assets.Stage.height)<1,"Transition copies the actual original menu pixels");f.View.Advance(.5f);f.Capture("transition-middle");
                        for(int step=0;step<250;step++)f.View.Advance(.01f);
                        Require(!f.View.Transitioning,"Original checker transition finishes");
                    }
                    else f.View.ShowScene("difficulty");
                    f.Capture("difficulty");
                    IAgeOfWarBattle battle=edition==1?(IAgeOfWarBattle)new AgeOfWarOneBattle(1,123):new AgeOfWarTwoBattle(.6,123);
                    f.View.CameraX=edition==1?0:800;f.View.CameraY=edition==1?0:390;f.View.StartBattle(battle.Capture(),battle);f.Capture("battle-start");
                    if(edition==2)
                    {
                        f.View.ShowTutorial();f.Capture("instructions");f.View.DismissTutorial();
                        f.View.Render(battle.Capture());f.View.Render(battle.Capture());
                        Require(Math.Abs(f.TutorialAlpha-1)<.00001,"Repeated render must not advance original tutorial tween");
                        f.View.Advance(.25f);Require(Math.Abs(f.TutorialAlpha-.25f)<.00001,"Original tutorial easeOut midpoint");f.Capture("instructions-midpoint");
                        f.View.Advance(.25f);
                    }
                    Require(battle.Execute(new AgeOfWarCommand(AgeOfWarAction.Train,1)),"Original first unit trains");
                    for(int i=0;i<battle.TickRate*12;i++)
                    {
                        battle.Tick();if(i%5==0)f.View.Render(battle.Capture());
                        foreach(var item in battle.DrainEvents())if(item.Sound.Length>0)Require(assets.LoadSound(item.Sound)!=null,"Source sound resolves "+item.Sound);
                    }
                    f.View.Render(battle.Capture());f.Capture("battle-12s");
                    Require(battle.Execute(new AgeOfWarCommand(AgeOfWarAction.Pause)),"Pause executes");f.View.Render(battle.Capture());f.Capture("paused");
                    battle.Execute(new AgeOfWarCommand(AgeOfWarAction.Pause));
                    Set(battle,edition==1?"_gold":"Gold",100000);
                    if(edition==2)Require(battle.Execute(new AgeOfWarCommand(AgeOfWarAction.BuyTurretSlot)),"Original first two turret platform purchase");
                    Require(battle.Execute(new AgeOfWarCommand(AgeOfWarAction.BuyTurret,1)),"Original turret selection");
                    f.View.Render(battle.Capture());f.Capture("build-turret");
                    var slots=assets.GetScene("build-slots");AgeOfWarHotspot buildHit=null;
                    foreach(var hit in slots.hotspots)if(hit.name==(edition==1?"b1":"btnBuildTurret1"))buildHit=hit;
                    Require(buildHit!=null,"Original build hotspot exists");
                    var point=new Vector2(buildHit.x+buildHit.w/2+f.View.CameraX,buildHit.y+buildHit.h/2+(edition==1?425:0)+f.View.CameraY);
                    Require(f.View.Hit(point)?.action=="build-slot","Original build geometry hit");
                    f.View.Pointer(point,true);f.Capture("build-turret-down");
                    Require(battle.Execute(new AgeOfWarCommand(AgeOfWarAction.PlaceTurret,0,1)),"Original turret placement");
                    f.View.Menu("sell");f.View.Render(battle.Capture());f.Capture("sell-turret");
                    Require(battle.Execute(new AgeOfWarCommand(AgeOfWarAction.SellTurret,0,1)),"Original turret sells");f.View.Menu("main");
                    if(edition==2)
                    {
                        var frame=battle.Capture();var visuals=new System.Collections.Generic.List<AgeOfWarVisual>(frame.Visuals);
                        visuals.Add(new AgeOfWarVisual{InstanceId=9000001,Text="37",TextScene="damage-normal",TextField="damageTXT",X=-450,Y=-160,Depth=400});
                        visuals.Add(new AgeOfWarVisual{InstanceId=9000002,Text="74",TextScene="damage-critical",TextField="damageTXT",X=-350,Y=-160,Depth=400});
                        frame.Visuals=visuals.ToArray();f.View.Render(frame);var damagePixels=f.Capture("damage-filters");f.DumpTextEffects();Require(CountRed(damagePixels,720,325,230,180,40)>20,"Actual UI damage numbers contain original red glyphs, not opaque filter rectangles");
                        f.View.BeginTransition(true);var damageSnapshot=f.Capture("damage-filter-snapshot");Require(PixelError(damagePixels,damageSnapshot,720,325,210,180,45)<1,"Transition preserves filtered dynamic text");f.View.Advance(.3f);f.View.Advance(.01f);
                    }
                    int maximum=edition==1?5:7;
                    for(int age=1;age<=maximum;age++)
                    {
                        Set(battle,edition==1?"_age":"Age",age);Set(battle,edition==1?"_enemyAge":"EnemyAge",age);
                        f.View.Render(battle.Capture());f.Capture("catalog-age-"+age);
                    }
                    f.View.ShowScene("victory");f.Capture("victory");f.View.ShowScene("defeat");f.Capture("defeat");
                    // Every exported animation must be loadable with the actual Android texture importer.
                    foreach(var clip in assets.Manifest.clips)
                    {
                        AgeOfWarSpriteFrame first,last;
                        Require(assets.TryGetFrame(clip.key,clip.action,0,out first),"Animation begins: "+clip.key+"/"+clip.action);
                        Require(assets.TryGetFrame(clip.key,clip.action,clip.DurationTicks-1,out last),"Animation ends: "+clip.key+"/"+clip.action);
                        Require(first.Sprite!=null&&last.Sprite!=null,"Original atlas sprites load");
                    }
                    report.AppendLine("PASS edition "+edition+": original scenes, live 12-second battle, pause, all-age catalog, all exported animations and emitted audio.");
                }
                checks+=AgeOfWarSliderChecks.Run();
                checks+=AgeOfWarTooltipChecks.Run();
                checks+=AgeOfWarTextLayoutChecks.Run();
                Require(runtimeErrors.Count==0,"Unity logged runtime errors: "+string.Join(" | ",runtimeErrors));
                report.AppendLine("AGE_OF_WAR_UI_CHECKS_PASS: "+checks+" assertions");
            }
            catch(Exception error){code=1;report.AppendLine("AGE_OF_WAR_UI_CHECKS_FAIL after "+checks+" assertions\n"+error);Debug.LogException(error);}
            Application.logMessageReceived-=Logged;
            File.WriteAllText(Path.Combine(output,"report.txt"),report.ToString());Debug.Log(report.ToString());EditorApplication.Exit(code);
        }
        private static int CountRed(Color32[] pixels,int width,int x,int y,int w,int h)
        {int count=0;for(int row=y;row<y+h;row++)for(int col=x;col<x+w;col++){var p=pixels[row*width+col];if(p.r>180&&p.g<60&&p.b<60)count++;}return count;}
        private static double PixelError(Color32[] a,Color32[] b,int width,int x,int y,int w,int h)
        {
            long error=0;for(int row=y;row<y+h;row++)for(int col=x;col<x+w;col++){int p=row*width+col;error+=Math.Abs(a[p].r-b[p].r)+Math.Abs(a[p].g-b[p].g)+Math.Abs(a[p].b-b[p].b);}
            return error/(double)(w*h*3);
        }
        private static void Set(object target,string name,object value){target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
        private sealed class Fixture:IDisposable
        {
            internal readonly AgeOfWarView View;
            private readonly AgeOfWarAssets assets;
            private readonly GameObject owner;
            private readonly Camera camera;
            private readonly RectTransform stage;
            private readonly RenderTexture texture;
            internal Fixture(AgeOfWarAssets source)
            {
                assets=source;int w=assets.Stage.width,h=assets.Stage.height;
                owner=new GameObject("Age of War real UGUI fixture");
                var root=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas)).GetComponent<RectTransform>();root.SetParent(owner.transform,false);root.sizeDelta=new Vector2(w,h);
                camera=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(owner.transform,false);camera.transform.position=new Vector3(0,0,-10);
                camera.enabled=false;camera.orthographic=true;camera.orthographicSize=h*.5f;camera.aspect=(float)w/h;camera.nearClipPlane=.1f;camera.farClipPlane=100;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                texture=new RenderTexture(w,h,24);texture.Create();camera.targetTexture=texture;
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
                stage=AgeOfWarDrawing.Rect(root,"Stage",0,0,w,h);stage.gameObject.AddComponent<RectMask2D>();View=new AgeOfWarView(stage,assets,false);
            }
            internal void Opening()
            {
                using(var movie=new AgeOfWarOpening(stage,assets,maximum=>maximum/2,cue=>{if(cue.Kind=="transition")View.BeginTransition(cue.Key=="fade");}))
                {
                    Capture("opening-loading");movie.ResourceProgress(0,0,true);
                    if(assets.Edition==1)movie.Advance(1.0/40);
                    Require(movie.CanPressPlay,"Bundled original opening becomes ready after resource verification");
                    Capture("opening-ready");
                    var point=assets.Edition==1?new Vector2(325,241):new Vector2(359,236);
                    var hit=movie.Hit(point);Require(hit!=null,"Original opening Play hit is visible");
                    movie.Pointer(hit,true,false);movie.Pointer(hit,false,true);
                    double step=1.0/(assets.Edition==1?40:30);
                    var openingState=(AgeOfWarOpeningState)typeof(AgeOfWarOpening).GetField("state",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(movie);
                    for(int tick=0;tick<400&&!movie.Done;tick++)
                    {
                        View.Advance((float)step);if(!View.Transitioning)movie.TransitionComplete();movie.Advance(step);
                        var key=openingState.SceneKey;
                        if((key=="intro-background"&&(movie.SourceFrame==1||movie.SourceFrame==22||movie.SourceFrame==80||movie.SourceFrame==160))||
                           (key=="sponsor"&&(movie.SourceFrame==1||movie.SourceFrame==130))||key=="author/1"||key=="author/66")
                            Capture("opening-"+key.Replace('/','-')+"-"+movie.SourceFrame);
                    }
                    Require(movie.Done,"Both original opening timelines reach the menu without missing resources");
                }
                View.ShowScene("menu");View.Advance(1);View.Advance(.01f);
            }
            internal void DumpTextEffects()
            {
                int index=0;foreach(var effect in owner.GetComponentsInChildren<AgeOfWarTextEffects>())
                {
                    foreach(string field in new[]{"source","result"})
                    {
                        var target=(RenderTexture)typeof(AgeOfWarTextEffects).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(effect);
                        if(target==null)continue;var previous=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                        try{pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(output,"text-"+index+"-"+field+".png"),pixels.EncodeToPNG());}
                        finally{Object.DestroyImmediate(pixels);RenderTexture.active=previous;}
                    }
                    index++;
                }
            }
            internal float TutorialAlpha {get{foreach(var group in owner.GetComponentsInChildren<CanvasGroup>())if(group.name=="instructions")return group.alpha;return -1;}}
            internal Color32[] Capture(string name)
            {
                Canvas.ForceUpdateCanvases();camera.Render();Canvas.ForceUpdateCanvases();camera.Render();
                var previous=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
                try{image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,"aow"+assets.Edition+"-"+name+".png"),image.EncodeToPNG());return image.GetPixels32();}
                finally{Object.DestroyImmediate(image);RenderTexture.active=previous;}
            }
            public void Dispose(){camera.targetTexture=null;Object.DestroyImmediate(owner);texture.Release();Object.DestroyImmediate(texture);}
        }
    }
}
