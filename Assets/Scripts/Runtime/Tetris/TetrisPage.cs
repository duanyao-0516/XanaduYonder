using System;
using UnityEngine;
using XanaduYonder.Core.Tetris;
namespace XanaduYonder.Runtime.Tetris
{
    public sealed class TetrisPage : MonoBehaviour
    {
        private TetrisSaveStore store;
        private TetrisSaveData data;
        private TetrisLoadResult loaded;
        private TetrisSession session;
        private TetrisView view;
        private TetrisTouchInput touch;
        private Action lobby;
        private Action<Action> settings;
        private Func<bool> accepts, sound, haptics;
        private Action<string,string,string,Action,string,Action> dialog;
        private GameFeedback feedback;
        private Action<string> status;
        private bool visible, foreground = true, dirty;
        private long savedRevision;
        private double checkpoint;
        private string hostWarning, loadWarning;
        public string SaveError { get; private set; }
        public bool CanReadRecord => loaded != null && loaded.CanWrite;
        public long BestScore => data == null ? 0 : data.BestScore;
        public bool HasActiveGame => session != null && session.HasStarted && !session.Snapshot.GameOver;
        public event Action Changed;
        public TetrisSession Session => session;
        internal TetrisView View => view;
        internal void Initialize(RectTransform root, Action onLobby, Action<Action> onSettings,
            Func<bool> acceptsInput, Func<bool> soundEnabled, Func<bool> hapticsEnabled,
            GameFeedback gameFeedback, Action<string> setStatus,
            Action<string,string,string,Action,string,Action> showDialog, TetrisSaveStore storeOverride = null)
        {
            lobby = onLobby; settings = onSettings; accepts = acceptsInput; sound = soundEnabled; haptics = hapticsEnabled;
            feedback = gameFeedback; status = setStatus; dialog = showDialog;
            store = storeOverride ?? new TetrisSaveStore(Application.persistentDataPath);
            view = new TetrisView(root, Back, OpenHelp, OpenSettings, TogglePause);
            touch = root.gameObject.AddComponent<TetrisTouchInput>();
            touch.Session = () => session;
            touch.AcceptsInput = () => visible && foreground && loaded.CanWrite && session.Phase == TetrisSessionPhase.Running && accepts();
            touch.Configure(view.ActionTargets);
            Load();
            visible = false; root.gameObject.SetActive(false);
        }
        private void Load()
        {
            loaded = store.Load();
            data = loaded.Data ?? new TetrisSaveData();
            loadWarning = loaded.Message;
            SaveError = loaded.CanWrite ? null : loaded.Message;
            SetSession(data.Game == null ? new TetrisGame(Seed()) : new TetrisGame(data.Game), data.Game != null);
            dirty = false; savedRevision = session.Revision; Refresh();
        }
        private static uint Seed() { uint seed = unchecked((uint)Guid.NewGuid().GetHashCode()); return seed == 0 ? 1u : seed; }
        private void SetSession(TetrisGame game, bool started)
        {
            if (session != null) session.Stepped -= OnStep;
            session = new TetrisSession(game, started);
            session.Stepped += OnStep;
            checkpoint = 0; touch?.Cancel();
        }
        public void Enter()
        {
            visible = true; gameObject.SetActive(true); touch.Cancel(); session.Pause();
            view.RefreshLayout(); Refresh();
        }
        public void Leave() { Suspend(); visible = false; gameObject.SetActive(false); }
        public void Suspend()
        {
            if (session == null) return;
            session.Pause(); touch?.Cancel(); view?.ClearVisuals(); feedback?.Stop(); Save(); Refresh();
        }
        public void SetForeground(bool value) { foreground = value; if (!value) Suspend(); }
        public void SetHostSaveWarning(string message) { hostWarning = message; RefreshWarning(); }
        public void Back() { Suspend(); lobby(); }
        private void TogglePause()
        {
            if (!loaded.CanWrite) return;
            if (session.Phase == TetrisSessionPhase.Running || session.Phase == TetrisSessionPhase.Countdown) Suspend();
            else if (session.Phase == TetrisSessionPhase.Ended) NewRound();
            else Begin();
        }
        private void Begin()
        {
            if (!visible || !foreground || !loaded.CanWrite || !accepts()) return;
            touch.Cancel(); session.StartOrResume(); Save(); Refresh();
        }
        private void NewRound()
        {
            if (!loaded.CanWrite) return;
            Action begin = () => {
                data.ResultRecorded = false; data.Game = null;
                SetSession(new TetrisGame(Seed()), false); dirty = true;
                Begin(); Changed?.Invoke();
            };
            Suspend();
            if (session.HasStarted && session.Phase != TetrisSessionPhase.Ended)
                dialog("重新开始？", "本局进度会清空，最高纪录会保留。", "重新开始", begin, "保留本局", Refresh);
            else begin();
        }
        private void OpenHelp()
        {
            Suspend();
            dialog("俄罗斯方块", "左右移动，双向旋转，填满横行即可消除。\n每消除 10 行提速，堆到顶部结束。\n左侧暂存每块一次，右侧预览 3 块。\n描边是落点；软降慢落，底部按钮直落。", "计分与续局", OpenScoringHelp, "返回游戏", Refresh);
        }
        private void OpenScoringHelp()
        {
            dialog("计分与续局", "单 / 双 / 三 / 四行：\n100 / 300 / 500 / 800 × 当前等级。\n软降每格 1 分，直落每格 2 分。\n不加算 T-spin、连击、B2B、全清分。", "知道了", Refresh, "操作说明", OpenHelp);
        }

        private void OpenSettings() { Suspend(); settings(Refresh); }
        private void OnStep(TetrisStepResult result)
        {
            dirty = true;
            var snapshot = session.Snapshot;
            long best = data.BestScore;
            data.BestScore = Math.Max(best, snapshot.Score);
            if (result.GameOver && !data.ResultRecorded)
            {
                data.ResultRecorded = true; feedback?.Miss(sound(), haptics());
            }
            else if (result.Locked || result.Held) feedback?.Play(result.ClearedLines > 0, sound(), haptics() && result.ClearedLines > 0);
            if (result.ClearedLines > 0) view.FlashLines(result.ClearedRows);
            if (result.Locked || result.Held || result.GameOver) { Save(); Changed?.Invoke(); }
        }
        public void Save()
        {
            if (session == null || loaded == null || !loaded.CanWrite || (!dirty && savedRevision == session.Revision)) return;
            data.Game = session.HasStarted ? session.Snapshot : null;
            if (data.Game != null) data.BestScore = Math.Max(data.BestScore, data.Game.Score);
            if (store.TrySave(data, out var error))
            {
                dirty = false; savedRevision = session.Revision; SaveError = null; loadWarning = null; checkpoint = 0;
            }
            else
            {
                SaveError = "进度暂未保存，请检查空间或存档权限。";
                Debug.LogWarning("Tetris save failed: " + error);
            }
            RefreshWarning(); status?.Invoke(SaveError ?? "");
        }
        private void LateUpdate()
        {
            if (!visible || session == null) return;
            if (view.RefreshLayout()) Suspend();
            if (!foreground || !accepts())
            {
                if (session.Phase == TetrisSessionPhase.Running || session.Phase == TetrisSessionPhase.Countdown) Suspend();
                return;
            }
            view.TickVisuals(Time.unscaledDeltaTime);
            var oldPhase = session.Phase;
            session.Advance(Time.unscaledDeltaTime);
            if (oldPhase != session.Phase && session.Phase == TetrisSessionPhase.Paused) Save();
            checkpoint += Time.unscaledDeltaTime;
            if (checkpoint >= 1) { checkpoint = 0; Save(); }
            Refresh();
        }
        private void RefreshWarning()
        {
            string message = SaveError;
            if (string.IsNullOrEmpty(message)) message = loadWarning;
            else if (!string.IsNullOrEmpty(loadWarning) && loadWarning != message) message += "  " + loadWarning;
            if (!string.IsNullOrEmpty(hostWarning)) message = hostWarning + (string.IsNullOrEmpty(message) ? "" : "  " + message);
            view?.SetWarning(message);
        }
        private void Refresh()
        {
            if (view == null || session == null) return;
            view.Render(session.Snapshot, session.GhostY, CanReadRecord ? (long?)BestScore : null);
            view.SetControlsEnabled(loaded.CanWrite && session.Phase == TetrisSessionPhase.Running && visible && foreground && accepts());
            view.SetPauseLabel(session.Phase == TetrisSessionPhase.Running || session.Phase == TetrisSessionPhase.Countdown);
            if (!loaded.CanWrite)
            {
                bool corrupt = loaded.Status == TetrisLoadStatus.Corrupt;
                view.SetOverlay(corrupt ? "存档需要处理" : "存档暂不可用", loaded.Message,
                    corrupt ? "重置损坏存档" : "重新读取", corrupt ? (Action)ConfirmReset : Load, "返回大厅", Back, true);
            }
            else
            {
                switch (session.Phase)
                {
                    case TetrisSessionPhase.Ready: view.SetOverlay("慢慢来，先放一块", "填满一行，就向前一点。", "开始游戏", Begin, null, null, true); break;
                    case TetrisSessionPhase.Paused: view.SetOverlay("歇一会儿", "这一局为你留着。", "继续游戏", Begin, "重新开始", NewRound, true); break;
                    case TetrisSessionPhase.Countdown: view.SetOverlay("倒计时：" + session.CountdownNumber, "准备继续", null, null, "保持暂停", Suspend, true); break;
                    case TetrisSessionPhase.Ended: view.SetOverlay("这一局，很不错", "得分 " + session.Snapshot.Score + " · 消除 " + session.Snapshot.Lines + " 行", "再来一局", NewRound, "返回大厅", Back, true); break;
                    default: view.SetOverlay(null,null,null,null,null,null,false); break;
                }
            }
            RefreshWarning();
        }
        private void ConfirmReset()
        {
            dialog("重置损坏存档？", "损坏文件会先归档，本机的方块进度和最高纪录将重新开始。", "归档并重置", () => {
                if (!store.TryResetCorrupt(out var error)) { SaveError = "暂时无法重置存档：" + error; RefreshWarning(); return; }
                Load(); Changed?.Invoke();
            }, "取消", Refresh);
        }
        private void OnDisable() { if (session != null) { session.Pause(); touch?.Cancel(); Save(); } }
        private void OnDestroy() { if (session != null) session.Stepped -= OnStep; }
    }
}
