using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Watermelon;

namespace XanaduYonder.Runtime.Watermelon
{
    internal sealed class WatermelonPage : MonoBehaviour
    {
        private const float BoardWidth = 572;
        private const float Scale = BoardWidth / WatermelonRules.Width;
        private const float BoardHeight = Scale * WatermelonRules.Height;
        private const float Step = 1f / 60f;
        private WatermelonGame game;
        private WatermelonSaveData data;
        private WatermelonSaveStore store;
        private WatermelonPhysicsWorld world;
        private WatermelonArtwork artwork;
        private readonly Dictionary<int, WatermelonIcon> icons = new Dictionary<int, WatermelonIcon>();
        private readonly List<int> removed = new List<int>();
        private readonly List<Text> bursts = new List<Text>();
        private readonly List<float> burstAges = new List<float>();
        private RectTransform board, effectLayer;
        private WatermelonIcon ghost, next;
        private Image dangerLine, aimLine;
        private Text score, best, hint, pauseLabel, dangerLabel;
        private Button[] themes;
        private WatermelonPointer pointer;
        private GameObject pauseOverlay;
        private AudioSource audioSource;
        private AudioClip dropSound, mergeSound, winSound, loseSound;
        private Func<bool> acceptsInput, soundEnabled, hapticsEnabled;
        private Action lobby;
        private Action<string> status;
        private Action<string, string, string, Action, string, Action> dialog;
        private Func<string, float, Action, RectTransform> openModal;
        private Action closeModal;
        private GameFeedback feedback;
        private bool visible, manuallyPaused, resultShown, dirty, wasRunning, wasMoving;
        private string loadWarning;
        private float accumulator, sinceSave, aimX = WatermelonRules.Width * .5f, feedbackTime;
        public string SaveError { get; private set; }
        public int BestScore => data.BestScore;
        public event Action Changed;

        public void Initialize(RectTransform root, Action goLobby, Action settings, Func<bool> accepts,
            Func<bool> sound, Func<bool> haptics, GameFeedback sharedFeedback, Action<string> setStatus,
            Action<string, string, string, Action, string, Action> showDialog,
            Func<string, float, Action, RectTransform> createModal, Action dismissModal)
        {
            lobby = goLobby; acceptsInput = accepts; soundEnabled = sound; hapticsEnabled = haptics;
            feedback = sharedFeedback; status = setStatus; dialog = showDialog; openModal = createModal; closeModal = dismissModal;
            store = new WatermelonSaveStore(Application.persistentDataPath);
            string warning;
            data = store.Load(out warning) ?? new WatermelonSaveData { Game = new WatermelonGame(NewSeed()).GetSnapshot() };
            game = new WatermelonGame(data.Game);
            artwork = new WatermelonArtwork();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.volume = .34f;
            dropSound = Resources.Load<AudioClip>("Watermelon/Audio/Drop");
            mergeSound = Resources.Load<AudioClip>("Watermelon/Audio/Merge");
            winSound = Resources.Load<AudioClip>("Watermelon/Audio/Win");
            loseSound = Resources.Load<AudioClip>("Watermelon/Audio/Lose");
            Build(root, settings);
            loadWarning = warning;
            Render(game.GetSnapshot());
            gameObject.SetActive(false);
        }

        private void Build(RectTransform root, Action settings)
        {
            var navigation = UiFactory.Band(root, "Watermelon navigation", 0, 94, 0);
            UiFactory.IconButton(navigation, UiIconKind.Home, 56, 34, 58, 58, lobby);
            UiFactory.IconButton(navigation, UiIconKind.Settings, 606, 34, 58, 58, () => { Suspend(); Save(); settings(); });
            var heading = UiFactory.Band(root, "Watermelon title", 108, 76, 0);
            UiFactory.Label(heading, "合成大西瓜", 56, 0, 500, 76, 48, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(heading, "04", 582, 8, 82, 58, 30, UiFactory.Accent, TextAnchor.MiddleRight);
            var themeBand = UiFactory.Band(root, "Watermelon themes", 204, 48, .08f);
            themes = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int theme = i;
                themes[i] = UiFactory.Button(themeBand, WatermelonArtwork.ThemeNames[i], 56 + i * 208, 0, 192, 48, () => SetTheme(theme));
            }
            var statistics = UiFactory.Band(root, "Watermelon scores", 268, 74, .14f);
            score = Stat(statistics, "本局得分", 56);
            best = Stat(statistics, "最高纪录", 260);
            UiFactory.Label(statistics, "下一颗", 488, 0, 176, 28, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
            next = artwork.Icon(statistics, "Next ball", 576, 55, 48, data.Theme, 0);
            var boardBand = UiFactory.Band(root, "Watermelon board band", 364, BoardHeight + 32, .5f);
            UiFactory.Panel(boardBand, "Bowl rim", 56, 0, 608, BoardHeight + 32, UiFactory.Hex("D9C9A5"));
            var bowl = UiFactory.Panel(boardBand, "Bowl", 64, 8, 592, BoardHeight + 16, UiFactory.Hex("FFF9E9"));
            board = UiFactory.Rect(bowl.transform, "Drop area", 10, 8, BoardWidth, BoardHeight);
            var surface = board.gameObject.AddComponent<Image>();
            surface.color = Color.clear; surface.raycastTarget = true;
            board.gameObject.AddComponent<RectMask2D>();
            pointer = board.gameObject.AddComponent<WatermelonPointer>();
            pointer.CanAim = () => CanPlay && game.DropCooldown <= 0;
            pointer.Aim = x => { aimX = x * WatermelonRules.Width; RenderPreview(game.GetSnapshot()); };
            pointer.Drop = x => Drop(x * WatermelonRules.Width);
            aimLine = UiFactory.Panel(board, "Aim line", BoardWidth * .5f, 0, 2, BoardHeight, new Color(.25f, .4f, .3f, .10f), false);
            dangerLine = UiFactory.Panel(board, "Danger line", 0, (WatermelonRules.Height - WatermelonRules.DangerY) * Scale, BoardWidth, 2, UiFactory.Hex("DDBD83"), false);
            dangerLabel = UiFactory.Label(board, "警戒线", 10, (WatermelonRules.Height - WatermelonRules.DangerY) * Scale + 5, BoardWidth - 20, 28, 17, UiFactory.Hex("B49364"), TextAnchor.UpperRight);
            ghost = artwork.Icon(board, "Current ball preview", 0, 0, 40, data.Theme, 0);
            effectLayer = UiFactory.Rect(board, "Merge effects", 0, 0, BoardWidth, BoardHeight);
            var overlay = UiFactory.Panel(board, "Paused overlay", 0, 0, BoardWidth, BoardHeight, new Color(.97f, .96f, .91f, .88f), false);
            overlay.raycastTarget = true;
            pauseOverlay = overlay.gameObject;
            UiFactory.Label(overlay.transform, "歇一会儿。", 30, 220, BoardWidth - 60, 76, 42, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Label(overlay.transform, "这一局的进度，会一直为你保留。", 30, 306, BoardWidth - 60, 48, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Button(overlay.transform, "继续游戏", 140, 392, BoardWidth - 280, 66, TogglePause, true);
            pauseOverlay.SetActive(false);
            var hintBand = UiFactory.Band(root, "Watermelon hint", 1120, 48, .82f);
            hint = UiFactory.Label(hintBand, "拖动瞄准，松手投放", 56, 0, 608, 48, 23, UiFactory.Accent, TextAnchor.MiddleCenter);
            var actions = UiFactory.Band(root, "Watermelon actions", 1196, 64, 1);
            UiFactory.Button(actions, "重新开始", 56, 0, 192, 64, ConfirmRestart);
            var pause = UiFactory.Button(actions, "暂停", 264, 0, 192, 64, TogglePause);
            pauseLabel = pause.GetComponentInChildren<Text>();
            UiFactory.Button(actions, "玩法 / 图鉴", 472, 0, 192, 64, ShowHelp);
        }

        private static Text Stat(Transform parent, string title, float x)
        {
            UiFactory.Label(parent, title, x, 0, 192, 28, 18, UiFactory.Muted);
            return UiFactory.Label(parent, "0", x, 28, 192, 48, 36, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private bool CanPlay => visible && !manuallyPaused && acceptsInput() && !game.GameOver
            && (!game.HasWatermelon || data.GoalAcknowledged);

        public void Enter()
        {
            visible = true; resultShown = false; accumulator = 0;
            gameObject.SetActive(true);
            EnsureWorld();
            Render(game.GetSnapshot());
            status(SaveError ?? loadWarning ?? "同级相遇，合成更大的一颗 · 自动续局");
            loadWarning = null;
            Changed?.Invoke();
            CheckEndState();
        }

        public void Leave()
        {
            if (visible) { Suspend(); Save(); }
            visible = false; gameObject.SetActive(false);
        }

        public void Suspend()
        {
            pointer?.Cancel(); accumulator = 0; wasRunning = false;
            audioSource?.Stop(); world?.Capture();
        }

        private void EnsureWorld()
        {
            if (world != null) return;
            world = new WatermelonPhysicsWorld(game);
            world.Merged += OnMerge;
        }

        private void Update()
        {
            bool running = CanPlay;
            if (!running)
            {
                if (wasRunning) { Suspend(); Save(); }
                return;
            }
            wasRunning = true;
            EnsureWorld();
            float elapsed = Mathf.Min(Time.unscaledDeltaTime, .1f);
            accumulator += elapsed;
            sinceSave += elapsed;
            while (accumulator >= Step)
            {
                world.Step(Step);
                accumulator -= Step;
                if (game.GameOver || (game.HasWatermelon && !data.GoalAcknowledged)) break;
            }
            bool moving = world.IsMoving;
            dirty |= moving || wasMoving;
            wasMoving = moving;
            Render(game.GetSnapshot());
            AnimateBursts(elapsed);
            if (dirty && sinceSave >= .8f) Save();
            CheckEndState();
        }

        private void Drop(float x)
        {
            if (!CanPlay) return;
            EnsureWorld(); aimX = x;
            if (!world.TryDrop(x))
            {
                if (game.DropCooldown <= 0) { hint.text = "上方空间不足，等水果落稳后再试"; feedbackTime = Time.unscaledTime + 1.4f; }
                return;
            }
            Play(dropSound);
            dirty = true; Save(); Render(game.GetSnapshot());
        }

        private void OnMerge(WatermelonMergeResult merge)
        {
            dirty = true;
            data.BestScore = Math.Max(data.BestScore, game.Score);
            Play(mergeSound);
            feedback.Play(true, false, hapticsEnabled());
            var ball = merge.NewBall;
            var label = UiFactory.Label(effectLayer, "+" + merge.AddedScore, ball.X * Scale - 50,
                (WatermelonRules.Height - ball.Y) * Scale - 40, 100, 46, 30, UiFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            bursts.Add(label); burstAges.Add(0);
            Changed?.Invoke();
        }

        private void Render(WatermelonSnapshot snapshot)
        {
            score.text = snapshot.Score.ToString();
            best.text = Math.Max(data.BestScore, snapshot.Score).ToString();
            var alive = new HashSet<int>();
            foreach (var ball in snapshot.Balls)
            {
                alive.Add(ball.Id);
                WatermelonIcon icon;
                float size = WatermelonRules.GetRadius(ball.Level) * Scale * 2;
                if (!icons.TryGetValue(ball.Id, out icon))
                {
                    icon = artwork.Icon(board, "Ball " + ball.Id, 0, 0, size, data.Theme, ball.Level);
                    icons.Add(ball.Id, icon);
                }
                icon.Rect.anchoredPosition = new Vector2(ball.X * Scale, -(WatermelonRules.Height - ball.Y) * Scale);
                icon.Rect.localRotation = Quaternion.Euler(0, 0, ball.Angle);
            }
            removed.Clear();
            foreach (var id in icons.Keys) if (!alive.Contains(id)) removed.Add(id);
            foreach (var id in removed) { icons[id].Rect.gameObject.SetActive(false); Destroy(icons[id].Rect.gameObject); icons.Remove(id); }
            ghost.Rect.SetAsLastSibling(); effectLayer.SetAsLastSibling(); pauseOverlay.transform.SetAsLastSibling();
            RenderPreview(snapshot);
            bool danger = snapshot.DangerTime > 0;
            dangerLine.color = danger ? UiFactory.Hex("CE604A") : UiFactory.Hex("DDBD83");
            dangerLabel.color = danger ? UiFactory.Hex("B64B35") : UiFactory.Hex("896833");
            dangerLabel.text = danger ? "越线 " + (WatermelonRules.DangerDuration - snapshot.DangerTime).ToString("0.0") + " 秒" : "警戒线";
            if (Time.unscaledTime >= feedbackTime)
                hint.text = snapshot.GameOver ? "这一局装满了，下次再多合一颗。" : manuallyPaused ? "已暂停 · 点击继续游戏" :
                    danger ? "快到顶了！让同级小球合并，降下堆叠" : "拖动瞄准，松手投放 · 同级碰撞合成";
            for (int i = 0; i < themes.Length; i++)
            {
                ((Image)themes[i].targetGraphic).color = data.Theme == i ? UiFactory.Accent : UiFactory.Hex("E7EDE4");
                themes[i].GetComponentInChildren<Text>().color = data.Theme == i ? Color.white : UiFactory.Ink;
            }
            pauseLabel.text = manuallyPaused ? "继续" : "暂停";
            pauseOverlay.SetActive(manuallyPaused && !snapshot.GameOver);
        }

        private void RenderPreview(WatermelonSnapshot snapshot)
        {
            float radius = WatermelonRules.GetRadius(snapshot.CurrentLevel);
            aimX = Mathf.Clamp(aimX, radius, WatermelonRules.Width - radius);
            ghost.Set(data.Theme, snapshot.CurrentLevel);
            ghost.Rect.sizeDelta = Vector2.one * radius * Scale * 2;
            ghost.Rect.anchoredPosition = new Vector2(aimX * Scale, -(WatermelonRules.Height - WatermelonRules.SpawnY) * Scale);
            ghost.Tint(new Color(1, 1, 1, snapshot.DropCooldown > 0 ? .24f : .7f));
            ghost.Rect.gameObject.SetActive(!snapshot.GameOver);
            aimLine.rectTransform.anchoredPosition = new Vector2(aimX * Scale, 0);
            aimLine.gameObject.SetActive(!snapshot.GameOver);
            next.Set(data.Theme, snapshot.NextLevel);
        }

        private void SetTheme(int theme)
        {
            if (!visible || !acceptsInput() || data.Theme == theme) return;
            pointer.Cancel(); data.Theme = theme;
            foreach (var ball in game.GetSnapshot().Balls) icons[ball.Id].Set(theme, ball.Level);
            dirty = true; Save(); Render(game.GetSnapshot());
            status("已切换" + WatermelonArtwork.ThemeNames[theme] + "主题 · 当前进度保持不变");
        }

        private void TogglePause()
        {
            if (!visible || !acceptsInput() || game.GameOver) return;
            manuallyPaused = !manuallyPaused;
            Suspend(); Save(); Render(game.GetSnapshot());
        }

        private void ConfirmRestart()
        {
            if (!visible || !acceptsInput()) return;
            Suspend(); Save();
            dialog("重新开始这一局？", "本局棋盘和分数将清空。\n最高纪录和主题会保留。", "重新开始", Restart, "继续当前游戏", CheckEndState);
        }

        private void Restart()
        {
            world?.Dispose(); world = null;
            game = new WatermelonGame(NewSeed());
            data.GoalAcknowledged = false;
            manuallyPaused = resultShown = false; accumulator = 0; aimX = WatermelonRules.Width * .5f;
            ClearBursts(); EnsureWorld(); dirty = true; Save(); Render(game.GetSnapshot());
            status("新的一局 · 拖动瞄准，松手投放");
        }

        public void CheckEndState()
        {
            if (!visible || !acceptsInput()) return;
            var snapshot = game.GetSnapshot();
            if (!snapshot.GameOver && (!game.HasWatermelon || data.GoalAcknowledged)) { resultShown = false; return; }
            if (resultShown) return;
            resultShown = true; Suspend(); Save();
            if (snapshot.GameOver)
            {
                dialog("这一局，很丰盛。", "小球持续越过警戒线，本局结束。\n本局得分  " + snapshot.Score + "  ·  最高纪录  " + data.BestScore,
                    "再来一局", Restart, "返回大厅", lobby);
                Play(loseSound);
            }
            else
            {
                dialog(data.Theme == 0 ? "大西瓜，合成了！" : "第 11 级，达成！", "本局得分  " + snapshot.Score + "\n你可以继续合成，挑战更高的纪录。",
                    "继续挑战", () => { data.GoalAcknowledged = true; resultShown = false; dirty = true; Save(); }, "返回大厅", lobby);
                Play(winSound);
            }
        }

        private void ShowHelp()
        {
            if (!visible || !acceptsInput()) return;
            Suspend(); Save();
            Action close = () => { closeModal(); CheckEndState(); };
            var panel = openModal("合成图鉴 · " + WatermelonArtwork.ThemeNames[data.Theme], 870, close);
            UiFactory.Label(panel, "拖动选位置，松手投放；点击也能投放。\n同级碰撞升一级，加合并前的等级分。\n初始随机 1–5 级，合到 11 级达成目标。", 32, 108, 532, 100, 22, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Label(panel, "左右越界仍然可以投放，上下越界不可投放", 32, 214, 532, 38, 21, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            for (int i = 0; i < 11; i++)
            {
                int column = i % 4, row = i / 4;
                artwork.Icon(panel, "Guide level " + (i + 1), 98 + column * 134, 296 + row * 132, 70, data.Theme, i);
                UiFactory.Label(panel, data.Theme == 2 ? (i + 1) + " " + WatermelonArtwork.CampusNames[i] : "第 " + (i + 1) + " 级",
                    32 + column * 134, 338 + row * 132, 132, 34, data.Theme == 2 ? 17 : 20, UiFactory.Ink, TextAnchor.MiddleCenter);
            }
            UiFactory.Label(panel, "落稳后仍越线 2 秒，本局结束。\n切换主题不影响进度；离开会自动存档。", 32, 674, 532, 74, 22, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Button(panel, "知道了", 32, 770, 532, 66, close, true);
        }

        public void Save()
        {
            if (game == null || store == null) return;
            world?.Capture();
            data.Game = game.GetSnapshot(); data.BestScore = Math.Max(data.BestScore, data.Game.Score);
            string error;
            if (!store.TrySave(data, out error))
            {
                SaveError = "进度暂未保存，请检查设备剩余空间。";
                Debug.LogWarning("Watermelon save failed: " + error);
                status(SaveError);
            }
            else
            {
                bool recovered = SaveError != null;
                SaveError = null; dirty = false;
                if (recovered) status("进度已保存。");
            }
            sinceSave = 0;
            Changed?.Invoke();
        }

        private void Play(AudioClip clip) { if (soundEnabled() && clip != null) audioSource.PlayOneShot(clip); }
        private static uint NewSeed() { return unchecked((uint)DateTime.UtcNow.Ticks ^ (uint)Environment.TickCount); }
        private void AnimateBursts(float dt)
        {
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                burstAges[i] += dt;
                if (burstAges[i] >= .65f) { Destroy(bursts[i].gameObject); bursts.RemoveAt(i); burstAges.RemoveAt(i); continue; }
                bursts[i].rectTransform.anchoredPosition += Vector2.up * dt * 60;
                var color = bursts[i].color; color.a = 1 - burstAges[i] / .65f; bursts[i].color = color;
            }
        }
        private void ClearBursts() { foreach (var label in bursts) Destroy(label.gameObject); bursts.Clear(); burstAges.Clear(); }
        private void OnDestroy() { world?.Dispose(); artwork?.Dispose(); }
    }
}
