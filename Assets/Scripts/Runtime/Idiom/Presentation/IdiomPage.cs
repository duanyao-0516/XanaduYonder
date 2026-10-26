using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Idiom;

namespace XanaduYonder.Runtime.Idiom
{
    internal sealed partial class IdiomPage : MonoBehaviour
    {
        private RectTransform root;
        private Action goLobby, closeModal;
        private Action<string> setStatus;
        private Func<bool> canInteract;
        private Func<string, float, Action, RectTransform> openModal;
        private IdiomKeyboardViewport keyboard;
        private IdiomCatalogLoader loader;
        private IdiomLexicon lexicon;
        private IdiomSaveStore store;
        private IdiomSaveData data;
        private IdiomGame characterGame, soundGame;
        private IdiomNotebookStore notebookStore;
        private IdiomNotebookData notebook;
        private readonly List<IdiomNotebookItem> notebookItems = new List<IdiomNotebookItem>();
        private readonly HashSet<IdiomGame> announcedResults = new HashSet<IdiomGame>();
        private readonly float[] chatOffsets = new float[2];
        private Coroutine loadingRoutine;
        private int loadRequest;
        private bool visible, loading, loaded, writingDraft, notebookVisible, notebookFromSummary;
        private bool modalOpen, draftDirty, saveFailed;
        private string loadError, saveError, notebookError, initialWarning;
        private float draftSaveAt;
        private Action modalBack;

        public event Action Changed;
        public bool KeyboardVisible => keyboard != null && keyboard.IsVisible;
        public bool KeyboardForeground => keyboard == null || keyboard.IsForeground;
        public string SaveError => saveError ?? notebookError;
        public string BestLabel => data == null ? "你的接龙纪录   等你开启" :
            (data.ActiveMode == IdiomMode.SameCharacter ? "同字最佳   " : "同音最佳   ") + ActiveSave.Best + " 个";
        private IdiomGame Game => data == null ? null : data.ActiveMode == IdiomMode.SameCharacter ? characterGame : soundGame;
        private IdiomModeSave ActiveSave => data.ActiveMode == IdiomMode.SameCharacter ? data.Character : data.Sound;
        private bool AcceptsInput => visible && !notebookVisible && !modalOpen && loaded && Game != null && canInteract();
        private bool CanNavigate => visible && !modalOpen && canInteract();

        public void Initialize(RectTransform pageRoot, Action lobby, Func<bool> acceptsInput, Action<string> status,
            Func<string, float, Action, RectTransform> modal, Action dismiss)
        {
            root = pageRoot; goLobby = lobby; canInteract = acceptsInput; setStatus = status;
            openModal = modal; closeModal = dismiss;
            loader = new IdiomCatalogLoader();
            keyboard = gameObject.AddComponent<IdiomKeyboardViewport>();
            keyboard.Initialize(root);
            BuildLayout();
            Refresh();
        }

        public void Enter()
        {
            visible = true; gameObject.SetActive(true);
            if (!loaded && !loading) BeginLoad();
            else { Refresh(); CheckEndState(); }
            setStatus(SaveError ?? initialWarning ?? "你一言，我一语，边接边学。");
        }

        public void Leave()
        {
            Save(); Suspend(); CloseDialog(); HideKeyboard();
            if (Game != null) chatOffsets[(int)data.ActiveMode] = chat.Offset;
            if (loadingRoutine != null) StopCoroutine(loadingRoutine);
            loadingRoutine = null; loading = false; loadRequest++;
            loader.ClearDetails();
            visible = false; gameObject.SetActive(false);
        }

        // Shell also calls this before opening a modal. Do not cancel loading,
        // discard composition/drafts, or recursively close the shell's modal.
        public void Suspend()
        {
            if (chat != null) chat.FinishAppearance();
        }

        public void PauseForBackground()
        {
            if (!visible || !loaded || Game == null) return;
            // End native keyboard ownership before Android pauses the Activity.
            // Otherwise its stale focus-time text can arrive after resume and
            // replace a draft that was already correctly saved on pause.
            var draft = ActiveSave.Draft;
            writingDraft = true;
            try { HideKeyboard(); }
            finally { writingDraft = false; }
            SetDraft(draft);
        }

        public bool HandleBack()
        {
            if (!visible) return false;
            if (modalOpen) { ReturnFromDialog(); return true; }
            if (KeyboardVisible || (input != null && input.isFocused)) { HideKeyboard(); return true; }
            if (keyboard != null && keyboard.JustClosed) return true;
            if (notebookVisible) { ReturnFromNotebook(); return true; }
            return false;
        }

        public void CheckEndState()
        {
            if (AcceptsInput && Game.IsEnded && !announcedResults.Contains(Game)) ShowSummary();
        }

        private void BeginLoad()
        {
            if (loading || !visible) return;
            loadError = null; loading = true;
            var request = ++loadRequest;
            Refresh();
            loadingRoutine = StartCoroutine(loader.Load(value =>
            {
                if (request != loadRequest || !visible) return;
                try
                {
                    lexicon = value;
                    store = new IdiomSaveStore(Application.persistentDataPath, lexicon);
                    data = store.Load(out initialWarning);
                    if (data == null && store.CanWrite) data = NewSave();
                    if (data != null) RestoreGames();
                    else saveError = "成语接龙存档需要处理，原文件已保留。";
                    notebookStore = new IdiomNotebookStore(Application.persistentDataPath);
                    string notebookWarning;
                    notebook = notebookStore.Load(out notebookWarning);
                    if (notebook == null && notebookStore.CanWrite) notebook = new IdiomNotebookData();
                    if (notebook == null) notebookError = "生词本暂不可读取，原文件已保留。";
                    if (initialWarning == null) initialWarning = notebookWarning;
                    loaded = true; loading = false; loadingRoutine = null;
                    RebuildNotebookItems(); Refresh(true);
                    if (store.CanWrite && data != null) Save();
                    setStatus(SaveError ?? initialWarning ?? "本局双方不重复，第 3 次提示后回顾本轮。");
                    Changed?.Invoke(); CheckEndState();
                }
                catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
                {
                    LoadFailed("词库与存档暂时无法载入，请重试。", exception.Message);
                }
            }, message =>
            {
                if (request == loadRequest && visible) LoadFailed("本地词库加载失败，请重试。", message);
            }));
        }

        private void LoadFailed(string message, string detail)
        {
            loaded = false; loading = false; loadingRoutine = null; loadError = message;
            Debug.LogWarning("XanaduYonder idiom load failed: " + detail);
            Refresh(); setStatus(message);
        }

        private IdiomSaveData NewSave()
        {
            return new IdiomSaveData
            {
                ActiveMode = IdiomMode.SameCharacter,
                Character = NewMode(IdiomMode.SameCharacter), Sound = NewMode(IdiomMode.SameSound)
            };
        }

        private IdiomModeSave NewMode(IdiomMode mode)
        {
            return new IdiomModeSave { Game = new IdiomGame(lexicon, mode, NewSeed()).GetSnapshot(), Draft = "", Best = 0 };
        }

        private static uint NewSeed()
        {
            var value = unchecked((uint)DateTime.UtcNow.Ticks ^ (uint)Environment.TickCount);
            return value == 0 ? 1u : value;
        }

        private void RestoreGames()
        {
            characterGame = new IdiomGame(lexicon, data.Character.Game);
            soundGame = new IdiomGame(lexicon, data.Sound.Game);
        }

        public void Save()
        {
            // Other games also invoke the shell's Save after every move. Once
            // Leave saved this page, do not re-encode a long unchanged transcript.
            if (!visible && !saveFailed) return;
            if (!loaded || store == null || data == null || characterGame == null || soundGame == null) return;
            data.Character.Game = characterGame.GetSnapshot();
            data.Sound.Game = soundGame.GetSnapshot();
            UpdateBest(data.Character, characterGame.PlayerCount);
            UpdateBest(data.Sound, soundGame.PlayerCount);
            string error;
            if (!store.TrySave(data, out error))
            {
                if (!saveFailed) Debug.LogWarning("XanaduYonder idiom save failed: " + error);
                saveFailed = true;
                saveError = store.NeedsRecovery ? "当前进度未保存，旧档已保留。请在右上角菜单恢复保存。" :
                    error ?? "成语接龙当前进度未保存，请重试。";
                if (visible) setStatus(saveError);
            }
            else
            {
                var wasFailed = saveFailed;
                saveFailed = false; saveError = null;
                if (wasFailed && visible) setStatus(notebookError ?? "成语接龙进度已保存。");
            }
            draftDirty = false;
        }

        private static void UpdateBest(IdiomModeSave mode, int count)
        {
            if (count <= mode.Best) return;
            mode.Best = count; mode.NewBestInRun = true;
        }

        private void StartGame(IdiomActor actor)
        {
            if (!AcceptsInput || Game.IsStarted) return;
            var result = Game.Start(actor);
            if (!result.Success) { ShowInputError(result.Error); return; }
            Save(); Refresh(true); Changed?.Invoke(); CheckEndState();
            if (actor == IdiomActor.Player && !Game.IsEnded) input.ActivateInputField();
        }

        private void Submit()
        {
            if (!AcceptsInput || Game.IsEnded || !Game.IsStarted) return;
            if (!string.IsNullOrEmpty(Input.compositionString)) { inlineError.text = "请先确认输入法中的汉字，再发送。"; return; }
            var follow = chat.NearBottom;
            var result = Game.Submit(input.text);
            if (!result.Success) { ShowInputError(result.Error); return; }
            SetDraft("");
            inlineError.text = "";
            Save(); Refresh(follow); Changed?.Invoke();
            chat.BeginAppearance(Game.History.Count - 1);
            if (!follow) newReplies.gameObject.SetActive(true);
            CheckEndState();
        }

        private void ChangeMode(IdiomMode mode)
        {
            if (!CanNavigate || !loaded || data == null || mode == data.ActiveMode) return;
            chatOffsets[(int)data.ActiveMode] = chat.Offset;
            Save(); HideKeyboard(); Suspend();
            data.ActiveMode = mode; inlineError.text = "";
            Save(); Refresh(); chat.SetOffset(chatOffsets[(int)mode]);
            newReplies.gameObject.SetActive(false);
            Changed?.Invoke(); CheckEndState();
        }

        private void SetDraft(string value)
        {
            if (data == null) return;
            ActiveSave.Draft = value ?? "";
            writingDraft = true; input.SetTextWithoutNotify(ActiveSave.Draft); writingDraft = false;
        }

        private void DraftChanged(string value)
        {
            if (writingDraft || !loaded || Game == null || !Game.IsStarted || Game.IsEnded) return;
            // UGUI first reads the canceled native keyboard's old text, then
            // sets wasCanceled and restores its own focus-time text. Protect
            // both callbacks: dismissing an editor must not discard the draft.
            var nativeKeyboard = input.touchScreenKeyboard;
            if (input.wasCanceled || nativeKeyboard != null && nativeKeyboard.status == TouchScreenKeyboard.Status.Canceled)
            { SetDraft(ActiveSave.Draft); return; }
            ActiveSave.Draft = value;
            draftDirty = true; draftSaveAt = Time.unscaledTime + .65f;
            inlineError.text = "";
        }

        private void HideKeyboard()
        {
            if (input != null) input.DeactivateInputField();
            if (keyboard != null) keyboard.Hide();
        }

        private void ShowInputError(IdiomError error)
        {
            switch (error)
            {
                case IdiomError.InvalidWord: inlineError.text = "请输入一个四字成语。"; break;
                case IdiomError.UnknownWord: inlineError.text = "词库暂未收录这个成语，换一个试试。"; break;
                case IdiomError.RepeatedWord: inlineError.text = "这个成语本轮已经用过了，换一个试试。"; break;
                case IdiomError.WrongStart: inlineError.text = TargetLabel(); break;
                default: inlineError.text = "请先选择开局方式，或开启新的一局。"; break;
            }
        }
    }
}
