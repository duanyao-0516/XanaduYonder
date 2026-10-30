using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Core.Sokoban;
using XanaduYonder.Core.LinkMatch;
using XanaduYonder.Runtime.Jump;
using XanaduYonder.Runtime.Sheep;
using XanaduYonder.Runtime.Idiom;
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
    public sealed class AppController : MonoBehaviour
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
        private readonly List<KeyValuePair<Selectable, Selectable.Transition>> zumaHostTransitions = new List<KeyValuePair<Selectable, Selectable.Transition>>();
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
        private TetrisPage tetrisPage;
        private TetrisLoadResult tetrisPreview;
        private FlappyBirdPage flappyBirdPage;
        private Sprite flappyLobbySprite;
        private long flappyBest;
        private string flappyLoadWarning;
        private Text status;
        private Text lobbyStatus;
        private enum Page { Lobby, Game2048, Jump, Sheep, Idiom, Sudoku, Watermelon, Minesweeper, Zuma, Dino, Gomoku, HappyBall, Sokoban, GoldMiner, SlidingPuzzle, Tetris, FlappyBird, LinkMatch }
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
                if (!startupComplete || activePage != Page.Lobby || paused || !focused || modal != null ||
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
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (UiFactory.Font == null) throw new InvalidOperationException("The bundled Chinese font is missing.");
            store = new LocalSaveStore(Application.persistentDataPath);
            data = store.Load(out saveWarning);
            tetrisPreview = new TetrisSaveStore(Application.persistentDataPath).Load();
            happyBallBest = new HappyBallSaveStore(Application.persistentDataPath).Load(out var happyBallWarning)?.BestScore ?? 0;
            if (data == null) data = new SaveData { Game = new Game2048(random).GetSnapshot() };
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
            var statusBand = UiFactory.Band(frame, "Status", 1278, 34, 1);
            status = UiFactory.Label(statusBand, "", 56, 0, 608, 34, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
            ShowLobby();
            SetStatus(saveWarning ?? "所有玩法离线可用 · 对局或最高纪录自动保存");
            startupComplete = true;
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

        private void BuildLobby()
        {
            var root = UiFactory.Rect(frame, "Lobby", 0, 0, 720, 1320);
            UiFactory.Stretch(root);
            lobbyPage = root.gameObject;
            var entries = new List<LobbyEntry>
            {
                LobbyGame("sudoku", "Sudoku", "数独", "06", "逻辑策略", "九宫推理", "九宫之间，找到唯一。", "", "", EnterSudoku, LobbyPreviews.Sudoku),
                LobbyGame("minesweeper", "Minesweeper", "扫雷", "07", "逻辑策略", "经典扫雷", "数字之间，步步为营。", "首击安全 · 三档挑战", "首击安全 · 三档挑战", EnterMinesweeper, LobbyPreviews.Minesweeper),
                LobbyGame("sliding-puzzle", "SlidingPuzzle", "数字华容道", "14", "逻辑策略", "数字归位", "留一格空白，理顺每一步。", "", "", EnterSlidingPuzzle, LobbyPreviews.SlidingPuzzle),
                LobbyGame("sokoban", "Sokoban", "推箱子", "12", "逻辑策略", "经典推箱", "把每一只箱子，推回它的位置。", "", "", EnterSokoban, AddSokobanPreview),
                LobbyGame("gomoku", "Gomoku", "五子棋", "10", "逻辑策略", "黑白对弈", "一子一步，先连成五。", "人机 / 本地双人 · 三档难度", "人机 / 双人", EnterGomoku, AddGomokuPreview),
                LobbyGame("2048", "2048", "2048", "01", "合成消除", "数字益智", "让相同的数字相遇。", "", "", EnterGame, LobbyPreviews.Numbers2048),
                LobbyGame("watermelon", "Watermelon", "合成大西瓜", "04", "合成消除", "碰碰合成", "小小一颗，合成满满的快乐。", "水果 / 表情 / 校徽 · 三种主题", "三种主题", EnterWatermelon, AddWatermelonPreview),
                LobbyGame("sheep", "Sheep", "旸了个旸", "03", "合成消除", "层层三消", "翻开一层，凑齐三个。", "", "", EnterSheep, AddSheepPreview),
                LobbyGame("link-match", "LinkMatch", "连连看", "17", "合成消除", "轻松连线", "找到相同，连起快乐。", "轻松 / 挑战 · 五档棋盘", "轻松 / 挑战", EnterLinkMatch, AddLinkMatchPreview),
                LobbyGame("zuma", "Zuma", "Zuma", "08", "合成消除", "神庙弹珠", "转动青蛙，让同色弹珠相遇。", "横屏游玩 · 18 张地图 · 4 档难度", "18 张地图", EnterZuma, LobbyPreviews.Zuma),
                LobbyGame("tetris", "Tetris", "俄罗斯方块", "15", "合成消除", "方块慢落", "落下一块，留一点从容。", "", "", EnterTetris, LobbyPreviews.Tetris),
                LobbyGame("jump", "Jump", "跳一跳", "02", "敏捷挑战", "轻巧向前", "按住，松手，再远一点。", "", "", EnterJump, AddJumpPreview),
                LobbyGame("dino", "Dino", "Dino", "09", "敏捷挑战", "像素跑酷", "横屏或竖屏，跑得更远一点。", "", "", EnterDino, LobbyPreviews.Dino),
                LobbyGame("flappy-bird", "FlappyBird", "Flappy Bird", "16", "敏捷挑战", "轻点飞行", "轻点一下，再飞远一点。", "", "", EnterFlappyBird, AddFlappyPreview),
                LobbyGame("happy-ball", "HappyBall", "欢乐球球", "11", "敏捷挑战", "轻巧下落", "转动高塔，穿过层层豁口。", "", "", EnterHappyBall, LobbyPreviews.HappyBall),
                LobbyGame("gold-miner", "Gold Miner", "黄金矿工", "13", "敏捷挑战", "一起寻宝", "看准时机，把宝藏带回家。", "单人竖屏 / 双人横屏合作 · 10 关挑战", "单人 / 双人", EnterGoldMiner, AddGoldMinerPreview),
                LobbyGame("idiom", "Idiom", "成语接龙", "05", "文字语言", "一词一世界", "一来一往，认识更多好词。", "同字 / 同音 · 随时收藏生词", "同字 / 同音", EnterIdiom, null)
            };
            lobbyView = root.gameObject.AddComponent<LobbyView>();
            lobbyView.Initialize(entries, OpenSettings, LobbyDisplayPreferences.Load(), LobbyDisplayPreferences.Save);
            lobbyScroll = lobbyView.Scroll;
            lobbyStatus = lobbyView.StatusLabel;
            RefreshDinoRecord();
        }

        private static LobbyEntry LobbyGame(string id, string objectName, string name, string number,
            string category, string tagline, string description, string record, string compactRecord,
            Action enter, Action<Transform> drawLogo)
        {
            return new LobbyEntry
            {
                Id = id, ObjectName = objectName, Name = name, Number = number,
                Category = category, Tagline = tagline, Description = description,
                Record = record, CompactRecord = compactRecord, Enter = enter,
                // BirdRest's 124x112 atlas cell has asymmetric transparent padding (alpha bounds
                // x=5..122, y=23..109 from the top). At 82x74 and 7 degrees, its visible center
                // is (1.39,-6.57) from the image center; keep that artwork optically centered.
                BuildLogo = drawLogo == null ? null : slot => LobbyArtwork.Build(slot, drawLogo,
                    id == "flappy-bird" ? new Vector2(1.39f, -6.57f) : Vector2.zero)
            };
        }

        private static void AddWatermelonPreview(Transform parent)
        {
            var fruit = UiFactory.Rect(parent, "Watermelon artwork", 0, 0, 90, 90).gameObject.AddComponent<RawImage>();
            fruit.texture = Resources.Load<Texture2D>("Watermelon/Fruit/11");
            fruit.raycastTarget = false;
        }

        private static void AddSokobanPreview(Transform parent)
        {
            var crate = UiFactory.Rect(parent, "Sokoban crate preview", 0, 0, 90, 90).gameObject.AddComponent<RawImage>();
            crate.texture = Resources.Load<Texture2D>("Sokoban/Sprites/crate");
            crate.raycastTarget = false;
        }

        private static void AddGoldMinerPreview(Transform parent)
        {
            var miner = UiFactory.Rect(parent, "Gold Miner celebrating miner", 0, 0, 142, 138).gameObject.AddComponent<Image>();
            foreach (var pose in Resources.LoadAll<Sprite>("GoldMiner/characters/miner-p1-states"))
                if (pose.name == "success") { miner.sprite = pose; break; }
            miner.preserveAspect = true;
            miner.raycastTarget = false;
            var treasure = UiFactory.Rect(parent, "Gold Miner prize", 90, 88, 62, 54).gameObject.AddComponent<Image>();
            treasure.sprite = Resources.Load<Sprite>("GoldMiner/objects/ore_gold_medium");
            treasure.preserveAspect = true;
            treasure.raycastTarget = false;
        }

        private void AddFlappyPreview(Transform parent)
        {
            var bird = UiFactory.Rect(parent, "Bird artwork", 0, 0, 82, 74).gameObject.AddComponent<Image>();
            bird.preserveAspect = true;
            if (flappyLobbySprite == null) flappyLobbySprite = FlappyArt.LoadSprite("BirdRest");
            bird.sprite = flappyLobbySprite;
            bird.raycastTarget = false;
            bird.rectTransform.localRotation = Quaternion.Euler(0, 0, 7);
        }

        private void EnterGoldMiner()
        {
            linkMatchPage?.Leave();
            flappyBirdPage?.Leave();
            tetrisPage?.Leave();
            slidingPuzzlePage?.Leave();
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
                    value => { data.SoundEnabled = value; if (!value) feedback.Stop(); Save(); },
                    value => { data.HapticsEnabled = value; Save(); },
                    () => feedback.Land(false, false, data.HapticsEnabled));
            }
            activePage = Page.GoldMiner;
            frame.gameObject.SetActive(false);
            goldMinerPage.SetForeground(focused, paused);
            goldMinerPage.Enter();
        }

        private static void AddGomokuPreview(Transform parent)
        {
            for (int i = 0; i < 3; i++)
            {
                UiFactory.Panel(parent, "Gomoku preview row " + i, 432, 82 + i * 34, 136, 1.5f, UiFactory.Hex("8C907A"), false).raycastTarget = false;
                UiFactory.Panel(parent, "Gomoku preview column " + i, 466 + i * 34, 62, 1.5f, 108, UiFactory.Hex("8C907A"), false).raycastTarget = false;
            }
            for (int i = 0; i < 5; i++)
            {
                float x = 466 + (i % 3) * 34, y = 82 + (i / 3) * 34;
                UiFactory.CirclePanel(parent, "Gomoku preview stone " + i, x - 13, y - 13, 26,
                    i % 2 == 0 ? UiFactory.Ink : UiFactory.Paper).raycastTarget = false;
            }
        }

        private static void AddLinkMatchPreview(Transform parent)
        {
            var art = UiFactory.Rect(parent, "Link Match fruit preview", 413, 56, 171, 171);
            string[] fruits = { "strawberry", "banana", "strawberry", "grapes" };
            for (int i = 0; i < fruits.Length; i++)
            {
                float x = 26 + i % 2 * 69, y = 15 + i / 2 * 74;
                UiFactory.Panel(art, "Fruit shadow " + i, x, y + 4, 59, 59, UiFactory.Hex("D3DBC3"));
                var tile = UiFactory.Panel(art, "Fruit tile " + i, x, y, 59, 59, UiFactory.Hex("FFFCF0"));
                var fruit = UiFactory.Rect(tile.transform, "Fruit", 3.5f, 3.5f, 52, 52).gameObject.AddComponent<RawImage>();
                fruit.texture = Resources.Load<Texture2D>("LinkMatch/Sprites/" + fruits[i]);
                fruit.raycastTarget = false;
            }
            var route = UiFactory.Hex("739652");
            UiFactory.Panel(art, "Pair route top", 16, 48.5f, 28, 3, route, false);
            UiFactory.Panel(art, "Pair route outside", 14.5f, 50, 3, 74, route, false);
            UiFactory.Panel(art, "Pair route bottom", 16, 122.5f, 28, 3, route, false);
        }

        private void EnterDino()
        {
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            gomokuPage?.Leave();
            StopAnimation(); CloseModal();
            minesweeperPage?.Leave();
            sudokuPage.Leave(); jumpPage.Leave(); sheepPage.Leave();
            idiomPage?.Leave(); watermelonPage?.Leave();
            lobbyPage.SetActive(false); gamePage.SetActive(false);
            if (status != null) status.gameObject.SetActive(false);
            activePage = Page.Dino;
            if (dinoPage == null)
            {
                var pageRoot = UiFactory.Rect(canvasRoot, "Dino page", 0, 0, 720, 1320);
                dinoPage = pageRoot.gameObject.AddComponent<DinoPage>();
                dinoPage.Initialize(pageRoot, canvasRoot, ShowLobby, RequestDinoOrientation,
                    () => activePage == Page.Dino && !paused && focused,
                    () => data.SoundEnabled, () => data.HapticsEnabled,
                    () => { data.SoundEnabled = !data.SoundEnabled; if (!data.SoundEnabled) feedback.Stop(); Save(); },
                    () => { data.HapticsEnabled = !data.HapticsEnabled; Save(); });
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
                SetStatus, OpenIdiomModal, CloseModal);
            idiomPage.Changed += RefreshIdiomRecord;
        }

        private RectTransform OpenIdiomModal(string title, float height, Action back)
        {
            var panel = CreateModal(title, height, back);
            dismissModal = back;
            return panel;
        }

        private void RefreshIdiomRecord()
        {
            if (idiomPage != null) lobbyView?.SetRecord("idiom", idiomPage.BestLabel, "同字 / 同音");
        }

        private void EnterIdiom()
        {
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
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
            activePage = Page.Idiom;
            lobbyPage.SetActive(false);
            gamePage.SetActive(false);
            BuildIdiom();
            if (status != null) status.gameObject.SetActive(false);
            idiomPage.Enter();
        }

        private void EnterWatermelon()
        {
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
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
            activePage = Page.Watermelon;
            lobbyPage.SetActive(false);
            gamePage.SetActive(false);
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
            dismissModal = back;
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
                SetZumaSetting, feedback);
            zumaPage.PresentationReady += () => { if (activePage == Page.Zuma) canvasRoot.gameObject.SetActive(false); };
        }

        private void EnterZuma()
        {
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
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
            SetZumaHostInput(false);
            zumaPage.Enter();
        }

        private void FinishZumaExit()
        {
            activePage = Page.Lobby;
            SetZumaHostInput(true);
            canvasRoot.gameObject.SetActive(true);
            ShowLobby();
        }

        private void SetZumaHostInput(bool enabled)
        {
            var interaction = canvasRoot.GetComponent<CanvasGroup>();
            if (interaction == null) interaction = canvasRoot.gameObject.AddComponent<CanvasGroup>();
            if (!enabled)
            {
                zumaHostTransitions.Clear();
                foreach (var control in canvasRoot.GetComponentsInChildren<Selectable>())
                {
                    zumaHostTransitions.Add(new KeyValuePair<Selectable, Selectable.Transition>(control, control.transition));
                    control.transition = Selectable.Transition.None;
                }
                interaction.interactable = false;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }
            else
            {
                interaction.interactable = true;
                foreach (var entry in zumaHostTransitions) if (entry.Key != null) entry.Key.transition = entry.Value;
                zumaHostTransitions.Clear();
            }
            canvasRoot.GetComponent<GraphicRaycaster>().enabled = enabled;
        }

        private bool SetZumaSetting(bool soundSetting, bool enabled)
        {
            if (soundSetting) data.SoundEnabled = enabled;
            else data.HapticsEnabled = enabled;
            if (!data.SoundEnabled) feedback.Stop();
            Save();
            return !saveFailed;
        }

        private static void AddSheepPreview(Transform parent)
        {
            for (var i = 0; i < 3; i++)
            {
                var tile = UiFactory.Rect(parent, "Sheep tile preview " + i, 420 + i * 52, i == 1 ? 66 : 82, 64, 72);
                var backing = tile.gameObject.AddComponent<RawImage>();
                backing.texture = Resources.Load<Texture2D>("Sheep/Tiles/block_bg");
                backing.raycastTarget = false;
                var icon = UiFactory.Rect(tile, "Tile icon", 0, 0, 64, 72).gameObject.AddComponent<RawImage>();
                icon.texture = Resources.Load<Texture2D>("Sheep/Tiles/block_" + (i + 1));
                icon.raycastTarget = false;
            }
        }

        private static void AddJumpPreview(Transform parent)
        {
            var frame = LobbyPreviews.Frame(parent, "Jump");
            var artwork = UiFactory.Rect(frame, "Jump artwork", 0, 0, 140, 167);
            UiFactory.Center(artwork);
            artwork.localScale = Vector3.one * .85f;
            var platform = UiFactory.Rect(artwork, "Jump platform preview", 0, 42, 140, 125).gameObject.AddComponent<RawImage>();
            platform.texture = Resources.Load<Texture2D>("Jump/Preview/platform");
            platform.raycastTarget = false;
            var pawn = UiFactory.Rect(artwork, "Jump pawn preview", 50, 0, 25, 69).gameObject.AddComponent<RawImage>();
            pawn.texture = Resources.Load<Texture2D>("Jump/Preview/pawn");
            pawn.raycastTarget = false;
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
            dismissModal = back;
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
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            happyBallPage?.Leave(); gomokuPage?.Leave(); dinoPage?.Leave();
            StopAnimation(); CloseModal(); minesweeperPage.Leave(); sudokuPage.Leave();
            jumpPage.Leave(); sheepPage.Leave(); idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = true;
            activePage = Page.Sokoban; lobbyPage.SetActive(false); gamePage.SetActive(false);
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
            linkMatchPage?.Leave();
            flappyBirdPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            sokobanPage?.Leave(); happyBallPage?.Leave(); gomokuPage?.Leave(); dinoPage?.Leave();
            StopAnimation(); CloseModal(); minesweeperPage?.Leave(); sudokuPage?.Leave();
            jumpPage?.Leave(); sheepPage?.Leave(); idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = true;
            activePage = Page.SlidingPuzzle; lobbyPage.SetActive(false); gamePage.SetActive(false);
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
                linkMatchPage.RetryHostSave = Save;
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
            flappyBirdPage?.Leave(); tetrisPage?.Leave(); slidingPuzzlePage?.Leave(); LeaveGoldMiner();
            sokobanPage?.Leave(); happyBallPage?.Leave(); gomokuPage?.Leave(); dinoPage?.Leave();
            StopAnimation(); CloseModal(); minesweeperPage.Leave(); sudokuPage.Leave();
            jumpPage.Leave(); sheepPage.Leave(); idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = true;
            activePage = Page.LinkMatch; lobbyPage.SetActive(false); gamePage.SetActive(false);
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
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            sokobanPage?.Leave();
            gomokuPage?.Leave(); dinoPage?.Leave();
            StopAnimation(); CloseModal();
            minesweeperPage.Leave(); sudokuPage.Leave(); jumpPage.Leave(); sheepPage.Leave();
            idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            activePage = Page.HappyBall; lobbyPage.SetActive(false); gamePage.SetActive(false);
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
            linkMatchPage?.Leave();
            flappyBirdPage?.Leave();
            slidingPuzzlePage?.Leave();
            LeaveGoldMiner(); sokobanPage?.Leave(); happyBallPage?.Leave(); gomokuPage?.Leave(); dinoPage?.Leave();
            StopAnimation(); CloseModal(); minesweeperPage.Leave(); sudokuPage.Leave(); jumpPage.Leave(); sheepPage.Leave();
            idiomPage?.Leave(); watermelonPage?.Leave();
            var safeFrame = frame.GetComponent<SafeDesignFrame>();
            safeFrame.WidthOnly = true;
            activePage = Page.Tetris; lobbyPage.SetActive(false); gamePage.SetActive(false);
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
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            sokobanPage?.Leave(); happyBallPage?.Leave(); gomokuPage?.Leave(); dinoPage?.Leave();
            StopAnimation(); CloseModal(); minesweeperPage.Leave(); sudokuPage.Leave();
            jumpPage.Leave(); sheepPage.Leave(); idiomPage?.Leave(); watermelonPage?.Leave();
            Screen.orientation = ScreenOrientation.Portrait;
            frame.GetComponent<SafeDesignFrame>().WidthOnly = true;
            activePage = Page.FlappyBird; lobbyPage.SetActive(false); gamePage.SetActive(false);
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
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            dinoPage?.Leave();
            StopAnimation(); CloseModal(); minesweeperPage?.Leave(); sudokuPage.Leave(); jumpPage.Leave(); sheepPage.Leave();
            idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            activePage = Page.Gomoku; lobbyPage.SetActive(false); gamePage.SetActive(false);
            if (status != null) status.gameObject.SetActive(true);
            gomokuPage.Enter();
        }

        private void EnterMinesweeper()
        {
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            gomokuPage?.Leave();
            dinoPage?.Leave();
            StopAnimation(); CloseModal(); sudokuPage.Leave(); jumpPage.Leave(); sheepPage.Leave();
            idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            activePage = Page.Minesweeper; lobbyPage.SetActive(false); gamePage.SetActive(false);
            if (status != null) status.gameObject.SetActive(true);
            minesweeperPage.Enter();
        }

        private void EnterSudoku()
        {
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
            sokobanPage?.Leave();
            happyBallPage?.Leave();
            gomokuPage?.Leave();
            dinoPage?.Leave();
            minesweeperPage?.Leave();
            StopAnimation(); CloseModal(); jumpPage.Leave(); sheepPage.Leave();
            idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            if (status != null) status.gameObject.SetActive(true);
            activePage = Page.Sudoku; lobbyPage.SetActive(false); gamePage.SetActive(false); sudokuPage.Enter();
        }

        private void EnterSheep()
        {
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
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
            activePage = Page.Sheep;
            lobbyPage.SetActive(false);
            gamePage.SetActive(false);
            sheepPage.Enter();
        }

        private void EnterJump()
        {
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
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
            activePage = Page.Jump;
            lobbyPage.SetActive(false);
            gamePage.SetActive(false);
            jumpPage.Enter();
        }

        private void BuildGame()
        {
            var root = UiFactory.Rect(frame, "2048 page", 0, 0, 720, 1320);
            UiFactory.Stretch(root);
            gamePage = root.gameObject;
            var navigation = UiFactory.Band(root, "Game navigation", 0, 92, 0);
            UiFactory.IconButton(navigation, UiIconKind.Home, 56, 34, 58, 58, ShowLobby);
            UiFactory.Label(navigation, "XANADU / YONDER", 226, 34, 278, 58, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.IconButton(navigation, UiIconKind.Settings, 606, 34, 58, 58, OpenSettings);
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
            UiFactory.Button(actions, "重新开始", 56, 0, 296, 76, ConfirmRestart);
            UiFactory.Button(actions, "玩法说明", 368, 0, 296, 76, ShowHelp);
            var instructions = UiFactory.Band(root, "Game instructions", 1170, 44, .86f);
            UiFactory.Label(instructions, "上下左右滑动，相同数字合并。", 56, 0, 608, 44, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
        }

        private void EnterGame()
        {
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
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
            activePage = Page.Game2048;
            lobbyPage.SetActive(false);
            gamePage.SetActive(true);
            board.Render(game.GetSnapshot());
            RefreshScores();
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
            linkMatchPage?.Leave();
            tetrisPage?.Leave();
            LeaveGoldMiner();
            slidingPuzzlePage?.Leave();
            flappyBirdPage?.Leave();
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
            activePage = Page.Lobby;
            gamePage.SetActive(false);
            lobbyPage.SetActive(true);
            RefreshScores();
            Save();
            SetStatus(saveWarning ?? "所有玩法离线可用 · 对局或最高纪录自动保存");
        }

        private void Move(Direction direction)
        {
            if (!CanAcceptMove) return;
            var result = game.Move(direction);
            if (!result.Changed) return;
            Save();
            RefreshScores();
            feedback.Play(result.ScoreDelta > 0, data.SoundEnabled, data.HapticsEnabled);
            animating = true;
            StartCoroutine(AnimateMove(result));
        }

        private bool CanAcceptMove => inGame && modal == null && !animating && !paused && focused && !game.IsGameOver;

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
            if (activePage == Page.Sokoban) { sokobanPage?.CheckEndState(); return; }
            if (activePage == Page.HappyBall) { happyBallPage?.CheckEndState(); return; }
            if (activePage == Page.Dino) return;
            if (activePage == Page.Gomoku) { gomokuPage?.CheckEndState(); return; }
            if (activePage == Page.Minesweeper) { minesweeperPage?.CheckEndState(); return; }
            if (activePage == Page.Watermelon) { watermelonPage?.CheckEndState(); return; }
            if (activePage == Page.Idiom) { idiomPage?.CheckEndState(); return; }
            if (activePage == Page.Jump) { jumpPage.CheckEndState(); return; }
            if (activePage == Page.Sheep) { sheepPage.CheckEndState(); return; }
            if (activePage == Page.Sudoku) { sudokuPage.CheckEndState(); return; }
            if (!inGame || modal != null || paused || !focused) return;
            if (game.HasWon && !game.HasAcknowledgedWin)
                ShowDialog("你好，2048！", "你已经完成这次挑战。\n还可以继续，看看数字能走多远。", "继续挑战", () =>
                {
                    game.AcknowledgeWin();
                    Save();
                    CheckEndState();
                }, "返回大厅", ShowLobby);
            else if (game.IsGameOver)
                ShowDialog("这一局，很精彩。", "暂时没有可以移动的方块了。\n本局得分  " + game.Score, "再来一局", Restart, "返回大厅", ShowLobby);
        }

        private void ConfirmRestart()
        {
            ShowDialog("重新开始这一局？", "当前棋盘将被清空，\n最高纪录和设置会保留。", "重新开始", Restart, "继续当前游戏", CheckEndState);
        }

        private void Restart()
        {
            StopAnimation();
            game = new Game2048(random);
            Save();
            EnterGame();
        }

        private void ShowHelp()
        {
            ShowDialog("让相同的数字相遇", "向一个方向滑动整个棋盘。\n相同的方块相遇，就合成两倍数字。\n每次有效移动后，会出现一个新方块。\n合成 2048 即可获胜，也能继续挑战。", "知道了", CheckEndState);
        }

        private void OpenSettings() { OpenSettings(null); }

        private void OpenSettings(Action after)
        {
            Action finish = () => { CloseModal(); if (after != null) after(); else CheckEndState(); };
            var panel = CreateModal("简单一点，刚刚好");
            UiFactory.Label(panel, "设置在所有游戏中共用", 32, 104, 532, 42, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Button(panel, "声音  ·  " + (data.SoundEnabled ? "开启" : "关闭"), 32, 178, 532, 70, () =>
            {
                data.SoundEnabled = !data.SoundEnabled;
                if (!data.SoundEnabled) feedback.Stop();
                goldMinerPage?.SettingsChanged();
                Save();
                CloseModal();
                OpenSettings(after);
            });
            UiFactory.Button(panel, "触感  ·  " + (data.HapticsEnabled ? "开启" : "关闭"), 32, 264, 532, 70, () =>
            {
                data.HapticsEnabled = !data.HapticsEnabled;
                Save();
                CloseModal();
                OpenSettings(after);
            });
            UiFactory.Button(panel, "完成", 32, 376, 532, 74, finish, true);
            dismissModal = finish;
        }

        private void ShowDialog(string title, string message, string primary, Action primaryAction, string secondary = null, Action secondaryAction = null)
        {
            var panel = CreateModal(title);
            UiFactory.Label(panel, message, 32, 114, 532, 166, 24, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Button(panel, primary, 32, 300, 532, 74, () => { CloseModal(); primaryAction(); }, true);
            if (secondary != null)
                UiFactory.Button(panel, secondary, 32, 390, 532, 62, () => { CloseModal(); secondaryAction(); });
            dismissModal = () =>
            {
                CloseModal();
                if (secondaryAction != null) secondaryAction();
                else CheckEndState();
            };
        }

        private RectTransform CreateModal(string title, float requestedHeight = 488, Action outsideDismiss = null)
        {
            StopAnimation();
            CloseModal();
            var overlay = UiFactory.Panel(canvasRoot, "Dialog overlay", 0, 0, 720, 1320, new Color(.06f, .14f, .1f, .48f), false);
            UiFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            modal = overlay.gameObject;
            if (outsideDismiss != null)
            {
                // Keep the dismiss button behind, rather than above, the panel. A click
                // on panel whitespace must not bubble to a parent dismiss handler.
                var backdrop = UiFactory.Panel(overlay.transform, "Dismiss outside dialog", 0, 0, 720, 1320, Color.clear, false);
                UiFactory.Stretch(backdrop.rectTransform);
                backdrop.raycastTarget = true;
                var dismiss = backdrop.gameObject.AddComponent<Button>();
                dismiss.targetGraphic = backdrop;
                dismiss.transition = Selectable.Transition.None;
                dismiss.onClick.AddListener(() => outsideDismiss());
            }
            var modalFrame = UiFactory.Rect(modal.transform, "Dialog safe area", 0, 0, 720, 1320);
            modalFrame.gameObject.AddComponent<SafeDesignFrame>().Initialize(modalFrame, canvasRoot);
            var height = Mathf.Clamp(requestedHeight, 300, Mathf.Max(488, modalFrame.rect.height - 64));
            var panel = UiFactory.Panel(modalFrame, "Dialog", 0, 0, 596, height, UiFactory.Paper);
            panel.raycastTarget = true;
            UiFactory.Center(panel.rectTransform);
            UiFactory.Label(panel.transform, title, outsideDismiss != null ? 98 : 32, 36,
                outsideDismiss != null ? 400 : 532, 62, 33, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold).supportRichText = false;
            return panel.rectTransform;
        }

        private void CloseModal()
        {
            if (modal != null)
            {
                modal.SetActive(false);
                Destroy(modal);
                modal = null;
            }
            dismissModal = null;
        }

        private void RefreshScores()
        {
            Update2048Records();
            scoreText.text = game.Score.ToString();
            bestText.text = data.BestScore.ToString();
            lobbyView?.SetRecord("2048", "最高纪录 " + data.BestScore + " · 最高数字 " + data.BestTile, "最高 " + data.BestScore + " 分");
            RefreshJumpBest();
            RefreshSheepRecord();
            RefreshIdiomRecord();
            RefreshWatermelonRecord();
            RefreshSudokuRecord();
            RefreshDinoRecord();
            RefreshMinesweeperRecord();
            RefreshSokobanRecord();
            RefreshLinkMatchRecord();
            RefreshSlidingPuzzleRecord();
            RefreshHappyBallRecord();
            RefreshTetrisRecord();
            RefreshFlappyRecord();
            RefreshGomokuRecord();
        }

        private void SetStatus(string message)
        {
            var pageError = activePage == Page.LinkMatch ? linkMatchPage?.SaveError : activePage == Page.FlappyBird ? flappyBirdPage?.SaveError : activePage == Page.SlidingPuzzle ? slidingPuzzlePage?.SaveError : activePage == Page.Tetris ? tetrisPage?.SaveError : activePage == Page.GoldMiner ? goldMinerPage?.SaveError : activePage == Page.Sokoban ? sokobanPage?.SaveError : activePage == Page.HappyBall ? happyBallPage?.SaveError : activePage == Page.Dino ? dinoPage?.SaveError : activePage == Page.Zuma ? zumaPage?.SaveError : activePage == Page.Gomoku ? gomokuPage?.SaveError : activePage == Page.Minesweeper ? minesweeperPage?.SaveError : activePage == Page.Sudoku ? sudokuPage?.SaveError : activePage == Page.Watermelon ? watermelonPage?.SaveError : activePage == Page.Idiom ? idiomPage?.SaveError :
                activePage == Page.Sheep ? sheepPage?.SaveError : activePage == Page.Jump ? jumpPage?.SaveError :
                activePage == Page.Lobby ? linkMatchPage?.SaveError ?? flappyBirdPage?.SaveError ?? flappyLoadWarning ?? slidingPuzzlePage?.SaveError ?? tetrisPage?.SaveError ?? goldMinerPage?.SaveError ?? sokobanPage?.SaveError ?? happyBallPage?.SaveError ?? dinoPage?.SaveError ?? zumaPage?.SaveError ?? gomokuPage?.SaveError ?? minesweeperPage?.SaveError ?? sudokuPage?.SaveError ?? watermelonPage?.SaveError ?? idiomPage?.SaveError ?? sheepPage?.SaveError ?? jumpPage?.SaveError : null;
            var target = activePage == Page.Lobby ? lobbyStatus : status;
            // The normal footer is hidden in Minesweeper focus mode; preserve shared save errors there.
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

        private void Save()
        {
            tetrisPage?.Save();
            flappyBirdPage?.Save();
            happyBallPage?.Save();
            dinoPage?.Save();
            jumpPage?.Save();
            sheepPage?.Save();
            idiomPage?.Save();
            watermelonPage?.Save();
            sudokuPage?.Save();
            minesweeperPage?.Save();
            zumaPage?.Save();
            gomokuPage?.Save();
            sokobanPage?.Save();
            linkMatchPage?.Save();
            goldMinerPage?.Save();
            slidingPuzzlePage?.Save();
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
                SetStatus(activePage == Page.Minesweeper || activePage == Page.Sokoban || activePage == Page.LinkMatch ? "" : "进度已保存。");
            }
            zumaPage?.CommonSettingsSaved(!saveFailed);
        }

        private void StopAnimation()
        {
            tetrisPage?.Suspend();
            flappyBirdPage?.Suspend();
            happyBallPage?.Suspend();
            dinoPage?.Suspend();
            feedback?.Stop();
            jumpPage?.Suspend();
            sheepPage?.Suspend();
            idiomPage?.Suspend();
            watermelonPage?.Suspend();
            minesweeperPage?.Suspend();
            zumaPage?.Suspend();
            gomokuPage?.Suspend();
            sokobanPage?.Suspend();
            linkMatchPage?.Suspend();
            slidingPuzzlePage?.Suspend();
            StopAllCoroutines();
            animating = false;
            if (board != null)
            {
                board.CancelGesture();
                if (game != null) board.Render(game.GetSnapshot());
            }
        }

        private void Update()
        {
            if (paused || (!focused && !(activePage == Page.Idiom && idiomPage != null && idiomPage.KeyboardVisible && idiomPage.KeyboardForeground))) return;
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

        private void HandleBack()
        {
            var splash = GetComponent<BrandSplash>();
            if (splash != null && splash.BlocksInput) return;
            if (activePage == Page.Lobby && lobbyView != null && lobbyView.HasDisplayOptions)
            { lobbyView.CloseDisplayOptions(); return; }
            if (activePage == Page.Zuma) { zumaPage.HandleBack(); return; }
            if (modal != null) dismissModal?.Invoke();
            else if (activePage == Page.GoldMiner && goldMinerPage.HandleBack()) { }
            else if (activePage == Page.FlappyBird) flappyBirdPage.Back();
            else if (activePage == Page.Dino) dinoPage.Back();
            else if (activePage == Page.Idiom && idiomPage.HandleBack()) { }
            else if (activePage == Page.Sudoku) sudokuPage.Back();
            else if (activePage == Page.Minesweeper) minesweeperPage.Back();
            else if (activePage == Page.Sokoban) sokobanPage.Back();
            else if (activePage == Page.LinkMatch) linkMatchPage.Back();
            else if (activePage == Page.SlidingPuzzle) slidingPuzzlePage.Back();
            else if (activePage == Page.HappyBall) happyBallPage.Back();
            else if (activePage == Page.Tetris) tetrisPage.Back();
            else if (activePage == Page.Gomoku) gomokuPage.Back();
            else if (activePage != Page.Lobby) ShowLobby();
            else ShowDialog("暂时离开？", "进度已经保存在这台手机上。", "退出应用", Application.Quit, "再待一会儿", () => { });
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            goldMinerPage?.SetForeground(focused, paused);
            zumaPage?.HostActivity(!paused && focused);
            if (value && (activePage == Page.Minesweeper || activePage == Page.Gomoku || activePage == Page.HappyBall || activePage == Page.Sokoban || activePage == Page.SlidingPuzzle || activePage == Page.Tetris || activePage == Page.FlappyBird || activePage == Page.LinkMatch)) CloseModal();
            tetrisPage?.SetForeground(!paused && focused);
            flappyBirdPage?.SetForeground(!paused && focused);
            happyBallPage?.SetForeground(!paused && focused);
            minesweeperPage?.SetForeground(!paused && focused);
            sokobanPage?.SetForeground(!paused && focused);
            slidingPuzzlePage?.SetForeground(!paused && focused);
            linkMatchPage?.SetForeground(!paused && focused);
            gomokuPage?.SetForeground(!paused && focused);
            sudokuPage?.SetForeground(!paused && focused);
            if (value) { idiomPage?.PauseForBackground(); StopAnimation(); feedback?.Stop(); Save(); }
            else CheckEndState();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            goldMinerPage?.SetForeground(focused, paused);
            zumaPage?.HostActivity(!paused && focused);
            if (!value && (activePage == Page.Minesweeper || activePage == Page.Gomoku || activePage == Page.HappyBall || activePage == Page.Sokoban || activePage == Page.SlidingPuzzle || activePage == Page.Tetris || activePage == Page.FlappyBird || activePage == Page.LinkMatch)) CloseModal();
            tetrisPage?.SetForeground(!paused && focused);
            flappyBirdPage?.SetForeground(!paused && focused);
            happyBallPage?.SetForeground(!paused && focused);
            minesweeperPage?.SetForeground(!paused && focused);
            sokobanPage?.SetForeground(!paused && focused);
            slidingPuzzlePage?.SetForeground(!paused && focused);
            linkMatchPage?.SetForeground(!paused && focused);
            gomokuPage?.SetForeground(!paused && focused);
            sudokuPage?.SetForeground(!paused && focused);
            if (!value)
            {
                // Android's soft input dialog sends focus(false) while the Activity
                // remains resumed. Real backgrounding is handled by OnApplicationPause.
                if (activePage != Page.Idiom) StopAnimation();
                feedback?.Stop();
                Save();
            }
            else CheckEndState();
        }

        private void OnDestroy() { if (flappyLobbySprite != null) Destroy(flappyLobbySprite); }

        private void OnApplicationQuit() { zumaPage?.Suspend(); linkMatchPage?.Suspend(); Save(); }
    }
}
