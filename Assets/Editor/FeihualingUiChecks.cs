using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using XanaduYonder.Runtime.Idiom;
using XanaduYonder.Core.Feihualing;
using XanaduYonder.Runtime.Feihualing;
using Object = UnityEngine.Object;
using XanaduYonder.Runtime;

namespace XanaduYonder.EditorTools
{
    public static class FeihualingUiChecks
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        public static void Run()
        {
            try { Verify(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void Verify()
        {
            var assembly = typeof(UiFactory).Assembly;
            var pageType = assembly.GetType("XanaduYonder.Runtime.Feihualing.FeihualingPage");
            Require(pageType != null, "The production Feihualing page is registered");
            var previousFont = UiFactory.Font;
            UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            try
            {
                var asset = Resources.Load<TextAsset>(FeihualingCatalogReader.ResourcePath);
                Require(asset != null, "The packaged poetry library is available to the page");
                var catalog = FeihualingCatalogReader.Read(asset.bytes);
                foreach (var size in new[] { new Vector2(720, 880), new Vector2(720, 1320), new Vector2(900, 1582) })
                    CheckPage(pageType, catalog, size);
            }
            finally { UiFactory.Font = previousFont; }
            Debug.Log("FEIHUALING_UI_CHECKS_PASS: responsive layout, shared clues, back navigation, Speed saves, and full-screen poem collection and stars");
        }

        private static void CheckPage(Type pageType, PoetryCatalog catalog, Vector2 size)
        {
            var rootObject = new GameObject("Feihualing UI fixture", typeof(RectTransform), typeof(Canvas));
            var root = (RectTransform)rootObject.transform;
            var workingDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "feihualing-ui-" + Guid.NewGuid());
            System.IO.Directory.CreateDirectory(workingDirectory);
            rootObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.sizeDelta = size;
            RectTransform modal = null;
            try
            {
                var page = (FeihualingPage)rootObject.AddComponent(pageType);
                var lobbyVisits = 0;
                Func<string, float, Action, RectTransform> open = (title, height, back) =>
                    modal = UiFactory.Rect(root, title, 0, 0, 596, height);
                Action close = () =>
                {
                    if (modal != null) { modal.gameObject.SetActive(false); Object.DestroyImmediate(modal.gameObject); }
                    modal = null;
                };
                var settingsRequests = 0;
                page.Initialize(root, () => lobbyVisits++, () => settingsRequests++, () => modal == null, _ => { }, open, close);
                var answers = 0;
                page.AnswerAccepted += () => answers++;
                var store = new FeihualingSaveStore(workingDirectory, catalog);
                var profile = store.Load(out _) ?? new FeihualingProfile();
                var notebookStore = new FeihualingNotebookStore(workingDirectory);
                Require(notebookStore.LoadNotebook(out _), "Temporary notebook opens");
                Set(page, "notebookStore", notebookStore);
                Set(page, "catalog", catalog);
                Set(page, "store", store);
                Set(page, "profile", profile);
                page.Enter();
                pageType.GetMethod("LateUpdate", Private).Invoke(page, Array.Empty<object>());
                Canvas.ForceUpdateCanvases();

                var header = root.GetComponentsInChildren<RectTransform>()
                    .Single(rect => rect.name == "Feihualing header");
                var title = Find<Text>(header, "Feihualing centered title");
                Require(title != null && title.text == "飞花令" &&
                    Mathf.Abs(header.InverseTransformPoint(title.rectTransform.TransformPoint(title.rectTransform.rect.center)).x -
                        header.rect.center.x) < 1, "The plain title is centered in the game header");
                Require(title.transform.childCount == 0 && Find<Graphic>(header, "Five-petal blossom") == null,
                    "There is no decorative game logo beside the title");
                Require(Find<Button>(header, "Home button") != null && Find<Button>(header, "Back button") == null,
                    "Setup keeps the lobby Home action");
                var settingsButton = Find<Button>(header, "Settings button");
                Require(settingsButton != null && settingsButton.IsInteractable(), "Settings remains beside Help");
                settingsButton.onClick.Invoke();
                Require(settingsRequests == 1, "The shared settings callback still opens");
                Find<Button>(header, "Notebook button").onClick.Invoke();
                Require(modal != null && modal.name == "诗词收藏" && modal.GetComponentsInChildren<Text>().Any(t => t.text.Contains("还没有")),
                    "The notebook opens from setup and explains the empty state");
                Click(modal, "关闭");
                CheckRules(root, () => modal);
                Require(page.HandleBack() && modal == null, "Back closes setup rules");
                Require(root.GetComponentsInChildren<Text>().Any(text => text.text == "月"), "The default opening card is 月");
                Require(page.ShowsAutoCompanionSetting && !page.AutoCompanionEnabled, "Solo Learning preference defaults to off");
                Click(root, "选题");
                Require(modal != null && modal.GetComponentInChildren<ScrollRect>() != null,
                    "Topics can be selected directly in a scrollable list");
                Click(modal, "月");
                profile.Drafts[profile.SelectedSlot] = "旧草稿";
                Click(root, "换一题");
                Require(string.IsNullOrEmpty(profile.Drafts[profile.SelectedSlot]),
                    "Switching an empty slot to a different card clears its legacy draft");
                Click(root, "选题");
                Click(modal, "月");
                profile.Drafts[profile.SelectedSlot] = "床前";
                var setup = Find<ScrollRect>(root, "Feihualing setup");
                Require(setup.viewport.rect.height > 500 && setup.viewport.rect.height < size.y, "Setup can scroll inside the frame");
                Click(root, "同机双人");
                Require(!page.ShowsAutoCompanionSetting, "Duo mode never offers an automatic companion");
                Click(root, "开始学习");
                Require(Find<Button>(header, "Back button") != null && Find<Button>(header, "Home button") == null,
                    "A match handoff shows Back to setup");
                CheckRules(root, () => modal);
                Click(modal, "知道了");
                Require(modal == null && Find<InputField>(root, "Poetry input") == null,
                    "Closing rules during handoff still needs the next player to confirm");
                Click(root, "准备好了");
                Click(root, "提示");
                Require(modal != null, "Learning prompts mount one modal");
                var hintModal = modal;
                var session = (PoetrySession)pageType.GetField("match", Private).GetValue(page);
                var targets = session.Hints();
                Require(targets.Count >= 2, "Catalog offers several clue targets");
                RequireHiddenTargets(modal, catalog, targets);
                var expand = Buttons(modal, "展开原诗");
                Require(expand.Length >= 2, "Hints list multiple separately expandable choices");
                expand[0].onClick.Invoke();
                Require(modal == hintModal && Buttons(modal, "收起原诗").Length == 1, "First original opens in the same hint modal");
                RequireHiddenTargets(modal, catalog, targets);
                Buttons(modal, "展开原诗")[0].onClick.Invoke();
                Require(modal == hintModal && Buttons(modal, "收起原诗").Length == 1, "Second original replaces the first expansion");
                RequireHiddenTargets(modal, catalog, targets);
                Require(Buttons(modal, "填入输入框").Length == 0, "Hints cannot fill in an answer");
                Click(modal, "关闭");
                var input = root.GetComponentInChildren<InputField>();
                Require(input != null && input.text.Length == 0, "Closing leaves the draft unchanged");
                var draft = session.Suggestion(targets[0]);
                input.text = draft;
                input.onEndEdit.Invoke(draft);
                Require(input.text == draft && Find<InputField>(root, "Poetry input") != null, "IME end-edit leaves the draft for Send");
                Click(root, "发送");
                Require(root.GetComponentInChildren<InputField>() == null, "Handoff hides the previous player's composer");
                Click(root, "准备好了");
                Require(root.GetComponentsInChildren<Text>().Any(label => label.text.Contains("左方 1") && label.text.Contains("辅助 1")),
                    "An answer aided by a clue counts as assisted for its player");
                CheckSpeed(root, page, profile, () => modal);
                CheckSoloGoals(root, page, catalog, notebookStore, () => modal);
                CheckDuoCompetition(root, page, () => modal);
                Require(answers == 6, "Shared answer feedback fires for accepted player submissions only");
                Require(lobbyVisits == 0 && page.HandleBack() && lobbyVisits == 1, "Only Back from setup opens the lobby");
                page.Leave();
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                System.IO.Directory.Delete(workingDirectory, true);
            }
        }

        private static void CheckSpeed(Transform root, FeihualingPage page, FeihualingProfile profile, Func<RectTransform> modal)
        {
            Require(page.HandleBack(), "The learning page handles Back");
            Click(root, "竞速");
            Click(root, "开始竞速");
            var session = (PoetrySession)page.GetType().GetField("match", Private).GetValue(page);
            Require(session.Phase == PoetryPhase.Handoff && session.CurrentPlayer == 0, "Competition begins with left-player handoff");
            Require(root.GetComponentInChildren<InputField>() == null, "Competition topic and composer stay hidden until ready");
            Click(root, "准备好了");
            Require(session.Phase == PoetryPhase.Active && Find<Button>(root, "Poetry hint action") == null, "Speed has no hints");
            var checkpoint = profile.Matches[profile.SelectedSlot];
            Set(page, "saveClockAt", 0L);
            page.GetType().GetMethod("Update", Private).Invoke(page, Array.Empty<object>());
            Require(!ReferenceEquals(checkpoint, profile.Matches[profile.SelectedSlot]),
                "An active clock checkpoints without any new answer or draft");
            CheckRules(root, modal);
            Require(session.Phase == PoetryPhase.Paused && profile.Matches[profile.SelectedSlot].Phase == PoetryPhase.Paused,
                "Opening rules pauses the Speed clock and persists the paused phase");
            Click(modal(), "知道了");
            Require(session.Phase == PoetryPhase.Paused, "Closing rules does not restart the Speed clock");
            Click(root, "继续");
            var slot = profile.SelectedSlot;
            var now = (long)page.GetType().GetProperty("Now", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var remaining = session.Remaining(now + 1000);
            Require(page.HandleBack() && Find<Button>(root, "Home button") != null, "System Back goes directly to Speed setup");
            Require(profile.SelectedSlot == slot && profile.Matches[slot].Phase == PoetryPhase.Paused &&
                profile.Matches[slot].RemainingMilliseconds <= remaining, "Returning to setup immediately saves the spent Speed time");
            Click(root, "继续上次");
            session = (PoetrySession)page.GetType().GetField("match", Private).GetValue(page);
            Require(session.Phase == PoetryPhase.Paused, "Continuing a saved Speed match starts paused");
            Click(root, "继续");
            var input = Find<InputField>(root, "Poetry input");
            input.text = "床前明月光，疑是地上霜。";
            Click(root, "发送");
            Require(session.History.Count == 0 && input.text.Contains("疑是地上霜"), "A multiple-line answer keeps the draft");
            Require(root.GetComponentsInChildren<Text>().Any(label => label.text.Contains("一次输入一句")),
                "The multiple-line message explains the accepted answer unit");
            input.text = "床前明月光";
            Click(root, "发送");
            Require(session.Phase == PoetryPhase.Handoff && session.CurrentPlayer == 1, "Send passes the device to the other player");
            Click(root, "准备好了");
            var history = HistoryButton(root, "床前明月光");
            Require(history != null && history.IsInteractable(), "Speed history opens a full original during play");
            Find<InputField>(root, "Poetry input").text = "明月";
            TapComputerLine(history);
            RequireFullPoem(root);
            Require(session.Phase == PoetryPhase.Paused && page.HandleBack(), "Opening an original pauses Speed and Back returns");
            Click(root, "继续");
            Require(Find<InputField>(root, "Poetry input").text == "明月", "Returning from the original preserves the Speed draft");
            Click(root, "暂停");
            Require(session.Phase == PoetryPhase.Paused && root.GetComponentInChildren<InputField>() == null,
                "Pause hides the composer and stops the competition clock");
            checkpoint = profile.Matches[profile.SelectedSlot];
            Set(page, "saveClockAt", 0L);
            page.GetType().GetMethod("Update", Private).Invoke(page, Array.Empty<object>());
            Require(ReferenceEquals(checkpoint, profile.Matches[profile.SelectedSlot]), "A paused clock has no periodic writes");
            Click(root, "继续");
            Click(root, "接不上");
            Require(modal() != null && session.Phase == PoetryPhase.Active, "The Pass confirmation leaves the clock running");
            Click(modal(), "确认接不上");
            Require(session.Phase == PoetryPhase.RoundOver && session.Wins(0) == 1, "Conceding credits one round to the other player");
            Click(root, "下一小局");
            Require(session.Phase == PoetryPhase.Handoff && session.Round == 2 && session.CurrentPlayer == 1,
                "Between-round navigation swaps the first player");
        }

        private static void CheckSoloGoals(Transform root, FeihualingPage page, PoetryCatalog catalog,
            FeihualingNotebookStore notebookStore, Func<RectTransform> modal)
        {
            var pageType = page.GetType();
            Require(page.HandleBack(), "Duo Speed handoff handles Back");
            Click(root, "单机");
            Click(root, "学习");
            Click(root, "开始学习");
            var session = (PoetrySession)pageType.GetField("match", Private).GetValue(page);
            var field = Find<InputField>(root, "Poetry input");
            Require(field.text == "床前", "Starting the same selected card in an empty slot restores the migrated draft");
            Require(Buttons(root, "陪练接一句").Length == 0, "Companion is a settings preference");
            field.text = "床前明月光";
            Click(root, "发送");
            Require(session.History.Count == 1 && !session.History[0].Companion, "Default-off companion stays silent");
            page.SetAutoCompanion(true);
            Require(session.History.Count == 1, "Turning companion on does not reply to earlier turns");
            field.text = "海上生明月";
            Click(root, "发送");
            Require(session.History.Count == 3 && session.History[2].Companion, "Enabled companion follows one valid human answer");
            Require(page.HandleBack(), "Solo Learning handles Back");
            Click(root, "竞技");
            Require(!page.ShowsAutoCompanionSetting, "Solo Competition uses the independent opponent");
            Click(root, "开始竞技");
            session = (PoetrySession)pageType.GetField("match", Private).GetValue(page);
            Require(session.Remaining(0) == -1 && Find<Text>(root, "Poetry countdown") == null,
                "Solo Competition has no clock");
            Click(root, "提示 3/3");
            Require(session.CompetitionHintsRemaining == 2 && session.Hints().Count == 1, "Competition spends one shared target to open");
            RequireHiddenTargets(modal(), catalog, session.Hints());
            Click(modal(), "展开原诗");
            RequireHiddenTargets(modal(), catalog, session.Hints());
            var closeIcon = Find<Button>(modal(), "Close button");
            Require(closeIcon != null, "The modal close icon is present");
            closeIcon.onClick.Invoke();
            Click(root, "提示 2/3");
            Require(session.CompetitionHintsRemaining == 2 && Buttons(modal(), "收起原诗").Length == 1,
                "Reopening the same turn preserves the clue and its expanded position");
            Click(modal(), "换一条");
            Click(modal(), "换一条");
            Require(session.CompetitionHintsRemaining == 0 && !Buttons(modal(), "换一条").Single().IsInteractable(),
                "Competition stops refreshing at the three-target limit");
            Click(modal(), "关闭");
            Click(root, "提示 0/3");
            Require(session.Hints().Count == 1 && session.CompetitionHintsRemaining == 0, "The last paid clue remains readable at zero");
            var hintedAnswer = session.Suggestion(session.Hints()[0]);
            Require(page.HandleBack() && modal() == null && Find<Button>(root, "Back button") != null, "System Back dismisses the clue first");
            field = Find<InputField>(root, "Poetry input");
            field.text = hintedAnswer;
            Click(root, "发送");
            Require(session.History.Count == 1 && Find<InputField>(root, "Poetry input") == null &&
                Find<Button>(root, "Poetry hint action") == null, "The offline opponent's turn has no clue action");
            Require(!session.History[0].Assisted, "Competition clues do not change the assisted tally");
            Set(page, "opponentDueAt", 1L);
            pageType.GetMethod("Update", Private).Invoke(page, Array.Empty<object>());
            Require(session.History.Count == 2 && session.History[1].Player == 1 &&
                !session.History[1].Companion && Find<InputField>(root, "Poetry input") != null,
                "Offline opponent responds once and returns the turn");
            Require(!Find<Button>(root, "Poetry hint action").IsInteractable(), "A later turn cannot reopen a spent hint");
            CheckNotebookGestures(root, page, catalog, session, notebookStore, modal);
            var own = HistoryButton(root, hintedAnswer);
            Require(own != null && own.IsInteractable(), "A human Competition answer also opens its full poem");
            Find<InputField>(root, "Poetry input").text = "暂存草稿";
            TapComputerLine(own);
            var ownPoem = RequireFullPoem(root);
            var ownWork = catalog.Works[catalog.Lines[session.History[0].LineId].WorkIndex];
            if (!notebookStore.Contains(ownWork.Id)) Click(ownPoem, "收藏整首");
            Require(Buttons(ownPoem, "已收藏").Single().interactable == false && notebookStore.Contains(ownWork.Id),
                "The full-screen original saves the entire work directly");
            Require(page.HandleBack() && Find<InputField>(root, "Poetry input").text == "暂存草稿",
                "Back from a full-screen Competition original preserves the draft");
            RequireSavedStar(HistoryButton(root, hintedAnswer), hintedAnswer);
            Click(root, "接不上");
            Click(modal(), "确认接不上");
            Require(session.Phase == PoetryPhase.Finished && session.Winner == 1, "Conceding finishes Solo Competition");
            CheckRules(root, modal);
            Click(modal(), "知道了");
            Require(session.Phase == PoetryPhase.Finished && session.Winner == 1, "Rules close back to the same result");
            Require(Find<Button>(root, "Poetry hint action") == null, "The result has no clue action");
            var reviewed = Find<Button>(root, "Poetry computer line");
            Require(reviewed != null && reviewed.IsInteractable(), "Review retains the computer poem action");
            TapComputerLine(reviewed);
            RequireFullPoem(root);
            Require(page.HandleBack(), "Back returns from the full-screen original to review");
            Find<Button>(root, "Back button").onClick.Invoke();
            Require(Find<Button>(root, "Home button") != null && Buttons(root, "查看回顾").Length == 1,
                "The header arrow keeps the finished match available in setup");
        }

        private static void CheckNotebookGestures(Transform root, FeihualingPage page, PoetryCatalog catalog,
            PoetrySession session, FeihualingNotebookStore store, Func<RectTransform> modal)
        {
            var computerRow = Find<Button>(root, "Poetry computer line");
            Require(computerRow != null && computerRow.IsInteractable(), "The computer reply opens its original during Competition");
            var work = catalog.Works[catalog.Lines[session.History[1].LineId].WorkIndex];
            TapComputerLine(computerRow);
            var full = RequireFullPoem(root);
            Require(full.GetComponentsInChildren<Text>().Any(text => text.text == "《" + work.Title + "》"),
                "Tapped original includes the computer poem's title");
            Require(store.Entries.Count == 0 && Buttons(full, "收藏整首").Length == 1,
                "A tap is read-only and exposes the collection action");
            Require(page.HandleBack(), "System Back closes the full poem");

            var hold = computerRow.GetComponent<FeihualingComputerLinePress>();
            var drag = PressComputerLine(computerRow);
            drag.dragging = true;
            hold.Advance(Time.unscaledTimeAsDouble + 1);
            ReleaseComputerLine(computerRow, drag);
            Require(store.Entries.Count == 0 && Find<RectTransform>(root, "Feihualing full poem") == null,
                "A drag neither saves nor opens a poem");

            var shifted = PressComputerLine(computerRow);
            var position = computerRow.transform.position;
            computerRow.transform.position = position + Vector3.up;
            hold.Advance(Time.unscaledTimeAsDouble + 1);
            computerRow.transform.position = position;
            ReleaseComputerLine(computerRow, shifted);
            Require(store.Entries.Count == 0 && Find<RectTransform>(root, "Feihualing full poem") == null,
                "A recycled viewport moving the row cancels the captured hold");

            var canceledByKeyboard = PressComputerLine(computerRow);
            var keyboard = page.GetComponent<IdiomKeyboardViewport>();
            Set(keyboard, "<IsVisible>k__BackingField", true);
            hold.Advance(Time.unscaledTimeAsDouble + 1);
            Set(keyboard, "<IsVisible>k__BackingField", false);
            ReleaseComputerLine(computerRow, canceledByKeyboard);
            Require(store.Entries.Count == 0 && Find<RectTransform>(root, "Feihualing full poem") == null,
                "Opening the keyboard cancels a pending hold");

            var rebound = PressComputerLine(computerRow);
            page.GetType().GetMethod("Render", Private).Invoke(page, new object[] { false });
            hold.Advance(Time.unscaledTimeAsDouble + 1);
            ReleaseComputerLine(computerRow, rebound);
            Require(store.Entries.Count == 0 && Find<RectTransform>(root, "Feihualing full poem") == null,
                "Rebinding a history row cancels its pending hold");

            var longPress = PressComputerLine(computerRow);
            hold.Advance(Time.unscaledTimeAsDouble + 1);
            hold.Advance(Time.unscaledTimeAsDouble + 2);
            ReleaseComputerLine(computerRow, longPress);
            Require(Find<RectTransform>(root, "Feihualing full poem") == null &&
                store.Entries.Count == 1 && store.Entries[0].Body == work.Body,
                "A long press saves the whole poem once without also opening it");
            var feedback = Find<Text>(root, "Poetry notebook feedback");
            Require(feedback != null && feedback.text == "已收藏这首诗词。", "A held contact gives one visible saved confirmation");
            longPress = PressComputerLine(computerRow);
            hold.Advance(Time.unscaledTimeAsDouble + 1);
            ReleaseComputerLine(computerRow, longPress);
            Require(store.Entries.Count == 1 && feedback.text == "这首诗词已收藏。",
                "Saving the same poem twice keeps one entry and acknowledges the duplicate");
            longPress = PressComputerLine(computerRow);
            Set(hold, "heldSince", Time.unscaledTimeAsDouble - .58);
            ReleaseComputerLine(computerRow, longPress);
            Require(Find<RectTransform>(root, "Feihualing full poem") == null &&
                !longPress.eligibleForClick && store.Entries.Count == 1,
                "Release across the hold threshold saves instead of also becoming a tap");

            RequireSavedStar(computerRow, session.History[1].Input);
            Find<Button>(root, "Notebook button").onClick.Invoke();
            var list = modal();
            Require(list != null && list.name == "诗词收藏", "The game header opens the notebook");
            TapComputerLine(Buttons(list, "《" + work.Title + "》").Single());
            Require(Buttons(RequireFullPoem(root), "已收藏").Length == 1, "Tapping a saved entry opens the full original");
            Require(page.HandleBack() && modal() != null && modal().name == "诗词收藏",
                "Back returns from a saved poem to the same notebook");
            Click(modal(), "关闭");
        }

        private static RectTransform RequireFullPoem(Transform root)
        {
            var full = Find<RectTransform>(root, "Feihualing full poem");
            Require(full != null && (Vector2.Distance(full.rect.size, ((RectTransform)root).rect.size) < .5f),
                "Poem detail covers the entire safe-area frame");
            Require(Find<ScrollRect>(full, "Scrollable poem") != null, "The full original scrolls independently");
            return full;
        }

        private static Button HistoryButton(Transform root, string answer) =>
            root.GetComponentsInChildren<Button>().SingleOrDefault(button => button.GetComponentsInChildren<Text>()
                .Any(label => label.name == "Poetry history answer" && label.text.StartsWith(answer, StringComparison.Ordinal)));

        private static void RequireSavedStar(Button button, string answer)
        {
            var label = Find<Text>(button.transform, "Poetry history answer");
            var star = Find<Text>(button.transform, "Poetry saved star");
            Require(label.text == answer && star != null && star.text == "★" &&
                ((Color32)star.color).Equals((Color32)UiFactory.Hex("D59C19")), "A saved answer draws a yellow five-point star");
            var answerPoints = HistoryTextVertices(label, button.transform);
            var starPoints = HistoryTextVertices(star, button.transform);
            var gap = starPoints.Min(point => point.x) - answerPoints.Max(point => point.x);
            Require(Mathf.Abs(gap - label.fontSize * .5f) <= 1, $"The horizontal star gap is half a character, got {gap:0.00}");
            var centerDelta = ((starPoints.Min(point => point.y) + starPoints.Max(point => point.y)) -
                (answerPoints.Min(point => point.y) + answerPoints.Max(point => point.y))) * .5f;
            Require(Mathf.Abs(centerDelta) <= 1, $"The star is vertically centered on the answer ink, got {centerDelta:0.00}");
        }

        private static Vector3[] HistoryTextVertices(Text text, Transform parent)
        {
            using var generator = new TextGenerator();
            Require(generator.Populate(text.text, text.GetGenerationSettings(text.rectTransform.rect.size)),
                "UGUI generates the saved answer or star glyphs");
            return generator.verts.Where(vertex => vertex.position != Vector3.zero).Select(vertex =>
                parent.InverseTransformPoint(text.transform.TransformPoint(vertex.position / text.pixelsPerUnit))).ToArray();
        }

        private static PointerEventData PressComputerLine(Button button)
        {
            var press = new PointerEventData(EventSystem.current)
            {
                pointerId = -1, button = PointerEventData.InputButton.Left, eligibleForClick = true,
                position = new Vector2(100, 100)
            };
            ExecuteEvents.Execute(button.gameObject, press, ExecuteEvents.pointerDownHandler);
            return press;
        }

        private static void ReleaseComputerLine(Button button, PointerEventData press)
        {
            ExecuteEvents.Execute(button.gameObject, press, ExecuteEvents.pointerUpHandler);
            if (press.eligibleForClick) ExecuteEvents.Execute(button.gameObject, press, ExecuteEvents.pointerClickHandler);
        }

        private static void TapComputerLine(Button button)
        {
            ReleaseComputerLine(button, PressComputerLine(button));
        }

        private static void CheckDuoCompetition(Transform root, FeihualingPage page, Func<RectTransform> modal)
        {
            Click(root, "同机双人");
            Click(root, "开始竞技");
            Require(Find<Button>(root, "Poetry hint action") == null, "The next player must confirm before receiving clues");
            Click(root, "准备好了");
            Click(root, "提示 3/3");
            Click(modal(), "关闭");
            var field = Find<InputField>(root, "Poetry input");
            field.text = "床前明月光";
            Click(root, "发送");
            Require(Find<Button>(root, "Poetry hint action") == null, "Competition handoff hides clues");
            Click(root, "准备好了");
            Click(root, "提示 2/3");
            Click(modal(), "关闭");
            Find<Button>(root, "Back button").onClick.Invoke();
            Require(Find<Button>(root, "Home button") != null && Buttons(root, "继续上次").Length == 1,
                "The header arrow preserves the same unfinished slot");
            Click(root, "继续上次");
            Click(root, "提示 1/3");
            var session = (PoetrySession)page.GetType().GetField("match", Private).GetValue(page);
            Require(session.CompetitionHintsRemaining == 1, "The shared allowance and current clue survive a return to setup");
            Click(modal(), "关闭");
            Require(page.HandleBack(), "Duo Competition returns to setup");
        }

        private static void CheckRules(Transform root, Func<RectTransform> currentModal)
        {
            var button = Find<Button>(root, "Book button");
            Require(button != null && button.IsInteractable(), "The upper-right book opens rules");
            button.onClick.Invoke();
            var panel = currentModal();
            Require(panel != null && panel.name == "飞花令 · 玩法说明", "The shared modal presents the poetry rules");
            var scroll = Find<ScrollRect>(panel, "Scrollable explanation");
            Require(scroll != null && scroll.vertical && scroll.content.rect.height > scroll.viewport.rect.height,
                "The rules can scroll independently of the page");
            var copy = string.Join("\n", scroll.content.GetComponentsInChildren<Text>().Select(label => label.text));
            var topics = new[] { "单字令", "字位令", "双字令", "主题令", "60 秒", "30 秒",
                "单机 · 学习", "同机双人 · 学习", "单机 · 竞技", "同机双人 · 竞技", "单机 · 竞速", "同机双人 · 竞速" };
            foreach (var topic in topics)
                Require(copy.Contains(topic), "Rules explain " + topic);
        }

        private static void RequireHiddenTargets(Transform modal, PoetryCatalog catalog, System.Collections.Generic.IReadOnlyList<int> targets)
        {
            var labels = modal.GetComponentsInChildren<Text>();
            foreach (var lineId in targets)
                Require(labels.All(label => !label.text.Contains(catalog.Text(lineId))),
                    "Neither the context nor its expanded source reveals a current target");
        }

        private static void Click(Transform root, string caption)
        {
            var match = Buttons(root, caption);
            Require(match.Length == 1, $"Exactly one active '{caption}' button is present (found {match.Length})");
            Require(match[0].IsInteractable(), $"'{caption}' can be pressed");
            match[0].onClick.Invoke();
        }

        private static Button[] Buttons(Transform root, string caption) => root.GetComponentsInChildren<Button>()
            .Where(button => button.GetComponentsInChildren<Text>().Any(text => text.text == caption)).ToArray();

        private static T Find<T>(Transform root, string name) where T : Component =>
            root.GetComponentsInChildren<T>().SingleOrDefault(component => component.gameObject.name == name);

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
