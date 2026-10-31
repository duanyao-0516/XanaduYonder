using System;
using UnityEngine;
using XanaduYonder.Core.AircraftFire;

namespace XanaduYonder.Runtime.AircraftFire
{
    internal sealed partial class AircraftFirePage : MonoBehaviour
    {
        private RectTransform root;
        private AircraftGame game;
        private AircraftScene scene;
        private AircraftPointer pointer;
        private AircraftAudio audioPlayer;
        private AircraftFireSaveData profile;
        private AircraftFireSaveStore store;
        private Action returnLobby;
        private Action<Action> openSettings;
        private Func<bool> acceptsInput, soundEnabled, hapticsEnabled;
        private GameFeedback feedback;
        private bool visible, foreground = true, settingsBlocked, dirty;
        private AircraftMode selectedMode;
        private bool selectedCrazy;
        private int settingsGeneration;
        private float sinceSave, elapsedPresentation, terminalPresentation;
        private Vector2 lastSize;
        private Rect lastSafe;
        private string loadWarning, saveError, hostSaveWarning;
        public event Action Changed;
        public long BestScore => profile?.BestScore ?? 0;
        public string SaveError => saveError ?? loadWarning;
        internal AircraftGame Game => game;
        internal RectTransform Surface => surface;
        internal AircraftPointer Pointer => pointer;
        internal bool HasDialog => dialog != null;
        private bool HostAllows => acceptsInput == null || acceptsInput();
        private bool CanPlay => visible && foreground && !settingsBlocked && dialog == null && HostAllows && game != null && game.Phase == AircraftPhase.Running;

        public void Initialize(RectTransform parent, Action lobby, Action<Action> settings, Func<bool> accepts,
            Func<bool> sound, Func<bool> haptics, GameFeedback sharedFeedback,
            AircraftFireSaveData initialData = null, AircraftFireSaveStore initialStore = null)
        {
            root = parent; returnLobby = lobby; openSettings = settings; acceptsInput = accepts;
            soundEnabled = sound; hapticsEnabled = haptics; feedback = sharedFeedback;
            store = initialData != null ? initialStore : initialStore ?? new AircraftFireSaveStore(Application.persistentDataPath);
            profile = initialData != null ? initialData.Copy() : store.Load(out loadWarning);
            Build(); scene = new AircraftScene(surface);
            audioPlayer = gameObject.AddComponent<AircraftAudio>(); audioPlayer.Initialize(soundEnabled);
            scene.EnemyExploded += audioPlayer.Enemy;
            pointer = surface.gameObject.AddComponent<AircraftPointer>();
            pointer.AcceptsInput = () => CanPlay;
            pointer.Moved = delta => { if (CanPlay) game.MoveHero(delta.x * 640 / surface.rect.width, delta.y * 640 / surface.rect.width); };
            gameObject.SetActive(false);
        }

        public void Enter()
        {
            settingsGeneration++; settingsBlocked = false; visible = true;
            gameObject.SetActive(true); transform.SetAsLastSibling();
            LayoutNow(); ShowMenu();
        }
        private void ShowMenu()
        {
            Suspend(); Save(); CloseDialog();
            game = null; terminalPresentation = 0; flashTime = 0;
            entry.gameObject.SetActive(true); battleHud.gameObject.SetActive(false);
            bombFlash.color = Color.clear;
            scene.Reset(); LayoutNow(); scene.RenderMenu(WorldHeight);
            RefreshWarning();
        }
        private void StartRun(AircraftMode mode, bool crazy)
        {
            if (!visible || !foreground || settingsBlocked || !HostAllows) return;
            Save(); CloseDialog(); pointer.Cancel();
            selectedMode = mode; selectedCrazy = mode == AircraftMode.Classic && crazy;
            game = new AircraftGame(mode, selectedCrazy, NewSeed(), WorldHeight);
            entry.gameObject.SetActive(false); battleHud.gameObject.SetActive(true);
            scene.Reset(); elapsedPresentation = 0; sinceSave = 0; terminalPresentation = 0;
            LayoutNow(); Refresh();
        }
        private static uint NewSeed() => unchecked((uint)DateTime.UtcNow.Ticks ^ (uint)Environment.TickCount) | 1u;
        private float WorldHeight => Mathf.Max(640, root.rect.height * 640 / Mathf.Max(1, root.rect.width));

        public void Leave()
        {
            if (!visible) return;
            Suspend(); Save(); settingsGeneration++; settingsBlocked = false;
            CloseDialog(); visible = false; pointer.Cancel(); audioPlayer.Stop();
            gameObject.SetActive(false);
        }
        public void Suspend()
        {
            pointer?.Cancel(); game?.Pause(); audioPlayer?.Stop();
        }
        public void SetForeground(bool value)
        {
            foreground = value;
            if (!value && visible)
            {
                Suspend(); Save(); settingsGeneration++; settingsBlocked = false;
                CloseDialog();
            }
        }
        public void SetHostSaveWarning(string warning) { hostSaveWarning = warning; RefreshWarning(); }
        public void Back()
        {
            if (!visible || settingsBlocked) return;
            if (dialogKind == DialogKind.Help || dialogKind == DialogKind.Records)
            {
                CloseDialog();
                return;
            }
            if (game == null) { Exit(); return; }
            if (dialogKind == DialogKind.Pause || dialogKind == DialogKind.Result) { ShowMenu(); return; }
            PauseGame();
        }
        internal void PauseGame()
        {
            if (!visible || game == null || settingsBlocked || !HostAllows) return;
            Suspend(); Save();
            if (game.Phase == AircraftPhase.GameOver || game.Phase == AircraftPhase.Completed) ShowResult();
            else ShowPause();
        }
        private void Continue()
        {
            if (!visible || !foreground || !HostAllows) return;
            CloseDialog(); pointer.Cancel(); game.Resume();
        }
        private void Exit() { Suspend(); Save(); returnLobby?.Invoke(); }
        internal void UseBomb()
        {
            if (!CanPlay || !game.UseBomb()) return;
            feedback?.Pulse(hapticsEnabled != null && hapticsEnabled());
            flashTime = .18f; Refresh();
        }
        internal void ShowRules()
        {
            if (!visible || game != null || !foreground || settingsBlocked || !HostAllows) return;
            ShowHelp();
        }
        internal void Settings()
        {
            if (!visible || game != null || !foreground || settingsBlocked || !HostAllows || openSettings == null) return;
            Save(); CloseDialog(); settingsBlocked = true;
            var generation = ++settingsGeneration;
            openSettings?.Invoke(() =>
            {
                if (!visible || generation != settingsGeneration) return;
                settingsBlocked = false;
                RefreshWarning();
            });
        }
        private void Record()
        {
            if (game == null) return;
            if (profile.Record(game.Mode == AircraftMode.Level, game.Crazy, game.Score, game.Stage))
            { dirty = true; Changed?.Invoke(); }
        }
        private void RetrySave() { dirty = true; Save(); }
        public void Save()
        {
            if (profile == null) return;
            Record(); sinceSave = 0;
            if (!dirty || store == null) return;
            if (store.TrySave(profile, out var reason))
            { dirty = false; saveError = null; loadWarning = null; }
            else
            {
                saveError = "纪录暂未保存，请检查设备空间；可在暂停页重试。";
                Debug.LogWarning("Aircraft Fire save failed: " + reason);
            }
            RefreshWarning(); Changed?.Invoke();
        }
        internal void Advance(float seconds)
        {
            if (!visible || game == null) return;
            if (!foreground || !HostAllows || settingsBlocked) { Suspend(); return; }
            if (game.Phase == AircraftPhase.Running && dialog == null)
            {
                // Do not convert a stalled/background frame into an unavoidable hit.
                if (seconds > .25f) { Suspend(); ShowPause(); return; }
                var lives = game.Lives; var boss = game.BossActive;
                game.Advance(Mathf.Max(0, seconds)); elapsedPresentation += seconds;
                if (game.Lives < lives) { audioPlayer.Hero(); feedback?.Pulse(hapticsEnabled != null && hapticsEnabled()); }
                if (!boss && game.BossActive) audioPlayer.BossAppeared();
                Record(); sinceSave += seconds;
                if (dirty && sinceSave >= 2) Save();
            }
            var terminal = game.Phase == AircraftPhase.GameOver || game.Phase == AircraftPhase.Completed;
            if (terminal) terminalPresentation += Mathf.Clamp(seconds, 0, .1f);
            if (dialog == null)
            {
                if (game.Phase == AircraftPhase.Paused) ShowPause();
                else if (terminal && terminalPresentation >= .8f)
                { Save(); ShowResult(); }
            }
            Refresh(); audioPlayer.Tick(CanPlay, terminal && terminalPresentation < 1.2f);
        }
        private void Update()
        {
            if (!visible) return;
            if (root.rect.size != lastSize || Screen.safeArea != lastSafe) LayoutNow();
            if (CanPlay)
            {
                var dx = (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? 1 : 0);
                var dy = (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? 1 : 0);
                game.MoveHero(dx * 480 * Time.unscaledDeltaTime, dy * 480 * Time.unscaledDeltaTime);
                if (Input.GetKeyDown(KeyCode.Space)) UseBomb();
                if (Input.GetKeyDown(KeyCode.P)) PauseGame();
            }
            Advance(Time.unscaledDeltaTime);
        }
        private void OnDisable() { pointer?.Cancel(); audioPlayer?.Stop(); if (visible) { Suspend(); Save(); } }
        private void OnDestroy() { settingsGeneration++; pointer?.Cancel(); audioPlayer?.Stop(); }
    }
}
