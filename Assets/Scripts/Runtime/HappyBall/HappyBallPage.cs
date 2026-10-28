using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.HappyBall;

namespace XanaduYonder.Runtime.HappyBall
{
    // Owns this live session. Shell supplies navigation, modal presentation and settings.
    internal sealed partial class HappyBallPage : MonoBehaviour
    {
        private HappyBallGame game;
        private HappyBallSaveData data;
        private HappyBallSaveStore store;
        private HappyBallSceneView scene;
        private HappyBallAudio audio;
        private HappyBallTouchInput input;
        private GameFeedback feedback;
        private Func<bool> acceptsInput, soundEnabled, hapticsEnabled;
        private Action returnLobby;
        private Action<Action> openSettings;
        private Action<string> setStatus;
        private Action<string, string, string, Action, string, Action> showDialog;
        private bool visible, foreground = true, manuallyPaused = true, dirty;
        private double accumulator, pendingRotation;
        private float sinceSave, lastPulse, deathFeedbackRemaining;
        private string hostWarning, loadWarning;
        private readonly List<HappyBallEvent> frameEvents = new List<HappyBallEvent>();
        public long BestScore => data == null ? 0 : data.BestScore;
        public string SaveError { get; private set; }
        public bool IsRunning => visible && foreground && !manuallyPaused && game != null && !game.GameOver && acceptsInput();
        public event Action Changed;

        public void Initialize(RectTransform root, Action lobby, Action<Action> settings, Func<bool> accepts,
            Func<bool> sound, Func<bool> haptics, GameFeedback sharedFeedback, Action<string> status,
            Action<string, string, string, Action, string, Action> dialog,
            HappyBallSaveStore storeOverride = null, HappyBallSaveData initialData = null)
        {
            returnLobby = lobby; openSettings = settings; acceptsInput = accepts;
            soundEnabled = sound; hapticsEnabled = haptics; feedback = sharedFeedback;
            setStatus = status; showDialog = dialog;
            store = storeOverride ?? new HappyBallSaveStore(Application.persistentDataPath);
            data = initialData != null ? initialData.Copy() : store.Load(out loadWarning);
            if (data == null) data = new HappyBallSaveData { Game = new HappyBallGame(NewSeed()).GetSnapshot() };
            if (!data.TryValidate(out var reason)) throw new ArgumentException(reason, nameof(initialData));
            game = new HappyBallGame(data.Game);
            data.Game = game.GetSnapshot();
            data.BestScore = Math.Max(data.BestScore, data.Game.Score);
            Build(root);
            scene = gameObject.AddComponent<HappyBallSceneView>(); scene.Initialize(stageImage); scene.Reset(game.GetSnapshot());
            audio = gameObject.AddComponent<HappyBallAudio>(); audio.Initialize();
            input = stageRect.gameObject.AddComponent<HappyBallTouchInput>();
            input.Configure(stageRect, stageImage); input.AcceptsInput = () => IsRunning;
            input.Rotate = angle => { if (IsRunning) pendingRotation += angle; };
            input.Canceled = () => pendingRotation = 0;
            RefreshScores(); RefreshOverlay(); RefreshError();
            gameObject.SetActive(false);
        }

        private static uint NewSeed()
        {
            var seed = unchecked((uint)DateTime.UtcNow.Ticks ^ (uint)Environment.TickCount);
            return seed == 0 ? 1u : seed;
        }

        public void Enter()
        {
            deathFeedbackRemaining = 0;
            visible = true; manuallyPaused = true; accumulator = pendingRotation = 0;
            gameObject.SetActive(true); scene.SetVisible(true); scene.Reset(game.GetSnapshot());
            input.Cancel(); RefreshScores(); RefreshOverlay(); RefreshError();
            setStatus(SaveError ?? loadWarning ?? "");
            CheckEndState();
        }

        public void Leave()
        {
            if (visible) { Suspend(); Save(); }
            visible = false; scene?.SetVisible(false); gameObject.SetActive(false);
        }

        public void Suspend()
        {
            deathFeedbackRemaining = 0;
            manuallyPaused = true; accumulator = pendingRotation = 0;
            input?.Cancel(); audio?.Stop(); RefreshOverlay();
        }

        public void SetForeground(bool value)
        {
            foreground = value;
            if (!value && visible) { Suspend(); Save(); }
        }

        public void Resume()
        {
            if (!visible || !foreground || !acceptsInput() || game.GameOver) return;
            input.Cancel(); accumulator = pendingRotation = 0;
            data.HasStarted = true; manuallyPaused = false;
            RefreshOverlay(); Save();
        }

        public void Back()
        {
            if (IsRunning) { Suspend(); Save(); }
            else returnLobby();
        }

        private void TogglePause()
        {
            if (IsRunning) { Suspend(); Save(); }
            else Resume();
        }

        private void LateUpdate()
        {
            if (!visible) return;
            if (deathFeedbackRemaining > 0)
            {
                if (!foreground || !acceptsInput()) Suspend();
                else Advance(Time.unscaledDeltaTime);
                return;
            }
            if (!IsRunning)
            {
                if (!manuallyPaused) Suspend();
                return;
            }
            Advance(Time.unscaledDeltaTime);
        }

        // One consistent entry point for production time and deterministic UI lifecycle checks.
        internal void Advance(float elapsed)
        {
            bool finishing = deathFeedbackRemaining > 0 && visible && foreground && acceptsInput();
            if (!IsRunning && !finishing) return;
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (elapsed > .25f) { Suspend(); Save(); return; }
            if (finishing)
            {
                scene.Tick(Math.Min(elapsed, deathFeedbackRemaining));
                deathFeedbackRemaining = Mathf.Max(0, deathFeedbackRemaining - elapsed);
                if (deathFeedbackRemaining == 0) CheckEndState();
                return;
            }
            accumulator += elapsed; sinceSave += elapsed;
            int count = (int)(accumulator / HappyBallRules.FixedStep);
            if (count == 0) { scene.Tick(elapsed); return; }
            double angle = pendingRotation / count; pendingRotation = 0;
            frameEvents.Clear();
            for (int i = 0; i < count; i++)
            {
                var result = game.Step(HappyBallRules.FixedStep, angle);
                frameEvents.AddRange(result.Events);
                accumulator -= HappyBallRules.FixedStep;
                if (game.GameOver) { accumulator = 0; break; }
            }
            var snapshot = game.GetSnapshot();
            scene.Render(snapshot, new HappyBallStepResult { Events = frameEvents.ToArray() });
            scene.Tick(elapsed);
            dirty = true; data.BestScore = Math.Max(data.BestScore, snapshot.Score);
            foreach (var item in frameEvents)
            {
                audio.Play(item.Kind, soundEnabled());
                bool strong = item.Kind == HappyBallEventKind.LayerBroken || item.Kind == HappyBallEventKind.Died;
                bool tactile = strong || item.Kind == HappyBallEventKind.Blocked || item.Kind == HappyBallEventKind.Bounce;
                if (tactile && Time.unscaledTime - lastPulse >= .08f)
                { feedback?.Pulse(hapticsEnabled()); lastPulse = Time.unscaledTime; }
            }
            RefreshScores();
            if (game.GameOver)
            {
                deathFeedbackRemaining = .33f;
                accumulator = pendingRotation = 0; input.Cancel();
                RecordResult(); RefreshOverlay();
            }
            else if (sinceSave >= 1f) Save();
        }

        public void CheckEndState()
        {
            if (game == null || !game.GameOver) return;
            deathFeedbackRemaining = 0;
            manuallyPaused = true; accumulator = pendingRotation = 0; input?.Cancel();
            RecordResult();
            RefreshScores(); RefreshOverlay();
        }

        private void RecordResult()
        {
            // Commit the terminal state immediately; the presentation delay is never
            // required for saving, restoring or recording the result exactly once.
            if (!data.ResultRecorded)
            { data.ResultRecorded = true; data.BestScore = Math.Max(data.BestScore, game.Score); Save(); }
        }

        private void ConfirmRestart()
        {
            Suspend(); Save();
            showDialog("重新开始这一局？", "当前进度将被清空，最高纪录会保留。", "重新开始", Restart, "继续当前游戏", RefreshOverlay);
        }

        private void Restart()
        {
            Suspend();
            game = new HappyBallGame(NewSeed()); data.Game = game.GetSnapshot();
            data.HasStarted = false; data.ResultRecorded = false;
            loadWarning = null;
            scene.Reset(data.Game); RefreshScores(); RefreshOverlay(); Save();
        }

        private void OpenSettings()
        {
            Suspend(); Save();
            openSettings(() => { RefreshOverlay(); RefreshError(); });
        }

        private void ShowHelp()
        {
            Suspend(); Save();
            showDialog("欢乐球球玩法", "左右拖动转塔，小球自动弹跳。\n黑色安全，普通碰红色结束。\n连落三层获得重击，可砸穿红板一次。\n黑色竖板会挡住旋转，跳高后可越过。",
                "知道了", RefreshOverlay, null, null);
        }

        public void Save()
        {
            if (game == null || store == null) return;
            data.Game = game.GetSnapshot(); data.BestScore = Math.Max(data.BestScore, data.Game.Score);
            if (!store.TrySave(data, out var error))
            {
                SaveError = "进度暂未保存，请检查设备剩余空间。";
                Debug.LogWarning("HappyBall save failed: " + error);
            }
            else { SaveError = null; loadWarning = null; dirty = false; sinceSave = 0; }
            RefreshError(); Changed?.Invoke();
            if (visible) setStatus(SaveError ?? "");
        }

        public void SetHostSaveWarning(string warning) { hostWarning = warning; RefreshError(); }

        private void RefreshScores()
        {
            if (scoreText == null || game == null) return;
            scoreText.text = game.Score.ToString(CultureInfo.InvariantCulture);
            bestText.text = data.BestScore.ToString(CultureInfo.InvariantCulture);
            if (pauseText != null) pauseText.text = IsRunning ? "暂停" : "继续";
        }

        private void RefreshOverlay()
        {
            if (pauseOverlay == null || data == null) return;
            bool over = game.GameOver;
            pauseOverlay.SetActive(deathFeedbackRemaining <= 0 && (manuallyPaused || over));
            overlayTitle.text = over ? "本局 " + game.Score : data.HasStarted ? "已暂停" : "准备好了";
            overlayDetail.text = over ? "最高纪录 " + data.BestScore : "";
            resumeText.text = over ? "再来一局" : data.HasStarted ? "继续游戏" : "开始游戏";
            pauseText.text = IsRunning ? "暂停" : "继续";
            pauseButton.interactable = !over;
        }

        private void RefreshError()
        {
            if (errorPanel == null) return;
            var message = hostWarning ?? SaveError ?? loadWarning;
            errorText.text = message ?? ""; errorPanel.SetActive(!string.IsNullOrEmpty(message));
        }

        private void OnDisable()
        {
            deathFeedbackRemaining = 0;
            input?.Cancel(); accumulator = pendingRotation = 0; audio?.Stop();
            if (visible) { manuallyPaused = true; if (dirty) Save(); }
        }
    }
}
