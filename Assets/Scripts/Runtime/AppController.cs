using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Runtime.Jump;
using XanaduYonder.Runtime.Sheep;
using XanaduYonder.Runtime.Idiom;
using XanaduYonder.Runtime.Watermelon;
using XanaduYonder.Runtime.Sudoku;

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
        private Text status;
        private enum Page { Lobby, Game2048, Jump, Sheep, Idiom, Sudoku, Watermelon }
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
            if (data == null) data = new SaveData { Game = new Game2048(random).GetSnapshot() };
            game = new Game2048(data.Game, random);
            feedback = gameObject.AddComponent<GameFeedback>();
            BuildCanvas();
            BuildLobby();
            BuildGame();
            BuildJump();
            BuildSheep();
            BuildSudoku();
            var statusBand = UiFactory.Band(frame, "Status", 1278, 34, 1);
            status = UiFactory.Label(statusBand, "", 56, 0, 608, 34, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
            ShowLobby();
            SetStatus(saveWarning ?? "所有玩法离线可用 · 进度自动保存");
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
            var navigation = UiFactory.Band(root, "Lobby navigation", 0, 106, 0);
            UiFactory.Label(navigation, "XANADU  /  YONDER", 56, 42, 470, 40, 22, UiFactory.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.IconButton(navigation, UiIconKind.Settings, 606, 34, 58, 58, OpenSettings);
            // Card dimensions and gaps stay fixed; additional games extend this content instead of shrinking it.
            var viewport = UiFactory.Rect(root, "Lobby list viewport", 0, 132, 720, 1092);
            UiFactory.Stretch(viewport);
            viewport.offsetMin = new Vector2(0, 96);
            viewport.offsetMax = new Vector2(0, -132);
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
            var card = AddLobbyCard(content, "2048", cardCount++, UiFactory.Ink);
            UiFactory.Label(card.transform, cardCount.ToString("00") + "   /   数字益智", 32, 22, 450, 36, 21, UiFactory.Hex("C9D8B8"));
            UiFactory.Label(card.transform, "2048", 28, 64, 552, 104, 82, UiFactory.Hex("F4E8B4"), TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(card.transform, "让相同的数字相遇。", 32, 170, 548, 40, 25, Color.white);
            lobbyBestText = UiFactory.Label(card.transform, "", 32, 216, 548, 38, 21, UiFactory.Hex("C9D8B8"));
            UiFactory.Button(card.transform, "开始 / 继续游戏  →", 32, 270, 544, 62, EnterGame);
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
            var idiomCard = AddLobbyCard(content, "Idiom", cardCount++, UiFactory.Hex("E1EBDD"));
            UiFactory.Label(idiomCard.transform, cardCount.ToString("00") + "   /   一词一世界", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(idiomCard.transform, "成语接龙", 28, 74, 548, 90, 60, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(idiomCard.transform, "一来一往，认识更多好词。", 32, 170, 548, 40, 25, UiFactory.Ink);
            lobbyIdiomRecordText = UiFactory.Label(idiomCard.transform, "同字 / 同音  ·  随时收藏生词", 32, 216, 548, 38, 21, UiFactory.Accent);
            UiFactory.Button(idiomCard.transform, "开始 / 继续游戏  →", 32, 270, 544, 62, EnterIdiom, true);
            var sudokuCard = AddLobbyCard(content, "Sudoku", cardCount++, UiFactory.Hex("DFE8DE"));
            UiFactory.Label(sudokuCard.transform, cardCount.ToString("00") + "   /   九宫推理", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(sudokuCard.transform, "数独", 28, 74, 380, 90, 62, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(sudokuCard.transform, "九宫之间，找到唯一。", 32, 170, 548, 40, 25, UiFactory.Ink);
            lobbySudokuRecordText = UiFactory.Label(sudokuCard.transform, "", 32, 216, 548, 38, 21, UiFactory.Accent);
            for (int i = 0; i < 9; i++)
            {
                var square = UiFactory.Panel(sudokuCard.transform, "Sudoku preview " + i, 426 + i % 3 * 45, 68 + i / 3 * 39, 40, 35, UiFactory.Paper, false);
                UiFactory.Label(square.transform, i == 1 || i == 6 ? "" : (i + 1).ToString(), 0, 0, 40, 35, 21, UiFactory.Accent, TextAnchor.MiddleCenter);
            }
            UiFactory.Button(sudokuCard.transform, "选择关卡 / 继续  →", 32, 270, 544, 62, EnterSudoku, true);
            var watermelonCard = AddLobbyCard(content, "Watermelon", cardCount++, UiFactory.Hex("F1E4C4"));
            UiFactory.Label(watermelonCard.transform, cardCount.ToString("00") + "   /   碰碰合成", 32, 22, 450, 36, 21, UiFactory.Accent);
            UiFactory.Label(watermelonCard.transform, "合成大西瓜", 28, 74, 460, 90, 54, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var fruitPreview = UiFactory.Rect(watermelonCard.transform, "Watermelon preview", 482, 72, 90, 90).gameObject.AddComponent<RawImage>();
            fruitPreview.texture = Resources.Load<Texture2D>("Watermelon/Fruit/11");
            fruitPreview.raycastTarget = false;
            UiFactory.Label(watermelonCard.transform, "小小一颗，合成满满的快乐。", 32, 170, 548, 40, 25, UiFactory.Ink);
            lobbyWatermelonRecordText = UiFactory.Label(watermelonCard.transform, "水果 / 表情 / 校徽  ·  三种主题", 32, 216, 548, 38, 21, UiFactory.Accent);
            UiFactory.Button(watermelonCard.transform, "开始 / 继续游戏  →", 32, 270, 544, 62, EnterWatermelon, true);
            var footerTop = 216 + cardCount * 374 + 4;
            UiFactory.Label(content, "无需联网  ·  无广告  ·  自动存档", 56, footerTop, 608, 46, 22, UiFactory.Accent, TextAnchor.MiddleCenter);
            content.sizeDelta = new Vector2(720, footerTop + 74);
        }

        private static Image AddLobbyCard(Transform parent, string name, int index, Color color)
        {
            return UiFactory.Panel(parent, name + " game card", 56, 216 + index * 374, 608, 350, color);
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

        private void RefreshSudokuRecord() { lobbySudokuRecordText.text = "四档难度 · 已完成 " + sudokuPage.CompletedCount + " / 2,000 关"; }

        private void EnterSudoku()
        {
            StopAnimation(); CloseModal(); jumpPage.Leave(); sheepPage.Leave();
            idiomPage?.Leave(); watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            if (status != null) status.gameObject.SetActive(true);
            activePage = Page.Sudoku; lobbyPage.SetActive(false); gamePage.SetActive(false); sudokuPage.Enter();
        }

        private void EnterSheep()
        {
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

        private void ShowLobby()
        {
            sudokuPage.Leave();
            StopAnimation();
            CloseModal();
            jumpPage.Leave();
            sheepPage.Leave();
            idiomPage?.Leave();
            watermelonPage?.Leave();
            frame.GetComponent<SafeDesignFrame>().WidthOnly = false;
            if (status != null) status.gameObject.SetActive(true);
            activePage = Page.Lobby;
            gamePage.SetActive(false);
            lobbyPage.SetActive(true);
            RefreshScores();
            Save();
            SetStatus(saveWarning ?? "所有玩法离线可用 · 进度自动保存");
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
            data.BestScore = Math.Max(data.BestScore, game.Score);
            scoreText.text = game.Score.ToString();
            bestText.text = data.BestScore.ToString();
            lobbyBestText.text = "你的最高纪录   " + data.BestScore;
            RefreshJumpBest();
            RefreshSheepRecord();
            RefreshIdiomRecord();
            RefreshWatermelonRecord();
            RefreshSudokuRecord();
        }

        private void SetStatus(string message)
        {
            var pageError = activePage == Page.Sudoku ? sudokuPage?.SaveError : activePage == Page.Watermelon ? watermelonPage?.SaveError : activePage == Page.Idiom ? idiomPage?.SaveError :
                activePage == Page.Sheep ? sheepPage?.SaveError : activePage == Page.Jump ? jumpPage?.SaveError :
                activePage == Page.Lobby ? sudokuPage?.SaveError ?? watermelonPage?.SaveError ?? idiomPage?.SaveError ?? sheepPage?.SaveError ?? jumpPage?.SaveError : null;
            if (status != null) status.text = saveFailed ? saveWarning : pageError ?? message;
        }

        private void Save()
        {
            jumpPage?.Save();
            sheepPage?.Save();
            idiomPage?.Save();
            watermelonPage?.Save();
            sudokuPage?.Save();
            if (store == null || game == null) return;
            data.Game = game.GetSnapshot();
            data.BestScore = Math.Max(data.BestScore, game.Score);
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
                SetStatus("进度已保存。");
            }
        }

        private void StopAnimation()
        {
            feedback?.Stop();
            jumpPage?.Suspend();
            sheepPage?.Suspend();
            idiomPage?.Suspend();
            watermelonPage?.Suspend();
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
                if (modal != null) dismissModal?.Invoke();
                else if (activePage == Page.Idiom && idiomPage.HandleBack()) { }
                else if (activePage == Page.Sudoku) sudokuPage.Back();
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
            sudokuPage?.SetForeground(!paused && focused);
            if (value) { idiomPage?.PauseForBackground(); StopAnimation(); feedback?.Stop(); Save(); }
            else CheckEndState();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
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

        private void OnApplicationQuit() { Save(); }
    }
}
