using System;
using UnityEngine;
using XanaduYonder.Core.FlappyBird;

namespace XanaduYonder.Runtime.FlappyBird
{
    // Rules and input buffering belong to FlappySession. This page owns only the
    // live view, interruption boundaries, and a process-lived best-score profile.
    internal sealed partial class FlappyBirdPage : MonoBehaviour
    {
        private RectTransform root;
        private FlappySession session;
        private FlappySaveData data;
        private FlappySaveStore store;
        private FlappyPointer pointer;
        private FlappyScene scene;
        private FlappyAudio soundEffects;
        private GameFeedback commonFeedback;
        private Action returnLobby;
        private Action<Action> openSettings;
        private Action<string> setStatus;
        private Func<bool> acceptsInput,soundEnabled,hapticsEnabled;
        private bool visible,foreground=true,settingsBlocked,hostBlocked,dirty;
        private long storedBestScore;
        private string loadWarning,hostSaveWarning,saveError;
        private Vector2 lastSize;
        private Rect lastSafeArea;
        private int settingsGeneration;
        public event Action Changed;
        public long BestScore { get { return data==null?0:data.BestScore; } }
        public string SaveError { get { return saveError??loadWarning; } }
        internal FlappySession Session { get { return session; } }
        internal RectTransform Surface { get { return surface; } }
        internal FlappyPointer Pointer { get { return pointer; } }
        internal bool HasDialog { get { return dialog!=null; } }
        internal bool SettingsBlocked { get { return settingsBlocked; } }
        internal long StoredBestScore { get { return storedBestScore; } }
        private bool HasUnsavedRecord { get { return data!=null&&data.BestScore>storedBestScore; } }

        public void Initialize(RectTransform parent,Action lobby,Action<Action> settings,Func<bool> accepts,
            Func<bool> sound,Func<bool> haptics,GameFeedback sharedFeedback,Action<string> status,
            FlappySaveStore initialStore=null,FlappySaveData initialData=null)
        {
            root=parent; returnLobby=lobby; openSettings=settings; acceptsInput=accepts;
            soundEnabled=sound; hapticsEnabled=haptics; commonFeedback=sharedFeedback; setStatus=status;
            // An explicit profile opts out of production disk access. Editor
            // fixtures can supply their own temporary store when testing writes.
            store=initialData!=null?initialStore:initialStore??new FlappySaveStore(Application.persistentDataPath);
            data=initialData!=null?initialData.Copy():store.Load(out loadWarning);
            if(data==null) data=new FlappySaveData();
            if(data.BestScore<0) throw new ArgumentOutOfRangeException(nameof(initialData));
            storedBestScore=data.BestScore;
            session=new FlappySession(NewSeed());
            Build();
            scene=new FlappyScene(surface);
            BuildSceneHints();
            pointer=surface.gameObject.AddComponent<FlappyPointer>();
            pointer.AcceptsInput=()=>CanFlap;
            pointer.Flap=()=>{ if(CanFlap) { session.Press(); Refresh(); } };
            pointer.Canceled=()=>session?.ClearInput();
            soundEffects=gameObject.AddComponent<FlappyAudio>(); soundEffects.Initialize();
            LayoutNow(); Refresh(); gameObject.SetActive(false);
        }
        private static uint NewSeed()
        {
            uint seed=unchecked((uint)DateTime.UtcNow.Ticks^(uint)Environment.TickCount);
            return seed==0?1u:seed;
        }
        private bool HostAllows { get { return acceptsInput==null||acceptsInput(); } }
        private bool CanFlap { get { return visible&&foreground&&!settingsBlocked&&dialog==null&&HostAllows&&session!=null&&(session.Phase==FlappyPhase.Ready||session.Phase==FlappyPhase.Running); } }
        private bool Sound { get { return soundEnabled!=null&&soundEnabled(); } }
        private bool Haptics { get { return hapticsEnabled!=null&&hapticsEnabled(); } }

        public void Enter()
        {
            settingsGeneration++; settingsBlocked=false; hostBlocked=false;
            CloseDialog(); session=new FlappySession(NewSeed()); scene.Reset();
            visible=true; gameObject.SetActive(true); pointer.ResetContacts();
            LayoutNow(); Refresh(); setStatus?.Invoke(SaveError??loadWarning??"");
        }
        public void Leave()
        {
            if(!visible) return;
            Suspend(); Save(); settingsGeneration++; settingsBlocked=false;
            CloseDialog(); visible=false; pointer.ResetContacts(); soundEffects.Stop(); gameObject.SetActive(false);
        }
        public void Suspend()
        {
            if(session==null) return;
            pointer?.Cancel(); session.Suspend(); soundEffects?.Stop(); Refresh();
        }
        public void SetForeground(bool value)
        {
            foreground=value;
            if(!value)
            {
                settingsGeneration++; settingsBlocked=false;
                pointer?.ResetContacts();
                if(visible) { Suspend(); Save(); }
            }
            else if(visible) { pointer?.ResetContacts(); Refresh(); }
        }
        public void Back()
        {
            if(!visible||settingsBlocked) return;
            if(dialogKind==DialogKind.Help||dialogKind==DialogKind.Exit) { CloseDialog(); Refresh(); return; }
            if(dialogKind==DialogKind.Pause) { RequestHome(); return; }
            if(dialogKind==DialogKind.Result) { RequestHome(); return; }
            if(session.Phase==FlappyPhase.Running||session.Phase==FlappyPhase.Countdown) { Suspend(); Save(); return; }
            RequestHome();
        }
        internal void TogglePause()
        {
            if(!visible||!foreground||settingsBlocked||!HostAllows||dialog!=null) return;
            if(session.Phase==FlappyPhase.Result||session.Phase==FlappyPhase.Dying) return;
            Suspend(); Save(); ShowPause();
        }
        private void Continue()
        {
            if(!visible||!foreground||settingsBlocked||!HostAllows) return;
            CloseDialog(); pointer.Cancel(); session.Resume(); Refresh();
        }
        internal void Restart()
        {
            if(!visible||!foreground||settingsBlocked||!HostAllows) return;
            if(session.Phase==FlappyPhase.Result&&!session.CanRestart) return;
            if(session.Phase!=FlappyPhase.Ready&&session.Phase!=FlappyPhase.Paused&&session.Phase!=FlappyPhase.Result) return;
            Save(); CloseDialog(); pointer.Cancel(); session.Restart(NewSeed()); scene.Reset(); Refresh();
        }
        internal void RequestHome()
        {
            if(!visible||settingsBlocked||!HostAllows) return;
            bool active=session.Phase==FlappyPhase.Running||session.Phase==FlappyPhase.Paused||session.Phase==FlappyPhase.Countdown;
            Suspend(); Save();
            if(active||HasUnsavedRecord) ShowExit();
            else returnLobby?.Invoke();
        }
        internal void ShowRules()
        {
            if(!visible||!foreground||settingsBlocked||!HostAllows) return;
            Suspend(); Save(); ShowHelpDialog();
        }
        internal void Settings()
        {
            if(!visible||!foreground||settingsBlocked||!HostAllows) return;
            Suspend(); Save(); CloseDialog(); settingsBlocked=true;
            int generation=++settingsGeneration;
            openSettings?.Invoke(()=>
            {
                if(generation!=settingsGeneration||!visible) return;
                settingsBlocked=false; pointer.Cancel(); Refresh();
            });
            if(openSettings==null) { settingsBlocked=false; Refresh(); }
        }

        private void Update()
        {
            if(!visible) return;
            Layout(false);
            if(!Sound) soundEffects.Stop();
            bool blocked=!HostAllows;
            if(blocked&&!hostBlocked) Suspend();
            hostBlocked=blocked;
            if(!foreground||settingsBlocked||blocked) return;
            Advance(Time.unscaledDeltaTime);
        }
        // The same boundary drives real frames and the production UGUI fixture.
        internal void Advance(float elapsed)
        {
            if(float.IsNaN(elapsed)||float.IsInfinity(elapsed)||elapsed<0) throw new ArgumentOutOfRangeException(nameof(elapsed));
            if(!visible||!foreground||settingsBlocked||!HostAllows) return;
            Layout(false);
            if(dialogKind==DialogKind.Help||dialogKind==DialogKind.Exit||dialogKind==DialogKind.Pause) return;
            var previous=session.Phase;
            var result=session.Advance(elapsed);
            if(previous==FlappyPhase.Running&&session.Phase==FlappyPhase.Paused) { pointer.Cancel(); soundEffects.Stop(); }
            if(result.Flapped) { scene.Flap(); soundEffects.Flap(Sound); }
            if(result.Points>0) { soundEffects.Point(Sound); commonFeedback?.Pulse(Haptics); }
            if(session.Game.Score>data.BestScore)
            {
                data.BestScore=session.Game.Score; dirty=true; Save();
            }
            if(result.Died) { pointer.Cancel(); soundEffects.Impact(Sound); commonFeedback?.Pulse(Haptics); Save(); }
            bool animate=session.Phase==FlappyPhase.Running||session.Phase==FlappyPhase.Dying;
            scene.Render(session,elapsed,animate); Refresh(false);
        }
        public void Save() { Persist(false); }
        private void RetrySave() { Persist(true); }
        private void Persist(bool explicitRetry)
        {
            if(data==null) return;
            if(session!=null&&session.Game.Score>data.BestScore) { data.BestScore=session.Game.Score; dirty=true; }
            // Ready/help/leave are not a new record. A default zero must not
            // create a player file or automatically try to replace an unreadable
            // history. Explicit retry may repair a valid zero-valued backup; the
            // store remains the authority on whether that write is legitimate.
            bool pristineZero=data.BestScore==0&&!dirty&&saveError==null;
            if(store!=null&&(!pristineZero||(explicitRetry&&loadWarning!=null)))
            {
                string error;
                if(!store.TrySave(data,out error))
                {
                    saveError=data.BestScore==0&&loadWarning!=null?null:"最高纪录尚未保存，请检查设备空间后重试。";
                    Debug.LogWarning("Flappy Bird save failed: "+error);
                }
                else { storedBestScore=data.BestScore; dirty=false; saveError=null; loadWarning=null; }
            }
            RefreshScores(); RefreshWarning(); Changed?.Invoke();
            if(visible) setStatus?.Invoke(SaveError??loadWarning??"");
        }
        internal void SetHostSaveWarning(string warning) { hostSaveWarning=warning; RefreshWarning(); }
        private void Refresh(bool render=true)
        {
            if(session==null||surface==null) return;
            RefreshScores(); RefreshWarning();
            readyHint.SetActive(session.Phase==FlappyPhase.Ready);
            countdownHint.SetActive(session.Phase==FlappyPhase.Countdown);
            countdownText.text=session.CountdownNumber.ToString();
            pauseButton.interactable=session.Phase!=FlappyPhase.Dying&&session.Phase!=FlappyPhase.Result;
            if(render&&scene!=null) scene.Render(session,0,false);
            if(settingsBlocked) return;
            if(dialog==null)
            {
                if(session.Phase==FlappyPhase.Paused) ShowPause();
                else if(session.Phase==FlappyPhase.Result) ShowResult();
            }
            if(resultRestart!=null) resultRestart.interactable=session.CanRestart;
        }
        private void OnDisable()
        {
            pointer?.ResetContacts(); soundEffects?.Stop();
            if(visible&&session!=null) { session.Suspend(); if(dirty) Save(); }
        }
        private void OnDestroy() { settingsGeneration++; pointer?.ResetContacts(); soundEffects?.Stop(); scene?.Dispose(); }
    }
}
