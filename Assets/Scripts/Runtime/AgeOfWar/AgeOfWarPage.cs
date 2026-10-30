using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.One;
using XanaduYonder.Core.AgeOfWar.Two;

namespace XanaduYonder.Runtime.AgeOfWar
{
    internal sealed class AgeOfWarPage : MonoBehaviour
    {
        private Action returnLobby;
        private Action<ScreenOrientation> requestOrientation=value=>Screen.orientation=value;
        private Func<bool> soundEnabled;
        private Action<bool> setSound;
        private AgeOfWarAssets assets;
        private AgeOfWarView view;
        private RectTransform stage;
        private AgeOfWarAudio audioPlayer;
        private AgeOfWarViewport viewport;
        private AgeOfWarClock clock;
        private readonly AgeOfWarStageClock stageClock=new AgeOfWarStageClock();
        private IAgeOfWarBattle battle;
        private GameObject canvasObject;
        private bool visible,transition,leaving,foreground=true,pressed;
        private bool originalCursor=true;
        private int edition,navigationToken,requestedFps=150,oldTargetFps;
        private bool showDamage;
        private string slider="";
        private string quality="HIGH",appliedQuality="HIGH",hoverKey="";
        private float pendingMusicVolume=1,pendingSoundVolume=1,qualitySliderX=446,fpsSliderX=306+125/275f*140;
        private Vector2 downPoint,lastPoint;
        private AgeOfWarHotspot downHit;
        private double cameraX;
        private float cameraY;
        internal bool IsVisible{get{return visible;}}
        internal string Error{get;private set;}
        internal event Action PresentationReady;
        internal void Initialize(Action lobby,Func<bool> sound,Action<bool> updateSound)
        {returnLobby=lobby;soundEnabled=sound;setSound=updateSound;}
        internal void Enter(int value)
        {
            if(visible||transition)return;edition=value;visible=true;transition=true;leaving=false;Error=null;
            stageClock.Reset();originalCursor=Cursor.visible;oldTargetFps=Application.targetFrameRate;StartCoroutine(EnterRoutine(++navigationToken));
        }
        private IEnumerator Direction(bool landscape,int token,Action<bool> result)
        {
            var elapsed=0d;var last=Time.realtimeSinceStartupAsDouble;int stable=0;Rect safe=Rect.zero;Vector2 size=Vector2.zero;
            while(token==navigationToken&&elapsed<3)
            {
                var now=Time.realtimeSinceStartupAsDouble;
                if(foreground)
                {
                    elapsed+=Math.Max(0,now-last);var current=new Vector2(Screen.width,Screen.height);var area=Screen.safeArea;
                    if((landscape?current.x>current.y:current.y>current.x)&&area.width>0&&area.height>0&&current==size&&area==safe)stable++;else stable=0;
                    size=current;safe=area;if(stable>=2){result(true);yield break;}
                }
                last=now;yield return null;
            }
            result(false);
        }
        private IEnumerator EnterRoutine(int token)
        {
            requestOrientation(ScreenOrientation.LandscapeLeft);bool ready=false;yield return Direction(true,token,r=>ready=r);
            if(token!=navigationToken)yield break;
            if(!ready){Error="横屏切换未完成，请重试。";Exit();yield break;}
            try
            {
                assets=AgeOfWarAssets.Load(edition);BuildCanvas();audioPlayer=gameObject.AddComponent<AgeOfWarAudio>();audioPlayer.Initialize(assets,soundEnabled);
                view.ShowScene("menu");if(edition==2)audioPlayer.SelectMusic("music/2");
                if(Application.isMobilePlatform)Cursor.visible=false;
                transition=false;canvasObject.SetActive(true);PresentationReady?.Invoke();
            }
            catch(Exception error){Fail(error);}
        }
        private void BuildCanvas()
        {
            canvasObject=new GameObject("Age of War "+edition+" landscape UI",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            canvasObject.SetActive(false);canvasObject.transform.SetParent(transform,false);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=10;
            var black=AgeOfWarDrawing.Rect(canvasObject.transform,"Letterbox");black.anchorMin=Vector2.zero;black.anchorMax=Vector2.one;black.sizeDelta=Vector2.zero;
            var image=black.gameObject.AddComponent<Image>();image.color=Color.black;image.raycastTarget=false;
            stage=AgeOfWarDrawing.Rect(canvasObject.transform,"Original stage",0,0,assets.Stage.width,assets.Stage.height);stage.gameObject.AddComponent<RectMask2D>();
            viewport=canvasObject.AddComponent<AgeOfWarViewport>();viewport.Initialize(stage,assets.Stage.width,assets.Stage.height);viewport.Changed+=ViewportChanged;
            view=new AgeOfWarView(stage,assets,false);view.Activated=Activate;view.Hovered=Hover;ViewportChanged();
        }
        private void ViewportChanged()
        {
            CancelPointer();if(view==null||viewport==null)return;
            view.SetViewportWidth((float)viewport.Bounds.StageWidth);
            cameraX=AgeOfWarCameraMath.Clamp(edition,cameraX,view.LogicalWidth);view.CameraX=(float)cameraX;
        }
        internal void StartBattle(double difficulty,uint seed)
        {
            view.BeginTransition(true);CancelPointer();battle=edition==1?(IAgeOfWarBattle)new AgeOfWarOneBattle((int)difficulty,seed):new AgeOfWarTwoBattle(difficulty,seed);
            clock=new AgeOfWarClock(battle.TickRate);cameraX=AgeOfWarCameraMath.Clamp(edition,edition==1?0:800,view.LogicalWidth);cameraY=edition==1?0:390;
            if(edition==2){requestedFps=150;Application.targetFrameRate=150;quality=appliedQuality="HIGH";assets.SetQuality(quality,()=>AgeOfWarImageBinding.RebindAll(stage));audioPlayer.MusicVolume=audioPlayer.SoundVolume=1;ResetPauseValues();((AgeOfWarTwoBattle)battle).SetShowDamage(showDamage);}
            view.CameraX=(float)cameraX;view.CameraY=cameraY;view.StartBattle(battle.Capture(),battle);audioPlayer.SelectMusic("music/1");
        }
        private void StartBattle(double difficulty){StartBattle(difficulty,unchecked((uint)DateTime.UtcNow.Ticks^(uint)Environment.TickCount));}
        private void Update()
        {
            if(!visible||transition||!foreground||view==null)return;
            try
            {
                double elapsed=stageClock.Sample(Time.realtimeSinceStartupAsDouble);
                view.Advance(edition==2?(float)elapsed:Time.unscaledDeltaTime);
                if(battle!=null)
                {
                    // Android's simulated mouse position survives the last touch. It must not
                    // become a hovering desktop pointer and scroll the camera after release.
                    var before=battle.Capture();Vector2 cameraPointer;bool cameraInside=StagePoint(out cameraPointer)&&!Application.isMobilePlatform&&Input.touchCount==0;
                    if(edition==2)
                    {
                        ((AgeOfWarTwoBattle)battle).Step(elapsed,dt=>
                        {
                            DrainBattleEvents();
                            if(cameraInside)cameraX=AgeOfWarCameraMath.StepTwo(cameraX,cameraPointer.x,cameraPointer.y,dt,false,view.LogicalWidth);
                        });
                        DrainBattleEvents();
                    }
                    else clock.Advance(Time.unscaledDeltaTime,true,()=>
                    {
                        battle.Tick();DrainBattleEvents();
                        if(cameraInside)cameraX=AgeOfWarCameraMath.Step(1,cameraX,cameraPointer.x,cameraPointer.y,battle.TickRate,before.Speed,before.Paused,view.LogicalWidth);
                    });
                    cameraX=AgeOfWarCameraMath.Clamp(edition,cameraX,view.LogicalWidth);var frame=battle.Capture();view.CameraX=(float)cameraX;view.CameraY=cameraY;
                    if(frame.Ended){view.Render(frame);view.BeginTransition(false);Cursor.visible=originalCursor;battle=null;clock=null;CancelPointer();view.ShowScene(frame.Victory?"victory":"defeat");if(edition==1)audioPlayer.SelectMusic(null);}
                    else
                    {
                        ReadPointer(frame);if(battle==null)return;frame=battle.Capture();view.CameraX=(float)cameraX;view.CameraY=cameraY;view.Render(frame);
                        if(frame.Paused)RenderPauseValues();
                        if(Input.GetKeyDown(KeyCode.Space)&&edition==1)Command(AgeOfWarAction.Pause);
                    }
                }
                else ReadPointer(null);
                audioPlayer.Refresh();
            }
            catch(Exception error){Fail(error);}
        }
        private void DrainBattleEvents()
        {
            foreach(var item in battle.DrainEvents())
            {
                audioPlayer.Event(item,(float)cameraX);view.Event(item);
                if(item.Kind=="camera-shake"){cameraX=(int)((cameraX+item.X)*20)/20.0;cameraY=(float)item.Y;}
            }
        }
        private bool StagePoint(out Vector2 point)
        {
            double x,y;bool inside=viewport.Bounds.ToStage(Input.mousePosition.x,Input.mousePosition.y,out x,out y);point=new Vector2((float)x,(float)y);return inside;
        }
        private void ReadPointer(AgeOfWarFrame frame)
        {
            Vector2 point;bool inside=StagePoint(out point);
            ProcessPointer(point,inside,Input.GetMouseButtonDown(0),Input.GetMouseButton(0),Input.GetMouseButtonUp(0),Input.touchCount,frame);
        }
        // Both physical input and the host regression checks use this same event path.
        private void ProcessPointer(Vector2 point,bool inside,bool down,bool held,bool up,int touches,AgeOfWarFrame frame)
        {
            if(Application.isMobilePlatform)Cursor.visible=false;else if(edition==1)Cursor.visible=battle==null||!inside||!foreground?originalCursor:false;
            if(!inside)view.HidePointer();
            if(down&&inside)
            {
                pressed=true;downPoint=lastPoint=point;downHit=view.Hit(point);
                if(edition==2&&frame!=null&&frame.Paused&&!view.HasTutorial)slider=SliderAt(point);
                // AVM1 on(press) buttons act on DOWN. Native Android touches use the same stage mapping.
                if(edition==1&&downHit!=null){view.Click(point);pressed=false;}
            }
            if(inside)
            {
                if(frame!=null&&(!frame.Paused||edition==1))
                {
                    if(touches>0&&pressed&&downHit==null){cameraX+=point.x-lastPoint.x;cameraX=AgeOfWarCameraMath.Clamp(edition,cameraX,view.LogicalWidth);}

                }
                lastPoint=point;if(slider.Length>0&&pressed)Slide(point.x-view.PauseOffsetX);view.Pointer(point,held);
            }
            if(up)
            {
                // Engine.mouseUp -> Hud.resetSliders runs before the subsequent CLICK handler.
                if(edition==2&&frame!=null&&frame.Paused&&!view.HasTutorial)ApplySliders();
                if(pressed&&inside&&(point-downPoint).sqrMagnitude<144){var hit=view.Hit(point);if(SameHit(hit,downHit))view.Click(point);}
                CancelPointer();
            }
        }
        private string SliderAt(Vector2 point) { return view.PauseSliderAt(point); }
        private void Slide(float x)
        {
            x=Mathf.Clamp(x,306,446);double fraction=(x-306)/140.0;
            if(slider=="sound")pendingSoundVolume=(float)fraction;
            if(slider=="music")pendingMusicVolume=(float)fraction;
            if(slider=="quality"){qualitySliderX=x;quality=fraction<.33?"LOW":fraction<.66?"MEDIUM":"HIGH";}
            if(slider=="fps"){fpsSliderX=x;requestedFps=(int)Math.Floor(fraction*275+.5)+25;}
            RenderPauseValues();
        }
        private void ApplySliders()
        {
            audioPlayer.SoundVolume=pendingSoundVolume;audioPlayer.MusicVolume=pendingMusicVolume;audioPlayer.Refresh();
            assets.SetQuality(quality,()=>AgeOfWarImageBinding.RebindAll(stage));appliedQuality=quality;
            if(battle.TickRate!=requestedFps){((AgeOfWarTwoBattle)battle).SetFrameRate(requestedFps);clock=new AgeOfWarClock(requestedFps);}
            Application.targetFrameRate=requestedFps;
        }
        private void ResetPauseValues()
        {
            pendingSoundVolume=audioPlayer.SoundVolume;pendingMusicVolume=audioPlayer.MusicVolume;
            requestedFps=battle.TickRate;quality=appliedQuality;
            qualitySliderX=quality=="LOW"?306:quality=="MEDIUM"?376:446;fpsSliderX=306+(requestedFps-25)/275f*140;
        }
        private void RenderPauseValues()
        {if(edition==2)view.PauseValues(pendingMusicVolume,pendingSoundVolume,requestedFps,quality,showDamage,qualitySliderX,fpsSliderX);}
        private static bool SameHit(AgeOfWarHotspot a,AgeOfWarHotspot b){return a!=null&&b!=null&&a.action==b.action&&a.id==b.id;}
        private void CancelPointer(){pressed=false;downHit=null;hoverKey="";slider="";}
        internal bool Command(AgeOfWarAction action,int id=0,int slot=0)
        {
            if(battle==null)return false;
            if(edition==2&&action==AgeOfWarAction.Pause&&!battle.Capture().Paused)ResetPauseValues();
            bool result=battle.Execute(new AgeOfWarCommand(action,id,slot));
            var state=battle.Capture();view.Render(state);if(state.Paused)RenderPauseValues();return result;
        }
        private void Activate(AgeOfWarHotspot hit)
        {
            if(!foreground||transition)return;
            if(hit.action=="mobile-exit"){Exit();return;}
            if(hit.action=="mobile-pause"){if(battle!=null&&!battle.Capture().Paused){CancelPointer();Command(AgeOfWarAction.Pause);}return;}
            if(hit.action=="mobile-resume"){if(battle!=null&&battle.Capture().Paused)Command(AgeOfWarAction.Pause);return;}
            if(battle==null){Navigate(hit);return;}
            var action=hit.action;int id;
            if(action=="dismiss-tutorial"){view.DismissTutorial();Command(AgeOfWarAction.Pause);return;}
            if(action=="btnReturn"||action=="btnPause"){Command(AgeOfWarAction.Pause);return;}
            if(action=="btnReturnToMenu"){Menu();return;}
            if(action=="btnGameSpeed"){Command(AgeOfWarAction.Speed);return;}
            if(action=="btnShowDamage"){showDamage=!showDamage;((AgeOfWarTwoBattle)battle).SetShowDamage(showDamage);return;}

            if(action=="build-slot"){Command(AgeOfWarAction.PlaceTurret,0,hit.id);view.Menu("main");return;}
            if(action=="sell-slot"){bool sold=Command(AgeOfWarAction.SellTurret,0,hit.id);if(sold)view.Menu("main");return;}
            if(action=="btnCancel"||action=="btc"||action=="stc"){Command(AgeOfWarAction.Cancel);view.Menu("main");if(action=="btc")view.Title("Menu - Turrets");return;}
            if(edition==1)
            {
                if(hit.id==179){view.Menu("units");return;}if(hit.id==181){view.Menu("turrets");return;}
                if(hit.id==83||hit.id==143){view.Menu("main");return;}
                if(hit.id==184){view.Menu("sell");return;}if(hit.id==186){Command(AgeOfWarAction.BuyTurretSlot);return;}
                if(hit.id==188){Command(AgeOfWarAction.Evolve);return;}
                if(action=="special"){Command(AgeOfWarAction.Special);return;}
                var state=battle.Capture();
                if(action.StartsWith("bf")&&int.TryParse(action.Substring(2),out id)){int index=hit.id==141?3:id-1;if(index<state.Units.Length)Command(AgeOfWarAction.Train,state.Units[index].Id);return;}
                if(action.StartsWith("bt")&&int.TryParse(action.Substring(2),out id)){int index=id-1;if(index<state.Turrets.Length)Command(AgeOfWarAction.BuyTurret,state.Turrets[index].Id);return;}
            }
            else
            {
                if(action.StartsWith("btnUP")&&int.TryParse(action.Substring(5),out id)){Command(AgeOfWarAction.Upgrade,id);return;}
                if(action.StartsWith("btnU")&&int.TryParse(action.Substring(4),out id)){Command(AgeOfWarAction.Train,id);return;}
                if(action.StartsWith("btnT")&&int.TryParse(action.Substring(4),out id)){Command(AgeOfWarAction.BuyTurret,id);return;}
                if(action.StartsWith("btnS")&&int.TryParse(action.Substring(4),out id)){Command(AgeOfWarAction.Special,id);return;}
                if(action=="btnEvolve"){Command(AgeOfWarAction.Evolve);return;}if(action=="btnAddTower"){Command(AgeOfWarAction.BuyTurretSlot);return;}
                if(action=="btnSellTurret"){foreach(var slot in battle.Capture().TurretSlots)if(!slot.Enemy&&slot.TurretId!=0){view.Menu("sell");break;}return;}
            }
            Navigate(hit);
        }
        private void Navigate(AgeOfWarHotspot hit)
        {
            string action=hit.action;
            if(action=="mobile-play"){SwitchScene("difficulty");return;}
            if(action=="mobile-instructions"){view.ShowScene("instructions");return;}
            if(edition==1)
            {
                switch(hit.id)
                {
                    case 1019:SwitchScene("difficulty");return;case 1020:view.ShowScene("instructions");return;
                    case 1026:Menu();return;
                    case 1032:StartBattle(1);return;case 1033:StartBattle(2);return;case 1034:StartBattle(3);return;
                }
            }
            if(action=="btnStart"){SwitchScene("difficulty");return;}
            if(action=="btnCredits"){SwitchScene("credits");return;}
            if(action=="btnReturn"){Menu();return;}
            if(action=="btnEasy"){StartBattle(.6);return;}if(action=="btnMedium"){StartBattle(1);return;}if(action=="btnHard"){StartBattle(1.5);return;}
        }
        private void Hover(AgeOfWarHotspot hit)
        {
            string key=hit==null?"":hit.action+"/"+hit.id;hoverKey=key;
            if(battle==null)return;
            if(hit==null){view.ClearHover();return;}
            AgeOfWarAction action;int id=0;string kind;
            if(!TooltipAction(hit,out action,out id,out kind)){view.ClearHover();return;}
            if(edition==2){view.HoverAction(action,id,lastPoint);return;}
            var values=edition==1?((AgeOfWarOneBattle)battle).CaptureTooltip(action,id):((AgeOfWarTwoBattle)battle).CaptureTooltip(action,id);
            float x=kind=="upgradeInfo"||kind=="specialInfo"||kind=="evolveInfo"?Mathf.Min(720,lastPoint.x+30):Mathf.Min(460,lastPoint.x);
            view.Tooltip(kind,values,x,75);
        }
        private bool TooltipAction(AgeOfWarHotspot hit,out AgeOfWarAction action,out int id,out string kind)
        {
            string name=hit.action;action=AgeOfWarAction.Train;id=0;kind="unitInfo";
            if(edition==1)
            {
                var state=battle.Capture();int index;
                if(name.StartsWith("bf")&&int.TryParse(name.Substring(2),out index)&&index<=state.Units.Length){id=hit.id==141?16:state.Units[index-1].Id;return true;}
                if(name.StartsWith("bt")&&int.TryParse(name.Substring(2),out index)&&index<=state.Turrets.Length){action=AgeOfWarAction.BuyTurret;id=state.Turrets[index-1].Id;return true;}
                if(hit.id==179){action=AgeOfWarAction.Train;return true;}if(hit.id==181){action=AgeOfWarAction.BuyTurret;return true;}if(hit.id==184){action=AgeOfWarAction.SellTurret;return true;}if(hit.id==83||hit.id==143){action=AgeOfWarAction.Cancel;return true;}if(name=="sell-slot"){action=AgeOfWarAction.SellTurret;id=hit.id;return true;}
                if(hit.id==186){action=AgeOfWarAction.BuyTurretSlot;return true;}if(hit.id==188){action=AgeOfWarAction.Evolve;return true;}return false;
            }
            if(name.StartsWith("btnUP")&&int.TryParse(name.Substring(5),out id)){action=AgeOfWarAction.Upgrade;kind="upgradeInfo";return true;}
            if(name.StartsWith("btnU")&&int.TryParse(name.Substring(4),out id))return true;
            if(name.StartsWith("btnT")&&int.TryParse(name.Substring(4),out id)){action=AgeOfWarAction.BuyTurret;kind="turretInfo";return true;}
            if(name.StartsWith("btnS")&&int.TryParse(name.Substring(4),out id)){action=AgeOfWarAction.Special;kind="specialInfo";return true;}
            if(name=="btnEvolve"){action=AgeOfWarAction.Evolve;kind="evolveInfo";return true;}
            if(name=="btnAddTower"){action=AgeOfWarAction.BuyTurretSlot;kind="addTowerInfo";return true;}
            if(name=="btnSellTurret"){action=AgeOfWarAction.SellTurret;kind="sellTurretInfo";return true;}
            if(name=="sell-slot"){foreach(var slot in battle.Capture().TurretSlots)if(!slot.Enemy&&slot.Slot==hit.id){action=AgeOfWarAction.SellTurret;kind="sellTurretInfo";id=slot.TurretId;return id!=0;}}
            return false;
        }
        private void SwitchScene(string key){view.BeginTransition(false);view.ShowScene(key);}
        private void Menu(){view.BeginTransition(false);Cursor.visible=originalCursor;bool fromBattle=battle!=null;battle=null;clock=null;CancelPointer();view.ShowScene("menu");if(edition==1)audioPlayer.SelectMusic(null);else if(fromBattle)audioPlayer.StopMusic();}
        internal void HandleBack()
        {
            if(transition){Exit();return;}if(view!=null&&view.Transitioning)return;if(battle!=null){if(view.HasTutorial){view.DismissTutorial();Command(AgeOfWarAction.Pause);return;}if(battle.Capture().Paused)Menu();else Command(AgeOfWarAction.Pause);return;}
            if(view!=null&&view.SceneKey!="menu"){Menu();return;}Exit();
        }
        internal void HostActivity(bool active){stageClock.Reset();foreground=active;if(!active){Cursor.visible=originalCursor;view?.HidePointer();}CancelPointer();if(clock!=null)clock.Reset();if(audioPlayer!=null)audioPlayer.Suspend(!active||leaving);}
        internal void Suspend(){stageClock.Reset();CancelPointer();if(clock!=null)clock.Reset();}
        internal void Exit()
        {
            if(leaving||!visible)return;leaving=true;transition=true;CancelPointer();StartCoroutine(ExitRoutine(++navigationToken));
        }
        private IEnumerator ExitRoutine(int token)
        {
            if(canvasObject!=null)canvasObject.GetComponent<GraphicRaycaster>().enabled=false;if(audioPlayer!=null)audioPlayer.Suspend(true);
            requestOrientation(ScreenOrientation.Portrait);bool ready=false;yield return Direction(false,token,r=>ready=r);
            if(token!=navigationToken)yield break;
            if(!ready)Error=Error??"竖屏恢复未完成，请重新打开应用。";
            Release();visible=false;transition=false;leaving=false;returnLobby?.Invoke();
        }
        private void Fail(Exception error){Debug.LogException(error);Error="战争进化史加载失败："+error.Message;Exit();}
        private void Release()
        {
            stageClock.Reset();battle=null;clock=null;if(view!=null){view.Dispose();view=null;}
            if(audioPlayer!=null){audioPlayer.Release();audioPlayer=null;}if(canvasObject!=null){canvasObject.SetActive(false);Destroy(canvasObject);canvasObject=null;}
            if(viewport!=null)viewport.Changed-=ViewportChanged;viewport=null;stage=null;
            if(assets!=null){assets.Dispose();assets=null;}Application.targetFrameRate=oldTargetFps;Cursor.visible=originalCursor;
        }
        private void OnDestroy(){++navigationToken;Release();}
    }
}
