using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Dino;

namespace XanaduYonder.Runtime.Dino
{
    internal sealed class DinoPage : MonoBehaviour
    {
        private enum Phase { Selecting, Turning, Ready, Running, Paused, Countdown, Finished, Unavailable }
        private enum Destination { Game, Selection, Lobby }
        private RectTransform root, canvas, content, overlay;
        private DinoViewport viewport;
        private DinoTrackView track;
        private DinoAudio sound;
        private DinoSaveStore store;
        private DinoSaveData profile;
        private DinoGame game;
        private readonly DinoActionOwners owners = new DinoActionOwners();
        private readonly DinoRunClock clock = new DinoRunClock();
        private readonly List<DinoTouchControl> touches = new List<DinoTouchControl>();
        private Action goHome;
        private Action<bool> requestOrientation;
        private Action toggleSound, toggleHaptics;
        private Func<bool> soundEnabled, hapticsEnabled, foreground;
        private Action overlayBack;
        private Phase phase;
        private Destination destination;
        private bool active, landscape, selectedLandscape, turningLandscape, inheritedDuck, dirty;
        private float turnStarted, unlockAt, lastSave;
        private int stableFrames;
        private long shownScore = -1, shownBest = -1, milestone, startingBest;
        private bool windowBlocked, pendingLayoutChange, waitingForNewGame;
        private Text scoreLabel, bestLabel, stateLabel, warningLabel;
        public event Action Changed;
        public string SaveError { get; private set; }
        public long PortraitBest => profile == null ? 0 : profile.BestPortraitScore;
        public long LandscapeBest => profile == null ? 0 : profile.BestLandscapeScore;
        public string CurrentPhase => phase.ToString();
        public double WorldWidth => game == null ? 0 : game.Rules.ViewportWidth;

        public void Initialize(RectTransform pageRoot, RectTransform canvasRoot, Action home, Action<bool> orient,
            Func<bool> isForeground, Func<bool> isSoundEnabled, Func<bool> isHapticsEnabled,
            Action changeSound, Action changeHaptics)
        {
            root = pageRoot; canvas = canvasRoot; goHome = home; requestOrientation = orient;
            foreground = isForeground; soundEnabled = isSoundEnabled; hapticsEnabled = isHapticsEnabled;
            toggleSound = changeSound; toggleHaptics = changeHaptics;
            viewport = new DinoViewport(canvas, root);
            store = new DinoSaveStore(Application.persistentDataPath);
            string warning;
            profile = store.Load(out warning);
            SaveError = warning;
            sound = gameObject.AddComponent<DinoAudio>();
            sound.Initialize(soundEnabled, hapticsEnabled);
            gameObject.SetActive(false);
        }

        public void Enter()
        {
            active = true; gameObject.SetActive(true);
            selectedLandscape = profile.PreferredOrientation == "landscape";
            BeginTurn(false, Destination.Selection);
        }

        public void Leave()
        {
            if (!active) return;
            Save(); active = false;
            ClearPhysicalInputs(); clock.Reset(); sound.Stop();
            requestOrientation(false);
            gameObject.SetActive(false);
        }

        private void BeginTurn(bool targetLandscape, Destination next)
        {
            ClearPhysicalInputs(); clock.Reset(); sound.Stop(); CloseOverlay();
            waitingForNewGame = false;
            phase = Phase.Turning; turningLandscape = targetLandscape; destination = next;
            stableFrames = 0; turnStarted = Time.unscaledTime;
            requestOrientation(targetLandscape);
            viewport.Refresh();
            BuildTurning();
        }

        private void BuildTurning()
        {
            ClearContent();
            Label("正在调整画面…", 24, viewport.Height * .4f, viewport.Width-48, 64, 32, TextAnchor.MiddleCenter);
            Button("取消", viewport.Width*.5f-100, viewport.Height*.6f, 200, 72,
                () => BeginTurn(false, destination == Destination.Selection ? Destination.Lobby : Destination.Selection));
        }

        private void Update()
        {
            if (!active || viewport == null) return;
            bool changed = viewport.Refresh() || pendingLayoutChange;
            if (!foreground())
            {
                pendingLayoutChange |= changed;
                if (phase == Phase.Running || phase == Phase.Countdown) Suspend();
                if (phase == Phase.Turning) turnStarted = Time.unscaledTime;
                return;
            }
            pendingLayoutChange = false;
            if (phase == Phase.Turning)
            {
                if (changed) BuildTurning();
                stableFrames = viewport.Landscape == turningLandscape && !changed ? stableFrames+1 : 0;
                if (stableFrames >= 2) FinishTurn();
                else if (Time.unscaledTime-turnStarted > 3) ShowOrientationFailure();
                return;
            }
            if (changed)
            {
                if (phase == Phase.Selecting) BuildSelection();
                else if (phase == Phase.Ready || waitingForNewGame) StartNewGame();
                else if (game != null)
                {
                    Suspend();
                    windowBlocked = !CanKeepCurrentWindow();
                    BuildGame();
                    if (game.IsGameOver) ShowResult();
                    else if (windowBlocked) ShowWindowChanged();
                    else ShowPauseMenu();
                }
            }
            if (game == null) return;
            HandleKeyboard();
            if (phase == Phase.Running)
            {
                if (!clock.AddFrame(Time.unscaledDeltaTime)) Suspend();
                else
                {
                    while (clock.ConsumeStep() && !game.IsGameOver) game.Step();
                    UpdateRecord();
                    if (game.IsGameOver) EndRun();
                    else if (game.Score / 100 > milestone) { milestone = game.Score/100; sound.PlayScore(); }
                    if (phase == Phase.Running)
                        stateLabel.text = game.Elapsed >= 40 && game.Elapsed < 45
                            ? "翼龙将出现\n低飞跳跃 · 中空下蹲" : "";
                }
            }
            else if (phase == Phase.Countdown)
            {
                if (Time.unscaledDeltaTime > .1f) Suspend();
                else if (clock.AdvanceCountdown(Time.unscaledDeltaTime))
                {
                    phase = Phase.Running; stateLabel.text = "";
                }
                else stateLabel.text = clock.CountdownNumber.ToString();
            }
            RefreshScore();
            if (track != null)
            {
                track.Render(game, phase == Phase.Running || phase == Phase.Ready ? Time.unscaledDeltaTime : 0);
                if (stateLabel != null) stateLabel.color = track.ForegroundColor;
            }
            if (dirty && Time.unscaledTime-lastSave >= 1) Save();
        }

        private void FinishTurn()
        {
            unlockAt = Time.unscaledTime + .15f;
            if (destination == Destination.Lobby) { FinishHome(); return; }
            if (destination == Destination.Selection) { game = null; phase = Phase.Selecting; BuildSelection(); return; }
            landscape = turningLandscape;
            StartNewGame();
        }

        private void ShowOrientationFailure()
        {
            phase = Phase.Unavailable; ClearContent();
            Label("暂时无法切换方向", 24, viewport.Height*.3f, viewport.Width-48, 64, 30, TextAnchor.MiddleCenter);
            Label("请退出分屏或窗口模式后重试。",24,viewport.Height*.3f+80,viewport.Width-48,64,24,TextAnchor.MiddleCenter);
            Button("重试",viewport.Width*.5f-190,viewport.Height*.6f,180,76,()=>BeginTurn(turningLandscape,destination),true);
            Button("返回",viewport.Width*.5f+10,viewport.Height*.6f,180,76,()=>BeginTurn(false,Destination.Selection));
        }

        private void BuildSelection()
        {
            ClearContent();
            float w=viewport.Width, h=viewport.Height;
            Button("返回",24,20,110,96,()=>BeginTurn(false,Destination.Lobby));
            Label("小恐龙",150,20,w-300,64,36,TextAnchor.MiddleCenter);
            Label("选择游玩方向",40,142,w-80,72,46);
            Label("经典像素跑酷 · 选好方向再出发",40,222,w-80,48,25);
            float cardTop=Mathf.Max(300,h*.27f), cardHeight=Mathf.Min(190,(h-560)/2);
            Button((selectedLandscape?"":"✓  ")+"竖屏\n竖握即玩，跑道铺满屏宽\n最高纪录  "+PortraitBest,40,cardTop,w-80,cardHeight,
                ()=>{selectedLandscape=false;BuildSelection();},!selectedLandscape);
            Button((selectedLandscape?"✓  ":"")+"横屏\n视野更宽，适合双手操作\n最高纪录  "+LandscapeBest,40,cardTop+cardHeight+24,w-80,cardHeight,
                ()=>{selectedLandscape=true;BuildSelection();},selectedLandscape);
            Button(selectedLandscape?"横屏开始":"竖屏开始",40,h-238,w-80,96,()=>BeginTurn(selectedLandscape,Destination.Game),true);
            Label("记住上次选择 · 两种方向分别记录最高分",24,h-115,w-48,40,21,TextAnchor.MiddleCenter);
            warningLabel=Label(SaveError??"本局退出后重新开始",24,h-68,w-48,40,20,TextAnchor.MiddleCenter);
        }

        private void StartNewGame()
        {
            double worldWidth = Math.Min(4096, viewport.Width / 1.2);
            if (!WindowFits(worldWidth))
            {
                waitingForNewGame = true; windowBlocked = true; phase = Phase.Unavailable;
                ClearContent();
                var panel = OpenPanel("请调整游戏窗口", 420);
                PanelLabel(panel, "请恢复所选方向并留出足够的跑道高度。\n调整完成后会进入准备画面。", 100, 120, 24);
                PanelButton(panel, "重新检查", 230, StartNewGame, true);
                PanelButton(panel, "重新选择方向", 318, () => BeginTurn(false, Destination.Selection));
                overlayBack = () => BeginTurn(false, Destination.Selection);
                return;
            }
            ClearPhysicalInputs(); clock.Reset(); inheritedDuck=false; milestone=0;
            uint seed=unchecked((uint)Environment.TickCount ^ (uint)DateTime.UtcNow.Ticks);
            game = new DinoGame(new DinoRules(worldWidth), seed);
            startingBest = profile.GetBest(landscape); windowBlocked = waitingForNewGame = false;
            profile.PreferredOrientation = landscape ? "landscape" : "portrait";
            dirty = true; Save();
            phase=Phase.Ready; BuildGame(); unlockAt=Time.unscaledTime+.15f;
        }

        private void BuildGame()
        {
            var presentation = track == null ? default(DinoTrackView.PresentationState) : track.Capture(game);
            ClearContent();
            float w=viewport.Width,h=viewport.Height;
            bool wide=viewport.Landscape;
            Button("首页",24,18,108,96,Back);
            Label("小恐龙",wide?152:150,18,wide?180:w-300,66,wide?30:36,wide?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter);
            Button("设置",w-132,18,108,96,ShowSettings);
            if (wide)
            {
                scoreLabel=Label("",w*.36f,22,w*.21f,60,30);
                bestLabel=Label("",w*.59f,22,w*.26f,60,23);
            }
            else
            {
                scoreLabel=Label("",24,122,w*.45f,94,43);
                bestLabel=Label("",w*.52f,122,w*.45f-24,94,29,TextAnchor.MiddleRight);
            }
            float top=wide?130:250;
            float trackHeight=Mathf.Max(240,h-top-(wide?220:320));
            var trackRoot=UiFactory.Rect(content,"Dino full width track",0,top,w,trackHeight);
            track=trackRoot.gameObject.AddComponent<DinoTrackView>();
            track.Initialize(trackRoot); track.SetLayout(w,trackHeight,game.Rules.ViewportWidth);
            track.Restore(game, presentation); track.Render(game,0);
            var touch=UiFactory.Panel(trackRoot,"Track jump input",0,0,w,trackHeight,Color.clear,false);
            touch.raycastTarget=true;
            AddTouch(touch.gameObject,JumpDown,JumpUp);
            stateLabel=UiFactory.Label(trackRoot,"",20,trackHeight*.25f,w-40,120,42,UiFactory.Ink,TextAnchor.MiddleCenter,FontStyle.Bold);
            if (phase==Phase.Ready) stateLabel.text="按下跳跃开始";
            float controlY=wide?h-194:h-258;
            float duckW=wide?196:Mathf.Min(218,w*.31f),jumpW=wide?Mathf.Min(260,w*.5f-142):w-duckW-72;
            AddActionButton("下蹲\n按住",24,controlY,duckW,wide?134:154,false,DuckDown,DuckUp);
            AddActionButton("跳跃\n轻按短跳 · 按住长跳",w-24-jumpW,controlY,jumpW,wide?134:154,true,JumpDown,JumpUp);
            if (wide) Button("暂停",w*.5f-94,controlY+14,188,110,ShowPause).gameObject.SetActive(phase!=Phase.Ready);
            else
            {
                Button("玩法说明",24,h-100,180,96,ShowHelp);
                Button("暂停",w-164,h-100,140,96,ShowPause);
            }
            warningLabel=Label(SaveError??"最高纪录自动保存",wide?230:24,wide?h-72:controlY-56,wide?w-520:w-48,42,18,TextAnchor.MiddleCenter);
            if (phase==Phase.Ready) Button("更换方向",wide?w*.5f-94:w*.5f-92,wide?controlY+14:h-100,184,wide?110:96,
                ()=>BeginTurn(false,Destination.Selection));
            shownScore=shownBest=-1; RefreshScore();
        }

        private void JumpDown(int id)
        {
            if (!CanPlay || game.Jumping || owners.DuckHeld) return;
            if (!owners.PressJump(id)) return;
            if(inheritedDuck) { game.SetDuck(false); inheritedDuck=false; }
            if(game.Jump())
            {
                if(phase==Phase.Ready) { phase=Phase.Running; clock.Reset(); stateLabel.text=""; HideReadyControls(); }
                sound.PlayJump();
            }
        }
        private void JumpUp(int id) { if(owners.ReleaseJump(id) && game!=null) game.ReleaseJump(); }
        private void DuckDown(int id)
        {
            if (!(CanPlay || (phase==Phase.Countdown && overlay==null && foreground())) || game==null) return;
            if(owners.PressDuck(id)) { inheritedDuck=false; game.SetDuck(true); }
        }
        private void DuckUp(int id) { if(owners.ReleaseDuck(id) && game!=null) game.SetDuck(false); }
        private bool CanPlay => active && game!=null && !windowBlocked && viewport.Landscape == landscape && overlay==null && foreground() && Time.unscaledTime>=unlockAt &&
            (phase==Phase.Ready || phase==Phase.Running);
        private void HideReadyControls()
        {
            // The ready-only direction control must not remain an unconfirmed exit during a run.
            foreach(Transform child in content)
            {
                if(child.name=="更换方向") child.gameObject.SetActive(false);
                if(child.name=="暂停") child.gameObject.SetActive(true);
            }
        }
        private void HandleKeyboard()
        {
            if(Input.GetKeyDown(KeyCode.Space)) JumpDown(-101);
            if(Input.GetKeyDown(KeyCode.UpArrow)) JumpDown(-102);
            if(Input.GetKeyDown(KeyCode.DownArrow)) DuckDown(-103);
            if(Input.GetKeyUp(KeyCode.Space)) JumpUp(-101);
            if(Input.GetKeyUp(KeyCode.UpArrow)) JumpUp(-102);
            if(Input.GetKeyUp(KeyCode.DownArrow)) DuckUp(-103);
        }
        private void ClearPhysicalInputs()
        {
            inheritedDuck = inheritedDuck || owners.DuckHeld || (game!=null && game.Ducking);
            owners.Clear();
            foreach(var input in touches) if(input!=null) input.ClearWithoutRelease();
        }
        public void Suspend()
        {
            if(!active) return;
            ClearPhysicalInputs(); clock.Reset(); sound.Stop(); Save();
            if(phase==Phase.Running || phase==Phase.Countdown)
            {
                phase=Phase.Paused;
                if(overlay==null) ShowPauseMenu();
            }
        }
        private void ShowPause() { Suspend(); if(game!=null && !game.IsGameOver) ShowPauseMenu(); }
        private void ShowPauseMenu()
        {
            var panel=OpenPanel(game!=null && game.IsStarted?"已暂停":"准备出发",480);
            PanelButton(panel,"继续游戏",130,Continue,true);
            PanelButton(panel,"玩法说明",214,ShowHelp);
            PanelButton(panel,"设置",298,ShowSettings);
            PanelButton(panel,"结束本局 / 更换方向",382,ConfirmChange);
            overlayBack=RequestExit;
        }
        private void Continue()
        {
            if (windowBlocked || !CanKeepCurrentWindow()) { ShowWindowChanged(); return; }
            CloseOverlay(); ClearPhysicalInputs(); unlockAt=Time.unscaledTime+.15f;
            if(game==null) return;
            if(game.IsGameOver) { phase=Phase.Finished; ShowResult(); return; }
            if(!game.IsStarted) {phase=Phase.Ready;stateLabel.text="按下跳跃开始";return;}
            phase=Phase.Countdown;clock.BeginCountdown();stateLabel.text="3";
        }
        private void ShowSettings()
        {
            Suspend();
            var panel=OpenPanel("共用设置",480);
            PanelLabel(panel,"声音和触感在所有游戏中共用",100,62,22);
            PanelButton(panel,"声音 · "+(soundEnabled()?"开启":"关闭"),185,()=>{toggleSound();sound.Stop();ShowSettings();});
            PanelButton(panel,"触感 · "+(hapticsEnabled()?"开启":"关闭"),273,()=>{toggleHaptics();ShowSettings();});
            PanelButton(panel,"完成",370,ReturnFromInfo,true);
            overlayBack=ReturnFromInfo;
        }
        private void ReturnFromInfo()
        {
            CloseOverlay();
            if(game!=null && game.IsGameOver) ShowResult();
            else if(game!=null && game.IsStarted) ShowPauseMenu();
        }
        private void ShowHelp()
        {
            Suspend();var panel=OpenPanel("小恐龙怎么玩",560);
            PanelLabel(panel,"按下跳跃，松手短跳、按住长跳。\n按住下蹲；空中按住可快速落地。\n跳过仙人掌与低空翼龙，蹲过中空翼龙。\n跑得越远分越高，横竖屏分别计纪录。\n返回前台先暂停，继续前倒数三秒。\n退出本局后重新开始，最高纪录保留。",110,270,24);
            PanelButton(panel,"素材与许可",398,ShowLicense);
            PanelButton(panel,"知道了",480,ReturnFromInfo,true);overlayBack=ReturnFromInfo;
        }
        private void ShowLicense()
        {
            var panel=OpenPanel("Chromium 素材许可",Mathf.Min(840,viewport.Height-40));
            float panelW=panel.rect.width, panelH=panel.rect.height;
            var area=UiFactory.Rect(panel,"License viewport",24,100,panelW-48,panelH-210);
            var img=area.gameObject.AddComponent<Image>();img.color=Color.clear;img.raycastTarget=true;
            area.gameObject.AddComponent<RectMask2D>();
            var scroll=area.gameObject.AddComponent<ScrollRect>();scroll.viewport=area;scroll.horizontal=false;
            var body=UiFactory.Rect(area,"License body",0,0,area.rect.width,1000);
            var asset=Resources.Load<TextAsset>("Dino/LICENSE");
            string text="像素与音效来源：Chromium\n固定版本 64574fa0f2daa1b48f2703a072687272000178ce\n本项目不代表 Google 官方产品。\n\n"+(asset==null?"素材许可证未能加载。":asset.text);
            var label=UiFactory.Label(body,text,0,0,body.rect.width,1000,19,UiFactory.Ink,TextAnchor.UpperLeft);
            label.supportRichText=false;Canvas.ForceUpdateCanvases();
            body.sizeDelta=new Vector2(body.rect.width,Mathf.Max(area.rect.height,label.preferredHeight+16));
            label.rectTransform.sizeDelta=body.sizeDelta;scroll.content=body;
            PanelButton(panel,"返回玩法",panelH-90,ShowHelp,true);overlayBack=ShowHelp;
        }
        private void ConfirmChange()
        {
            if(game==null || !game.IsStarted || game.IsGameOver) { BeginTurn(false,Destination.Selection);return; }
            var panel=OpenPanel("结束本局并更换方向？",420);
            PanelLabel(panel,"当前对局会结束，最高纪录保留。",105,100,24);
            PanelButton(panel,"结束并选择方向",230,()=>{Save();BeginTurn(false,Destination.Selection);},true);
            PanelButton(panel,"继续当前游戏",318,ShowPauseMenu);overlayBack=ShowPauseMenu;
        }
        public void Back()
        {
            if(!active)return;
            if(overlay!=null) { overlayBack?.Invoke();return; }
            if(phase==Phase.Turning) {BeginTurn(false,Destination.Selection);return;}
            if(game==null || !game.IsStarted || game.IsGameOver) {BeginTurn(false,Destination.Lobby);return;}
            RequestExit();
        }
        private void RequestExit()
        {
            Suspend();
            var panel=OpenPanel("结束本局并返回？",420);
            PanelLabel(panel,"最高纪录会保留。\n下次进入将开始新的一局。",105,100,24);
            PanelButton(panel,"结束本局并返回",230,()=>{Save();BeginTurn(false,Destination.Lobby);},true);
            PanelButton(panel,"继续游戏",318,Continue);overlayBack=ShowPauseMenu;
        }
        private bool CanKeepCurrentWindow()
        {
            if (game == null) return true;
            // Keep the entire original world and all obstacle timestamps. Uniform rendering
            // changes neither travel time nor the collision trajectory; never restart warning clocks.
            return WindowFits(game.Rules.ViewportWidth);
        }
        private bool WindowFits(double worldWidth)
        {
            double scale = viewport.Width / worldWidth;
            double top = viewport.Landscape ? 130 : 250;
            double trackHeight = Math.Max(240, viewport.Height - top - (viewport.Landscape ? 220 : 320));
            return viewport.Landscape == landscape && scale >= .96 &&
                trackHeight / scale >= 212; // 30 ground + 113 maximum jump + 64 sprite + margin.
        }
        private void ShowWindowChanged()
        {
            windowBlocked = !CanKeepCurrentWindow();
            if (!windowBlocked) { ShowPauseMenu(); return; }
            var panel=OpenPanel("等待恢复游戏窗口",500);
            PanelLabel(panel,"本局已经暂停，进度和障碍位置保留。\n请恢复原来的方向或窗口大小后继续。",100,120,24);
            PanelButton(panel,"检查窗口并继续",234,Continue,true);
            PanelButton(panel,"结束并重新选择方向",318,ConfirmChange);
            PanelButton(panel,"返回大厅",402,RequestExit);
            overlayBack=RequestExit;
        }
        private void EndRun()
        {
            phase=Phase.Finished;ClearPhysicalInputs();clock.Reset();sound.PlayHit();Save();ShowResult();
        }
        private void ShowResult()
        {
            var panel=OpenPanel(game.Score > startingBest ? "新纪录！" : "这一局，很精彩",510);
            PanelLabel(panel,"本局  "+game.Score+"\n"+(landscape?"横屏":"竖屏")+"最高  "+profile.GetBest(landscape),105,125,34);
            PanelButton(panel,"再来一局",250,()=>{CloseOverlay();StartNewGame();},true);
            PanelButton(panel,"更换方向",334,()=>BeginTurn(false,Destination.Selection));
            PanelButton(panel,"返回大厅",418,()=>BeginTurn(false,Destination.Lobby));
            overlayBack=()=>BeginTurn(false,Destination.Lobby);
        }
        private void UpdateRecord()
        {
            if(game!=null && profile.Record(landscape,game.Score)) {dirty=true;Changed?.Invoke();}
        }
        public void Save()
        {
            if(profile==null)return;
            UpdateRecord();if(!dirty)return;
            string error;
            if(store.TrySave(profile,out error)) {dirty=false;SaveError=null;}
            else {SaveError="最高纪录暂未保存，请检查设备剩余空间。";Debug.LogWarning("Dino save: "+error);}
            lastSave=Time.unscaledTime;
            if(warningLabel!=null)warningLabel.text=SaveError??"最高纪录自动保存";
        }
        private void RefreshScore()
        {
            if(game==null || scoreLabel==null)return;
            if(shownScore!=game.Score){shownScore=game.Score;scoreLabel.text="本局  "+game.Score.ToString("D5");}
            var best=profile.GetBest(landscape);
            if(shownBest!=best){shownBest=best;bestLabel.text=(landscape?"横屏最高  ":"竖屏最高  ")+best.ToString("D5");}
        }
        private void FinishHome(){Save();goHome();}
        private void ClearContent()
        {
            ClearPhysicalInputs();touches.Clear();CloseOverlay();
            if(content!=null){content.gameObject.SetActive(false);Destroy(content.gameObject);}
            content=UiFactory.Rect(root,"Dino content",0,0,viewport.Width,viewport.Height);
            UiFactory.Panel(content,"Page background",0,0,viewport.Width,viewport.Height,UiFactory.Paper,false);
            track=null;scoreLabel=bestLabel=stateLabel=warningLabel=null;
        }
        private Text Label(string text,float x,float y,float w,float h,int size,TextAnchor anchor=TextAnchor.MiddleLeft)
        {return UiFactory.Label(content,text,x,y,w,h,size,UiFactory.Ink,anchor);}
        private Button Button(string text,float x,float y,float w,float h,Action action,bool primary=false)
        {return UiFactory.Button(content,text,x,y,w,h,action,primary);}
        private void AddActionButton(string title,float x,float y,float w,float h,bool primary,Action<int> down,Action<int> up)
        {
            var panel=UiFactory.Panel(content,title,x,y,w,h,primary?UiFactory.Accent:UiFactory.Hex("E7EDE4"));panel.raycastTarget=true;
            UiFactory.Label(panel.transform,title,10,0,w-20,h,27,primary?Color.white:UiFactory.Ink,TextAnchor.MiddleCenter);
            AddTouch(panel.gameObject,down,up);
        }
        private void AddTouch(GameObject target,Action<int> down,Action<int> up)
        {var input=target.AddComponent<DinoTouchControl>();input.Initialize(down,up);touches.Add(input);}
        private RectTransform OpenPanel(string title,float requestedHeight)
        {
            CloseOverlay();ClearPhysicalInputs();
            overlay=UiFactory.Rect(root,"Dino modal overlay",0,0,viewport.Width,viewport.Height);
            var dim=overlay.gameObject.AddComponent<Image>();dim.color=new Color(.06f,.14f,.1f,.58f);dim.raycastTarget=true;
            float w=Mathf.Min(620,viewport.Width-40),h=Mathf.Min(requestedHeight,viewport.Height-32);
            var panel=UiFactory.Panel(overlay,"Dino dialog",(viewport.Width-w)*.5f,(viewport.Height-h)*.5f,w,h,UiFactory.Paper);
            panel.raycastTarget=true;
            UiFactory.Label(panel.transform,title,24,24,w-48,66,32,UiFactory.Ink,TextAnchor.MiddleCenter,FontStyle.Bold);
            return panel.rectTransform;
        }
        private void PanelLabel(RectTransform panel,string message,float y,float h,int size)
        {UiFactory.Label(panel,message,24,y,panel.rect.width-48,h,size,UiFactory.Ink,TextAnchor.MiddleCenter).supportRichText=false;}
        private void PanelButton(RectTransform panel,string title,float y,Action action,bool primary=false)
        {UiFactory.Button(panel,title,24,y,panel.rect.width-48,76,action,primary);}
        private void CloseOverlay(){if(overlay!=null){overlay.gameObject.SetActive(false);Destroy(overlay.gameObject);overlay=null;}overlayBack=null;}
        private void OnDisable(){if(sound!=null)sound.Stop();ClearPhysicalInputs();clock.Reset();}
        private void OnDestroy(){Save();if(active)requestOrientation?.Invoke(false);}
    }
}
