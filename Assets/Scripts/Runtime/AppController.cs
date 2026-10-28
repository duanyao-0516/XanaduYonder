using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Core.Sokoban;
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
using XanaduYonder.Runtime.GoldMiner;

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
        private GameObject gamePage;
        private GameObject modal;
        private Action dismissModal;
        private BoardView board;
        private GameFeedback feedback;
        private Text scoreText;
        private Text bestText;
        private Text lobbyBestText;
        private Text lobbyJumpBestText;
        private Text lobbySheepRecordText;
        private Text lobbySudokuRecordText;
        private JumpPage jumpPage;
        private SheepPage sheepPage;
        private IdiomPage idiomPage;
        private Text lobbyIdiomRecordText;
        private WatermelonPage watermelonPage;
        private Text lobbyWatermelonRecordText;
        private SudokuPage sudokuPage;
        private DinoPage dinoPage;
        private DinoSaveData dinoPreview;
        private Text lobbyDinoRecordText;
        private MinesweeperPage minesweeperPage;
        private ZumaPage zumaPage;
        private Text lobbyMinesweeperRecordText;
        private HappyBallPage happyBallPage;
        private Text lobbyHappyBallRecordText;
        private long happyBallBest;
        private GomokuPage gomokuPage;
        private Text lobbyGomokuRecordText;
        private SokobanPage sokobanPage;
        private Text lobbySokobanRecordText;
        private GoldMinerPage goldMinerPage;
        private Text status;
        private Text lobbyStatus;
        private enum Page { Lobby, Game2048, Jump, Sheep, Idiom, Sudoku, Watermelon, Minesweeper, Zuma, Dino, Gomoku, HappyBall, Sokoban, GoldMiner }
        private Page activePage;
        private bool inGame => activePage == Page.Game2048;
        private bool animating;
        private bool paused;
        private bool focused = true;
        private string saveWarning;
        private bool saveFailed;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (UiFactory.Font == null) throw new InvalidOperationException("The bundled Chinese font is missing.");
            store = new LocalSaveStore(Application.persistentDataPath);
            data = store.Load(out saveWarning);
            happyBallBest = new HappyBallSaveStore(Application.persistentDataPath).Load(out var happyBallWarning)?.BestScore ?? 0;
            if (data == null) data = new SaveData { Game = new Game2048(random).GetSnapshot() };
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
            var statusBand = UiFactory.Band(frame, "Status", 1278, 34, 1);
            status = UiFactory.Label(statusBand, "", 56, 0, 608, 34, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
            ShowLobby();
            SetStatus(saveWarning ?? "所有玩法离线可用 · 对局或最高纪录自动保存");
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
            var navigation = UiFactory.Band(root, "Lobby navigation", 0, 92, 0);
            UiFactory.Label(navigation, "XANADU  /  YONDER", 56, 30, 470, 40, 22, UiFactory.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.IconButton(navigation, UiIconKind.Settings, 606, 22, 58, 58, OpenSettings);
            // Card dimensions and gaps stay fixed; additional games extend this content instead of shrinking it.
            var viewport = UiFactory.Rect(root, "Lobby list viewport", 0, 108, 720, 1196);
            UiFactory.Stretch(viewport);
            viewport.offsetMin = new Vector2(0, 16);
            viewport.offsetMax = new Vector2(0, -108);
            var scrollSurface = viewport.gameObject.AddComponent<Image>();
            scrollSurface.color = Color.clear;
            scrollSurface.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiFactory.Rect(viewport, "Lobby list content", 0, 0, 720, 1790);
            content.anchorMin = content.anchorMax = new Vector2(.5f, 1);
            content.pivot = new Vector2(.5f, 1);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = .12f;
            scroll.decelerationRate = .10f;
            scroll.scrollSensitivity = 36;

            var introduction = UiFactory.Rect(content, "Lobby introduction", 0, 0, 720, 180);
            UiFactory.Label(introduction, "小小一局，慢慢向前。", 56, 0, 608, 92, 44, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(introduction, "把片刻空闲，留给一点简单的快乐。", 56, 100, 608, 52, 25, UiFactory.Muted);
            var cardCount = 0;
            var card = AddLobbyCard(content, "2048", cardCount++, UiFactory.Hex("E3E9DF"));
            UiFactory.Label(card.transform, cardCount.ToString("00") + "   /   数字益智", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(card.transform, "2048", 28, 64, 552, 104, 82, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(card.transform, "让相同的数字相遇。", 32, 170, 548, 40, 25, UiFactory.Ink);
            lobbyBestText = UiFactory.Label(card.transform, "", 32, 216, 548, 38, 21, UiFactory.Accent);
            lobbyBestText.resizeTextForBestFit = true; lobbyBestText.resizeTextMinSize = 14; lobbyBestText.resizeTextMaxSize = 21;
            UiFactory.Button(card.transform, "开始 / 继续游戏  →", 32, 270, 544, 62, EnterGame, true);
            var jumpCard = AddLobbyCard(content, "Jump", cardCount++, UiFactory.Hex("E4E9D3"));
            UiFactory.Label(jumpCard.transform, cardCount.ToString("00") + "   /   轻巧向前", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(jumpCard.transform, "跳一跳", 28, 74, 350, 90, 62, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(jumpCard.transform, "按住，松手，再远一点。", 32, 170, 548, 40, 25, UiFactory.Ink);
            lobbyJumpBestText = UiFactory.Label(jumpCard.transform, "", 32, 216, 548, 38, 21, UiFactory.Accent);
            AddJumpPreview(jumpCard.transform);
            UiFactory.Button(jumpCard.transform, "开始 / 继续游戏  →", 32, 270, 544, 62, EnterJump, true);
            var sheepCard = AddLobbyCard(content, "Sheep", cardCount++, UiFactory.Hex("EEE7D3"));
            UiFactory.Label(sheepCard.transform, cardCount.ToString("00") + "   /   层层三消", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(sheepCard.transform, "旸了个旸", 28, 74, 380, 90, 60, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(sheepCard.transform, "翻开一层，凑齐三个。", 32, 170, 548, 40, 25, UiFactory.Ink);
            lobbySheepRecordText = UiFactory.Label(sheepCard.transform, "", 32, 216, 548, 38, 21, UiFactory.Accent);
            AddSheepPreview(sheepCard.transform);
            UiFactory.Button(sheepCard.transform, "开始 / 继续游戏  →", 32, 270, 544, 62, EnterSheep, true);
            var watermelonCard = AddLobbyCard(content, "Watermelon", cardCount++, UiFactory.Hex("F1E4C4"));
            UiFactory.Label(watermelonCard.transform, cardCount.ToString("00") + "   /   碰碰合成", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(watermelonCard.transform, "合成大西瓜", 28, 74, 460, 90, 54, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var fruitPreview = UiFactory.Rect(watermelonCard.transform, "Watermelon preview", 482, 72, 90, 90).gameObject.AddComponent<RawImage>();
            fruitPreview.texture = Resources.Load<Texture2D>("Watermelon/Fruit/11");
            fruitPreview.raycastTarget = false;
            UiFactory.Label(watermelonCard.transform, "小小一颗，合成满满的快乐。", 32, 170, 548, 40, 25, UiFactory.Ink);
            lobbyWatermelonRecordText = UiFactory.Label(watermelonCard.transform, "水果 / 表情 / 校徽  ·  三种主题", 32, 216, 548, 38, 21, UiFactory.Accent);
            UiFactory.Button(watermelonCard.transform, "开始 / 继续游戏  →", 32, 270, 544, 62, EnterWatermelon, true);
            var idiomCard = AddLobbyCard(content, "Idiom", cardCount++, UiFactory.Hex("E1EBDD"));
            UiFactory.Label(idiomCard.transform, cardCount.ToString("00") + "   /   一词一世界", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(idiomCard.transform, "成语接龙", 28, 74, 548, 90, 60, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(idiomCard.transform, "一来一往，认识更多好词。", 32, 170, 548, 40, 25, UiFactory.Ink);
            lobbyIdiomRecordText = UiFactory.Label(idiomCard.transform, "同字 / 同音  ·  随时收藏生词", 32, 216, 548, 38, 21, UiFactory.Accent);
            UiFactory.Button(idiomCard.transform, "开始 / 继续游戏  →", 32, 270, 544, 62, EnterIdiom, true);
            var sudokuCard = AddLobbyCard(content, "Sudoku", cardCount++, UiFactory.Hex("DFE8DE"));
            UiFactory.Label(sudokuCard.transform, cardCount.ToString("00") + "   /   九宫推理", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(sudokuCard.transform, "数独", 28, 74, 380, 90, 62, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(sudokuCard.transform, "九宫之间，找到唯一。", 32, 166, 548, 34, 25, UiFactory.Ink);
            lobbySudokuRecordText = UiFactory.Label(sudokuCard.transform, "", 32, 206, 548, 58, 18, UiFactory.Accent);
            for (int i = 0; i < 9; i++)
            {
                var square = UiFactory.Panel(sudokuCard.transform, "Sudoku preview " + i, 426 + i % 3 * 45, 68 + i / 3 * 39, 40, 35, UiFactory.Paper, false);
                UiFactory.Label(square.transform, i == 1 || i == 6 ? "" : (i + 1).ToString(), 0, 0, 40, 35, 21, UiFactory.Accent, TextAnchor.MiddleCenter);
            }
            UiFactory.Button(sudokuCard.transform, "选择关卡 / 继续  →", 32, 270, 544, 62, EnterSudoku, true);
            var minesweeperCard = AddLobbyCard(content, "Minesweeper", cardCount++, UiFactory.Hex("DFE8DE"));
            UiFactory.Label(minesweeperCard.transform, cardCount.ToString("00") + "   /   经典扫雷", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(minesweeperCard.transform, "扫雷", 28, 74, 380, 90, 62, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(minesweeperCard.transform, "数字之间，步步为营。", 32, 170, 548, 40, 25, UiFactory.Ink);
            lobbyMinesweeperRecordText = UiFactory.Label(minesweeperCard.transform, "首击安全 · 三档挑战", 32, 216, 548, 38, 21, UiFactory.Accent);
            for (int i = 0; i < 9; i++)
            {
                var square = UiFactory.Panel(minesweeperCard.transform, "Minesweeper preview " + i, 426 + i % 3 * 45, 68 + i / 3 * 39, 40, 35, i == 0 || i == 3 ? UiFactory.Paper : UiFactory.Hex("BBCBB6"), false);
                if (i == 0 || i == 3) UiFactory.Label(square.transform, i == 0 ? "1" : "2", 0, 0, 40, 35, 21, UiFactory.Accent, TextAnchor.MiddleCenter);
                if (i == 4)
                {
                    var icon = UiFactory.Rect(square.transform, "Flag", 8, 6, 24, 24).gameObject.AddComponent<MinesweeperIconGraphic>();
                    icon.Kind = MinesweeperIconKind.Flag; icon.color = UiFactory.Hex("B34842"); icon.ContrastFlagPole = true; icon.raycastTarget = false;
                }
            }
            UiFactory.Button(minesweeperCard.transform, "选择难度 / 继续  →", 32, 270, 544, 62, EnterMinesweeper, true);
            var zumaCard = AddLobbyCard(content, "Zuma", cardCount++, UiFactory.Hex("394F35"));
            UiFactory.Label(zumaCard.transform, cardCount.ToString("00") + "   /   神庙弹珠", 32, 22, 450, 36, 21, UiFactory.Hex("DAD7A4"));
            UiFactory.Label(zumaCard.transform, "Zuma", 28, 74, 540, 90, 62, UiFactory.Hex("F4E8B4"), TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(zumaCard.transform, "转动青蛙，让同色弹珠相遇。", 32, 170, 548, 40, 25, Color.white);
            UiFactory.Label(zumaCard.transform, "横屏游玩  ·  18 张地图  ·  4 档难度", 32, 216, 548, 38, 21, UiFactory.Hex("DAD7A4"));
            UiFactory.Button(zumaCard.transform, "开始 / 继续游戏  →", 32, 270, 544, 62, EnterZuma);
            var dinoCard = AddLobbyCard(content, "Dino", cardCount++, UiFactory.Hex("E7E8DB"));
            UiFactory.Label(dinoCard.transform, cardCount.ToString("00") + "   /   像素跑酷", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(dinoCard.transform, "Dino", 28, 74, 380, 90, 62, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(dinoCard.transform, "横屏或竖屏，跑得更远一点。", 32, 166, 548, 38, 25, UiFactory.Ink);
            string dinoWarning;
            dinoPreview = new DinoSaveStore(Application.persistentDataPath).Load(out dinoWarning);
            lobbyDinoRecordText = UiFactory.Label(dinoCard.transform, "", 32, 216, 548, 38, 21, UiFactory.Accent);
            RefreshDinoRecord();
            UiFactory.Button(dinoCard.transform, "选择方向 / 开始游戏  →", 32, 270, 544, 62, EnterDino, true);
            var gomokuCard = AddLobbyCard(content, "Gomoku", cardCount++, UiFactory.Hex("EEE7D3"));
            UiFactory.Label(gomokuCard.transform, cardCount.ToString("00") + "   /   黑白对弈", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(gomokuCard.transform, "五子棋", 28, 74, 380, 90, 62, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(gomokuCard.transform, "一子一步，先连成五。", 32, 170, 548, 40, 25, UiFactory.Ink);
            lobbyGomokuRecordText = UiFactory.Label(gomokuCard.transform, "人机 / 本地双人 · 三档难度", 32, 216, 548, 38, 21, UiFactory.Accent);
            AddGomokuPreview(gomokuCard.transform);
            UiFactory.Button(gomokuCard.transform, "选择模式 / 继续  →", 32, 270, 544, 62, EnterGomoku, true);
            var happyCard = AddLobbyCard(content, "HappyBall", cardCount++, UiFactory.Hex("E4E9DC"));
            UiFactory.Label(happyCard.transform, cardCount.ToString("00") + "   /   轻巧下落", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(happyCard.transform, "欢乐球球", 28, 74, 548, 90, 60, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(happyCard.transform, "转动高塔，穿过层层豁口。", 32, 170, 548, 40, 25, UiFactory.Ink);
            lobbyHappyBallRecordText = UiFactory.Label(happyCard.transform, "", 32, 216, 548, 38, 21, UiFactory.Accent);
            UiFactory.Button(happyCard.transform, "开始 / 继续游戏  →", 32, 270, 544, 62, EnterHappyBall, true);
            var sokobanCard = AddLobbyCard(content, "Sokoban", cardCount++, UiFactory.Hex("EAE4D4"));
            UiFactory.Label(sokobanCard.transform, cardCount.ToString("00") + "   /   经典推箱", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(sokobanCard.transform, "推箱子", 28, 74, 380, 90, 62, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(sokobanCard.transform, "把每一只箱子，推回它的位置。", 32, 170, 548, 40, 25, UiFactory.Ink);
            var cratePreview = UiFactory.Rect(sokobanCard.transform, "Sokoban crate preview", 462, 74, 90, 90).gameObject.AddComponent<RawImage>();
            cratePreview.texture = Resources.Load<Texture2D>("Sokoban/Sprites/crate");
            cratePreview.raycastTarget = false;
            lobbySokobanRecordText = UiFactory.Label(sokobanCard.transform, "", 32, 216, 548, 38, 21, UiFactory.Accent);
            UiFactory.Button(sokobanCard.transform, "开始 / 继续游戏  →", 32, 270, 544, 62, EnterSokoban, true);
            var goldCard = AddLobbyCard(content, "Gold Miner", cardCount++, UiFactory.Hex("EDE0BD"));
            UiFactory.Label(goldCard.transform, cardCount.ToString("00") + "   /   一起寻宝", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(goldCard.transform, "黄金矿工", 28, 74, 380, 90, 60, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var goldCover = UiFactory.Rect(goldCard.transform, "Gold Miner celebrating miner", 430, 36, 142, 138).gameObject.AddComponent<Image>();
            foreach (var pose in Resources.LoadAll<Sprite>("GoldMiner/characters/miner-p1-states"))
                if (pose.name == "success") { goldCover.sprite = pose; break; }
            goldCover.preserveAspect = true;
            goldCover.raycastTarget = false;
            var treasure = UiFactory.Rect(goldCard.transform, "Gold Miner prize", 520, 124, 62, 54).gameObject.AddComponent<Image>();
            treasure.sprite = Resources.Load<Sprite>("GoldMiner/objects/ore_gold_medium");
            treasure.preserveAspect = true;
            treasure.raycastTarget = false;
            UiFactory.Label(goldCard.transform, "看准时机，把宝藏带回家。", 32, 170, 548, 40, 25, UiFactory.Ink);
            UiFactory.Label(goldCard.transform, "单人竖屏 / 双人横屏合作 · 10 关挑战", 32, 216, 548, 38, 21, UiFactory.Accent);
            UiFactory.Button(goldCard.transform, "选择单人 / 双人  →", 32, 270, 544, 62, EnterGoldMiner, true);
            var footerTop = 216 + cardCount * 374 + 4;
            UiFactory.Label(content, "无需联网  ·  无广告  ·  本地记录", 56, footerTop, 608, 46, 22, UiFactory.Accent, TextAnchor.MiddleCenter);
            lobbyStatus = UiFactory.Label(content, "所有玩法离线可用 · 对局或最高纪录自动保存", 56, footerTop + 46, 608, 38, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
            content.sizeDelta = new Vector2(720, footerTop + 108);
        }

        private static Image AddLobbyCard(Transform parent, string name, int index, Color color)
        {
            return UiFactory.Panel(parent, name + " game card", 56, 216 + index * 374, 608, 350, color);
        }

        private void EnterGoldMiner()
        {
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

        private void EnterDino()
        {
            LeaveGoldMiner();
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
            if (lobbyDinoRecordText == null) return;
            long portrait = dinoPage != null ? dinoPage.PortraitBest : dinoPreview == null ? 0 : dinoPreview.BestPortraitScore;
            long landscape = dinoPage != null ? dinoPage.LandscapeBest : dinoPreview == null ? 0 : dinoPreview.BestLandscapeScore;
            lobbyDinoRecordText.text = "竖屏最高 " + portrait + "  ·  横屏最高 " + landscape;
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
            if (idiomPage != null) lobbyIdiomRecordText.text = idiomPage.BestLabel;
        }

        private void EnterIdiom()
        {
            LeaveGoldMiner();
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
            LeaveGoldMiner();
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
            if (watermelonPage != null) lobbyWatermelonRecordText.text = "你的最高纪录   " + watermelonPage.BestScore + "  ·  三种主题";
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
            LeaveGoldMiner();
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
            var platform = UiFactory.Rect(parent, "Jump platform preview", 432, 66, 140, 125).gameObject.AddComponent<RawImage>();
            platform.texture = Resources.Load<Texture2D>("Jump/Preview/platform");
            platform.raycastTarget = false;
            var pawn = UiFactory.Rect(parent, "Jump pawn preview", 482, 24, 25, 69).gameObject.AddComponent<RawImage>();
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

        private void RefreshJumpBest() { lobbyJumpBestText.text = "你的最高纪录   " + jumpPage.BestScore; }

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

        private void RefreshSheepRecord() { lobbySheepRecordText.text = "通关 " + sheepPage.CompletedRuns + " 次  ·  无尽最高 " + sheepPage.EndlessBest; }

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
            lobbySudokuRecordText.text = "四档难度 · 已完成 " + sudokuPage.CompletedCount + "/2000关\n" +
                "入门 " + sudokuPage.CompletedCountForDifficulty(0) + " · 简单 " + sudokuPage.CompletedCountForDifficulty(1) +
                " · 中等 " + sudokuPage.CompletedCountForDifficulty(2) + " · 困难 " + sudokuPage.CompletedCountForDifficulty(3);
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
            if (sokobanPage != null && lobbySokobanRecordText != null)
                lobbySokobanRecordText.text = "已完成 " + sokobanPage.CompletedCount + "/108 关 · 随时撤回";
        }

        private void EnterSokoban()
        {
            LeaveGoldMiner();
            happyBallPage?.Leave(); gomokuPage?.Leave(); dinoPage?.Leave();
            StopAnimation(); CloseModal(); minesweeperPage.Leave(); sudokuPage.Leave();
            jumpPage.Leave(); sheepPage.Leave(); idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = true;
            activePage = Page.Sokoban; lobbyPage.SetActive(false); gamePage.SetActive(false);
            if (status != null) status.gameObject.SetActive(false);
            sokobanPage.Enter();
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
            LeaveGoldMiner();
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

        private void RefreshHappyBallRecord()
        {
            if (lobbyHappyBallRecordText == null) return;
            if (happyBallPage != null) happyBallBest = Math.Max(happyBallBest, happyBallPage.BestScore);
            lobbyHappyBallRecordText.text = "你的最高纪录   " + happyBallBest;
        }

        private void RefreshMinesweeperRecord()
        {
            if (minesweeperPage != null && lobbyMinesweeperRecordText != null)
                lobbyMinesweeperRecordText.text = minesweeperPage.CompletedCount == 0 ? "首击安全 · 三档挑战" : "累计胜局 " + minesweeperPage.CompletedCount + " · 三档独立纪录";
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
            if (lobbyGomokuRecordText != null) lobbyGomokuRecordText.text = "人机 / 本地双人 · 三档难度";
        }

        private void EnterGomoku()
        {
            LeaveGoldMiner();
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
            LeaveGoldMiner();
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
            LeaveGoldMiner();
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
            LeaveGoldMiner();
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
            LeaveGoldMiner();
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
            LeaveGoldMiner();
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
            LeaveGoldMiner();
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
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
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
            lobbyBestText.text = "最高纪录 " + data.BestScore + " · 最高数字 " + data.BestTile;
            RefreshJumpBest();
            RefreshSheepRecord();
            RefreshIdiomRecord();
            RefreshWatermelonRecord();
            RefreshSudokuRecord();
            RefreshDinoRecord();
            RefreshMinesweeperRecord();
            RefreshSokobanRecord();
            RefreshHappyBallRecord();
            RefreshGomokuRecord();
        }

        private void SetStatus(string message)
        {
            var pageError = activePage == Page.GoldMiner ? goldMinerPage?.SaveError : activePage == Page.Sokoban ? sokobanPage?.SaveError : activePage == Page.HappyBall ? happyBallPage?.SaveError : activePage == Page.Dino ? dinoPage?.SaveError : activePage == Page.Zuma ? zumaPage?.SaveError : activePage == Page.Gomoku ? gomokuPage?.SaveError : activePage == Page.Minesweeper ? minesweeperPage?.SaveError : activePage == Page.Sudoku ? sudokuPage?.SaveError : activePage == Page.Watermelon ? watermelonPage?.SaveError : activePage == Page.Idiom ? idiomPage?.SaveError :
                activePage == Page.Sheep ? sheepPage?.SaveError : activePage == Page.Jump ? jumpPage?.SaveError :
                activePage == Page.Lobby ? goldMinerPage?.SaveError ?? sokobanPage?.SaveError ?? happyBallPage?.SaveError ?? dinoPage?.SaveError ?? zumaPage?.SaveError ?? gomokuPage?.SaveError ?? minesweeperPage?.SaveError ?? sudokuPage?.SaveError ?? watermelonPage?.SaveError ?? idiomPage?.SaveError ?? sheepPage?.SaveError ?? jumpPage?.SaveError : null;
            var target = activePage == Page.Lobby ? lobbyStatus : status;
            // The normal footer is hidden in Minesweeper focus mode; preserve shared save errors there.
            if (activePage == Page.Minesweeper) minesweeperPage?.SetHostSaveWarning(saveFailed ? saveWarning : null);
            if (activePage == Page.HappyBall) happyBallPage?.SetHostSaveWarning(saveFailed ? saveWarning : null);
            if (activePage == Page.Sokoban) sokobanPage?.SetHostSaveWarning(saveFailed ? saveWarning : null);
            if (target != null) target.text = saveFailed ? saveWarning : pageError ?? message;
        }

        private void Update2048Records()
        {
            data.BestScore = Math.Max(data.BestScore, game.Score);
            for (int cell = 0; cell < 16; cell++) data.BestTile = Math.Max(data.BestTile, game.GetCell(cell));
        }

        private void Save()
        {
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
            goldMinerPage?.Save();
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
                SetStatus(activePage == Page.Minesweeper || activePage == Page.Sokoban ? "" : "进度已保存。");
            }
            zumaPage?.CommonSettingsSaved(!saveFailed);
        }

        private void StopAnimation()
        {
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
                if (activePage == Page.Zuma) { zumaPage.HandleBack(); return; }
                if (modal != null) dismissModal?.Invoke();
                else if (activePage == Page.GoldMiner && goldMinerPage.HandleBack()) { }
                else if (activePage == Page.Dino) dinoPage.Back();
                else if (activePage == Page.Idiom && idiomPage.HandleBack()) { }
                else if (activePage == Page.Sudoku) sudokuPage.Back();
                else if (activePage == Page.Minesweeper) minesweeperPage.Back();
                else if (activePage == Page.Sokoban) sokobanPage.Back();
                else if (activePage == Page.HappyBall) happyBallPage.Back();
                else if (activePage == Page.Gomoku) gomokuPage.Back();
                else if (activePage != Page.Lobby) ShowLobby();
                else ShowDialog("暂时离开？", "进度已经保存在这台手机上。", "退出应用", Application.Quit, "再待一会儿", () => { });
                return;
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Move(Direction.Left);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Move(Direction.Right);
            if (Input.GetKeyDown(KeyCode.UpArrow)) Move(Direction.Up);
            if (Input.GetKeyDown(KeyCode.DownArrow)) Move(Direction.Down);
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            goldMinerPage?.SetForeground(focused, paused);
            zumaPage?.HostActivity(!paused && focused);
            if (value && (activePage == Page.Minesweeper || activePage == Page.Gomoku || activePage == Page.HappyBall || activePage == Page.Sokoban)) CloseModal();
            happyBallPage?.SetForeground(!paused && focused);
            minesweeperPage?.SetForeground(!paused && focused);
            sokobanPage?.SetForeground(!paused && focused);
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
            if (!value && (activePage == Page.Minesweeper || activePage == Page.Gomoku || activePage == Page.HappyBall || activePage == Page.Sokoban)) CloseModal();
            happyBallPage?.SetForeground(!paused && focused);
            minesweeperPage?.SetForeground(!paused && focused);
            sokobanPage?.SetForeground(!paused && focused);
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

        private void OnApplicationQuit() { zumaPage?.Suspend(); Save(); }
    }
}
