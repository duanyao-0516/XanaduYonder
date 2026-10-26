using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core;

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
        private Text status;
        private bool inGame;
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
            var statusBand = UiFactory.Band(frame, "Status", 1250, 48, 1);
            status = UiFactory.Label(statusBand, "", 56, 0, 608, 48, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
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
            UiFactory.Button(navigation, "设置", 552, 34, 112, 58, OpenSettings);
            var introduction = UiFactory.Band(root, "Lobby introduction", 166, 238, .25f);
            UiFactory.Label(introduction, "小小一局，\n慢慢向前。", 56, 0, 608, 164, 59, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(introduction, "把片刻空闲，留给一点简单的快乐。", 56, 186, 608, 52, 25, UiFactory.Muted);
            var cardBand = UiFactory.Band(root, "Lobby game", 478, 530, .55f);
            var card = UiFactory.Panel(cardBand, "2048 game card", 56, 0, 608, 530, UiFactory.Ink);
            UiFactory.Label(card.transform, "01   /   数字益智", 32, 28, 450, 40, 22, UiFactory.Hex("C9D8B8"));
            UiFactory.Label(card.transform, "2048", 28, 88, 552, 160, 126, UiFactory.Hex("F4E8B4"), TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(card.transform, "轻轻一滑，让相同的数字相遇。", 32, 276, 548, 48, 26, Color.white);
            lobbyBestText = UiFactory.Label(card.transform, "", 32, 339, 548, 42, 22, UiFactory.Hex("C9D8B8"));
            UiFactory.Button(card.transform, "开始 / 继续游戏  →", 32, 415, 544, 78, EnterGame);
            var details = UiFactory.Band(root, "Lobby details", 1088, 112, .88f);
            UiFactory.Label(details, "一台手机，一个属于你的小游戏角落。", 56, 0, 608, 50, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Label(details, "无需联网  ·  无广告  ·  自动存档", 56, 66, 608, 46, 22, UiFactory.Accent, TextAnchor.MiddleCenter);
        }

        private void BuildGame()
        {
            var root = UiFactory.Rect(frame, "2048 page", 0, 0, 720, 1320);
            UiFactory.Stretch(root);
            gamePage = root.gameObject;
            var navigation = UiFactory.Band(root, "Game navigation", 0, 92, 0);
            UiFactory.Button(navigation, "‹  大厅", 56, 34, 150, 58, ShowLobby);
            UiFactory.Label(navigation, "XANADU / YONDER", 226, 34, 278, 58, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Button(navigation, "设置", 552, 34, 112, 58, OpenSettings);
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
            CloseModal();
            inGame = true;
            lobbyPage.SetActive(false);
            gamePage.SetActive(true);
            board.Render(game.GetSnapshot());
            RefreshScores();
            SetStatus(saveWarning ?? "合成 2048，然后向更高的数字出发。");
            CheckEndState();
        }

        private void ShowLobby()
        {
            StopAnimation();
            CloseModal();
            inGame = false;
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

        private void OpenSettings()
        {
            var panel = CreateModal("简单一点，刚刚好");
            UiFactory.Label(panel, "设置在所有游戏中共用", 32, 104, 532, 42, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Button(panel, "声音  ·  " + (data.SoundEnabled ? "开启" : "关闭"), 32, 178, 532, 70, () =>
            {
                data.SoundEnabled = !data.SoundEnabled;
                Save();
                CloseModal();
                OpenSettings();
            });
            UiFactory.Button(panel, "触感  ·  " + (data.HapticsEnabled ? "开启" : "关闭"), 32, 264, 532, 70, () =>
            {
                data.HapticsEnabled = !data.HapticsEnabled;
                Save();
                CloseModal();
                OpenSettings();
            });
            UiFactory.Button(panel, "完成", 32, 376, 532, 74, () => { CloseModal(); CheckEndState(); }, true);
            dismissModal = () => { CloseModal(); CheckEndState(); };
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

        private Transform CreateModal(string title)
        {
            StopAnimation();
            CloseModal();
            var overlay = UiFactory.Panel(canvasRoot, "Dialog overlay", 0, 0, 720, 1320, new Color(.06f, .14f, .1f, .48f), false);
            UiFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            modal = overlay.gameObject;
            var modalFrame = UiFactory.Rect(modal.transform, "Dialog safe area", 0, 0, 720, 1320);
            modalFrame.gameObject.AddComponent<SafeDesignFrame>().Initialize(modalFrame, canvasRoot);
            var panel = UiFactory.Panel(modalFrame, "Dialog", 0, 0, 596, 488, UiFactory.Paper);
            UiFactory.Center(panel.rectTransform);
            UiFactory.Label(panel.transform, title, 32, 36, 532, 62, 33, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            return panel.transform;
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
        }

        private void SetStatus(string message) { if (status != null) status.text = message; }

        private void Save()
        {
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
            if (paused || !focused) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (modal != null) dismissModal?.Invoke();
                else if (inGame) ShowLobby();
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
            if (value) { StopAnimation(); feedback?.Stop(); Save(); }
            else CheckEndState();
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value) { StopAnimation(); feedback?.Stop(); Save(); }
            else CheckEndState();
        }

        private void OnApplicationQuit() { Save(); }
    }
}
