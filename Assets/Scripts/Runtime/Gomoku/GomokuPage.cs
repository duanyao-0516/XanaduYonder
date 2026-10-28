using System;
using UnityEngine;
using XanaduYonder.Core.Gomoku;

namespace XanaduYonder.Runtime.Gomoku
{
    internal sealed partial class GomokuPage : MonoBehaviour
    {
        private RectTransform root;
        private GomokuSaveData data;
        private GomokuSaveStore store;
        private GomokuSession session;
        private GomokuBoardView board;
        private Action returnLobby;
        private Action<Action> openSettings;
        private Action<string> setStatus;
        private Func<bool> acceptsInput, soundEnabled, hapticsEnabled;
        private GameFeedback feedback;
        private bool visible, foreground = true, invalidSave, saveFailed, resultShown, settingsOpen;
        private string warning, aiError;
        public event Action Changed;
        public string SaveError { get { return saveFailed ? "五子棋进度暂未保存，请重试。" : invalidSave ? warning : null; } }

        public void Initialize(RectTransform parent, Action lobby, Action<Action> settings, Func<bool> canInput,
            Func<bool> sound, Func<bool> haptics, GameFeedback sharedFeedback, Action<string> status,
            GomokuSaveData initialData = null, GomokuSaveStore initialStore = null)
        {
            root = parent; returnLobby = lobby; openSettings = settings; acceptsInput = canInput;
            soundEnabled = sound; hapticsEnabled = haptics; feedback = sharedFeedback; setStatus = status;
            store = initialData != null ? initialStore : initialStore ?? new GomokuSaveStore(Application.persistentDataPath);
            data = initialData != null ? initialData.Copy() : store.Load(out warning) ?? new GomokuSaveData();
            invalidSave = initialData == null && store.LastLoadStatus == GomokuLoadStatus.Invalid;
            Build();
        }

        public void Enter()
        {
            visible = true; gameObject.SetActive(true);
            RestoreSelected(); Refresh();
            if (invalidSave) ShowInvalidSave();
            else if (session == null) ShowSetup(false);
        }

        private void RestoreSelected()
        {
            CancelSearch(); CloseDialog(false);
            var saved = data.Slots[(int)data.SelectedMode];
            session = saved == null ? null : new GomokuSession(saved);
            session?.SetForeground(foreground);
            resultShown = session != null && session.Game.State != GomokuState.Playing;
            aiError = null;
        }

        internal void StartGame(GomokuMode mode, GomokuDifficulty difficulty, GomokuStone humanStone, int blackPlayer)
        {
            CancelSearch(); CloseDialog(false);
            invalidSave = false; warning = null; aiError = null;
            data.SelectedMode = mode; data.PreferredDifficulty = difficulty;
            data.PreferredHumanStone = humanStone; data.PreferredBlackPlayer = blackPlayer;
            session = GomokuSession.Create(mode, difficulty, humanStone, blackPlayer);
            session.SetForeground(foreground); resultShown = false;
            Save(); Refresh(); EnsureSearch();
        }

        private bool Allowed { get { return visible && foreground && !settingsOpen && dialog == null && acceptsInput(); } }
        private void Select(int index)
        {
            if (!Allowed || session == null || !session.TrySelect(index)) return;
            Refresh();
        }
        private void Confirm()
        {
            if (!Allowed) return;
            if (session == null) { ShowSetup(false); return; }
            if (session.Game.State != GomokuState.Playing) { ShowResult(); return; }
            if (session.Paused) { aiError = null; session.Resume(); Refresh(); EnsureSearch(); return; }
            if (!session.ConfirmSelection()) return;
            Committed();
        }
        private void Committed()
        {
            feedback?.Play(session.Game.State != GomokuState.Playing, soundEnabled(), hapticsEnabled());
            Save(); Refresh(); CheckEndState(); EnsureSearch();
        }
        private void ClearSelection() { if (Allowed) { session?.ClearSelection(); board.CancelInput(); Refresh(); } }
        private void Pause()
        {
            if (!Allowed || session == null || session.Game.State != GomokuState.Playing) return;
            CancelSearch(); session.Pause(); Save(); Refresh();
        }
        private void RequestUndo()
        {
            if (!Allowed || session == null || !session.CanUndo) return;
            if (session.Mode == GomokuMode.Local)
            {
                var next = PlayerFor(session.Game.ToPlay);
                ShowDialog("同意悔棋吗？", "请玩家 " + next + " 确认。\n撤回上一手后，由另一位玩家重新落子。", "同意悔棋", Undo, "不同意", () => { });
            }
            else Undo();
        }
        private void Undo()
        {
            CancelSearch(); aiError = null;
            if (session != null && session.Undo()) { Save(); Refresh(); EnsureSearch(); }
        }
        private int PlayerFor(GomokuStone stone) { return stone == GomokuStone.Black ? session.BlackPlayer : 3 - session.BlackPlayer; }
        private void SwitchMode(GomokuMode mode)
        {
            if (!Allowed || data.SelectedMode == mode) return;
            CancelSearch(); session?.Pause(); Save();
            data.SelectedMode = mode; RestoreSelected(); Save(); Refresh();
            if (session == null) ShowSetup(false);
        }
        public void Save()
        {
            if (data == null || invalidSave) return;
            if (session != null) data.Slots[(int)session.Mode] = session.Export();
            if (store != null)
            {
                string error;
                saveFailed = !store.TrySave(data, out error);
                if (saveFailed) Debug.LogWarning("Gomoku save failed: " + error);
            }
            Changed?.Invoke();
            setStatus?.Invoke(SaveError ?? warning ?? "");
            if (retryButton != null) retryButton.gameObject.SetActive(saveFailed);
        }
        private void RetrySave() { Save(); Refresh(); }
        public void Leave()
        {
            CancelSearch(); CloseDialog(false); settingsOpen = false;
            session?.SetBlocked(false); session?.Pause(); board?.CancelInput(); Save();
            visible = false; gameObject.SetActive(false);
        }
        public void Suspend()
        {
            if (!visible) return;
            CancelSearch(); board?.CancelInput(); session?.Pause(); Save(); Refresh();
        }
        public void SetForeground(bool value)
        {
            foreground = value;
            if (!value)
            {
                CancelSearch(); CloseDialog(false); settingsOpen = false;
                session?.SetBlocked(false); board?.CancelInput();
            }
            session?.SetForeground(value);
            if (!value) Save();
            if (visible) Refresh();
        }
        public void Back()
        {
            if (dialog != null) { dismissDialog?.Invoke(); return; }
            RequestLeave();
        }
        private void RequestLeave()
        {
            Save();
            if (!saveFailed) { returnLobby(); return; }
            ShowDialog("进度尚未保存", "最近的落子可能在退出应用后丢失。\n可以重试保存，或留在棋盘继续。", "重试保存", () => { Save(); RequestLeave(); },
                "留在棋盘", () => { }, "仍然返回大厅", returnLobby);
        }
        public void CheckEndState()
        {
            if (!Allowed || session == null) return;
            if (session.Game.State != GomokuState.Playing && !resultShown) ShowResult();
            else EnsureSearch();
        }
        private void Settings()
        {
            if (!Allowed) return;
            var previous = session; bool resume = previous != null && !previous.Paused;
            CancelSearch(); board.CancelInput(); previous?.SetBlocked(true); settingsOpen = true;
            Save(); Refresh();
            openSettings(() =>
            {
                if (!settingsOpen) return;
                settingsOpen = false; previous?.SetBlocked(false);
                if (visible && foreground && previous == session && resume) previous.Resume();
                Refresh(); EnsureSearch();
            });
        }
        private void OnDisable() { CancelSearch(); board?.CancelInput(); session?.Pause(); }
        private void OnDestroy() { CancelSearch(); }
    }
}
