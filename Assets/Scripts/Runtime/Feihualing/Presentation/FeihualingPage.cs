using System;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using XanaduYonder.Core.Feihualing;
using XanaduYonder.Runtime.Idiom;
using Debug = UnityEngine.Debug;

namespace XanaduYonder.Runtime.Feihualing
{
    public sealed partial class FeihualingPage : MonoBehaviour
    {
        private const int GoalCount = 3;
        private const int RuleCount = 4;
        private const long OpponentDelayMilliseconds = 360;
        private const long ClockCheckpointMilliseconds = 1000;
        private const long SaveRetryMilliseconds = 2000;
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly string[] GoalNames = { "学习", "竞技", "竞速" };
        private static readonly string[] RuleNames = { "单字令", "字位令", "双字令", "主题令" };
        private static readonly string[] RuleDescriptions = { "含一个指定字", "按顺序轮换字位", "同句包含两个字", "符合审核过的主题" };

        private RectTransform root;
        private Action goLobby, openSettings, closeModal;
        private Action<string> setStatus;
        private Func<bool> acceptsInput;
        private Func<string, float, Action, RectTransform> openModal;
        private IdiomKeyboardViewport keyboard;
        private PoetryCatalog catalog;
        private FeihualingSaveStore store;
        private FeihualingProfile profile;
        private Task<PoetryCatalog> loading;
        private PoetrySession match;
        private bool visible, modalOpen, writingDraft, dirty;
        private string loadError, initialWarning, inlineMessage;
        private long saveDraftAt, saveClockAt, opponentDueAt;

        public event Action AnswerAccepted;

        public bool AutoCompanionEnabled => profile?.AutoCompanion == true;
        public bool ShowsAutoCompanionSetting => profile != null &&
            (match?.Players ?? Players) == PoetryPlayers.Solo && (match?.Goal ?? Goal) == PoetryGoal.Learning;

        public void SetAutoCompanion(bool enabled)
        {
            if (!visible || !ShowsAutoCompanionSetting || enabled == profile.AutoCompanion) return;
            profile.AutoCompanion = enabled;
            Commit();
        }

        public bool KeyboardVisible => keyboard != null && keyboard.IsVisible;
        public bool KeyboardForeground => keyboard == null || keyboard.IsForeground;
        public string SaveError { get; private set; }
        private static long Now => Clock.ElapsedMilliseconds;
        private bool CanInteract => visible && !modalOpen && acceptsInput();
        private bool CanAnswer => CanInteract && store?.NeedsRecovery == false &&
            match?.Phase == PoetryPhase.Active && !match.ComputerTurn;
        private int Slot => profile.SelectedSlot;
        private PoetryPlayers Players => (PoetryPlayers)(Slot / (RuleCount * GoalCount));
        private PoetryGoal Goal => (PoetryGoal)(Slot / RuleCount % GoalCount);
        private PoetryRule Rule => (PoetryRule)(Slot % RuleCount);
        private bool Learning => match.Goal == PoetryGoal.Learning;
        private bool Competition => match.Goal == PoetryGoal.Competition;
        private bool Speed => match.Goal == PoetryGoal.Speed;
        private bool Duo => match.Players == PoetryPlayers.Duo;
        private static string PlayerName(int player) => player == 0 ? "左方" : "右方";

        public void Initialize(RectTransform pageRoot, Action lobby, Action settings, Func<bool> canInteract,
            Action<string> status, Func<string, float, Action, RectTransform> modal, Action dismiss)
        {
            root = pageRoot;
            goLobby = lobby;
            openSettings = settings;
            acceptsInput = canInteract;
            setStatus = status;
            openModal = modal;
            closeModal = dismiss;
            keyboard = gameObject.AddComponent<IdiomKeyboardViewport>();
            keyboard.Initialize(root);
            BuildLayout();
        }

        public void Enter()
        {
            visible = true;
            gameObject.SetActive(true);
            if (catalog == null && loading == null) BeginLoad();
            else Render();
        }

        public void Leave()
        {
            if (!visible) return;
            PauseForBackground();
            match = null;
            opponentDueAt = 0;
            inlineMessage = null;
            notebookToast.gameObject.SetActive(false);
            visible = false;
            gameObject.SetActive(false);
        }

        public void PauseForBackground()
        {
            if (!visible) return;
            opponentDueAt = 0;
            CloseDialog();
            HideKeyboard();
            PauseMatch();
            Save();
        }

        public void Save()
        {
            if (!visible || profile == null) return;
            var now = Now;
            AdvanceClock(now);
            SaveAt(now);
        }

        public bool HandleBack()
        {
            if (!visible) return false;
            if (modalOpen) { DismissDialog(); return true; }
            if (KeyboardVisible || input.isFocused) { HideKeyboard(); Save(); return true; }
            if (keyboard.JustClosed) return true;
            BackFromPage();
            return true;
        }

        public void CheckEndState()
        {
            if (!visible || match == null) return;
            var now = Now;
            if (AdvanceClock(now)) SaveAt(now);
            DrawClock(now);
        }

        private void Update()
        {
            if (!visible) return;
            if (loading != null && loading.IsCompleted) FinishLoad();
            CheckEndState();
            var now = Now;
            var clockDue = match?.Phase == PoetryPhase.Active && Speed && now >= saveClockAt;
            if (store != null && store.CanWrite && (clockDue || (dirty && now >= saveDraftAt)))
            {
                dirty = true;
                Save();
            }
            AnswerForOpponent(now);
            if (notebookToast.gameObject.activeSelf && now >= notebookToastUntil)
                notebookToast.gameObject.SetActive(false);
            RefreshSend();
        }

        private void AnswerForOpponent(long now)
        {
            if (match?.ComputerTurn != true || !CanInteract || store?.NeedsRecovery != false)
            {
                opponentDueAt = 0;
                return;
            }
            if (opponentDueAt == 0) opponentDueAt = now + OpponentDelayMilliseconds;
            if (now < opponentDueAt) return;
            opponentDueAt = 0;
            match.Computer(now);
            Commit(true);
        }

        private bool AdvanceClock(long now)
        {
            if (match == null || !match.Tick(now)) return false;
            CloseDialog();
            HideKeyboard();
            dirty = true;
            Render(true);
            return true;
        }

        private void BeginLoad()
        {
            loadError = null;
            var asset = Resources.Load<TextAsset>(FeihualingCatalogReader.ResourcePath);
            if (asset == null) loadError = "随包诗库暂不可用。";
            else
            {
                var compressed = asset.bytes;
                Resources.UnloadAsset(asset);
                loading = Task.Run(() => FeihualingCatalogReader.Read(compressed));
            }
            Render();
        }

        private void FinishLoad()
        {
            try
            {
                var loadedCatalog = loading.GetAwaiter().GetResult();
                var loadedStore = new FeihualingSaveStore(Application.persistentDataPath, loadedCatalog);
                var loadedProfile = loadedStore.LoadProfile(out initialWarning);
                store = loadedStore;
                catalog = loadedCatalog;
                profile = loadedProfile ?? (store.CanWrite ? new FeihualingProfile() : null);
            }
            catch (Exception exception)
            {
                loadError = "本地诗库或进度暂时无法读取，可以重试。";
                Debug.LogWarning("Feihualing load failed: " + exception);
            }
            finally { loading = null; }
            Render();
        }

        private void SaveAt(long now)
        {
            if (!dirty || store == null || !store.CanWrite) return;
            if (match != null)
            {
                var snapshot = match.Capture(now);
                profile.Matches[Slot] = snapshot;
                if (snapshot.AnswerLineLimit == 0 && !Duo && Speed && match.Outcome == PoetryOutcome.Completed)
                    profile.RecordBest(match.Card.Id, Math.Max(1, match.History[match.History.Count - 1].ElapsedMilliseconds));
            }
            if (store.TrySave(profile, out var error))
            {
                dirty = false;
                saveClockAt = now + ClockCheckpointMilliseconds;
            }
            else saveDraftAt = saveClockAt = now + SaveRetryMilliseconds;
            SaveError = error;
            if (store.NeedsRecovery)
            {
                CloseDialog();
                HideKeyboard();
                if (match?.Phase == PoetryPhase.Active && Speed) match.Pause(now);
                Render();
            }
            else UpdateNotice();
        }

        private void Commit(bool followHistory = false)
        {
            dirty = true;
            Save();
            Render(followHistory);
        }

        private PoetryCard SelectedCard()
        {
            var selected = catalog.FindCard(profile.SelectedCards[Slot]);
            if (selected != null && selected.Rule == Rule) return selected;
            PoetryCard first = null;
            foreach (var card in catalog.Cards)
            {
                if (card.Rule != Rule) continue;
                if (card.Rule == PoetryRule.Character && card.Key == "月") return card;
                if (first == null) first = card;
            }
            return first;
        }

        private void Select(PoetryPlayers players, PoetryGoal goal, PoetryRule rule)
        {
            if (!CanInteract || profile == null || match != null) return;
            var slot = FeihualingProfile.SlotFor(players, goal, rule);
            if (slot == Slot) return;
            profile.SelectedSlot = slot;
            Commit();
        }

        private void ChangeTopic(PoetryCard current = null)
        {
            if (!CanInteract || profile == null || match != null) return;
            if (current == null) current = SelectedCard();
            PoetryCard first = null;
            var chooseNext = false;
            foreach (var card in catalog.Cards)
            {
                if (card.Rule != Rule) continue;
                if (first == null) first = card;
                if (chooseNext) { first = card; break; }
                chooseNext = card.Id == current?.Id;
            }
            if (first == null || first.Id == current?.Id) return;
            SelectTopic(first);
        }

        private void SelectTopic(PoetryCard card)
        {
            if (!CanInteract || match != null || card.Rule != Rule || card.Id == SelectedCard()?.Id) return;
            // A saved match still owns its draft even while a different topic is selected.
            if (profile.Matches[Slot] == null) profile.Drafts[Slot] = "";
            profile.SelectedCards[Slot] = card.Id;
            Commit();
        }

        private void StartNew()
        {
            if (!CanInteract || profile == null) return;
            var card = SelectedCard();
            if (card == null) return;
            var previous = profile.Matches[Slot];
            if (match?.Phase != PoetryPhase.Finished && previous != null && previous.Phase != PoetryPhase.Finished)
            {
                Confirm("开始新局？", "这组玩法还有进度。开始新局后，会替换该组的旧进度。", "开始新局",
                    () => StartWithCard(card), destructive: true);
            }
            else StartWithCard(card);
        }

        private void StartWithCard(PoetryCard card)
        {
            var draft = profile.Matches[Slot] == null && card.Id == SelectedCard()?.Id ? profile.Drafts[Slot] : "";
            profile.SelectedCards[Slot] = card.Id;
            match = PoetrySession.Start(catalog, card, Players, Goal, Now);
            opponentDueAt = 0;
            SetDraft(draft);
            inlineMessage = null;
            Commit(true);
        }

        private void ContinueSaved()
        {
            if (!CanInteract || !PoetrySession.TryRestore(catalog, profile.Matches[Slot], Now, out var restored))
                return;
            match = restored;
            opponentDueAt = 0;
            SetDraft(match.Phase == PoetryPhase.Finished ? "" : profile.Drafts[Slot]);
            inlineMessage = null;
            Render(true);
        }

        private void ReturnToSetup()
        {
            HideKeyboard();
            PauseMatch();
            Save();
            match = null;
            opponentDueAt = 0;
            inlineMessage = null;
            Render();
        }

        private void BackFromPage()
        {
            if (!CanInteract) return;
            if (match == null) goLobby();
            else ReturnToSetup();
        }

        private void OpenSettings()
        {
            if (!CanInteract) return;
            SuspendForMenu();
            openSettings();
        }

        private void SuspendForMenu()
        {
            opponentDueAt = 0;
            HideKeyboard();
            PauseMatch();
            Save();
        }

        private void PauseMatch()
        {
            if (match?.Phase != PoetryPhase.Active || !Speed) return;
            var phase = match.Phase;
            var paused = match.Pause(Now);
            if (paused || match.Phase != phase) Commit();
        }

        private void Transition(Func<long, bool> perform, bool clearDraft = false)
        {
            if (!CanInteract || match == null) return;
            var now = Now;
            if (AdvanceClock(now)) { SaveAt(now); return; }
            if (!perform(now)) return;
            HideKeyboard();
            if (clearDraft) SetDraft("");
            inlineMessage = null;
            Commit(true);
        }

        private void Submit()
        {
            if (!CanAnswer) return;
            if (!string.IsNullOrEmpty(Input.compositionString))
            {
                ShowInline("请先确认输入法中的汉字，再发送。");
                return;
            }
            var answer = match.Submit(input.text, Now);
            if (answer.Status == PoetryAnswerStatus.Accepted)
            {
                inlineMessage = null;
                HideKeyboard();
                SetDraft("");
                if (Learning && !Duo && AutoCompanionEnabled && match.Phase == PoetryPhase.Active)
                {
                    var reply = match.Companion(Now);
                    if (reply.Status == PoetryAnswerStatus.Unavailable && match.Phase == PoetryPhase.Active)
                        inlineMessage = "暂时没有陪练句，可以继续作答。";
                }
                Commit(true);
                AnswerAccepted?.Invoke();
                return;
            }
            if (answer.Status == PoetryAnswerStatus.TimedOut || match.Phase != PoetryPhase.Active)
            {
                HideKeyboard();
                Commit(true);
                return;
            }
            if (answer.Status == PoetryAnswerStatus.Duplicate) ShowPreviousAnswer(answer.LineId);
            ShowInline(AnswerMessage(answer.Status));
        }

        private void ShowPreviousAnswer(int lineId)
        {
            var group = catalog.Lines[lineId].Group;
            for (var index = match.History.Count - 1; index >= 0; index--)
            {
                var entry = match.History[index];
                if (entry.Round != match.Round) break;
                if (catalog.Lines[entry.LineId].Group != group) continue;
                history.SetOffset(index * HistoryRowHeight);
                break;
            }
        }

        private string AnswerMessage(PoetryAnswerStatus status)
        {
            switch (status)
            {
                case PoetryAnswerStatus.Empty: return "请输入一句完整诗词。";
                case PoetryAnswerStatus.MultipleLines: return "一次输入一句，可删去后一分句。";
                case PoetryAnswerStatus.WrongCharacter: return "这句里需要有「" + match.Card.Key + "」。";
                case PoetryAnswerStatus.WrongPosition: return Condition(match.Card, match.Position) + "。";
                case PoetryAnswerStatus.WrongPair: return "同一句里需要有「" + match.Card.Key + "」和「" + match.Card.SecondKey + "」。";
                case PoetryAnswerStatus.WrongTheme: return "这句未收录为「" + match.Card.Title + "」题的答案。";
                case PoetryAnswerStatus.Duplicate: return "这句本轮已答过。";
                case PoetryAnswerStatus.Unknown: return "本地诗库未收录，无法确认。";
                case PoetryAnswerStatus.Unavailable: return "这句未收录为本题的答案。";
                default: return "当前回合暂时无法发送。";
            }
        }

        private void DraftChanged(string value)
        {
            if (writingDraft || profile == null || match?.Phase != PoetryPhase.Active || match.ComputerTurn) return;
            var nativeKeyboard = input.touchScreenKeyboard;
            if (input.wasCanceled || nativeKeyboard != null && nativeKeyboard.status == TouchScreenKeyboard.Status.Canceled)
            {
                SetDraft(profile.Drafts[Slot]);
                return;
            }
            profile.Drafts[Slot] = value;
            saveDraftAt = Now + 500;
            dirty = true;
            ShowInline(null);
        }

        private void SetDraft(string value)
        {
            writingDraft = true;
            try { input.SetTextWithoutNotify(value ?? ""); }
            finally { writingDraft = false; }
            profile.Drafts[Slot] = input.text;
        }

        private void HideKeyboard()
        {
            if (!KeyboardVisible && !input.isFocused) return;
            writingDraft = true;
            try { input.DeactivateInputField(); keyboard.Hide(); }
            finally { writingDraft = false; }
            if (profile != null && match != null) SetDraft(profile.Drafts[Slot]);
        }
    }
}
