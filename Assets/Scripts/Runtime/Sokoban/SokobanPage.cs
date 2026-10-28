using System;
using UnityEngine;
using XanaduYonder.Core;
using XanaduYonder.Core.Sokoban;

namespace XanaduYonder.Runtime.Sokoban
{
    internal sealed partial class SokobanPage : MonoBehaviour
    {
        private RectTransform root,surface;
        private SokobanCatalog catalog;
        private SokobanProfile profile;
        private SokobanSaveStore store;
        private SokobanSession session;
        private SokobanViewport viewport;
        private SokobanBoardView board;
        private SokobanPointer pointer;
        private SokobanFeedback feedback;
        private GameFeedback commonFeedback;
        private Action returnLobby;
        private Action<Action> openSettings;
        private Action<string> setStatus;
        private Func<bool> acceptsInput,soundEnabled,hapticsEnabled;
        private GameObject dialog;
        private Action dismissDialog;
        private bool visible,foreground=true,settingsBlocked,choosing,resultDismissed,saveFailed,hasStarted,isEntry=true;
        private string warning,hostSaveWarning;
        private Direction facing=Direction.Down;
        private Direction? queued;
        private Vector2 lastSize;
        private bool lastWarning;
        private float toastRemaining;
        public event Action Changed;
        public long CompletedCount { get { return profile==null?0:profile.CompletedCount; } }
        public string SaveError { get { return saveFailed?warning:null; } }
        internal SokobanSession Session { get { return session; } }
        internal SokobanViewport Viewport { get { return viewport; } }
        internal bool HasDialog { get { return dialog!=null; } }
        internal bool IsChoosing { get { return choosing; } }
        internal bool IsEntry { get { return isEntry; } }
        internal bool IsAnimating { get { return board!=null&&board.Animating; } }
        public void Initialize(RectTransform parent,Action lobby,Action<Action> settings,Func<bool> canInput,
            Func<bool> sound,Func<bool> haptics,GameFeedback sharedFeedback,Action<string> status,
            SokobanCatalog initialCatalog=null,SokobanProfile initialProfile=null,SokobanSaveStore initialStore=null)
        {
            root=parent; returnLobby=lobby; openSettings=settings; acceptsInput=canInput;
            soundEnabled=sound; hapticsEnabled=haptics; commonFeedback=sharedFeedback; setStatus=status;
            var asset=initialCatalog==null?Resources.Load<TextAsset>("Sokoban/campaign"):null;
            if(initialCatalog==null&&asset==null) throw new InvalidOperationException("Missing Sokoban campaign.");
            catalog=initialCatalog??SokobanCatalog.Parse(asset.text);
            // Supplying a profile explicitly opts out of player disk access unless a store is also supplied.
            store=initialProfile!=null?initialStore:initialStore??new SokobanSaveStore(Application.persistentDataPath,catalog);
            if(initialProfile!=null) profile=initialProfile;
            else { var saved=store.Load(out warning); profile=saved==null?new SokobanProfile(catalog):new SokobanProfile(catalog,saved); }
            var active=profile.GetActive(); hasStarted=active!=null;
            // A migrated profile may keep its old selection without a resumable attempt.
            // The fresh entry previews the same tutorial that its start action opens.
            session=active==null?new SokobanSession(catalog.At(0)):new SokobanSession(active,catalog);
            session.SetView(1,.5,.5); session.Leave();
            feedback=gameObject.AddComponent<SokobanFeedback>(); BuildGame(); BuildEntry(); SetupBoard(); Refresh();
        }
        public void Enter()
        {
            visible=true; gameObject.SetActive(true); settingsBlocked=false; session.SetBlocked(false); session.SetForeground(foreground);
            resultDismissed=false; ShowEntry();
        }
        public void Leave()
        {
            CancelInput(); CloseDialog(); session?.Leave(); feedback?.Stop(); Save(); visible=false; gameObject.SetActive(false);
        }
        public void Suspend() { CancelInput(); feedback?.Stop(); Save(); }
        public void SetForeground(bool value)
        {
            foreground=value; CancelInput(); feedback?.Stop();
            if(!value) { CloseDialog(); settingsBlocked=false; session?.SetBlocked(false); if(session!=null&&session.Game.Won) resultDismissed=false; }
            else if(settingsBlocked) { settingsBlocked=false; session?.SetBlocked(false); }
            session?.SetForeground(value); Save(); Refresh(); if(value) CheckEndState();
        }
        public void Back()
        {
            if(dialog!=null) { dismissDialog?.Invoke(); return; }
            if(isEntry) Home(); else ShowLevels();
        }
        private void Home() { CancelInput(); feedback.Stop(); Save(); returnLobby(); }
        internal void StartLevel(string levelId,bool fresh=false)
        {
            CancelInput(); CloseDialog(); feedback.Stop();
            if(!hasStarted||session==null||session.Game.Level.LevelId!=levelId||fresh)
            { if(hasStarted&&session!=null) profile.Capture(session); session=profile.StartLevel(levelId); facing=Direction.Down; }
            hasStarted=true; isEntry=false;
            session.SetForeground(foreground); session.Enter(); session.SetBlocked(false); settingsBlocked=false; choosing=false; resultDismissed=false;
            SetViewVisibility(); SetupBoard(); Save(); Refresh();
        }
        private void SetupBoard()
        {
            LayoutNow(); viewport=new SokobanViewport(session.Game.Level.Width,session.Game.Level.Height,Math.Max(1,surface.rect.width),Math.Max(1,surface.rect.height),12*Scale);
            pointer.Initialize(surface,viewport,directionRects,directionValues,()=>ViewAllowed,HandleGesture);
            board.Render(session.Game,viewport,facing); RenderView();
        }
        private bool ViewAllowed { get { return visible&&!isEntry&&foreground&&!settingsBlocked&&dialog==null&&acceptsInput()!=false&&session!=null; } }
        private bool MoveAllowed { get { return ViewAllowed&&session.CanInteract; } }
        internal void Move(Direction direction)
        {
            if(!MoveAllowed) return;
            if(board.Animating) { if(!queued.HasValue) queued=direction; return; }
            int player=session.Game.Player,delta=direction==Direction.Up?-session.Game.Level.Width:direction==Direction.Down?session.Game.Level.Width:direction==Direction.Left?-1:1;
            int boxFrom=player+delta,boxTo=boxFrom+delta;
            var result=session.Move(direction); facing=direction;
            if(result.Changed)
            {
                Save(); RefreshCounts(); board.Render(session.Game,viewport,facing,player,result.Pushed?boxFrom:-1,result.Pushed?boxTo:-1);
                feedback.Play(result,session.Game.Won,soundEnabled(),hapticsEnabled(),commonFeedback);
                if(result.StaticCorner) Toast("可能推不回来了，可撤回");
            }
            else
            {
                board.Render(session.Game,viewport,facing);
                if(session.Game.AtMoveLimit) Toast("本次尝试步数已达上限，可撤回或重来");
                else Toast(session.Game.HasBox(boxFrom)?"箱子前方没有空位":"前方是墙");
            }
        }
        internal void Undo()
        {
            if(!MoveAllowed) return; CancelInput(); feedback.Stop();
            if(session.Undo()) { resultDismissed=false; Save(); Refresh(); board.Render(session.Game,viewport,facing); }
        }
        internal void Restart()
        {
            CancelInput(); CloseDialog(); feedback.Stop(); session.Restart(); session.Enter(); facing=Direction.Down; resultDismissed=false;
            Save(); Refresh(); board.Render(session.Game,viewport,facing); RenderView();
        }
        private void HandleGesture(SokobanInputResult result)
        {
            if(result.CancelQueued) { queued=null; board.StopAnimation(); }
            if(result.Direction.HasValue) Move(result.Direction.Value);
        }
        private void CancelInput() { pointer?.CancelGesture(); queued=null; board?.StopAnimation(); ClearToast(); }
        private void Settings()
        {
            if(dialog!=null||settingsBlocked) return; CancelInput(); feedback.Stop(); settingsBlocked=true; session.SetBlocked(true); Save();
            openSettings(()=>{ settingsBlocked=false; session.SetBlocked(false); CancelInput(); Refresh(); CheckEndState(); });
        }
        public void Save()
        {
            if(profile==null) return;
            if(hasStarted&&session!=null)
            { session.SetView(1,.5,.5); profile.Capture(session); }
            if(store!=null)
            {
                string error;
                if(!store.TrySave(profile.Export(),out error)) { saveFailed=true; warning="推箱子进度尚未保存，请检查设备空间。"; Debug.LogWarning("Sokoban save failed: "+error); }
                else if(saveFailed) { saveFailed=false; warning=null; }
            }
            if(visible) setStatus(warning??""); RefreshCompactStatus(); Changed?.Invoke();
        }
        internal void SetHostSaveWarning(string message) { hostSaveWarning=message; RefreshCompactStatus(); }
        internal void LayoutNow() { Layout(true); }
        private void Update()
        {
            if(!visible) return;
            if(!soundEnabled()) feedback.Stop(); Layout(false);
            if(board.Tick(Time.unscaledDeltaTime))
            { if(queued.HasValue&&MoveAllowed) { var next=queued.Value; queued=null; Move(next); } else { queued=null; CheckEndState(); } }
            if(toastRemaining>0) { toastRemaining-=Time.unscaledDeltaTime; if(toastRemaining<=0) ClearToast(); }
            if(MoveAllowed&&!pointer.HasOwner&&Input.touchCount==0&&!Input.GetMouseButton(0))
            {
                if(Input.GetKeyDown(KeyCode.UpArrow)||Input.GetKeyDown(KeyCode.W)) Move(Direction.Up);
                else if(Input.GetKeyDown(KeyCode.DownArrow)||Input.GetKeyDown(KeyCode.S)) Move(Direction.Down);
                else if(Input.GetKeyDown(KeyCode.LeftArrow)||Input.GetKeyDown(KeyCode.A)) Move(Direction.Left);
                else if(Input.GetKeyDown(KeyCode.RightArrow)||Input.GetKeyDown(KeyCode.D)) Move(Direction.Right);
            }
            // A resize or dialog may finish the view animation synchronously.
            // Terminal delivery follows model state, not an animation completion callback.
            CheckEndState();
        }
        private void OnDisable() { CancelInput(); feedback?.Stop(); }
        private void OnDestroy() { pointer?.CancelGesture(); feedback?.Stop(); queued=null; }
    }
}
