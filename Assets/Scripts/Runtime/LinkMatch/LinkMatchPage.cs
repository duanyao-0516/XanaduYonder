using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.LinkMatch;
namespace XanaduYonder.Runtime.LinkMatch
{
    internal sealed partial class LinkMatchPage : MonoBehaviour
    {
        private RectTransform root,content,surface;
        private LinkMatchProfile profile;
        private LinkMatchSaveStore store;
        private LinkMatchSession session;
        private LinkMatchBoardView board;
        private LinkMatchPointer pointer;
        private LinkMatchLayout layout;
        private Action returnLobby;
        private Action<Action> openSettings;
        private Action<string> setStatus;
        private Func<bool> acceptsInput,soundEnabled,hapticsEnabled;
        private GameFeedback feedback;
        private bool visible,foreground=true,isEntry=true,settingsBlocked,saveFailed;
        private string warning,hostSaveWarning,dialogKind;
        private GameObject dialog;
        private Vector2 lastSize;
        private int selected=-1,hintA=-1,hintB=-1;
        private LinkMatchPath shownPath;
        private double lastClock,checkpoint;
        private float effectRemaining,toastRemaining;
        private bool removalFeedback,needsRecovery;
        private uint seedCounter;
        public event Action Changed;
        internal Action RetryHostSave { get; set; }
        public long CompletedCount { get { return profile==null?0:profile.CompletedCount; } }
        public string SaveError { get { return saveFailed?warning:null; } }
        internal LinkMatchSession Session { get { return session; } }
        internal LinkMatchProfile Profile { get { return profile; } }
        internal bool IsEntry { get { return isEntry; } }
        internal bool HasDialog { get { return dialog!=null; } }
        internal bool BoardConcealed { get { return board==null||!board.Content.gameObject.activeSelf; } }
        internal LinkMatchLayout Layout { get { return layout; } }
        internal RectTransform Surface { get { return surface; } }
        private bool CanPlay { get { return visible&&!isEntry&&foreground&&!settingsBlocked&&dialog==null&&acceptsInput()&&session!=null&&session.CanInteract; } }
        public void Initialize(RectTransform parent,Action lobby,Action<Action> settings,Func<bool> canInput,Func<bool> sound,Func<bool> haptics,GameFeedback commonFeedback,Action<string> status,LinkMatchProfile initialProfile=null,LinkMatchSaveStore initialStore=null)
        {
            root=parent;returnLobby=lobby;openSettings=settings;acceptsInput=canInput;soundEnabled=sound;hapticsEnabled=haptics;feedback=commonFeedback;setStatus=status;
            store=initialProfile!=null?initialStore:initialStore??new LinkMatchSaveStore(Application.persistentDataPath);
            if(initialProfile!=null) profile=initialProfile;
            else { var saved=store.Load(out warning);profile=saved==null?new LinkMatchProfile():new LinkMatchProfile(saved); }
            seedCounter=unchecked((uint)DateTime.UtcNow.Ticks);lastClock=Time.realtimeSinceStartupAsDouble;LayoutNow();
        }
        public void Enter() { visible=true;gameObject.SetActive(true);settingsBlocked=false;ShowEntry(); }
        public void Leave()
        {
            if(profile==null) return;TickClock();CancelTransient();CloseDialog();session?.Leave();Save();visible=false;gameObject.SetActive(false);
        }
        public void Suspend() { TickClock();CancelTransient();Save(); }
        public void SetForeground(bool value)
        {
            TickClock();foreground=value;session?.SetForeground(value);CancelTransient();lastClock=Time.realtimeSinceStartupAsDouble;
            if(visible&&!isEntry&&session!=null)
            {
                settingsBlocked=false;
                if(session.IsTerminal) CheckEndState(); else ShowPause();
            }
            Save();
        }
        public void Back()
        {
            if(settingsBlocked) return;
            if(dialog!=null)
            {
                if(dialogKind=="pause") return;
                else if(dialogKind=="won"||dialogKind=="timeout") ShowEntry();
                else if(isEntry) { CloseDialog();RefreshBoard(); }
                else ShowPause();
                return;
            }
            if(isEntry) Home();else ShowEntry();
        }
        private void Home() { TickClock();Save();CancelTransient();returnLobby(); }
        internal void ShowEntry()
        {
            TickClock();CancelTransient();CloseDialog();session?.Leave();Save();isEntry=true;LayoutNow();
        }
        internal void SelectMode(LinkMatchMode mode) { profile.SelectedMode=mode;CancelTransient();LayoutNow(); }
        internal void ContinueGame()
        {
            var active=profile.GetLatestActive();StartLevel(active==null?profile.SelectedMode:active.Mode,active==null?LinkMatchLevel.Get(1).Id:active.LevelId);
        }
        internal void StartLevel(LinkMatchMode mode,string levelId,bool fresh=false)
        {
            TickClock();CancelTransient();CloseDialog();if(session!=null) {session.Leave();profile.Capture(session);}
            seedCounter=unchecked(seedCounter*1664525u+1013904223u);
            needsRecovery=false;session=profile.Start(mode,levelId,seedCounter,fresh);profile.SelectedMode=mode;profile.SelectedLevelId=levelId;
            session.SetForeground(foreground);session.SetBlocked(true);session.EnsureMove();isEntry=false;settingsBlocked=false;LayoutNow();
            session.Enter();session.SetBlocked(false);lastClock=Time.realtimeSinceStartupAsDouble;checkpoint=lastClock;Save();RefreshCounts();
            if(!foreground) ShowPause();else CheckEndState();
        }
        internal void SelectTile(int index)
        {
            TickClock();if(!CanPlay||removalFeedback||index<0||index>=session.Game.Level.Columns*session.Game.Level.Rows||session.Game.TileAt(index)<0) {CheckEndState();return;}
            ClearEffect();
            if(selected==index) selected=-1;
            else if(selected<0||session.Game.TileAt(selected)!=session.Game.TileAt(index)) selected=index;
            else
            {
                if(session.TryMatch(selected,index,out var path))
                {
                    selected=-1;shownPath=path;effectRemaining=.22f;removalFeedback=true;needsRecovery=true;Save();
                    feedback?.Play(true,soundEnabled(),hapticsEnabled());
                }
                else { selected=index;Toast("这两张暂时连不到，换一对试试"); }
            }
            RefreshBoard();RefreshCounts();CheckEndState();
        }
        private void Hint()
        {
            TickClock();if(!CanPlay||removalFeedback) {CheckEndState();return;}CancelTransient();
            RecoverNoMove();var pair=session.TryHint();
            if(pair==null) {Toast("本局提示次数已用完");return;}
            hintA=pair.First;hintB=pair.Second;shownPath=pair.Path;effectRemaining=1.4f;Save();RefreshBoard();RefreshCounts();
        }
        private void Shuffle()
        {
            TickClock();if(!CanPlay||removalFeedback) {CheckEndState();return;}CancelTransient();
            if(session.TryShuffle()) { Save();RefreshBoard();RefreshCounts();feedback?.Play(false,soundEnabled(),false); }
            else Toast("本局洗牌次数已用完");
        }
        private void RecoverNoMove()
        {
            needsRecovery=false;
            if(session==null||session.IsTerminal) return;
            TickClock();if(session.IsTerminal) return;
            if(session.EnsureMove()) {lastClock=Time.realtimeSinceStartupAsDouble;CancelTransient();Save();RefreshBoard();Toast("没有可连的水果，已免费重新排列");}
        }
        private void TickClock()
        {
            double now=Time.realtimeSinceStartupAsDouble,delta=Math.Max(0,now-lastClock);lastClock=now;
            if(visible&&!isEntry&&session!=null) session.Tick(delta);
        }
        public void CheckEndState()
        {
            if(!visible||isEntry||session==null||!session.IsTerminal||settingsBlocked) return;
            string kind=session.Outcome==LinkMatchOutcome.Won?"won":"timeout";
            if(dialogKind==kind) return;
            // The model and save are already terminal; only the short path presentation waits.
            if(kind=="won"&&removalFeedback&&effectRemaining>0) return;
            CancelTransient();Save();ShowDialog(kind);
        }
        public void Save()
        {
            if(profile==null) return;
            if(session!=null) profile.Capture(session);
            if(store!=null)
            {
                if(!store.TrySave(profile.Export(),out var error)) { saveFailed=true;warning="进度尚未保存，请暂时保留本页";Debug.LogWarning("LinkMatch save failed: "+error); }
                else if(saveFailed) { saveFailed=false;warning=null; }
            }
            if(visible) setStatus(warning??"");RefreshWarning();Changed?.Invoke();
        }
        internal void SetHostSaveWarning(string message) { if(hostSaveWarning==message) return;hostSaveWarning=message;RefreshWarning(); }
        private void Update()
        {
            if(!visible||profile==null) return;
            TickClock();
            if(root.rect.size!=lastSize) { CancelTransient();LayoutNow(); }
            if(!soundEnabled()) feedback?.Stop();
            if(effectRemaining>0&&visible&&!isEntry&&foreground&&!settingsBlocked&&dialog==null)
            {
                effectRemaining-=Time.unscaledDeltaTime;
                if(effectRemaining<=0) {bool recover=removalFeedback;ClearEffect();RefreshBoard();if(recover)RecoverNoMove();}
            }
            if(toastRemaining>0) {toastRemaining-=Time.unscaledDeltaTime;if(toastRemaining<=0&&toastPanel!=null)toastPanel.gameObject.SetActive(false);}
            if(CanPlay&&effectRemaining<=0&&needsRecovery) RecoverNoMove();
            if(!isEntry) { RefreshCounts();CheckEndState();if(Time.realtimeSinceStartupAsDouble-checkpoint>=5) {checkpoint=Time.realtimeSinceStartupAsDouble;Save();} }
        }
        private void ClearEffect() {shownPath=null;hintA=hintB=-1;effectRemaining=0;removalFeedback=false;}
        private void CancelTransient() {pointer?.Cancel();selected=-1;ClearEffect();toastRemaining=0;if(toastPanel!=null)toastPanel.gameObject.SetActive(false);}
        private void OnRectTransformDimensionsChange()
        {
            // EventSystem processes releases before Update; invalidate the gesture synchronously.
            // Defer rebuilding children to Update to avoid re-entering Unity's layout traversal.
            if(root!=null&&root.rect.size!=lastSize) CancelTransient();
        }
        private void OnDisable() {CancelTransient();session?.Leave();}
        private void OnDestroy() {pointer?.Cancel();}
        private static string ModeName(LinkMatchMode mode) { return mode==LinkMatchMode.Relaxed?"轻松":"挑战"; }
        private static string Clock(double seconds) {int whole=(int)Math.Min(359999,Math.Max(0,Math.Ceiling(seconds)));return (whole/60).ToString("00")+":"+(whole%60).ToString("00");}
    }
}
