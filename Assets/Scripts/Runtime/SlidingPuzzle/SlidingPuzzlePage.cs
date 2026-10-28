using System;
using UnityEngine;
using XanaduYonder.Core.SlidingPuzzle;

namespace XanaduYonder.Runtime.SlidingPuzzle
{
    internal sealed partial class SlidingPuzzlePage : MonoBehaviour
    {
        private RectTransform root;
        private SlidingPuzzleProfile profile;
        private SlidingPuzzleSaveStore store;
        private SlidingPuzzleBoardView board;
        private SlidingPuzzlePointer pointer;
        private Action returnLobby;
        private Action<Action> openSettings;
        private Func<bool> acceptsInput, soundEnabled, hapticsEnabled;
        private Func<double> realtime;
        private double lastRealtime;
        private Action<string> setStatus;
        private GameFeedback feedback;
        private bool visible, foreground = true, settingsBlocked, choosing, resultDismissed, saveFailed;
        private string warning, hostSaveWarning;
        private float saveElapsed, recoveryNoticeRemaining;
        private Vector2 lastSize;
        public event Action Changed;
        public int CompletedCount { get { return profile == null ? 0 : profile.CompletedCount; } }
        public string SaveError { get { return saveFailed ? warning : null; } }
        internal SlidingPuzzleSession Session { get { return profile.ActiveSession; } }
        internal SlidingPuzzleProfile Profile { get { return profile; } }
        internal bool IsChoosing { get { return choosing; } }
        internal bool HasDialog { get { return dialog != null; } }
        internal bool IsAnimating { get { return board != null && board.Animating; } }
        public void Initialize(RectTransform parent, Action lobby, Action<Action> settings, Func<bool> canInput,
            Func<bool> sound, Func<bool> haptics, GameFeedback sharedFeedback, Action<string> status,
            SlidingPuzzleProfile initialProfile = null, SlidingPuzzleSaveStore initialStore = null, Func<double> realtime = null)
        {
            root = parent; returnLobby = lobby; openSettings = settings; acceptsInput = canInput;
            soundEnabled = sound; hapticsEnabled = haptics; feedback = sharedFeedback; setStatus = status;
            this.realtime = realtime ?? (() => Time.realtimeSinceStartupAsDouble); lastRealtime = this.realtime();
            store = initialProfile != null ? initialStore : initialStore ?? new SlidingPuzzleSaveStore(Application.persistentDataPath);
            if (initialProfile != null) profile = initialProfile;
            else
            {
                var saved = store.Load(out warning);
                profile = saved == null ? new SlidingPuzzleProfile() : new SlidingPuzzleProfile(saved);
                if (!string.IsNullOrEmpty(warning))
                {
                    warning = saved == null ? "存档无法恢复，已开始新局" : "存档异常，已从备份恢复";
                    recoveryNoticeRemaining = 8;
                }
            }
            choosing = profile.SelectedMode == 1 && Session == null;
            Build(); Refresh(); Session?.Leave();
        }
        public void Enter()
        {
            lastRealtime = realtime();
            visible = true; foreground = true; settingsBlocked = false; resultDismissed = false; gameObject.SetActive(true);
            choosing = profile.SelectedMode == 1 && (Session == null || Session.Game.Won);
            Session?.Enter(); SynchronizeGate(); Build(); Refresh(); CheckEndState();
        }
        public void Leave()
        {
            CancelInput(); CloseDialog(); Session?.Leave(); Save(); visible = false; gameObject.SetActive(false);
        }
        public void Suspend() { CancelInput(); Save(); }
        public void SetForeground(bool value)
        {
            // Android can include the suspended interval in its first unscaledDeltaTime.
            // Start a fresh foreground interval at each lifecycle callback instead.
            lastRealtime = realtime();
            foreground = value; CancelInput();
            if (!value) { CloseDialog(); settingsBlocked = false; resultDismissed = false; }
            Session?.SetForeground(value); SynchronizeGate(); Save(); if (value) CheckEndState();
        }
        public void Back()
        {
            if (dialog != null) { DismissDialog(); return; }
            if (profile.SelectedMode == 1 && !choosing) ShowLevels(); else Home();
        }
        private void Home() { CancelInput(); Save(); returnLobby(); }
        internal void SelectMode(int mode)
        {
            if (!CanNavigate) return;
            CancelInput(); Save(); profile.SelectMode(mode); choosing = mode == 1;
            resultDismissed = false; SynchronizeGate(); BuildContent(); Refresh(); Save();
        }
        internal void SelectSize(int size)
        {
            if (!CanNavigate || profile.SelectedMode != 0 || size == profile.SelectedSize) return;
            CancelInput(); Save(); profile.StartFree(size); resultDismissed = false; choosing = false;
            SynchronizeGate(); BuildContent(); Refresh(); Save();
        }
        internal void StartLevel(int index)
        {
            if (!CanNavigate || !profile.IsLevelUnlocked(index)) return;
            bool replay = profile.ActiveLevelIndex == index && Session != null && Session.Game.Won;
            CancelInput(); CloseDialog(); Save(); profile.StartLevel(index, replay); choosing = false; resultDismissed = false;
            Session.Enter(); SynchronizeGate(); BuildContent(); Refresh(); Save(); CheckEndState();
        }
        internal void ShowLevels()
        {
            CancelInput(); CloseDialog(); Save(); choosing = true; SynchronizeGate(); BuildContent(); Refresh();
        }
        private void SelectChapter(int chapter)
        { if (!CanNavigate || !profile.SelectChapter(chapter)) return; choosing = true; BuildContent(); Refresh(); Save(); }
        private bool CanNavigate { get { return visible && foreground && !settingsBlocked && dialog == null && acceptsInput(); } }
        private bool CanMove { get { return CanNavigate && !choosing && Session != null && Session.CanInteract && !IsAnimating; } }
        internal void Move(int tile)
        {
            if (!CanMove) return;
            if (!Session.Move(tile)) { RefreshFooter(); return; }
            resultDismissed = false; Save(); RefreshCounts(); board.Render(Session.Game, Scale, true);
            feedback?.Play(Session.Game.Won, soundEnabled(), hapticsEnabled());
        }
        internal void Undo()
        {
            if (!CanMove || !Session.Undo()) return;
            CancelInput(); resultDismissed = false; Save(); board.Render(Session.Game, Scale); Refresh();
        }
        internal void Restart()
        {
            CancelInput(); CloseDialog(); if (Session == null) return;
            Session.Restart(); Session.Enter(); resultDismissed = false; SynchronizeGate(); BuildContent(); Refresh(); Save();
        }
        private void NewFree()
        {
            CancelInput(); CloseDialog(); profile.StartFree(profile.SelectedSize, true); choosing = false;
            resultDismissed = false; SynchronizeGate(); BuildContent(); Refresh(); Save();
        }
        public void Save()
        {
            if (profile == null) return;
            var data = profile.Capture();
            if (store != null)
            {
                string error;
                if (!store.TrySave(data, out error)) { saveFailed = true; warning = "进度尚未保存，请检查设备空间"; Debug.LogWarning("Sliding puzzle save failed: " + error); }
                else if (saveFailed) { saveFailed = false; warning = null; }
            }
            saveElapsed = 0; RefreshFooter(); if (visible) setStatus(warning ?? ""); Changed?.Invoke();
        }
        internal void SetHostSaveWarning(string text) { hostSaveWarning = text; RefreshFooter(); }
        private void Settings()
        {
            if (!CanNavigate) return;
            CancelInput(); settingsBlocked = true; SynchronizeGate(); Save();
            openSettings(() => { settingsBlocked = false; CancelInput(); SynchronizeGate(); Refresh(); CheckEndState(); });
        }
        private void SynchronizeGate()
        {
            if (Session == null) return;
            Session.SetForeground(foreground);
            Session.SetBlocked(!visible || choosing || settingsBlocked || dialog != null || !acceptsInput());
        }
        private void CancelInput() { pointer?.CancelGesture(); board?.Finish(); }
        private void Update()
        {
            if (!visible) return;
            double now = realtime();
            float frameSeconds = (float)Math.Max(0, now - lastRealtime); lastRealtime = now;
            if (foreground && !saveFailed && recoveryNoticeRemaining > 0)
            {
                recoveryNoticeRemaining -= frameSeconds;
                if (recoveryNoticeRemaining <= 0) { warning = null; setStatus(""); }
            }
            if (lastSize != root.rect.size) { CancelInput(); Build(); Refresh(); }
            SynchronizeGate();
            Session?.Tick(frameSeconds);
            board?.Tick(frameSeconds); RefreshCounts(); RefreshFooter();
            if (Session != null && Session.CanInteract && Session.Game.Moves > 0 && !choosing)
            { saveElapsed += frameSeconds; if (saveElapsed >= 10) Save(); }
            CheckEndState();
        }
        private void OnDisable() { CancelInput(); }
        private void OnDestroy() { CancelInput(); }
        internal void LayoutNow() { CancelInput(); Build(); Refresh(); }
    }
}
