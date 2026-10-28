using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Snake;

namespace XanaduYonder.Runtime.Snake
{
    internal sealed partial class SnakePage : MonoBehaviour
    {
        private RectTransform root;
        private SnakeCatalog catalog;
        private SnakeSession session;
        private SnakeProfile profile;
        private SnakeSaveStore store;
        private SnakeBoardView board;
        private SnakeInput input;
        private CanvasGroup gameInput;
        private SnakeDropdown levelDropdown;
        private Button modeButton, pauseButton, boundaryButton;
        private Text scoreText, progressText, modeText, countdownText, warningText;
        private GameObject dialog, countdown, warningBanner;
        private RectTransform boardBand, controls;
        private Image boundaryTrack;
        private RectTransform boundaryThumb;
        private SnakeIconGraphic boundaryLock;
        private GameObject[] exits;
        private Action returnLobby, dismissDialog, returnFromDropdown;
        private int interactionEpoch;
        private Action<Action> openSettings;
        private Func<bool> acceptsInput, soundEnabled, hapticsEnabled;
        private Action<string> setStatus;
        private GameFeedback feedback;
        private bool visible, foreground = true, sessionStarted, saveFailed;
        private float saveElapsed;
        private string warning, loadWarning, resultShown, hostSaveWarning;
        private Vector2 lastSize;
        private int lastCountdown;
        private static readonly string[] Difficulties = { "轻松", "标准", "挑战" };
        public event Action Changed;
        public string SaveError { get { return saveFailed ? warning : null; } }
        public int CompletedCount { get { return profile == null ? 0 : profile.CompletedCount; } }
        public int BestScore
        {
            get { int best=0; if(profile!=null)for(int d=0;d<3;d++)foreach(SnakeBoundary b in new[]{SnakeBoundary.Wrap,SnakeBoundary.Solid})best=Math.Max(best,profile.GetBestScore(d,b));return best; }
        }
        private bool Available { get { return visible && foreground && acceptsInput != null && acceptsInput(); } }
        private bool Interactive { get { return Available && dialog == null && !levelDropdown.IsExpanded && !input.IsQuarantined && session != null && session.CanPlay; } }
        public void Initialize(RectTransform parent, Action lobby, Action<Action> settings, Func<bool> canInput,
            Func<bool> sound, Func<bool> haptics, GameFeedback commonFeedback, Action<string> status, string saveDirectory = null)
        {
            root=parent; returnLobby=lobby; openSettings=settings; acceptsInput=canInput;
            soundEnabled=sound; hapticsEnabled=haptics; feedback=commonFeedback; setStatus=status;
            var resource=Resources.Load<TextAsset>("Snake/levels");
            if(resource==null)throw new InvalidOperationException("The offline Snake levels are missing.");
            catalog=SnakeCatalog.Parse(resource.text);
            store=new SnakeSaveStore(saveDirectory ?? Application.persistentDataPath,catalog);
            profile=store.Load(out loadWarning) ?? new SnakeProfile(catalog);
            LoadSelected(); BuildLayout(); Refresh();
        }
        private void LoadSelected()
        {
            SnakeRules rules;
            SnakeSessionData saved;
            if(profile.SelectedMode==1)
            {
                int level=Mathf.Clamp(profile.SelectedLevel,0,Math.Min(profile.UnlockedLevel,catalog.Count-1));
                profile.SelectedLevel=level; rules=catalog.Get(level);
                saved=profile.CampaignLevel==level?profile.CampaignSlot:null;
            }
            else { rules=SnakeRules.Classic(profile.SelectedDifficulty,profile.SelectedBoundary);saved=profile.GetClassic(profile.SelectedDifficulty,profile.SelectedBoundary); }
            sessionStarted=saved!=null;
            session=saved==null?new SnakeSession(new SnakeGame(rules,NewSeed())):new SnakeSession(rules,saved);
        }
        private static uint NewSeed() { uint seed=BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(),0);return seed==0?1u:seed; }
        public void Enter()
        {
            visible=true; gameObject.SetActive(true); foreground=true; resultShown=null;
            session.Pause(); input.Cancel(); Refresh();
            if(!string.IsNullOrEmpty(loadWarning))
            {
                string message=loadWarning;
                ShowDialog("贪吃蛇进度",message,"进入游戏",()=>{loadWarning=null;StartOrShowResult();});
            }
            else StartOrShowResult();
        }
        public void Leave()
        {
            if(session==null)return;
            InvalidateOverlayReturn();visible=false; levelDropdown?.Hide(); input?.Cancel(); session.Pause(); board?.FinishAnimation();
            CloseDialog(); Save(); gameObject.SetActive(false);
        }
        public void Suspend()
        {
            if(session==null)return;
            returnFromDropdown=null;levelDropdown?.Hide(); input?.Cancel(); session.Pause(); board?.FinishAnimation();
            countdown?.SetActive(false); Save();
        }
        public void SetForeground(bool value)
        {
            foreground=value;
            if(!value) { InvalidateOverlayReturn();Suspend();CloseDialog(); }
            else if(visible) ShowCurrentState();
        }
        public void Save()
        {
            if(profile==null||store==null||session==null)return;
            if(sessionStarted)profile.Capture(session);
            string error;
            if(!store.TrySave(profile,out error))
            {
                saveFailed=true;warning="贪吃蛇进度暂未保存，请检查设备空间。";
                Debug.LogWarning("Snake save failed: "+error);setStatus?.Invoke(warning);
            }
            else if(saveFailed) { saveFailed=false;warning=null;setStatus?.Invoke(""); }
            RefreshWarning();
            Changed?.Invoke();
        }
        public void SetHostSaveWarning(string value) { hostSaveWarning=value;RefreshWarning(); }
        private void RefreshWarning()
        {
            if(warningBanner==null)return;
            var message=saveFailed?warning:hostSaveWarning;
            warningBanner.SetActive(!string.IsNullOrEmpty(message));warningText.text=message??"";
        }
        private void Update()
        { AdvanceFrame(Time.unscaledDeltaTime); }
        // One foreground frame path; Editor checks drive the same path with deterministic deltas.
        private void AdvanceFrame(float deltaSeconds)
        { AdvancePointerFrame(deltaSeconds, SnakeInput.CaptureFrame()); }
        private void AdvancePointerFrame(float deltaSeconds, SnakePointerFrame pointerFrame)
        {
            if(!visible||session==null)return;
            // Cancellation must clear queued turns before this same frame can consume a tick.
            // Do not rely on the execution order of two MonoBehaviour.Update callbacks.
            input.PollFrame(pointerFrame);
            if(root.rect.size!=lastSize) { input.Cancel();session.ClearInput();board.FinishAnimation();Layout(); }
            gameInput.interactable=Available&&dialog==null;
            if(!Available)
            {
                if(!session.Paused||session.CountdownSeconds>0)Suspend();
                return;
            }
            if(dialog!=null||levelDropdown.IsExpanded)return;
            if(Interactive)
            {
                if(Input.GetKeyDown(KeyCode.UpArrow)||Input.GetKeyDown(KeyCode.W))RequestDirection(SnakeDirection.Up);
                if(Input.GetKeyDown(KeyCode.RightArrow)||Input.GetKeyDown(KeyCode.D))RequestDirection(SnakeDirection.Right);
                if(Input.GetKeyDown(KeyCode.DownArrow)||Input.GetKeyDown(KeyCode.S))RequestDirection(SnakeDirection.Down);
                if(Input.GetKeyDown(KeyCode.LeftArrow)||Input.GetKeyDown(KeyCode.A))RequestDirection(SnakeDirection.Left);
            }
            bool wasRunning=session.CanPlay||session.CountdownSeconds>0;
            int before=session.Game.FoodEaten;
            int ticks=session.Advance(deltaSeconds);
            if(ticks>0)
            {
                // The renderer interpolates one-cell motion. A catch-up frame may contain a turn,
                // so display its final committed snapshot instead of inventing a cross-board path.
                board.Render(session.Game.Rules,session.Game.GetSnapshot(),ticks==1,session.Game.CurrentTickMilliseconds/1000f*.8f);
                RefreshHud();
                if(session.Game.FoodEaten>before) { feedback?.Play(true,soundEnabled(),hapticsEnabled());Save(); }
                if(session.Game.State!=SnakeState.Playing)
                {
                    Save();
                    // Effects belong to the transition, not to reopening an existing result panel.
                    if(session.Game.State==SnakeState.Lost)feedback?.Miss(soundEnabled(),hapticsEnabled());
                    else feedback?.Land(true,soundEnabled(),hapticsEnabled());
                    CheckEndState();return;
                }
            }
            int count=Mathf.CeilToInt((float)session.CountdownSeconds);
            countdown.SetActive(count>0);
            if(count>0) { countdownText.text=count.ToString();if(count!=lastCountdown)feedback?.Play(false,soundEnabled(),false); }
            lastCountdown=count;
            if(session.CanPlay)
            { saveElapsed+=deltaSeconds;if(saveElapsed>=2){saveElapsed=0;Save();} }
            if(wasRunning&&session.Paused&&session.CountdownSeconds<=0)ShowPause();
        }
        private void RequestDirection(SnakeDirection direction) { if(Interactive)session.QueueDirection(direction); }
        private void OnDisable() { InvalidateOverlayReturn();input?.Cancel();session?.Pause(); }
        private void StartClassic(int difficulty,SnakeBoundary boundary,bool fresh)
        {
            InvalidateOverlayReturn();Suspend();CloseDialog();
            profile.SelectedMode=0;profile.SelectedDifficulty=difficulty;profile.SelectedBoundary=boundary;
            var rules=SnakeRules.Classic(difficulty,boundary);var saved=fresh?null:profile.GetClassic(difficulty,boundary);
            if(saved!=null&&saved.Board.State!=SnakeState.Playing)saved=null;
            session=saved==null?new SnakeSession(new SnakeGame(rules,NewSeed())):new SnakeSession(rules,saved);
            sessionStarted=true;resultShown=null;saveElapsed=0;Refresh();Save();StartOrShowResult();
        }
        private void StartCampaign(int level,bool fresh)
        {
            if(level<0||level>=catalog.Count||level>profile.UnlockedLevel)return;
            InvalidateOverlayReturn();Suspend();CloseDialog();profile.SelectedMode=1;profile.SelectedLevel=level;
            var saved=!fresh&&profile.CampaignLevel==level?profile.CampaignSlot:null;
            if(saved!=null&&saved.Board.State!=SnakeState.Playing)saved=null;
            session=saved==null?new SnakeSession(new SnakeGame(catalog.Get(level),NewSeed())):new SnakeSession(catalog.Get(level),saved);
            sessionStarted=true;resultShown=null;saveElapsed=0;Refresh();Save();StartOrShowResult();
        }
        private void Refresh()
        {
            if(session==null||board==null)return;
            board.Render(session.Game.Rules,session.Game.GetSnapshot());RefreshHud();
            var rules=session.Game.Rules;
            levelDropdown.SetWidth(rules.Mode==SnakeMode.Classic?164:260);
            levelDropdown.ClearOptions();
            if(rules.Mode==SnakeMode.Classic)levelDropdown.AddOptions(new System.Collections.Generic.List<string>(Difficulties));
            else
            {
                var options=new System.Collections.Generic.List<string>();
                for(int i=0;i<catalog.Count;i++)options.Add((i+1).ToString("00")+" "+catalog.Name(i)+(i>profile.UnlockedLevel?" · 锁定":""));
                levelDropdown.AddOptions(options);
            }
            levelDropdown.SetValueWithoutNotify(rules.Mode==SnakeMode.Classic?rules.Difficulty:profile.SelectedLevel);levelDropdown.RefreshShownValue();
            bool wrap=rules.Boundary==SnakeBoundary.Wrap;
            boundaryButton.interactable=rules.Mode==SnakeMode.Classic;
            boundaryTrack.color=wrap?UiFactory.Accent:UiFactory.Hex("AEBCB0");
            boundaryThumb.anchoredPosition=new Vector2(wrap?26:4,-4);
            boundaryLock.gameObject.SetActive(rules.Mode==SnakeMode.Campaign);
            foreach(var exit in exits)exit.SetActive(wrap);
            Layout();
        }
        private void RefreshHud()
        {
            var game=session.Game;var rules=game.Rules;
            modeText.text=rules.Mode==SnakeMode.Classic?"经典无限":"障碍闯关";
            scoreText.text="得分 "+HudNumber(game.Score.ToString());
            progressText.text=rules.Mode==SnakeMode.Classic?"长度 "+HudNumber(game.Body.Length.ToString()):"果实 "+HudNumber(game.FoodEaten+"/"+rules.TargetFood);
            pauseButton.interactable=game.State==SnakeState.Playing;
        }
        private static string HudNumber(string value) { return "<color=#173D35><size=32><b>"+value+"</b></size></color>"; }
        public void CheckEndState()
        {
            if(!Available||session==null||session.Game.State==SnakeState.Playing||dialog!=null)return;
            if(resultShown==session.SessionId)return;
            resultShown=session.SessionId;Save();ShowResult();
        }
        private void StartOrShowResult()
        { if(session.Game.State==SnakeState.Playing)ContinueGame();else ShowCurrentState(); }
        private void ShowCurrentState()
        { if(session.Game.State==SnakeState.Playing)ShowPause();else{resultShown=null;CheckEndState();} }
        private Action CaptureOverlayReturn()
        {
            bool resume=session.CanPlay||session.CountdownSeconds>0;
            string sessionId=session.SessionId;int epoch=interactionEpoch;
            return ()=>
            {
                // A background/leave event or a different session invalidates old UI callbacks.
                if(!Available||epoch!=interactionEpoch||session.SessionId!=sessionId)return;
                if(resume&&session.Game.State==SnakeState.Playing)ContinueGame();
                else ShowCurrentState();
            };
        }
        private void InvalidateOverlayReturn() { interactionEpoch++;returnFromDropdown=null; }
        private void ReturnFromDropdown()
        {
            var after=returnFromDropdown;returnFromDropdown=null;
            if(Available&&dialog==null&&session.Paused)after?.Invoke();
        }
        public void Back()
        {
            // A preview can already be visible while UGUI is still fading its old picker.
            if(dialog!=null){dismissDialog?.Invoke();return;}
            if(levelDropdown.IsExpanded){levelDropdown.Hide();ReturnFromDropdown();return;}
            ShowPause();
        }
    }
}
