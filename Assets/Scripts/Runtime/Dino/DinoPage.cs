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
        private Func<DateTime> localDate = () => DateTime.Now;
        private DinoCelebration celebration;
        private Action overlayBack;
        private Phase phase;
        private Destination destination;
        private bool active, landscape, selectedLandscape, turningLandscape, inheritedDuck, dirty;
        private float turnStarted, unlockAt, lastSave;
        private int stableFrames;
        private long shownScore = -1, shownBest = -1, milestone, startingBest;
        private bool windowBlocked, pendingLayoutChange, waitingForNewGame;
        private Text scoreLabel, bestLabel, stateLabel, warningLabel;
        private Button directionButton, pauseButton;
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
            celebration = DinoCelebrationCalendar.Resolve(localDate());
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
                    phase = Phase.Running; stateLabel.fontSize = 25; stateLabel.fontStyle = FontStyle.Normal; stateLabel.text = "";
                }
                else stateLabel.text = clock.CountdownNumber.ToString();
            }
            RefreshScore();
            if (track != null)
            {
                track.Render(game, phase == Phase.Running || phase == Phase.Ready ? Time.unscaledDeltaTime : 0);
                if (stateLabel != null) stateLabel.color = track.BackgroundColor.grayscale < .4f ? UiFactory.Paper : UiFactory.Ink;
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
            float w = viewport.Width, h = viewport.Height;
            float extra = Mathf.Max(0, h - UiFactory.DesignHeight);
            BuildNavigation(false);
            BuildHeading(112 + extra * .08f, false);
            UiFactory.Label(content, "选择游玩方向，再向前跑远一点。", 56, 208 + extra * .08f,
                w - 112, 42, 24, UiFactory.Muted);
            bool birthday = celebration == DinoCelebration.Birthday;
            float top = 300 + extra * .2f;
            if (birthday)
            {
                var banner = UiFactory.Panel(content, "Birthday banner", 56, top - 28, w - 112, 64, UiFactory.Hex("F0E3C3"));
                banner.raycastTarget = false;
                UiFactory.Label(banner.transform, "生日快乐", 20, 0, w - 152, 64, 26,
                    UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                top += 60;
            }
            AddOrientationCard(false, top, "竖屏", "单手轻触，随时出发", PortraitBest);
            AddOrientationCard(true, top + 168, "横屏", "双手操作，看得更远", LandscapeBest);
            Button(selectedLandscape ? "横屏开始" : "竖屏开始", 56, top + 364, w - 112, 76,
                () => BeginTurn(selectedLandscape, Destination.Game), true);
            UiFactory.Label(content, "横竖屏分别记录最高分 · 记住上次选择", 56, top + 466,
                w - 112, 42, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
            warningLabel = UiFactory.Label(content, SaveError ?? "本局退出后重新开始，最高纪录保留。",
                56, h - 42, w - 112, 34, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
        }

        private void AddOrientationCard(bool isLandscape, float y, string title, string description, long best)
        {
            float width = viewport.Width - 112;
            bool selected = selectedLandscape == isLandscape;
            var button = UiFactory.Button(content, "", 56, y, width, 148,
                () => { selectedLandscape = isLandscape; BuildSelection(); });
            button.name = isLandscape ? "Landscape choice" : "Portrait choice";
            button.targetGraphic.color = selected ? UiFactory.Accent : UiFactory.Hex("DDE5DA");
            UiFactory.Panel(button.transform, "Choice surface", 2, 2, width - 4, 144, UiFactory.Paper);
            UiFactory.Label(button.transform, title, 28, 16, width - 160, 48, 30,
                UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(button.transform, description, 28, 65, width - 56, 34, 22, UiFactory.Muted);
            UiFactory.Label(button.transform, "最高纪录  " + best, 28, 105, width - 56, 30, 21, UiFactory.Muted);
            UiFactory.Label(button.transform, selected ? "已选择" : "选择", width - 128, 20, 100, 42,
                21, selected ? UiFactory.Accent : UiFactory.Muted, TextAnchor.MiddleRight);
        }

        private void BuildNavigation(bool gameControls)
        {
            float w = viewport.Width;
            var nav = UiFactory.Rect(content, "Dino navigation", 0, 0, w, 92);
            UiFactory.IconButton(nav, UiIconKind.Home, 56, 34, 58, 58, Back);
            if (gameControls) UiFactory.IconButton(nav, UiIconKind.Restart, 126, 34, 58, 58, ConfirmRestart);
            UiFactory.Label(nav, "XANADU / YONDER", w * .5f - 150, 34, 300, 58, 19,
                UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.IconButton(nav, UiIconKind.Book, w - 184, 34, 58, 58, ShowHelp);
            UiFactory.IconButton(nav, UiIconKind.Settings, w - 114, 34, 58, 58, ShowSettings);
        }

        private void BuildHeading(float y, bool wide)
        {
            UiFactory.Label(content, "Dino", 56, y, wide ? 170 : 400, wide ? 82 : 88,
                wide ? 48 : 63, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            if (!wide) UiFactory.Label(content, "07 / 像素跑酷", viewport.Width - 280, y + 30,
                224, 42, 22, UiFactory.Muted, TextAnchor.MiddleRight);
        }

        private Text ScorePanel(string title, float x, float y, float width)
        {
            var panel = UiFactory.Panel(content, title, x, y, width, 96, UiFactory.Hex("E7EDE4"));
            UiFactory.Label(panel.transform, title, 20, 8, width - 40, 30, 19, UiFactory.Muted);
            var value = UiFactory.Label(panel.transform, "0", 20, 36, width - 40, 54, 37,
                UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 14; value.resizeTextMaxSize = 37;
            return value;
        }

        private void StartNewGame()
        {
            double worldWidth = Math.Min(4096, new DinoPageLayout(viewport.Width, viewport.Height).WorldWidth);
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
            float w = viewport.Width, h = viewport.Height;
            var layout = new DinoPageLayout(w, h);
            BuildNavigation(true);
            BuildHeading(layout.HeadingY, layout.Landscape);
            float cardWidth = layout.Landscape ? Mathf.Min(232, (w - 302) * .5f) : (w - 128) * .5f;
            float scoreX = layout.Landscape ? w - 72 - cardWidth * 2 : 56;
            scoreLabel = ScorePanel("本局分数", scoreX, layout.ScoresY, cardWidth);
            bestLabel = ScorePanel(landscape ? "横屏最高纪录" : "竖屏最高纪录",
                scoreX + cardWidth + 16, layout.ScoresY, cardWidth);

            Rect area = layout.Track;
            var card = UiFactory.Panel(content, "Dino track card", area.x, area.y, area.width, area.height, Color.white);
            // Stencil masking clips the sky and every sprite to the same rounded panel.
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            track = card.gameObject.AddComponent<DinoTrackView>();
            track.Initialize(card.rectTransform); track.SetCelebration(celebration);
            track.SetLayout(area.width, area.height, game.Rules.ViewportWidth);
            track.Restore(game, presentation); track.Render(game, 0);
            var touch = UiFactory.Panel(card.transform, "Track jump input", 0, 0, area.width, area.height, Color.clear, false);
            touch.raycastTarget = true;
            AddTouch(touch.gameObject, JumpDown, JumpUp);
            stateLabel = UiFactory.Label(card.transform, phase == Phase.Ready ? "按下跳跃，开始奔跑" : "",
                20, 22, area.width - 40, 76, 25, UiFactory.Ink, TextAnchor.MiddleCenter);
            AddActionButton("Dino duck input", "下蹲", layout.Duck, DuckDown, DuckUp);
            AddActionButton("Dino jump input", "跳跃", layout.Jump, JumpDown, JumpUp);
            UiFactory.Label(content, "轻按短跳 · 按住长跳 · 按住下蹲", 56, layout.HintY,
                w - 112, 38, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
            Rect pause = layout.Pause, direction = layout.Direction;
            pauseButton = Button("暂停", pause.x, pause.y, pause.width, pause.height, ShowPause);
            directionButton = Button("更换方向", direction.x, direction.y, direction.width, direction.height,
                () => { Suspend(); ConfirmChange(); });
            if (layout.Landscape)
            {
                directionButton.gameObject.SetActive(phase == Phase.Ready);
                pauseButton.gameObject.SetActive(phase != Phase.Ready);
            }
            warningLabel = UiFactory.Label(content, SaveError ?? "最高纪录自动保存", 56, h - 42,
                w - 112, 34, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
            shownScore = shownBest = -1; RefreshScore();
        }

        private void ConfirmRestart()
        {
            if (game == null) return;
            if (!game.IsStarted || game.IsGameOver) { StartNewGame(); return; }
            Suspend();
            var panel = OpenPanel("重新开始这一局？", 420);
            PanelLabel(panel, "当前进度将清空，最高纪录和设置会保留。", 105, 100, 24);
            PanelButton(panel, "重新开始", 230, StartNewGame, true);
            PanelButton(panel, "继续当前游戏", 318, Continue); overlayBack = ShowPauseMenu;
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
            if (viewport.Landscape && directionButton != null) directionButton.gameObject.SetActive(false);
            if (pauseButton != null) pauseButton.gameObject.SetActive(true);
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
            if(!game.IsStarted) {phase=Phase.Ready;stateLabel.text="按下跳跃，开始奔跑";return;}
            phase=Phase.Countdown;clock.BeginCountdown();stateLabel.fontSize=37;stateLabel.fontStyle=FontStyle.Bold;stateLabel.text="3";
        }
        private void ShowSettings()
        {
            Suspend();
            var panel=OpenPanel("简单一点，刚刚好",488);
            PanelLabel(panel,"设置在所有游戏中共用",104,42,23);
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
            Suspend();var panel=OpenPanel("Dino · 找到奔跑的节奏",560);
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
            var layout = new DinoPageLayout(viewport.Width, viewport.Height);
            double scale = layout.Track.width / worldWidth;
            return worldWidth >= 320 && viewport.Landscape == landscape && scale >= .96 &&
                layout.Bottom <= viewport.Height - 8 && layout.Track.height / scale >= 212; // 30 ground + 113 maximum jump + 64 sprite + margin.
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
            if(shownScore!=game.Score){shownScore=game.Score;scoreLabel.text=game.Score.ToString();}
            var best=profile.GetBest(landscape);
            if(shownBest!=best){shownBest=best;bestLabel.text=best.ToString();}
        }
        private void FinishHome(){Save();goHome();}
        private void ClearContent()
        {
            ClearPhysicalInputs();touches.Clear();CloseOverlay();
            if(content!=null){content.gameObject.SetActive(false);Destroy(content.gameObject);}
            content=UiFactory.Rect(root,"Dino content",0,0,viewport.Width,viewport.Height);
            UiFactory.Panel(content,"Page background",0,0,viewport.Width,viewport.Height,UiFactory.Paper,false);
            track=null;scoreLabel=bestLabel=stateLabel=warningLabel=null;directionButton=pauseButton=null;
        }
        private Text Label(string text,float x,float y,float w,float h,int size,TextAnchor anchor=TextAnchor.MiddleLeft)
        {return UiFactory.Label(content,text,x,y,w,h,size,UiFactory.Ink,anchor);}
        private Button Button(string text,float x,float y,float w,float h,Action action,bool primary=false)
        {return UiFactory.Button(content,text,x,y,w,h,action,primary);}
        private void AddActionButton(string name, string title, Rect area, Action<int> down, Action<int> up)
        {
            var button = UiFactory.Button(content, title, area.x, area.y, area.width, area.height, () => { });
            button.name = name;
            AddTouch(button.gameObject, down, up);
        }
        private void AddTouch(GameObject target,Action<int> down,Action<int> up)
        {var input=target.AddComponent<DinoTouchControl>();input.Initialize(down,up);touches.Add(input);}
        private RectTransform OpenPanel(string title,float requestedHeight)
        {
            CloseOverlay();ClearPhysicalInputs();
            overlay=UiFactory.Rect(root,"Dino modal overlay",0,0,viewport.Width,viewport.Height);
            var dim=overlay.gameObject.AddComponent<Image>();dim.color=new Color(.06f,.14f,.1f,.48f);dim.raycastTarget=true;
            float w=Mathf.Min(596,viewport.Width-64),h=Mathf.Min(requestedHeight,viewport.Height-32);
            var panel=UiFactory.Panel(overlay,"Dino dialog",(viewport.Width-w)*.5f,(viewport.Height-h)*.5f,w,h,UiFactory.Paper);
            panel.raycastTarget=true;
            UiFactory.Label(panel.transform,title,32,36,w-64,62,33,UiFactory.Ink,TextAnchor.MiddleCenter,FontStyle.Bold);
            return panel.rectTransform;
        }
        private void PanelLabel(RectTransform panel,string message,float y,float h,int size)
        {UiFactory.Label(panel,message,32,y,panel.rect.width-64,h,size,UiFactory.Muted,TextAnchor.MiddleCenter).supportRichText=false;}
        private void PanelButton(RectTransform panel,string title,float y,Action action,bool primary=false)
        {UiFactory.Button(panel,title,32,y,panel.rect.width-64,74,action,primary);}
        private void CloseOverlay(){if(overlay!=null){overlay.gameObject.SetActive(false);Destroy(overlay.gameObject);overlay=null;}overlayBack=null;}
        private void OnDisable(){if(sound!=null)sound.Stop();ClearPhysicalInputs();clock.Reset();}
        private void OnDestroy(){Save();if(active)requestOrientation?.Invoke(false);}
    }
}
