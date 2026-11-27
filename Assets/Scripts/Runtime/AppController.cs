using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Core.Sokoban;
using XanaduYonder.Core.GoldMiner;
using XanaduYonder.Core.LinkMatch;
using XanaduYonder.Runtime.Jump;
using XanaduYonder.Runtime.Sheep;
using XanaduYonder.Runtime.Idiom;
using XanaduYonder.Runtime.Feihualing;
using XanaduYonder.Runtime.Watermelon;
using XanaduYonder.Runtime.Sudoku;
using XanaduYonder.Runtime.Dino;
using XanaduYonder.Runtime.Minesweeper;
using XanaduYonder.Runtime.HappyBall;
using XanaduYonder.Runtime.Zuma;
using XanaduYonder.Runtime.Gomoku;
using XanaduYonder.Runtime.Sokoban;
using XanaduYonder.Runtime.LinkMatch;
using XanaduYonder.Runtime.GoldMiner;
using XanaduYonder.Runtime.Tetris;
using XanaduYonder.Core.Tetris;
using XanaduYonder.Runtime.SlidingPuzzle;
using XanaduYonder.Runtime.FlappyBird;

namespace XanaduYonder.Runtime
{
    public sealed partial class AppController : MonoBehaviour
    {
        private sealed class RandomSource : IRandomSource
        {
            private readonly System.Random random = new System.Random();
            public int Next(int maximum) { return random.Next(maximum); }
        }

        private readonly IRandomSource random = new RandomSource();
        private Game2048 game;
        private SaveData data;
        private LocalSaveStore store;
        private RectTransform canvasRoot;
        private readonly List<KeyValuePair<Selectable, Selectable.Transition>> landscapeHostTransitions = new List<KeyValuePair<Selectable, Selectable.Transition>>();
        private RectTransform frame;
        private GameObject lobbyPage;
        private ScrollRect lobbyScroll;
        private LobbyView lobbyView;
        private bool startupComplete;
        private GameObject gamePage;
        private GameObject modal;
        private Action dismissModal;
        private BoardView board;
        private GameFeedback feedback;
        private Text scoreText;
        private Text bestText;
        private JumpPage jumpPage;
        private SheepPage sheepPage;
        private IdiomPage idiomPage;
        private FeihualingPage feihualingPage;
        private WatermelonPage watermelonPage;
        private SudokuPage sudokuPage;
        private DinoPage dinoPage;
        private DinoSaveData dinoPreview;
        private MinesweeperPage minesweeperPage;
        private ZumaPage zumaPage;
        private HappyBallPage happyBallPage;
        private long happyBallBest;
        private GomokuPage gomokuPage;
        private SlidingPuzzlePage slidingPuzzlePage;
        private SokobanPage sokobanPage;
        private LinkMatchPage linkMatchPage;
        private GoldMinerPage goldMinerPage;
        private GoldMinerRootSnapshot goldMinerPreview;
        private TetrisPage tetrisPage;
        private TetrisLoadResult tetrisPreview;
        private FlappyBirdPage flappyBirdPage;
        private Sprite flappyLobbySprite;
        private long flappyBest;
        private string flappyLoadWarning;
        private Text status;
        private Text lobbyStatus;
        private enum Page { Lobby, Game2048, Jump, Sheep, Idiom, Sudoku, Watermelon, Minesweeper, Zuma, Dino, Gomoku, HappyBall, Sokoban, GoldMiner, SlidingPuzzle, Tetris, FlappyBird, LinkMatch, AircraftFire, Snake, BakeryBasket, ClassicBoards, Feihualing }
        private Page activePage;
        private bool inGame => activePage == Page.Game2048;
        private bool animating;
        private bool paused;
        private bool focused = true;
        private string saveWarning;
        private bool saveFailed;

        // Startup presentation may reveal the lobby only after the actual input route
        // and layout are ready. Reading this must not initialize pages or save again.
        internal bool LobbyReadyForInput
        {
            get
            {
                if (!startupComplete || activePage != Page.Lobby || paused || !focused || modal != null || IsExternalGameRunning ||
                    (lobbyView != null && lobbyView.HasDisplayOptions) ||
                    lobbyPage == null || !lobbyPage.activeInHierarchy || canvasRoot == null ||
                    !canvasRoot.gameObject.activeInHierarchy || lobbyScroll == null || !lobbyScroll.isActiveAndEnabled ||
                    !lobbyScroll.vertical || lobbyScroll.content == null || lobbyScroll.viewport == null)
                    return false;
                var canvas = canvasRoot.GetComponent<Canvas>();
                var raycaster = canvasRoot.GetComponent<GraphicRaycaster>();
                var group = canvasRoot.GetComponent<CanvasGroup>();
                var events = EventSystem.current;
                var input = events != null ? events.currentInputModule : null;
                return canvas != null && canvas.isActiveAndEnabled && raycaster != null && raycaster.isActiveAndEnabled &&
                    (group == null || (group.interactable && group.blocksRaycasts)) &&
                    events != null && events.isActiveAndEnabled && events.isFocused &&
                    input != null && input.isActiveAndEnabled && input.IsActive() &&
                    lobbyScroll.viewport.rect.width > 0 && lobbyScroll.viewport.rect.height > 0 &&
                    lobbyScroll.content.rect.width > 0 && lobbyScroll.content.rect.height > 0;
            }
        }

        private void Awake()
        {
            Input.backButtonLeavesApp = false;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (UiFactory.Font == null) throw new InvalidOperationException("The bundled Chinese font is missing.");
            store = new LocalSaveStore(Application.persistentDataPath);
            data = store.Load(out saveWarning);
            tetrisPreview = new TetrisSaveStore(Application.persistentDataPath).Load();
            happyBallBest = new HappyBallSaveStore(Application.persistentDataPath).Load(out var happyBallWarning)?.BestScore ?? 0;
            if (data == null) data = new SaveData { Game = new Game2048(random).GetSnapshot() };
            LoadAircraftFireRecord();
            flappyBest = new FlappySaveStore(Application.persistentDataPath).Load(out flappyLoadWarning).BestScore;
            string dinoWarning;
            dinoPreview = new DinoSaveStore(Application.persistentDataPath).Load(out dinoWarning);
            game = new Game2048(data.Game, random);
            feedback = gameObject.AddComponent<GameFeedback>();
            BuildCanvas();
            BuildLobby();
            BuildGame();
            BuildJump();
            BuildSheep();
            BuildSudoku();
            BuildMinesweeper();
            BuildZuma();
            BuildGomoku();
            BuildSokoban();
            BuildSlidingPuzzle();
            BuildLinkMatch();
            BuildSnake();
            var statusBand = UiFactory.Band(frame, "Status", 1278, 34, 1);
            status = UiFactory.Label(statusBand, "", 56, 0, 608, 34, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
            ShowLobby();
            SetStatus(saveWarning ?? "所有玩法离线可用 · 对局或最高纪录自动保存");
            startupComplete = true;
            RestoreWebGamePendingPreferences();
            Debug.Log("XanaduYonder ready; save=" + store.SavePath);
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("XanaduYonder UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvasRoot = (RectTransform)canvasObject.transform;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiFactory.DesignWidth, UiFactory.DesignHeight);
            scaler.matchWidthOrHeight = 0;
            var background = UiFactory.Panel(canvasObject.transform, "Background", 0, 0, 720, 1320, UiFactory.Paper, false);
            UiFactory.Stretch(background.rectTransform);
            frame = UiFactory.Rect(canvasObject.transform, "Safe area design frame", 0, 0, 720, 1320);
            Canvas.ForceUpdateCanvases();
            frame.gameObject.AddComponent<SafeDesignFrame>().Initialize(frame, canvasRoot);
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("UI Event System", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }
            if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        }

        private void EnterGoldMiner()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            flappyBirdPage?.Leave();
            tetrisPage?.Leave();
            slidingPuzzlePage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            gomokuPage?.Leave();
            dinoPage?.Leave();
            minesweeperPage?.Leave();
            StopAnimation();
            CloseModal();
            jumpPage.Leave();
            sheepPage.Leave();
            sudokuPage.Leave();
            idiomPage?.Leave();
            watermelonPage?.Leave();
            if (goldMinerPage == null)
            {
                // This independent canvas owns full-screen landscape and its own safe area.
                var root = new GameObject("Gold Miner page", typeof(RectTransform));
                root.transform.SetParent(transform, false);
                goldMinerPage = root.AddComponent<GoldMinerPage>();
                goldMinerPage.Initialize(ShowLobby, () => data.SoundEnabled, () => data.HapticsEnabled,
                    value => SetFeedbackSetting(true, value),
                    value => SetFeedbackSetting(false, value),
                    () => feedback.Land(false, false, data.HapticsEnabled));
                goldMinerPage.Changed += RefreshGoldMinerRecord;
            }
            activePage = Page.GoldMiner;
            frame.gameObject.SetActive(false);
            goldMinerPage.SetForeground(focused, paused);
            goldMinerPage.Enter();
        }

        private void RefreshGoldMinerRecord()
        {
            if (lobbyView == null) return;
            var progress = goldMinerPage != null ? goldMinerPage.GetProgressSnapshot() : null;
            if (progress == null)
            {
                if (goldMinerPreview == null)
                {
                    string ignored;
                    goldMinerPreview = new GoldMinerSaveStore(Application.persistentDataPath).Load(out ignored) ?? new GoldMinerRootSnapshot();
                }
                progress = goldMinerPreview;
            }
            var solo = progress.GetRecord(GoldMinerMode.Solo, GoldMinerBalanceProfile.Source);
            var coop = progress.GetRecord(GoldMinerMode.Coop, GoldMinerBalanceProfile.Source);
            if (solo.BestClearedLevel == 0 && coop.BestClearedLevel == 0) return;
            lobbyView.SetRecord("gold-miner", "原版纪录 · 单人 " + solo.BestClearedLevel + " 关 · 双人 " + coop.BestClearedLevel + "/30 关",
                "单人 " + solo.BestClearedLevel + " · 双人 " + coop.BestClearedLevel + "/30");
        }

        private void EnterDino()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            gomokuPage?.Leave();
            StopAnimation(); CloseModal();
            minesweeperPage?.Leave();
            sudokuPage.Leave(); jumpPage.Leave(); sheepPage.Leave();
            idiomPage?.Leave(); watermelonPage?.Leave();
            SetActivePage(Page.Dino);
            if (status != null) status.gameObject.SetActive(false);
            if (dinoPage == null)
            {
                var pageRoot = UiFactory.Rect(canvasRoot, "Dino page", 0, 0, 720, 1320);
                dinoPage = pageRoot.gameObject.AddComponent<DinoPage>();
                dinoPage.Initialize(pageRoot, canvasRoot, ShowLobby, RequestDinoOrientation,
                    () => activePage == Page.Dino && !paused && focused,
                    () => data.SoundEnabled, () => data.HapticsEnabled,
                    () => SetFeedbackSetting(true, !data.SoundEnabled),
                    () => SetFeedbackSetting(false, !data.HapticsEnabled));
                dinoPage.Changed += RefreshDinoRecord;
            }
            dinoPage.transform.SetAsLastSibling();
            dinoPage.Enter();
        }

        private void RequestDinoOrientation(bool landscape)
        {
            Screen.orientation = landscape
                ? (Input.deviceOrientation == DeviceOrientation.LandscapeRight ? ScreenOrientation.LandscapeRight : ScreenOrientation.LandscapeLeft)
                : ScreenOrientation.Portrait;
        }

        private void RefreshDinoRecord()
        {
            if (lobbyView == null) return;
            long portrait = dinoPage != null ? dinoPage.PortraitBest : dinoPreview == null ? 0 : dinoPreview.BestPortraitScore;
            long landscape = dinoPage != null ? dinoPage.LandscapeBest : dinoPreview == null ? 0 : dinoPreview.BestLandscapeScore;
            lobbyView.SetRecord("dino", "竖屏最高 " + portrait + " · 横屏最高 " + landscape, "最高 " + Math.Max(portrait, landscape) + " 分");
        }

        private void BuildIdiom()
        {
            if (idiomPage != null) return;
            var root = UiFactory.Rect(frame, "Idiom page", 0, 0, 720, 1320);
            UiFactory.Stretch(root);
            idiomPage = root.gameObject.AddComponent<IdiomPage>();
            idiomPage.Initialize(root, ShowLobby,
                () => activePage == Page.Idiom && modal == null && !paused &&
                    (focused || (idiomPage.KeyboardVisible && idiomPage.KeyboardForeground)),
                SetStatus, OpenTextGameModal, CloseModal);
            idiomPage.Changed += RefreshIdiomRecord;
        }

        private RectTransform OpenTextGameModal(string title, float height, Action back)
        {
            var panel = CreateModal(title, height, back);
            SetModalBack(back);
            return panel;
        }

        private void RefreshIdiomRecord()
        {
            if (idiomPage != null) lobbyView?.SetRecord("idiom", idiomPage.BestLabel, "同字 / 同音");
        }

        private void EnterIdiom()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            gomokuPage?.Leave();
            dinoPage?.Leave();
            minesweeperPage?.Leave();
            sudokuPage.Leave();
            StopAnimation();
            CloseModal();
            jumpPage.Leave();
            sheepPage.Leave();
            watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = true;
            SetActivePage(Page.Idiom);
            BuildIdiom();
            if (status != null) status.gameObject.SetActive(false);
            idiomPage.Enter();
        }

        private void EnterWatermelon()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            gomokuPage?.Leave();
            dinoPage?.Leave();
            minesweeperPage?.Leave();
            sudokuPage.Leave();
            StopAnimation();
            CloseModal();
            jumpPage.Leave();
            sheepPage.Leave();
            idiomPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            SetActivePage(Page.Watermelon);
            if (watermelonPage == null)
            {
                var root = UiFactory.Rect(frame, "Watermelon page", 0, 0, 720, 1320);
                UiFactory.Stretch(root);
                watermelonPage = root.gameObject.AddComponent<WatermelonPage>();
                watermelonPage.Initialize(root, ShowLobby, OpenSettings,
                    () => activePage == Page.Watermelon && modal == null && !paused && focused,
                    () => data.SoundEnabled, () => data.HapticsEnabled, feedback, SetStatus, ShowDialog, OpenGameModal, CloseModal);
                watermelonPage.Changed += RefreshWatermelonRecord;
            }
            if (status != null) status.gameObject.SetActive(true);
            watermelonPage.Enter();
        }

        private RectTransform OpenGameModal(string title, float height, Action back)
        {
            var panel = CreateModal(title, height);
            SetModalBack(back);
            return panel;
        }

        private void RefreshWatermelonRecord()
        {
            if (watermelonPage != null) lobbyView?.SetRecord("watermelon", "你的最高纪录 " + watermelonPage.BestScore + " · 三种主题", "最高 " + watermelonPage.BestScore + " 分");
        }

        private void BuildZuma()
        {
            // Keep the session host active when either canvas is hidden, so lifecycle saves still run.
            var host = new GameObject("Zuma session host");
            host.transform.SetParent(transform, false);
            zumaPage = host.AddComponent<ZumaPage>();
            zumaPage.Initialize(FinishZumaExit, () => data.SoundEnabled, () => data.HapticsEnabled,
                SetFeedbackSetting, feedback);
            zumaPage.PresentationReady += () => { if (activePage == Page.Zuma) canvasRoot.gameObject.SetActive(false); };
        }

        private void EnterZuma()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            if (activePage == Page.Zuma) return;
            gomokuPage?.Leave();
            dinoPage?.Leave();
            StopAnimation();
            CloseModal();
            jumpPage.Leave();
            sheepPage.Leave();
            sudokuPage.Leave();
            minesweeperPage.Leave();
            idiomPage?.Leave();
            watermelonPage?.Leave();
            Save();
            activePage = Page.Zuma;
            // Keep the lobby visible while Android rotates; the complete Zuma page hides it.
            SetLandscapeHostInput(false);
            zumaPage.Enter();
        }

        private void FinishZumaExit()
        {
            activePage = Page.Lobby;
            SetLandscapeHostInput(true);
            canvasRoot.gameObject.SetActive(true);
            ShowLobby();
        }

        private void SetLandscapeHostInput(bool enabled)
        {
            var interaction = canvasRoot.GetComponent<CanvasGroup>();
            if (interaction == null) interaction = canvasRoot.gameObject.AddComponent<CanvasGroup>();
            if (!enabled)
            {
                landscapeHostTransitions.Clear();
                foreach (var control in canvasRoot.GetComponentsInChildren<Selectable>())
                {
                    landscapeHostTransitions.Add(new KeyValuePair<Selectable, Selectable.Transition>(control, control.transition));
                    control.transition = Selectable.Transition.None;
                }
                interaction.interactable = false;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }
            else
            {
                interaction.interactable = true;
                foreach (var entry in landscapeHostTransitions) if (entry.Key != null) entry.Key.transition = entry.Value;
                landscapeHostTransitions.Clear();
            }
            canvasRoot.GetComponent<GraphicRaycaster>().enabled = enabled;
        }

        private bool SetFeedbackSetting(bool soundSetting, bool enabled)
        {
            if (soundSetting) data.SoundEnabled = enabled;
            else data.HapticsEnabled = enabled;
            if (!data.SoundEnabled) feedback?.Stop();
            SaveHostData();
            return !saveFailed;
        }

        private void BuildJump()
        {
            var root = UiFactory.Rect(frame, "Jump page", 0, 0, 720, 1320);
            UiFactory.Stretch(root);
            jumpPage = root.gameObject.AddComponent<JumpPage>();
            jumpPage.Initialize(root, ShowLobby, OpenSettings,
                () => activePage == Page.Jump && modal == null && !paused && focused,
                () => data.SoundEnabled, () => data.HapticsEnabled, feedback, SetStatus, ShowDialog);
            jumpPage.Changed += RefreshJumpBest;
            jumpPage.Leave();
        }

        private void RefreshJumpBest() { lobbyView?.SetRecord("jump", "你的最高纪录 " + jumpPage.BestScore, "最高 " + jumpPage.BestScore + " 分"); }

        private void BuildSheep()
        {
            var root = UiFactory.Rect(frame, "Sheep page", 0, 0, 720, 1320);
            UiFactory.Stretch(root);
            sheepPage = root.gameObject.AddComponent<SheepPage>();
            sheepPage.Initialize(root, ShowLobby, after => OpenSettings(after),
                () => activePage == Page.Sheep && modal == null && !paused && focused,
                () => data.SoundEnabled, () => data.HapticsEnabled, feedback, SetStatus, ShowDialog, OpenSheepModal, CloseModal);
            sheepPage.Changed += RefreshSheepRecord;
            sheepPage.Leave();
        }

        private RectTransform OpenSheepModal(string title, float height, Action back)
        {
            var panel = CreateModal(title, height);
            SetModalBack(back);
            return panel;
        }

        private void RefreshSheepRecord() { lobbyView?.SetRecord("sheep", "通关 " + sheepPage.CompletedRuns + " 次 · 无尽最高 " + sheepPage.EndlessBest, "已通关 " + sheepPage.CompletedRuns + " 次"); }

        private void BuildSudoku()
        {
            var root = UiFactory.Rect(frame, "Sudoku page", 0, 0, 720, 1320);
            UiFactory.Stretch(root);
            sudokuPage = root.gameObject.AddComponent<SudokuPage>();
            sudokuPage.Initialize(root, ShowLobby, OpenSettings,
                () => activePage == Page.Sudoku && modal == null && !paused && focused,
                () => data.SoundEnabled, () => data.HapticsEnabled, feedback, SetStatus);
            sudokuPage.Changed += RefreshSudokuRecord;
            sudokuPage.Leave();
        }

        private void RefreshSudokuRecord()
        {
            lobbyView?.SetRecord("sudoku", SudokuSummary(sudokuPage.CompletedCount,
                sudokuPage.CompletedCountForDifficulty(0), sudokuPage.CompletedCountForDifficulty(1),
                sudokuPage.CompletedCountForDifficulty(2), sudokuPage.CompletedCountForDifficulty(3)), "已完成 " + sudokuPage.CompletedCount + " 关");
        }

        private static string SudokuSummary(int completed, int beginner, int easy, int medium, int hard)
        {
            return "已完成 " + completed + "/2000 · 入门 " + beginner + " · 简单 " + easy +
                " · 中等 " + medium + " · 困难 " + hard;
        }

        private void BuildSokoban(SokobanCatalog initialCatalog = null, SokobanProfile initialProfile = null, SokobanSaveStore initialStore = null)
        {
            var safeFrame = frame.GetComponent<SafeDesignFrame>();
            bool previousWidthOnly = safeFrame.WidthOnly;
            try
            {
                // Construct at the same 720-wide coordinate system used on entry, including short screens.
                safeFrame.WidthOnly = true;
                var root = UiFactory.Rect(frame, "Sokoban page", 0, 0, 720, 1320);
                UiFactory.Stretch(root);
                sokobanPage = root.gameObject.AddComponent<SokobanPage>();
                sokobanPage.Initialize(root, ShowLobby, after => OpenSettings(after),
                    () => activePage == Page.Sokoban && modal == null && !paused && focused,
                    () => data.SoundEnabled, () => data.HapticsEnabled, feedback, SetStatus,
                    initialCatalog, initialProfile, initialStore);
                sokobanPage.Changed += RefreshSokobanRecord;
                sokobanPage.Leave();
            }
            finally { safeFrame.WidthOnly = previousWidthOnly; }
        }

        private void RefreshSokobanRecord()
        {
            if (sokobanPage != null && lobbyView != null)
                lobbyView.SetRecord("sokoban", "已完成 " + sokobanPage.CompletedCount + "/108 关 · 随时撤回", "已完成 " + sokobanPage.CompletedCount + " 关");
        }

        private void EnterSokoban()
        {
            snakePage?.Leave();
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            happyBallPage?.Leave(); gomokuPage?.Leave(); dinoPage?.Leave();
            StopAnimation(); CloseModal(); minesweeperPage.Leave(); sudokuPage.Leave();
            jumpPage.Leave(); sheepPage.Leave(); idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = true;
            SetActivePage(Page.Sokoban);
            if (status != null) status.gameObject.SetActive(false);
            sokobanPage.Enter();
            SetStatus("");
        }

        private void BuildSlidingPuzzle(SlidingPuzzleProfile initialProfile = null, SlidingPuzzleSaveStore initialStore = null)
        {
            var safeFrame = frame.GetComponent<SafeDesignFrame>();
            bool previous = safeFrame.WidthOnly;
            try
            {
                safeFrame.WidthOnly = true;
                var root = UiFactory.Rect(frame, "SlidingPuzzle page", 0, 0, 720, 1320);
                UiFactory.Stretch(root);
                slidingPuzzlePage = root.gameObject.AddComponent<SlidingPuzzlePage>();
                slidingPuzzlePage.Initialize(root, ShowLobby, after => OpenSettings(after),
                    () => activePage == Page.SlidingPuzzle && modal == null && !paused && focused,
                    () => data.SoundEnabled, () => data.HapticsEnabled, feedback, SetStatus, initialProfile, initialStore);
                slidingPuzzlePage.Changed += RefreshSlidingPuzzleRecord;
                slidingPuzzlePage.Leave();
            }
            finally { safeFrame.WidthOnly = previous; }
        }

        private void RefreshSlidingPuzzleRecord()
        {
            if (slidingPuzzlePage != null && lobbyView != null)
                lobbyView.SetRecord("sliding-puzzle", "自由拼图 / 解锁闯关 · 已完成 " + slidingPuzzlePage.CompletedCount + "/18 关", "已完成 " + slidingPuzzlePage.CompletedCount + " 关");
        }

        private void EnterSlidingPuzzle()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            flappyBirdPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            snakePage?.Leave();
            sokobanPage?.Leave(); happyBallPage?.Leave(); gomokuPage?.Leave(); dinoPage?.Leave();
            StopAnimation(); CloseModal(); minesweeperPage?.Leave(); sudokuPage?.Leave();
            jumpPage?.Leave(); sheepPage?.Leave(); idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = true;
            SetActivePage(Page.SlidingPuzzle);
            if (status != null) status.gameObject.SetActive(false);
            slidingPuzzlePage.Enter(); SetStatus("");
        }

        private void BuildLinkMatch(LinkMatchProfile initialProfile = null, LinkMatchSaveStore initialStore = null)
        {
            var safeFrame = frame.GetComponent<SafeDesignFrame>();
            bool previousWidthOnly = safeFrame.WidthOnly;
            try
            {
                safeFrame.WidthOnly = true;
                var root = UiFactory.Rect(frame, "Link Match page", 0, 0, 720, 1320);
                UiFactory.Stretch(root);
                linkMatchPage = root.gameObject.AddComponent<LinkMatchPage>();
                linkMatchPage.Initialize(root, ShowLobby, after => OpenSettings(after),
                    () => activePage == Page.LinkMatch && modal == null && !paused && focused,
                    () => data.SoundEnabled, () => data.HapticsEnabled, feedback, SetStatus,
                    initialProfile, initialStore);
                linkMatchPage.RetryHostSave = SaveHostData;
                linkMatchPage.Changed += RefreshLinkMatchRecord;
                linkMatchPage.Leave();
            }
            finally { safeFrame.WidthOnly = previousWidthOnly; }
        }

        private void RefreshLinkMatchRecord()
        {
            if (linkMatchPage != null && lobbyView != null)
                lobbyView.SetRecord("link-match", linkMatchPage.CompletedCount == 0
                    ? "轻松 / 挑战 · 五档棋盘"
                    : "已完成 " + linkMatchPage.CompletedCount + "/" + (LinkMatchLevel.Count * 2) + " 关 · 轻松 / 挑战", linkMatchPage.CompletedCount == 0 ? "轻松 / 挑战" : "已完成 " + linkMatchPage.CompletedCount + " 关");
        }

        private void EnterLinkMatch()
        {
            LeaveAircraftFire();
            flappyBirdPage?.Leave(); tetrisPage?.Leave(); slidingPuzzlePage?.Leave(); LeaveGoldMiner();
            snakePage?.Leave();
            sokobanPage?.Leave(); happyBallPage?.Leave(); gomokuPage?.Leave(); dinoPage?.Leave();
            StopAnimation(); CloseModal(); minesweeperPage.Leave(); sudokuPage.Leave();
            jumpPage.Leave(); sheepPage.Leave(); idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = true;
            SetActivePage(Page.LinkMatch);
            if (status != null) status.gameObject.SetActive(false);
            linkMatchPage.SetForeground(!paused && focused);
            linkMatchPage.SetHostSaveWarning(saveFailed ? saveWarning : null);
            linkMatchPage.Enter();
            SetStatus("");
        }

        private void BuildMinesweeper()
        {
            var root = UiFactory.Rect(frame, "Minesweeper page", 0, 0, 720, 1320);
            UiFactory.Stretch(root);
            minesweeperPage = root.gameObject.AddComponent<MinesweeperPage>();
            minesweeperPage.Initialize(root, ShowLobby, after => OpenSettings(after),
                () => activePage == Page.Minesweeper && modal == null && !paused && focused,
                () => data.SoundEnabled, () => data.HapticsEnabled, feedback, SetStatus,
                full => { if (status != null && activePage == Page.Minesweeper) status.gameObject.SetActive(!full); });
            minesweeperPage.Changed += RefreshMinesweeperRecord;
            minesweeperPage.Leave();
        }

        private void EnterHappyBall()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave();
            gomokuPage?.Leave(); dinoPage?.Leave();
            StopAnimation(); CloseModal();
            minesweeperPage.Leave(); sudokuPage.Leave(); jumpPage.Leave(); sheepPage.Leave();
            idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            SetActivePage(Page.HappyBall);
            if (status != null) status.gameObject.SetActive(false);
            if (happyBallPage == null)
            {
                var root = UiFactory.Rect(frame, "HappyBall page", 0, 0, 720, 1320);
                UiFactory.Stretch(root);
                happyBallPage = root.gameObject.AddComponent<HappyBallPage>();
                happyBallPage.Initialize(root, ShowLobby, after => OpenSettings(after),
                    () => activePage == Page.HappyBall && modal == null && !paused && focused,
                    () => data.SoundEnabled, () => data.HapticsEnabled, feedback, SetStatus, ShowDialog);
                happyBallPage.Changed += RefreshHappyBallRecord;
            }
            happyBallPage.SetForeground(!paused && focused);
            happyBallPage.SetHostSaveWarning(saveFailed ? saveWarning : null);
            happyBallPage.Enter();
        }

        private void EnterTetris()
        {
            snakePage?.Leave();
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            flappyBirdPage?.Leave();
            slidingPuzzlePage?.Leave();
            LeaveGoldMiner(); sokobanPage?.Leave(); happyBallPage?.Leave(); gomokuPage?.Leave(); dinoPage?.Leave();
            StopAnimation(); CloseModal(); minesweeperPage.Leave(); sudokuPage.Leave(); jumpPage.Leave(); sheepPage.Leave();
            idiomPage?.Leave(); watermelonPage?.Leave();
            var safeFrame = frame.GetComponent<SafeDesignFrame>();
            safeFrame.WidthOnly = true;
            SetActivePage(Page.Tetris);
            if (status != null) status.gameObject.SetActive(false);
            if (tetrisPage == null)
            {
                RectTransform root = null;
                try
                {
                    root = UiFactory.Rect(frame, "Tetris page", 0, 0, 720, 1320); UiFactory.Stretch(root);
                    Canvas.ForceUpdateCanvases();
                    tetrisPage = root.gameObject.AddComponent<TetrisPage>();
                    tetrisPage.Initialize(root, ShowLobby, after => OpenSettings(after),
                        () => activePage == Page.Tetris && modal == null && !paused && focused,
                        () => data.SoundEnabled, () => data.HapticsEnabled, feedback, SetStatus, ShowDialog);
                    tetrisPage.Changed += RefreshTetrisRecord;
                }
                catch (Exception error)
                {
                    if (root != null) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
                    tetrisPage = null; ShowLobby();
                    SetStatus("俄罗斯方块暂时无法打开，请重试。"); Debug.LogException(error); return;
                }
            }
            tetrisPage.SetForeground(!paused && focused); tetrisPage.SetHostSaveWarning(saveFailed ? saveWarning : null);
            tetrisPage.Enter(); RefreshTetrisRecord();
        }

        private void RefreshTetrisRecord()
        {
            if (lobbyView == null) return;
            long best = tetrisPage != null ? tetrisPage.BestScore : tetrisPreview?.Data?.BestScore ?? 0;
            bool readable = tetrisPage != null ? tetrisPage.CanReadRecord : tetrisPreview != null && tetrisPreview.CanWrite;
            lobbyView.SetRecord("tetris", readable ? "你的最高纪录 " + best : "存档暂不可用 · 进入查看详情", readable ? "最高 " + best + " 分" : "存档暂不可用");
        }

        private void EnterFlappyBird()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave(); happyBallPage?.Leave(); gomokuPage?.Leave(); dinoPage?.Leave();
            StopAnimation(); CloseModal(); minesweeperPage.Leave(); sudokuPage.Leave();
            jumpPage.Leave(); sheepPage.Leave(); idiomPage?.Leave(); watermelonPage?.Leave();
            Screen.orientation = ScreenOrientation.Portrait;
            frame.GetComponent<SafeDesignFrame>().WidthOnly = true;
            SetActivePage(Page.FlappyBird);
            if (status != null) status.gameObject.SetActive(false);
            if (flappyBirdPage == null)
            {
                var root = UiFactory.Rect(frame, "FlappyBird page", 0, 0, 720, 1320);
                UiFactory.Stretch(root);
                flappyBirdPage = root.gameObject.AddComponent<FlappyBirdPage>();
                flappyBirdPage.Initialize(root, ShowLobby, after => OpenSettings(after),
                    () => activePage == Page.FlappyBird && modal == null && !paused && focused,
                    () => data.SoundEnabled, () => data.HapticsEnabled, feedback, SetStatus);
                flappyBirdPage.Changed += RefreshFlappyRecord;
            }
            flappyBirdPage.SetForeground(!paused && focused);
            flappyBirdPage.SetHostSaveWarning(saveFailed ? saveWarning : null);
            flappyBirdPage.Enter();
            SetStatus("");
        }

        private void RefreshFlappyRecord()
        {
            if (lobbyView == null) return;
            if (flappyBirdPage != null)
            {
                flappyBest = Math.Max(flappyBest, flappyBirdPage.BestScore);
                flappyLoadWarning = flappyBirdPage.SaveError;
            }
            var warning = flappyLoadWarning == null ? "" : " · 未保存";
            // Keep a save failure visible even when an unusually long score is ellipsized in the compact view.
            lobbyView.SetRecord("flappy-bird", "最高纪录 " + flappyBest + warning,
                (flappyLoadWarning == null ? "" : "未保存 · ") + "最高 " + flappyBest + " 分");
        }

        private void RefreshHappyBallRecord()
        {
            if (lobbyView == null) return;
            if (happyBallPage != null) happyBallBest = Math.Max(happyBallBest, happyBallPage.BestScore);
            lobbyView.SetRecord("happy-ball", "你的最高纪录 " + happyBallBest, "最高 " + happyBallBest + " 分");
        }

        private void RefreshMinesweeperRecord()
        {
            if (minesweeperPage != null && lobbyView != null)
                lobbyView.SetRecord("minesweeper", minesweeperPage.CompletedCount == 0 ? "首击安全 · 三档挑战" : "累计胜局 " + minesweeperPage.CompletedCount + " · 三档独立纪录", minesweeperPage.CompletedCount == 0 ? "首击安全 · 三档挑战" : "累计胜局 " + minesweeperPage.CompletedCount);
        }

        private void BuildGomoku()
        {
            var root = UiFactory.Rect(frame, "Gomoku page", 0, 0, 720, 1320);
            UiFactory.Stretch(root);
            gomokuPage = root.gameObject.AddComponent<GomokuPage>();
            gomokuPage.Initialize(root, ShowLobby, after => OpenSettings(after),
                () => activePage == Page.Gomoku && modal == null && !paused && focused,
                () => data.SoundEnabled, () => data.HapticsEnabled, feedback, SetStatus);
            gomokuPage.Changed += RefreshGomokuRecord;
            gomokuPage.Leave();
        }

        private void RefreshGomokuRecord()
        {
            lobbyView?.SetRecord("gomoku", "人机 / 本地双人 · 三档难度", "人机 / 双人");
        }

        private void EnterGomoku()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            dinoPage?.Leave();
            StopAnimation(); CloseModal(); minesweeperPage?.Leave(); sudokuPage.Leave(); jumpPage.Leave(); sheepPage.Leave();
            idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            SetActivePage(Page.Gomoku);
            if (status != null) status.gameObject.SetActive(true);
            gomokuPage.Enter();
        }

        private void EnterMinesweeper()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            gomokuPage?.Leave();
            dinoPage?.Leave();
            StopAnimation(); CloseModal(); sudokuPage.Leave(); jumpPage.Leave(); sheepPage.Leave();
            idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            SetActivePage(Page.Minesweeper);
            if (status != null) status.gameObject.SetActive(true);
            minesweeperPage.Enter();
        }

        private void EnterSudoku()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            gomokuPage?.Leave();
            dinoPage?.Leave();
            minesweeperPage?.Leave();
            StopAnimation(); CloseModal(); jumpPage.Leave(); sheepPage.Leave();
            idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            if (status != null) status.gameObject.SetActive(true);
            SetActivePage(Page.Sudoku); sudokuPage.Enter();
        }

        private void EnterSheep()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            gomokuPage?.Leave();
            dinoPage?.Leave();
            minesweeperPage?.Leave();
            sudokuPage.Leave();
            StopAnimation();
            CloseModal();
            jumpPage.Leave();
            idiomPage?.Leave();
            watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            if (status != null) status.gameObject.SetActive(true);
            SetActivePage(Page.Sheep);
            sheepPage.Enter();
        }

        private void EnterJump()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            gomokuPage?.Leave();
            dinoPage?.Leave();
            minesweeperPage?.Leave();
            sudokuPage.Leave();
            StopAnimation();
            CloseModal();
            sheepPage.Leave();
            idiomPage?.Leave();
            watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            if (status != null) status.gameObject.SetActive(true);
            SetActivePage(Page.Jump);
            jumpPage.Enter();
        }

        private void BuildGame()
        {
            var root = UiFactory.Rect(frame, "2048 page", 0, 0, 720, 1320);
            UiFactory.Stretch(root);
            gamePage = root.gameObject;
            var navigation = UiFactory.Band(root, "Game navigation", 0, 92, 0);
            UiFactory.NavigationButton(navigation, UiIconKind.Home, ShowLobby);
            var brand = UiFactory.Label(navigation, "XANADU / YONDER", 226, 34, 278, 58, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
            ResultPresentation.For(this, root).SetReopenSlot(
                UiFactory.Rect(navigation, "Result review slot", 244, 34, 232, 58), brand);
            UiFactory.NavigationButton(navigation, UiIconKind.Book, ShowHelp);
            UiFactory.NavigationButton(navigation, UiIconKind.Settings, OpenSettings);
            var introduction = UiFactory.Band(root, "Game introduction", 116, 156, .16f);
            UiFactory.Label(introduction, "2048", 56, 0, 608, 111, 86, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(introduction, "每一次相遇，都让数字更进一步。", 60, 114, 604, 42, 24, UiFactory.Muted);
            var scores = UiFactory.Band(root, "Game scores", 294, 102, .32f);
            var score = UiFactory.Panel(scores, "Current score", 56, 0, 296, 102, UiFactory.Hex("E7EDE4"));
            UiFactory.Label(score.transform, "本局分数", 20, 10, 256, 30, 19, UiFactory.Muted);
            scoreText = UiFactory.Label(score.transform, "0", 20, 39, 256, 56, 37, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var best = UiFactory.Panel(scores, "Best score", 368, 0, 296, 102, UiFactory.Hex("E7EDE4"));
            UiFactory.Label(best.transform, "最高纪录", 20, 10, 256, 30, 19, UiFactory.Muted);
            bestText = UiFactory.Label(best.transform, "0", 20, 39, 256, 56, 37, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            scoreText.resizeTextForBestFit = bestText.resizeTextForBestFit = true;
            scoreText.resizeTextMinSize = bestText.resizeTextMinSize = 14;
            scoreText.resizeTextMaxSize = bestText.resizeTextMaxSize = 37;
            var boardBand = UiFactory.Band(root, "Game board", 422, 608, .48f);
            var boardImage = UiFactory.Panel(boardBand, "Swipe board", 56, 0, 608, 608, UiFactory.Hex("BBCBB6"));
            boardImage.raycastTarget = true;
            board = boardImage.gameObject.AddComponent<BoardView>();
            board.Initialize(Move, () => CanAcceptMove);
            var actions = UiFactory.Band(root, "Game actions", 1062, 76, .72f);
            DialogUi.ActionButton(actions, "重新开始", 212, 0, 296, 76, ConfirmRestart);
            var instructions = UiFactory.Band(root, "Game instructions", 1170, 44, .86f);
            UiFactory.Label(instructions, "上下左右滑动，相同数字合并。", 56, 0, 608, 44, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
        }

        private void EnterGame()
        {
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            gomokuPage?.Leave();
            dinoPage?.Leave();
            minesweeperPage?.Leave();
            sudokuPage.Leave();
            CloseModal();
            jumpPage.Leave();
            sheepPage.Leave();
            idiomPage?.Leave();
            watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            if (status != null) status.gameObject.SetActive(true);
            SetActivePage(Page.Game2048);
            Refresh2048Page();
        }

        private void Refresh2048Page()
        {
            board.Render(game.GetSnapshot());
            Refresh2048Scores();
            SetStatus(saveWarning ?? "合成 2048，然后向更高的数字出发。");
            CheckEndState();
        }

        private void LeaveGoldMiner()
        {
            goldMinerPage?.Leave();
            frame.gameObject.SetActive(true);
        }

        private void ShowLobby()
        {
            bakeryPage?.Leave();
            ResultPresentation.Clear(this);
            ResetBackExitConfirmation();
            boardPage?.Leave();
            if (activePage == Page.Feihualing) feihualingPage?.Leave();
            LeaveAircraftFire();
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            snakePage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            if (activePage == Page.Zuma && zumaPage.IsVisible) { zumaPage.Exit(); return; }
            gomokuPage?.Leave();
            minesweeperPage?.Leave();
            dinoPage?.Leave();
            sudokuPage.Leave();
            StopAnimation();
            CloseModal();
            jumpPage.Leave();
            sheepPage.Leave();
            idiomPage?.Leave();
            watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = true;
            if (status != null) status.gameObject.SetActive(false);
            SetActivePage(Page.Lobby);
            RefreshScores();
            Save();
            SetStatus(saveWarning ?? "所有玩法离线可用 · 对局或最高纪录自动保存");
        }

        private void Move(Direction direction)
        {
            if (!CanAcceptMove) return;
            var result = game.Move(direction);
            if (!result.Changed) return;
            SaveHostData();
            Refresh2048Scores();
            feedback.Play(result.ScoreDelta > 0, data.SoundEnabled, data.HapticsEnabled);
            animating = true;
            StartCoroutine(AnimateMove(result));
        }

        private bool CanAcceptMove => inGame && modal == null && !animating && !paused && focused && !game.IsGameOver && !(game.HasWon && !game.HasAcknowledgedWin);

        private IEnumerator AnimateMove(MoveResult result)
        {
            yield return board.Animate(result, game.GetSnapshot());
            animating = false;
            SetStatus(saveWarning ?? (result.ScoreDelta > 0 ? "漂亮的合并！  +" + result.ScoreDelta : "继续寻找下一次相遇。"));
            CheckEndState();
        }

        private void CheckEndState()
        {
            if (activePage == Page.LinkMatch) { linkMatchPage?.CheckEndState(); return; }
            if (activePage == Page.Tetris) return;
            if (activePage == Page.SlidingPuzzle) { slidingPuzzlePage?.CheckEndState(); return; }
            if (activePage == Page.Snake) { snakePage?.CheckEndState(); return; }
            if (activePage == Page.Sokoban) { sokobanPage?.CheckEndState(); return; }
            if (activePage == Page.HappyBall) { happyBallPage?.CheckEndState(); return; }
            if (activePage == Page.Dino) return;
            if (activePage == Page.Gomoku) { gomokuPage?.CheckEndState(); return; }
            if (activePage == Page.Minesweeper) { minesweeperPage?.CheckEndState(); return; }
            if (activePage == Page.Watermelon) { watermelonPage?.CheckEndState(); return; }
            if (activePage == Page.Idiom) { idiomPage?.CheckEndState(); return; }
            if (activePage == Page.Feihualing) { feihualingPage?.CheckEndState(); return; }
            if (activePage == Page.Jump) { jumpPage.CheckEndState(); return; }
            if (activePage == Page.Sheep) { sheepPage.CheckEndState(); return; }
            if (activePage == Page.Sudoku) { sudokuPage.CheckEndState(); return; }
            if (!inGame || modal != null || paused || !focused) return;
            if(game.IsGameOver||(game.HasWon&&!game.HasAcknowledgedWin))
            {
                var ended=game;
                if(!ResultPresentation.For(this,(RectTransform)gamePage.transform).Gate(ended,
                    ()=>inGame&&modal==null&&!paused&&focused&&game==ended&&(game.IsGameOver||(game.HasWon&&!game.HasAcknowledgedWin)),CheckEndState))return;
            }
            if (game.HasWon && !game.HasAcknowledgedWin)
                ShowDialog("你好，2048！", "你已经完成这次挑战。\n还可以继续，看看数字能走多远。", "继续挑战", () =>
                {
                    ResultPresentation.Clear(this);
                    game.AcknowledgeWin();
                    SaveHostData();
                    CheckEndState();
                }, "查看最终局面", ()=>ResultPresentation.For(this,(RectTransform)gamePage.transform).Inspect(CloseModal));
            else if (game.IsGameOver)
                ShowDialog("这一局，很精彩。", "暂时没有可以移动的方块了。\n本局得分  " + game.Score, "再来一局", Restart, "查看最终局面", ()=>ResultPresentation.For(this,(RectTransform)gamePage.transform).Inspect(CloseModal));
        }

        private void ConfirmRestart()
        {
            ShowMessageDialog("重新开始这一局？", "当前棋盘将被清空，\n最高纪录和设置会保留。", "重新开始", Restart, "继续当前游戏", CheckEndState, true);
        }

        private void Restart()
        {
            ResultPresentation.Clear(this);
            Cancel2048Animation();
            CloseModal();
            game = new Game2048(random);
            SaveHostData();
            Refresh2048Page();
        }

        private void ShowHelp()
        {
            ShowDialog("让相同的数字相遇", "滑动棋盘，相同方块相遇时合成两倍数字。\n每次有效移动后，会出现一个新方块。\n合成 2048 即可获胜，也能继续挑战。\n合并时有短触感；说明见大厅设置。", "知道了", CheckEndState);
        }

        private void Refresh2048Scores()
        {
            Update2048Records();
            scoreText.text = game.Score.ToString();
            bestText.text = data.BestScore.ToString();
            lobbyView?.SetRecord("2048", "最高纪录 " + data.BestScore + " · 最高数字 " + data.BestTile, "最高 " + data.BestScore + " 分");
        }

        private void RefreshScores()
        {
            Refresh2048Scores();
            RefreshJumpBest();
            RefreshSheepRecord();
            RefreshIdiomRecord();
            RefreshWatermelonRecord();
            RefreshSudokuRecord();
            RefreshDinoRecord();
            RefreshGoldMinerRecord();
            RefreshMinesweeperRecord();
            RefreshSnakeRecord();
            RefreshSokobanRecord();
            RefreshLinkMatchRecord();
            RefreshSlidingPuzzleRecord();
            RefreshHappyBallRecord();
            RefreshTetrisRecord();
            RefreshFlappyRecord();
            RefreshAircraftFireRecord();
            RefreshGomokuRecord();
        }

        private void SetStatus(string message)
        {
            var pageError = activePage == Page.Feihualing ? feihualingPage?.SaveError : activePage == Page.Snake ? snakePage?.SaveError : activePage == Page.AircraftFire ? aircraftFirePage?.SaveError : activePage == Page.LinkMatch ? linkMatchPage?.SaveError : activePage == Page.FlappyBird ? flappyBirdPage?.SaveError : activePage == Page.SlidingPuzzle ? slidingPuzzlePage?.SaveError : activePage == Page.Tetris ? tetrisPage?.SaveError : activePage == Page.GoldMiner ? goldMinerPage?.SaveError : activePage == Page.Sokoban ? sokobanPage?.SaveError : activePage == Page.HappyBall ? happyBallPage?.SaveError : activePage == Page.Dino ? dinoPage?.SaveError : activePage == Page.Zuma ? zumaPage?.SaveError : activePage == Page.Gomoku ? gomokuPage?.SaveError : activePage == Page.Minesweeper ? minesweeperPage?.SaveError : activePage == Page.Sudoku ? sudokuPage?.SaveError : activePage == Page.Watermelon ? watermelonPage?.SaveError : activePage == Page.Idiom ? idiomPage?.SaveError :
                activePage == Page.Sheep ? sheepPage?.SaveError : activePage == Page.Jump ? jumpPage?.SaveError :
                activePage == Page.Lobby ? snakePage?.SaveError ?? aircraftFirePage?.SaveError ?? aircraftFireLoadWarning ?? linkMatchPage?.SaveError ?? flappyBirdPage?.SaveError ?? flappyLoadWarning ?? slidingPuzzlePage?.SaveError ?? tetrisPage?.SaveError ?? goldMinerPage?.SaveError ?? sokobanPage?.SaveError ?? happyBallPage?.SaveError ?? dinoPage?.SaveError ?? zumaPage?.SaveError ?? gomokuPage?.SaveError ?? minesweeperPage?.SaveError ?? sudokuPage?.SaveError ?? watermelonPage?.SaveError ?? idiomPage?.SaveError ?? feihualingPage?.SaveError ?? sheepPage?.SaveError ?? jumpPage?.SaveError : null;
            var target = activePage == Page.Lobby ? lobbyStatus : status;
            // The normal footer is hidden in Minesweeper focus mode; preserve shared save errors there.
            if (activePage == Page.Snake) snakePage?.SetHostSaveWarning(saveFailed ? saveWarning : null);
            if (activePage == Page.AircraftFire) aircraftFirePage?.SetHostSaveWarning(saveFailed ? saveWarning : null);
            if (activePage == Page.Minesweeper) minesweeperPage?.SetHostSaveWarning(saveFailed ? saveWarning : null);
            if (activePage == Page.Tetris) tetrisPage?.SetHostSaveWarning(saveFailed ? saveWarning : null);
            if (activePage == Page.HappyBall) happyBallPage?.SetHostSaveWarning(saveFailed ? saveWarning : null);
            if (activePage == Page.Sokoban) sokobanPage?.SetHostSaveWarning(saveFailed ? saveWarning : null);
            if (activePage == Page.LinkMatch) linkMatchPage?.SetHostSaveWarning(saveFailed ? saveWarning : null);
            if (activePage == Page.SlidingPuzzle) slidingPuzzlePage?.SetHostSaveWarning(saveFailed ? saveWarning : null);
            if (activePage == Page.FlappyBird) flappyBirdPage?.SetHostSaveWarning(saveFailed ? saveWarning : null);
            if (target != null) target.text = saveFailed ? saveWarning : pageError ?? message;
        }

        private void Update2048Records()
        {
            data.BestScore = Math.Max(data.BestScore, game.Score);
            for (int cell = 0; cell < 16; cell++) data.BestTile = Math.Max(data.BestTile, game.GetCell(cell));
        }

        // Checkpoint initialized games at navigation and lifecycle boundaries.
        private void Save()
        {
            aircraftFirePage?.Save();
            tetrisPage?.Save();
            flappyBirdPage?.Save();
            happyBallPage?.Save();
            dinoPage?.Save();
            jumpPage?.Save();
            sheepPage?.Save();
            idiomPage?.Save();
            if (activePage == Page.Feihualing) feihualingPage?.Save();
            watermelonPage?.Save();
            sudokuPage?.Save();
            minesweeperPage?.Save();
            zumaPage?.Save();
            gomokuPage?.Save();
            snakePage?.Save();
            sokobanPage?.Save();
            linkMatchPage?.Save();
            goldMinerPage?.Save();
            slidingPuzzlePage?.Save();
            SaveHostData();
        }

        // 2048 and shared preferences occupy the same atomic save file.
        private void SaveHostData()
        {
            if (store == null || game == null) return;
            data.Game = game.GetSnapshot();
            Update2048Records();
            string error;
            if (!store.TrySave(data, out error))
            {
                saveFailed = true;
                saveWarning = "进度暂未保存，请检查设备剩余空间。";
                Debug.LogWarning("XanaduYonder save failed: " + error);
                SetStatus(saveWarning);
            }
            else if (saveFailed)
            {
                saveFailed = false;
                saveWarning = null;
                SetStatus(activePage == Page.Snake || activePage == Page.Minesweeper || activePage == Page.Sokoban || activePage == Page.LinkMatch ? "" : "进度已保存。");
            }
            if (!saveFailed) AcknowledgeWebGameImportedPreferences();
            zumaPage?.CommonSettingsSaved(!saveFailed);
        }

        private void StopAnimation()
        {
            bakeryPage?.Suspend();
            aircraftFirePage?.Suspend();
            tetrisPage?.Suspend();
            flappyBirdPage?.Suspend();
            happyBallPage?.Suspend();
            dinoPage?.Suspend();
            jumpPage?.Suspend();
            sheepPage?.Suspend();
            idiomPage?.Suspend();
            watermelonPage?.Suspend();
            minesweeperPage?.Suspend();
            zumaPage?.Suspend();
            gomokuPage?.Suspend();
            snakePage?.Suspend();
            sokobanPage?.Suspend();
            linkMatchPage?.Suspend();
            slidingPuzzlePage?.Suspend();
            Settle2048Animation();
        }

        private void Cancel2048Animation()
        {
            feedback?.Stop();
            StopAllCoroutines();
            animating = false;
            board?.CancelGesture();
        }

        private void Settle2048Animation()
        {
            Cancel2048Animation();
            if (board != null && game != null) board.Render(game.GetSnapshot());
        }

        private void Update()
        {
            if (backExitPage != activePage) { ResetBackExitConfirmation(); backExitPage = activePage; }
            if (IsExternalGameRunning) return;
            if (paused || (!focused && !IsTextKeyboardInForeground())) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleBack();
                return;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Move(Direction.Left);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Move(Direction.Right);
            if (Input.GetKeyDown(KeyCode.UpArrow)) Move(Direction.Up);
            if (Input.GetKeyDown(KeyCode.DownArrow)) Move(Direction.Down);
        }

        private void HandleBackCore()
        {
            if (IsExternalGameRunning) return;
            var splash = GetComponent<BrandSplash>();
            if (splash != null && splash.BlocksInput) return;
            if (activePage == Page.Lobby && lobbyView != null && lobbyView.HasDisplayOptions)
            { lobbyView.CloseDisplayOptions(); return; }
            if (activePage == Page.Zuma) { zumaPage.HandleSystemBack(ConfirmSystemBackExit); return; }
            if (modal != null)
            {
                if (!modalBackReturnsToLobby || ConfirmSystemBackExit()) dismissModal?.Invoke();
            }
            else if (activePage == Page.GoldMiner && goldMinerPage.HandleSystemBack(ConfirmSystemBackExit)) { }
            else if (activePage == Page.AircraftFire) aircraftFirePage.HandleSystemBack(ConfirmSystemBackExit);
            else if (activePage == Page.FlappyBird) flappyBirdPage.HandleSystemBack(ConfirmSystemBackExit);
            else if (activePage == Page.Dino) dinoPage.HandleSystemBack(ConfirmSystemBackExit);
            else if (activePage == Page.Idiom && idiomPage.HandleBack()) { }
            else if (activePage == Page.Feihualing && feihualingPage.HandleBack()) { }
            else if (activePage == Page.Sudoku) sudokuPage.HandleSystemBack(ConfirmSystemBackExit);
            else if (activePage == Page.Minesweeper) minesweeperPage.HandleSystemBack(ConfirmSystemBackExit);
            else if (activePage == Page.Snake) snakePage.HandleSystemBack(ConfirmSystemBackExit);
            else if (activePage == Page.Sokoban) sokobanPage.HandleSystemBack(ConfirmSystemBackExit);
            else if (activePage == Page.LinkMatch) linkMatchPage.HandleSystemBack(ConfirmSystemBackExit);
            else if (activePage == Page.SlidingPuzzle) slidingPuzzlePage.HandleSystemBack(ConfirmSystemBackExit);
            else if (activePage == Page.HappyBall) happyBallPage.HandleSystemBack(ConfirmSystemBackExit);
            else if (activePage == Page.Tetris) tetrisPage.HandleSystemBack(ConfirmSystemBackExit);
            else if (activePage == Page.Gomoku) gomokuPage.HandleSystemBack(ConfirmSystemBackExit);
            else if (activePage == Page.ClassicBoards) boardPage.Back(ConfirmSystemBackExit);
            else if (activePage != Page.Lobby) { if (ConfirmSystemBackExit()) ShowLobby(); }
            else ShowDialog("暂时离开？", "进度已经保存在这台手机上。", "退出应用", Application.Quit, "再待一会儿", () => { });
        }
    }
}
