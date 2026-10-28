using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Core.Minesweeper;

namespace XanaduYonder.Runtime.Minesweeper
{
    internal sealed partial class MinesweeperPage : MonoBehaviour
    {
        private sealed class RandomSource : IRandomSource
        { private readonly System.Random random=new System.Random(); public int Next(int n) { return random.Next(n); } }
        private readonly IRandomSource random=new RandomSource();
        private RectTransform root, surface;
        private GameObject gameRoot, selectionRoot, dialog;
        private MinesweeperSession session;
        private MinesweeperProfile profile;
        private MinesweeperSaveStore store;
        private MinesweeperViewport viewport;
        private MinesweeperBoardView board;
        private MinesweeperPointer pointer;
        private MinesweeperFeedback feedback;
        private GameFeedback commonFeedback;
        private Action returnLobby;
        private Action<Action> openSettings;
        private Action<string> setStatus;
        private Action<bool> fullscreenChanged;
        private Func<bool> acceptsInput, soundEnabled, hapticsEnabled;
        private Action dismissDialog;
        private bool visible, choosing=true, fullscreen, resultDismissed, saveFailed, viewDirty;
        private string warning, viewportSessionId;
        private float periodicSave;
        private Vector2 lastLayoutSize;
        public event Action Changed;
        public string SaveError { get { return saveFailed?warning:null; } }
        public long CompletedCount
        {
            get
            {
                long result=0;
                if(profile!=null) for(int d=0;d<3;d++) result=MinesweeperSession.SaturatingAdd(result,profile.GetRecord(d).Wins);
                return result;
            }
        }
        public void Initialize(RectTransform parent,Action lobby,Action<Action> settings,Func<bool> canInput,
            Func<bool> sound,Func<bool> haptics,GameFeedback sharedFeedback,Action<string> status,
            Action<bool> onFullscreenChanged=null,MinesweeperProfile initialProfile=null,MinesweeperSaveStore initialStore=null)
        {
            root=parent; returnLobby=lobby; openSettings=settings; acceptsInput=canInput;
            soundEnabled=sound; hapticsEnabled=haptics; commonFeedback=sharedFeedback; setStatus=status;
            fullscreenChanged=onFullscreenChanged;
            // Dependency injection also supports memory-only pages without touching player data.
            store=initialProfile!=null?initialStore:initialStore??new MinesweeperSaveStore(Application.persistentDataPath);
            if(initialProfile!=null) profile=initialProfile;
            else { var saved=store.Load(out warning); profile=saved==null?new MinesweeperProfile():new MinesweeperProfile(saved); }
            feedback=gameObject.AddComponent<MinesweeperFeedback>();
            BuildGame(); BuildSelection(); RefreshSelection();
            gameRoot.SetActive(false);
        }
        public void Enter()
        {
            visible=true; gameObject.SetActive(true); choosing=true;
            gameRoot.SetActive(false); selectionRoot.SetActive(true); fullscreenChanged?.Invoke(false);
            RefreshSelection(); setStatus(warning??"首击安全 · 三档挑战 · 自动保存");
            if(!profile.TutorialSeen) ShowTutorial();
        }
        public void Leave()
        {
            CancelInput(); ExitOverview(); CloseDialog(); session?.Leave(); feedback?.Stop(); Save();
            visible=false; fullscreenChanged?.Invoke(false); gameObject.SetActive(false);
        }
        public void Suspend()
        {
            if(!visible||choosing||session==null) return;
            CancelInput(); ExitOverview(); feedback.Stop();
            // The shared modal has already blocked the session. Ready must not gain a manual pause.
            if(session.Foreground&&!session.Blocked&&session.Game.State==MinesweeperState.Active) session.Pause();
            Save(); Refresh();
        }
        public void SetForeground(bool foreground)
        {
            if(!foreground) { CancelInput(); ExitOverview(); CloseDialog(); feedback?.Stop(); }
            session?.SetForeground(foreground);
            if(!foreground) Save();
            if(visible&&!choosing) Refresh();
        }
        public void Back()
        {
            if(dialog!=null) { dismissDialog?.Invoke(); return; }
            if(!choosing&&viewport!=null&&viewport.IsOverview) { CancelInput(); ExitOverview(); Save(); Refresh(); return; }
            if(!choosing&&fullscreen) { ToggleFullscreen(); return; }
            if(!choosing) ReturnToSelection(); else returnLobby();
        }
        private void ReturnToSelection()
        {
            CancelInput(); ExitOverview(); CloseDialog(); session?.Leave(); feedback.Stop(); Save();
            choosing=true; gameRoot.SetActive(false); selectionRoot.SetActive(true); fullscreenChanged?.Invoke(false);
            RefreshSelection(); setStatus(warning??"选择难度 · 每档独立续局与纪录");
        }
        internal void StartGame(int difficulty,bool fresh)
        {
            CancelInput(); ExitOverview(); CloseDialog();
            if(session!=null) { session.Leave(); Save(); }
            var saved=fresh?null:profile.GetSlot(difficulty);
            session=saved==null?new MinesweeperSession(difficulty,random):new MinesweeperSession(saved,random);
            profile.SelectedDifficulty=difficulty; fullscreen=session.Fullscreen;
            resultDismissed=session.IsTerminal; choosing=false; visible=true;
            selectionRoot.SetActive(false); gameRoot.SetActive(true);
            SetupBoard();
            if(saved==null) viewport.RestoreNormal(1,Math.Min(session.Game.Width/2.0,viewport.Width/(2*viewport.CellSize)),Math.Min(session.Game.Height/2.0,viewport.Height/(2*viewport.CellSize)));
            fullscreenChanged?.Invoke(fullscreen); Save(); Refresh();
        }
        private void RequestNew(int difficulty)
        {
            var old=profile.GetSlot(difficulty);
            bool needsConfirm=old!=null&&(old.Board.State==MinesweeperState.Active||old.Board.State==MinesweeperState.Ready&&Array.Exists(old.Board.Flags,f=>f));
            if(needsConfirm) ShowDialog("开始新的随机棋盘？","当前难度的对局会被替换。\n其他难度与最佳纪录保留。","开始新局",()=>StartGame(difficulty,true),"取消",()=>{});
            else StartGame(difficulty,true);
        }
        private void ConfirmRestart() { if(session!=null) RequestNew(session.Game.GetSnapshot().Difficulty); }
        private void SetupBoard()
        {
            Layout(true);
            viewport=new MinesweeperViewport(session.Game.Width,session.Game.Height,surface.rect.width,surface.rect.height,BaseCellSize());
            viewport.RestoreNormal(session.ZoomScale,session.CenterColumn,session.CenterRow);
            if(profile.GetSlot(session.Game.GetSnapshot().Difficulty)==null)
                viewport.RestoreNormal(1,Math.Min(session.Game.Width/2.0,viewport.Width/(2*viewport.CellSize)),Math.Min(session.Game.Height/2.0,viewport.Height/(2*viewport.CellSize)));
            viewportSessionId=session.SessionId;
            pointer.Initialize(surface,viewport,contacts=>new MinesweeperInputFrame((long)(Time.realtimeSinceStartupAsDouble*1000),session.SessionId,
                ViewAllowed,CellAllowed,session.InputMode==MinesweeperInputMode.Flag,contacts),
                i=>session!=null&&i>=0&&i<session.Game.Width*session.Game.Height&&!session.Game.GetSnapshot().IsRevealed(i),HandleGesture);
        }
        private bool ViewAllowed { get { return visible&&!choosing&&dialog==null&&acceptsInput()&&session!=null&&session.CanView; } }
        private bool CellAllowed { get { return ViewAllowed&&session.CanInteract; } }
        private void HandleGesture(MinesweeperGestureResult result)
        {
            if(session==null) return;
            board.Preview(result.PreviewCellIndex);
            if(result.ViewChanged) { viewDirty=true; RenderView(); }
            if(result.Action==MinesweeperGestureAction.OverviewLocated)
            { session.SetOverview(false); Save(); Refresh(); }
            else if(result.Action==MinesweeperGestureAction.ShortPress||result.Action==MinesweeperGestureAction.ToggleFlag)
            {
                if(!CellAllowed||result.GameId!=session.SessionId) return;
                bool marking=result.Action==MinesweeperGestureAction.ToggleFlag||result.FlagMode;
                var before=session.Game.GetSnapshot();
                var move=session.Execute(result.GameId,result.CellIndex,marking);
                if(move.Changed)
                {
                    // A hold must acknowledge completion before a synchronous disk flush can delay it.
                    if(marking) feedback.Play(move.State,true,soundEnabled(),hapticsEnabled(),commonFeedback);
                    Save(); Refresh(); board.Animate(move.ChangedIndices);
                    CheckEndState();
                    if(!marking) feedback.Play(move.State,false,soundEnabled(),hapticsEnabled(),commonFeedback);
                }
                else if(result.Action==MinesweeperGestureAction.ShortPress&&before.IsRevealed(result.CellIndex)&&before.AdjacentMines(result.CellIndex)>0)
                { board.FlashNeighbors(result.CellIndex); hint.text=marking?"插旗模式 · 已开数字仅显示邻域":"周围旗数还不匹配"; }
                else if(move.Reason==MinesweeperMoveReason.Flagged) hint.text="先移除旗帜，再翻开这个格子";
            }
            if(result.GestureCompleted&&viewDirty) { viewDirty=false; Save(); }
        }
        private void CancelInput() { pointer?.CancelGesture(); board?.ClearEffects(); }
        private void SetMode(MinesweeperInputMode mode)
        { if(!CellAllowed) return; CancelInput(); session.SetInputMode(mode); Save(); Refresh(); }
        private void Zoom(int direction)
        { if(!ViewAllowed||viewport.IsOverview) return; CancelInput(); viewport.StepZoom(direction); Save(); RenderView(); }
        private void ResetZoom()
        { if(!ViewAllowed||viewport.IsOverview) return; CancelInput(); viewport.ResetZoom(); Save(); RenderView(); }
        private void ToggleOverview()
        {
            if(!ViewAllowed) return;
            CancelInput();
            if(viewport.IsOverview) ExitOverview();
            else { Save(); viewport.EnterOverview(); session.SetOverview(true); }
            Save(); Refresh();
        }
        private void ExitOverview()
        { if(viewport!=null&&viewport.IsOverview) viewport.ExitOverview(); session?.SetOverview(false); }
        private void ToggleFullscreen()
        {
            if(choosing||session==null||dialog!=null||viewport.IsOverview) return;
            CancelInput(); fullscreen=!fullscreen; Layout(true); fullscreenChanged?.Invoke(fullscreen); Save(); Refresh();
        }
        private void Pause()
        { if(session==null||session.IsTerminal||session.Paused) return; CancelInput(); ExitOverview(); session.Pause(); feedback.Stop(); Save(); Refresh(); }
        private void Resume()
        { CancelInput(); session?.Resume(); Save(); Refresh(); }
        private void Settings()
        {
            if(dialog!=null) return;
            CancelInput(); ExitOverview(); feedback.Stop();
            session?.SetBlocked(true); Save(); Refresh();
            openSettings(()=>{ session?.SetBlocked(false); Refresh(); });
        }
        public void CheckEndState()
        {
            if(!visible||choosing||session==null||!session.IsTerminal||resultDismissed||dialog!=null||!acceptsInput()||!session.Foreground) return;
            resultDismissed=true;
            bool won=session.Game.State==MinesweeperState.Won;
            ShowDialog(won?"排雷成功":"触雷了",MinesweeperRules.Get(session.Game.GetSnapshot().Difficulty).Name+" · "+session.Game.MineCount+" 雷\n用时 "+FormatTime(session.ElapsedMilliseconds,true)+(session.IsNewBest?"\n刷新本档最佳纪录":""),
                "再来一局",()=>StartGame(session.Game.GetSnapshot().Difficulty,true),"选择难度",ReturnToSelection,"查看棋盘",()=>{});
        }
        public void Save()
        {
            if(profile==null) return;
            if(session!=null)
            {
                if(viewport!=null&&viewportSessionId==session.SessionId)
                    session.SetView(fullscreen,viewport.ZoomScale,viewport.CenterColumn,viewport.CenterRow);
                profile.Capture(session);
            }
            if(store!=null)
            {
                string error;
                if(!store.TrySave(profile.Export(),out error))
                { saveFailed=true; warning="扫雷进度暂未保存，请检查设备空间。"; Debug.LogWarning("Minesweeper save failed: "+error); }
                else if(saveFailed) { saveFailed=false; warning=null; setStatus("扫雷进度已保存。"); }
            }
            if(warning!=null) setStatus(warning);
            if(compactStatus!=null) compactStatus.text=warning??"";
            Changed?.Invoke();
        }
        private void Update()
        {
            if(!visible) return;
            if(!soundEnabled()) feedback.Stop();
            if(choosing||session==null) return;
            Layout(false); board.Tick(Time.unscaledDeltaTime);
            timer.text=FormatTime(session.ElapsedMilliseconds,false);
            focusTimer.text=timer.text;
            if(session.CanView&&session.Game.State==MinesweeperState.Active)
            { periodicSave+=Time.unscaledDeltaTime; if(periodicSave>=10) { periodicSave=0; Save(); } }
        }
        private void Refresh()
        {
            if(session==null||choosing) return;
            if(viewport==null||viewportSessionId!=session.SessionId||viewport.Columns!=session.Game.Width||viewport.Rows!=session.Game.Height) SetupBoard();
            Layout(false); board.Render(session.Game.GetSnapshot());
            remaining.text=session.IsTerminal?(session.Game.State==MinesweeperState.Won?"排雷成功":"触雷了"):"标记 "+session.Game.RemainingMarkers;
            remaining.color=session.Game.RemainingMarkers<0?UiFactory.Hex("B34842"):UiFactory.Ink;
            focusRemaining.text=remaining.text; focusRemaining.color=remaining.color;
            difficulty.text=MinesweeperRules.Get(session.Game.GetSnapshot().Difficulty).Name+"\n"+session.Game.Width+" × "+session.Game.Height+" · "+session.Game.MineCount+" 雷";
            timer.text=FormatTime(session.ElapsedMilliseconds,false); focusTimer.text=timer.text;
            bool cover=session.Paused||session.Blocked||!session.Foreground;
            pauseCover.SetActive(cover); resumeButton.gameObject.SetActive(session.Paused&&!session.Blocked&&session.Foreground&&!session.IsTerminal);
            pauseText.text=session.Blocked?"游戏已遮挡":!session.Foreground?"游戏已暂停":"已暂停";
            pauseButton.interactable=focusPause.interactable=!session.IsTerminal&&!cover;
            RenderView();
        }
        private void RenderView()
        {
            if(viewport==null) return;
            board.RenderView(viewport);
            bool overview=viewport.IsOverview, view=ViewAllowed;
            zoomLabel.text=overview?"全盘":Math.Round(viewport.ZoomScale*100)+"%";
            overviewLabel.text=overview?"返回局部":"总览";
            overviewNote.gameObject.SetActive(overview);
            zoomOut.interactable=view&&!overview&&viewport.ZoomScale>.5;
            zoomIn.interactable=view&&!overview&&viewport.ZoomScale<2;
            zoomValue.interactable=view&&!overview; overviewButton.interactable=view;
            revealButton.interactable=flagButton.interactable=CellAllowed;
            ModeColor(revealButton,session.InputMode==MinesweeperInputMode.Reveal);
            ModeColor(flagButton,session.InputMode==MinesweeperInputMode.Flag);
            enterFullscreen.interactable=exitFullscreen.interactable=dialog==null&&!overview;
            hint.text=overview?"总览仅观察和定位 · 进行中的对局继续计时":viewport.ZoomScale<1?
                (session.InputMode==MinesweeperInputMode.Flag?"插旗模式":"翻开模式")+" · 小格视图 · 点击倍率恢复 100%":
                session.InputMode==MinesweeperInputMode.Flag?"点击插旗 / 拔旗 · 双指缩放和平移":"点击翻开 · 长按即标记 · 双指缩放和平移";
            if(!overview&&session.Game.RemainingMarkers<0) hint.text="标记数超过总雷数 · 旗帜仅代表你的判断";
        }
        internal static string FormatTime(long ms,bool precision)
        {
            long seconds=ms/1000;
            string formatted=seconds>=3600?(seconds/3600)+":"+(seconds/60%60).ToString("00")+":"+(seconds%60).ToString("00"):
                (seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
            return precision?formatted+"."+(ms%1000).ToString("000"):formatted;
        }
        private void OnDestroy() { CancelInput(); feedback?.Stop(); }
    }
}
