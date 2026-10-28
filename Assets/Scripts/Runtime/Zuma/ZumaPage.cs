using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Zuma;

namespace XanaduYonder.Runtime.Zuma
{
    // The page owns its run and views; the shell owns shared settings and the destination lobby.
    internal sealed class ZumaPage : MonoBehaviour
    {
        private Action returnLobby;
        private Func<bool> sound,haptics;
        private Func<bool,bool,bool> setSetting;
        private GameFeedback feedback;
        private ZumaAssets assets;
        private ZumaAudio audioPlayer;
        private ZumaView view;
        private ZumaViewport viewport;
        private GameObject canvasObject;
        private ZumaGame game;
        private ZumaLevelCache levels;
        private ZumaSaveData data;
        private ZumaSaveStore store;
        private ZumaLoadStatus loadStatus;
        private readonly ZumaAimGesture gesture=new ZumaAimGesture();
        private bool visible,transition,foreground=true,running,dirty;
        private long lastSavedRevision=-1;
        private int profileRevision,lastSavedProfileRevision=-1,navigationToken;
        private double nextSave,nextRetry,orientationClock;
        private float accumulator;
        private string saveError,loadWarning,settingsError,navigationError;
        private Action dialogBack;
        public bool IsVisible { get { return visible; } }
        public string SaveError { get { return saveError??navigationError; } }
        private bool AcceptsInput { get { return visible&&!transition&&foreground&&running&&game!=null&&view!=null&&!view.HasDialog; } }

        private bool AcceptsSelectionInput { get { return visible&&!transition&&foreground&&game==null&&view!=null&&!view.HasDialog; } }

        public void Initialize(Action lobby,Func<bool> soundEnabled,Func<bool> hapticsEnabled,Func<bool,bool,bool> updateSetting,GameFeedback common)
        {
            returnLobby=lobby;sound=soundEnabled;haptics=hapticsEnabled;setSetting=updateSetting;feedback=common;
            levels=new ZumaLevelCache(LoadLevelDefinition);
        }
        public void Enter()
        {
            if(visible||transition)return;
            visible=true;transition=true;running=false;
            var token=++navigationToken;
            StartCoroutine(EnterRoutine(token));
        }
        private IEnumerator EnterRoutine(int token)
        {
            Screen.orientation=ScreenOrientation.LandscapeLeft;
            var ready=false;
            yield return WaitForDirection(true,token,value=>ready=value);
            if(token!=navigationToken)yield break;
            if(!ready){navigationError="横屏切换未完成，请重试。";Exit();yield break;}
            try
            {
                assets=new ZumaAssets();
                BuildCanvas();
                audioPlayer=gameObject.AddComponent<ZumaAudio>();audioPlayer.Initialize(assets,sound,haptics,feedback);
                if(store==null)store=new ZumaSaveStore(Application.persistentDataPath,ResolveLevel);
                if(data==null)LoadData();
                transition=false;
                navigationError=null;
                if(data==null){ShowLoadError();yield break;}
                ShowLoadedPage();
            }
            catch(Exception ex)
            {
                LoadFailed(ex);
            }
        }
        private IEnumerator WaitForDirection(bool landscape,int token,Action<bool> completed)
        {
            var elapsed=0d;var stable=0;var previous=Rect.zero;var previousSize=Vector2.zero;
            orientationClock=Time.realtimeSinceStartupAsDouble;
            while(token==navigationToken&&elapsed<3)
            {
                var now=Time.realtimeSinceStartupAsDouble;
                if(foreground)
                {
                    elapsed+=Math.Max(0,now-orientationClock);
                    var safe=Screen.safeArea;var size=new Vector2(Screen.width,Screen.height);
                    var correct=landscape?size.x>size.y:size.y>size.x;
                    if(correct&&safe.width>0&&safe.height>0&&safe==previous&&size==previousSize)stable++;else stable=0;
                    previous=safe;previousSize=size;
                    if(stable>=2){completed(true);yield break;}
                }
                orientationClock=now;yield return null;
            }
            completed(false);
        }
        private void BuildCanvas()
        {
            canvasObject=new GameObject("Zuma landscape UI",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform,false);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=10;
            var background=UiFactory.Panel(canvasObject.transform,"Letterbox",0,0,1280,720,new Color(.025f,.022f,.016f),false);
            UiFactory.Stretch(background.rectTransform);
            var frame=ZumaArt.Rect(canvasObject.transform,"1280x720 safe game frame",0,0,1280,720);
            frame.gameObject.AddComponent<RectMask2D>();
            viewport=canvasObject.AddComponent<ZumaViewport>();viewport.Initialize(frame);
            viewport.Changed+=OnViewportChanged;
            view=new ZumaView(frame,assets);
        }
        private ZumaLevelDefinition ResolveLevel(string id)
        {
            return levels.Get(id);
        }
        private ZumaLevelDefinition LoadLevelDefinition(string id)
        {
            if(assets!=null)return assets.LoadLevel(id);
            // Persistence outlives visual resources, including retries after leaving the page.
            // This temporary catalog reads JSON only; it never acquires a texture or AudioClip.
            using(var catalog=new ZumaAssets())return catalog.LoadLevel(id);
        }
        private void LoadData()
        {
            var result=store.Load();loadStatus=result.Status;loadWarning=result.Warning;
            if(result.Status==ZumaLoadStatus.Missing){data=ZumaSaveData.CreateNew();profileRevision++;dirty=true;}
            else data=result.Data;
            lastSavedRevision=-1;lastSavedProfileRevision=-1;
            if(result.Status==ZumaLoadStatus.RecoveredFromBackup)dirty=true;
        }
        private void ShowLoadError()
        {
            var panel=view.Dialog("SAVE DATA");
            view.Chinese(panel,loadWarning??"无法读取Zuma进度，现有文件已保留。",55,118,495,140);
            view.StandardButton(panel,"BACK",65,362,475,72,Exit);
            if(loadStatus==ZumaLoadStatus.Corrupt)
                view.StandardButton(panel,"RESET",65,274,475,64,ConfirmReset);
            else if(loadStatus==ZumaLoadStatus.IoError)
                view.StandardButton(panel,"RETRY",65,274,475,64,()=>{LoadData();if(data==null)ShowLoadError();else ShowLoadedPage();});
            dialogBack=Exit;ShowWarnings();
        }
        private void ConfirmReset()
        {
            var panel=view.Dialog("RESET?");
            view.Chinese(panel,"将归档损坏的Zuma存档并重新开始。\n其他游戏和公共设置不受影响。",55,126,495,120);
            view.StandardButton(panel,"RESET",65,274,475,64,()=>
            {
                var result=store.ResetCorrupt();
                if(!result.Success){loadWarning="保留恢复副本失败，原文件未重置。";ShowLoadError();return;}
                data=ZumaSaveData.CreateNew();game=null;profileRevision++;dirty=true;loadWarning=null;Save();ShowSelection();
            });
            view.StandardButton(panel,"CANCEL",65,362,475,72,ShowLoadError);dialogBack=ShowLoadError;
        }
        private void ShowSelection()
        {
            try { ShowSelectionCore(); }
            catch(Exception ex) { LoadFailed(ex); }
        }
        private void ShowLoadedPage()
        {
            ShowSelection();
            if(transition||!visible||data.Session==null||data.Session.ResultAcknowledged)return;
            if(IsTerminal(data.Session))RestoreSession(false);else ShowContinuePrompt();
        }
        private void ShowSelectionCore()
        {
            if(data==null)return;
            CancelAim();running=false;accumulator=0;game=null;
            audioPlayer.Suspend(false);audioPlayer.SelectMusic("gauntlet");
            var p=data.Profile;
            view.ShowSelection(p.LastMapId,p.LastDifficultyId,data.GetRecord(p.LastMapId,p.LastDifficultyId),()=>{if(AcceptsSelectionInput)Exit();},
                ()=>SelectMap(-1),StartSelected,()=>SelectMap(1),SelectDifficulty,
                data.Session!=null&&!data.Session.ResultAcknowledged?(Action)(()=>{if(AcceptsSelectionInput)ShowContinuePrompt();}):null,ShowHelp);
            dialogBack=null;ShowWarnings();
        }
        private void ShowHelp()
        {
            if(!AcceptsSelectionInput)return;
            CancelAim();
            view.ShowHelp(CloseHelp);
            dialogBack=CloseHelp;
        }
        private void CloseHelp()
        {
            view.CloseDialog();
            dialogBack=null;
        }
        private void SelectMap(int delta)
        {
            if(!AcceptsSelectionInput)return;audioPlayer.Button();
            var index=Array.IndexOf(ZumaSaveData.MapIds,data.Profile.LastMapId);
            data.Profile.LastMapId=ZumaSaveData.MapIds[(index+delta+18)%18];profileRevision++;Save();ShowSelection();
        }
        private void SelectDifficulty(int index)
        {
            if(!AcceptsSelectionInput)return;audioPlayer.Button();
            data.Profile.LastDifficultyId=ZumaSaveData.DifficultyIds[index];profileRevision++;Save();ShowSelection();
        }
        private void ShowContinuePrompt()
        {
            if(data.Session==null)return;
            if(IsTerminal(data.Session)){RestoreSession(false);return;}
            var panel=view.Dialog("CONTINUE?");
            view.Chinese(panel,"已有一局尚未结束。\n继续时会从上次保存的位置恢复。",55,122,495,118);
            view.StandardButton(panel,"CONTINUE",65,274,475,64,()=>RestoreSession(true));
            view.StandardButton(panel,"SELECT LEVEL",65,362,475,72,ShowSelection);
            dialogBack=ShowSelection;
        }
        private void StartSelected()
        {
            if(!AcceptsSelectionInput)return;
            if(data.Session!=null&&!data.Session.ResultAcknowledged)
            {
                var panel=view.Dialog("NEW GAME?");
                view.Chinese(panel,"开始新游戏将替换当前Zuma进度。\n已有最高成绩会保留。",55,122,495,118);
                view.StandardButton(panel,"PLAY",65,274,475,64,StartNew);
                view.StandardButton(panel,"CANCEL",65,362,475,72,ShowSelection);dialogBack=ShowSelection;return;
            }
            StartNew();
        }
        private void StartNew()
        {
            try
            {
                var p=data.Profile;var definition=ResolveLevel(p.LastMapId);
                var seed=unchecked((uint)DateTime.UtcNow.Ticks^(uint)Environment.TickCount);
                var candidate=new ZumaGame(definition,p.LastDifficultyId,seed,data.GetRecord(p.LastMapId,p.LastDifficultyId).BestTimeSeconds);
                ShowRun(definition,candidate,true);
                data.Session=game.Snapshot();dirty=true;lastSavedRevision=-1;Save();
            }
            catch(Exception ex) { LoadFailed(ex); }
        }
        private void RestoreSession(bool resume)
        {
            try
            {
                var definition=ResolveLevel(data.Session.MapId);
                var candidate=ZumaGame.Restore(definition,data.Session);ShowRun(definition,candidate,resume);
                if(IsTerminal(data.Session))ShowResult();else if(!resume)ShowPause();
            }
            catch(Exception ex){LoadFailed(ex);}
        }
        private void ShowRun(ZumaLevelDefinition definition,ZumaGame candidate,bool resume)
        {
            CancelAim();accumulator=0;running=false;dialogBack=null;
            view.ShowGame(definition,candidate.Snapshot(),ShowPause,Swap);
            view.Touch.Initialize(BeginAim,MoveAim,ReleaseAim);
            audioPlayer.SelectMusic("game");audioPlayer.Suspend(!resume);
            // Only replace the authoritative run after every required visual/audio resource loaded.
            game=candidate;running=resume;
            nextSave=Time.realtimeSinceStartupAsDouble+2;ShowWarnings();
        }
        private void LoadFailed(Exception exception)
        {
            Debug.LogException(exception);
            navigationError="Zuma资源加载失败，已有进度已保留。";
            Exit();
        }
        private void BeginAim(int id,Vector2 screen)
        {
            float x,y;var inside=viewport.Bounds.ToDesign(screen.x,screen.y,out x,out y);
            if(gesture.Begin(id,inside,AcceptsInput))game.Aim(x,y);
        }
        private void MoveAim(int id,Vector2 screen)
        {
            if(!gesture.Owns(id)||!AcceptsInput)return;
            float x,y;if(viewport.Bounds.ToDesign(screen.x,screen.y,out x,out y))game.Aim(x,y);
        }
        private void ReleaseAim(int id,Vector2 screen,bool canceled,bool onField)
        {
            float x,y;var inside=viewport.Bounds.ToDesign(screen.x,screen.y,out x,out y)&&onField;
            if(!gesture.Owns(id))return;
            if(AcceptsInput&&inside)game.Aim(x,y);
            if(gesture.Release(id,canceled,inside,AcceptsInput&&game.CanFire)&&game.TryFire())
            { HandleEvents();Save(); }
        }
        private void CancelAim()
        { gesture.Cancel();view?.HideAim(); }
        private void Swap()
        { if(!AcceptsInput)return;CancelAim();if(game.Swap()){HandleEvents();Save();} }
        private void ShowPause()
        {
            if(game==null||transition)return;
            Suspend();
            if(IsTerminal(game.Snapshot())){ShowResult();return;}
            var panel=view.Dialog("mENU");
            view.Toggle(panel,"SOUND",sound(),66,104,()=>ToggleSetting(true));
            view.Toggle(panel,"HAPTICS",haptics(),66,183,()=>ToggleSetting(false));
            view.StandardButton(panel,"RETURN TO LEVEL SELECT",65,290,475,64,()=>{Save();ShowSelection();});
            view.StandardButton(panel,"BACK TO GAME",65,368,475,64,Resume);
            dialogBack=Resume;ShowWarnings();
        }
        private void ToggleSetting(bool soundSetting)
        {
            var enabled=soundSetting?!sound():!haptics();
            var success=setSetting(soundSetting,enabled);
            settingsError=success?null:"设置未保存，请检查设备存储空间。";
            ShowPause();
        }
        public void CommonSettingsSaved(bool success)
        { if(success)settingsError=null;else settingsError="设置未保存，请检查设备存储空间。";ShowWarnings(); }
        private void Resume()
        {
            if(!foreground||transition||game==null)return;
            CancelAim();view.CloseDialog();dialogBack=null;running=true;accumulator=0;
            audioPlayer.Suspend(false);nextSave=Time.realtimeSinceStartupAsDouble+2;
        }
        private void ShowResult()
        {
            CancelAim();running=false;Save();
            var snapshot=game.Snapshot();audioPlayer.Suspend(false);audioPlayer.SelectMusic(snapshot.Phase=="won"?"win":"game_over");
            view.Stats(snapshot,AcknowledgeResult);dialogBack=AcknowledgeResult;ShowWarnings();
        }
        private void AcknowledgeResult()
        {
            if(game==null)return;
            game.AcknowledgeResult();data.AcknowledgeResult();Save();ShowSelection();
        }
        public void HandleBack()
        {
            if(!visible||transition)return;
            CancelAim();
            if(view!=null&&view.HasDialog){dialogBack?.Invoke();return;}
            if(game!=null)ShowPause();else Exit();
        }
        public void Suspend()
        {
            CancelAim();running=false;accumulator=0;
            audioPlayer?.Suspend(true);
            Save();
        }
        public void HostActivity(bool active)
        {
            foreground=active;orientationClock=Time.realtimeSinceStartupAsDouble;
            if(!active)Suspend();
            else if(visible&&!transition&&game!=null)ShowPause();
            else if(visible&&!transition)audioPlayer?.Suspend(false);
            else if(!visible&&!transition)Screen.orientation=ScreenOrientation.Portrait;
        }
        private void OnViewportChanged()
        {
            CancelAim();accumulator=0;
            if(visible&&!transition&&running&&game!=null)ShowPause();
        }
        private void Update()
        {
            var now=Time.realtimeSinceStartupAsDouble;
            if(foreground&&dirty&&now>=nextRetry)Save();
            if(!AcceptsInput)return;
            if(Input.GetMouseButtonDown(1))Swap();
            accumulator+=Mathf.Min(Time.unscaledDeltaTime,5f/60f);
            var steps=0;
            while(accumulator>=1f/60f&&steps++<5){game.Tick();accumulator-=1f/60f;}
            HandleEvents();
            var snapshot=game.Snapshot();view.Render(snapshot,gesture.IsAiming);view.Animate(Time.unscaledDeltaTime);
            if(IsTerminal(snapshot)){ShowResult();return;}
            if(now>=nextSave){Save();nextSave=now+2;}
        }
        private void HandleEvents()
        { foreach(var ev in game.DrainEvents()){audioPlayer.Handle(ev);view.Event(ev);} }
        private static bool IsTerminal(ZumaSnapshot s) { return s.Phase=="won"||s.Phase=="lost"; }
        public void Save()
        {
            if(store==null||data==null)return;
            if(game!=null)
            {
                var snapshot=game.Snapshot();
                if(IsTerminal(snapshot))
                {
                    data.CommitResult(snapshot);game.MarkResultCommitted();
                    if(data.Session.ResultAcknowledged)game.AcknowledgeResult();
                    data.Session=game.Snapshot();
                }
                else data.Session=snapshot;
            }
            var revision=data.Session==null?0:data.Session.Revision;
            if(!dirty&&revision==lastSavedRevision&&profileRevision==lastSavedProfileRevision)return;
            var result=store.Save(data);
            nextRetry=Time.realtimeSinceStartupAsDouble+2;
            if(result.Success)
            { dirty=false;saveError=null;lastSavedRevision=revision;lastSavedProfileRevision=profileRevision; }
            else
            { dirty=true;saveError="Zuma进度未保存，请检查设备存储空间。";Debug.LogWarning("Zuma save failed: "+result.Error); }
            ShowWarnings();
        }
        private void ShowWarnings()
        {
            if(view==null)return;
            view.Warning(settingsError!=null&&saveError!=null?settingsError+"  "+saveError:settingsError??saveError??navigationError??loadWarning);
        }
        public void Exit()
        {
            if(!visible)return;
            ++navigationToken;transition=true;Suspend();StartCoroutine(ExitRoutine(navigationToken));
        }
        private IEnumerator ExitRoutine(int token)
        {
            ReleasePresentation();game=null;
            Screen.orientation=ScreenOrientation.Portrait;
            var ready=false;yield return WaitForDirection(false,token,value=>ready=value);
            if(token!=navigationToken)yield break;
            if(!ready)navigationError=navigationError??"屏幕方向暂未恢复，已安全返回大厅。";
            visible=transition=false;returnLobby();
        }
        private void ReleasePresentation()
        {
            if(audioPlayer!=null){audioPlayer.Release();Destroy(audioPlayer);audioPlayer=null;}
            view?.Dispose();view=null;
            if(viewport!=null)viewport.Changed-=OnViewportChanged;viewport=null;
            if(canvasObject!=null){canvasObject.SetActive(false);Destroy(canvasObject);canvasObject=null;}
            assets?.Dispose();assets=null;
        }
        private void OnDestroy() { ++navigationToken;CancelAim();Save();ReleasePresentation(); }
    }
}
