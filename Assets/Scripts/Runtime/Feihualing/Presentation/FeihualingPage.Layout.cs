using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Feihualing;
using XanaduYonder.Runtime.Idiom;

namespace XanaduYonder.Runtime.Feihualing
{
    public sealed partial class FeihualingPage
    {
        private const float BodyWidth = 656;
        private const float HistoryRowHeight = 148;
        private const string AnswerUnit = "一次写一句诗词，例如「床前明月光」。";
        private static readonly Color Sage = UiFactory.Hex("E4ECE0");
        private static readonly Color Pale = UiFactory.Hex("F1F5EF");
        private static readonly Color Red = UiFactory.Hex("9B5143");

        private RectTransform column, startup, setup, play, gate, composer, actions, reviewActions;
        private RectTransform setupViewport, setupContent;
        private ScrollRect setupScroll;
        private Text startupTitle, startupMessage, goalHelp, topicTitle, topicCondition, topicRecord, notice;
        private Text playMode, playTitle, playCondition, clock, scoreboard, emptyHistory, feedback;
        private Text gateMode, gateTitle, gateBody;
        private readonly Button[] playerOptions = new Button[2];
        private readonly Button[] goalOptions = new Button[GoalCount];
        private readonly Button[] ruleOptions = new Button[RuleCount];
        private Button homeButton, backButton;
        private Button startupAction, continueButton, startButton, changeTopic, chooseTopic, send, hint, pause, pass, endLearning;
        private Button gatePrimary, gateSave, gateAbandon;
        private InputField input;
        private IdiomVirtualList history;
        private float previousWidth = -1, previousHeight = -1, previousInset = -1;
        private bool previousKeyboard;
        private PoetryPhase? previousPhase;
        private Action gateMainAction;

        public static void DrawLobbySeal(Transform parent)
        {
            var seal = UiFactory.Rect(parent, "Feihualing seal", 0, 0, 68, 68);
            UiFactory.Center(seal);
            UiFactory.Panel(seal, "Vermilion border", 0, 0, 68, 68, Red);
            UiFactory.Panel(seal, "Seal paper", 2, 2, 64, 64, UiFactory.Hex("F7EAE2"));
            IdiomUi.Label(seal, "诗", 0, 0, 68, 68, 42, Red, TextAnchor.MiddleCenter, true);
        }

        private void BuildLayout()
        {
            column = UiFactory.Rect(root, "Feihualing centered column", 0, 0, 720, 1320);
            var header = UiFactory.Rect(column, "Feihualing header", 0, 0, 720, 104);
            homeButton = UiFactory.NavigationButton(header, UiIconKind.Home, BackFromPage);
            backButton = UiFactory.NavigationButton(header, UiIconKind.Back, BackFromPage);
            var title = IdiomUi.Label(header, "飞花令", 264, 31, 192, 66, 35, UiFactory.Ink, TextAnchor.MiddleCenter, true);
            title.name = "Feihualing centered title";
            UiFactory.NavigationButton(header, UiIconKind.Notebook, OpenNotebook);
            UiFactory.NavigationButton(header, UiIconKind.Book, ShowRules);
            UiFactory.NavigationButton(header, UiIconKind.Settings, OpenSettings);

            startup = UiFactory.Rect(column, "Feihualing startup", 32, 200, BodyWidth, 340);
            startupTitle = IdiomUi.Label(startup, "", 0, 32, BodyWidth, 66, 34, UiFactory.Ink, TextAnchor.MiddleCenter, true);
            startupMessage = IdiomUi.Label(startup, "", 56, 111, 544, 92, 24, UiFactory.Muted, TextAnchor.MiddleCenter);
            startupAction = IdiomUi.PageButton(startup, "重试", 128, 246, 400, 68, StartupAction, true);

            BuildSetup();
            BuildPlay();
            BuildGate();
            notice = IdiomUi.Label(column, "", 32, 1272, BodyWidth, 40, 19, Red, TextAnchor.MiddleCenter);
            notice.verticalOverflow = VerticalWrapMode.Truncate;
            BuildNotebookToast();
            Render();
        }

        private void BuildSetup()
        {
            setup = UiFactory.Rect(column, "Feihualing setup container", 0, 106, 720, 1000);
            setupScroll = IdiomUi.Scroll(setup, "Feihualing setup", out setupViewport, out setupContent);
            IdiomUi.Place(setupViewport, 32, 0, BodyWidth, 1000);
            IdiomUi.Place(setupContent, 0, 0, BodyWidth, 1022);

            Section(setupContent, "人数", 0);
            playerOptions[0] = IdiomUi.PageButton(setupContent, "单机", 0, 42, 320, 62,
                () => Select(PoetryPlayers.Solo, Goal, Rule));
            playerOptions[1] = IdiomUi.PageButton(setupContent, "同机双人", 336, 42, 320, 62,
                () => Select(PoetryPlayers.Duo, Goal, Rule));

            Section(setupContent, "目标", 126);
            for (var i = 0; i < goalOptions.Length; i++)
            {
                var goal = (PoetryGoal)i;
                goalOptions[i] = IdiomUi.PageButton(setupContent, GoalNames[i], i * 224, 168, 208, 62,
                    () => Select(Players, goal, Rule));
            }
            goalHelp = IdiomUi.Label(setupContent, "", 2, 242, 652, 81, 23, UiFactory.Muted, TextAnchor.UpperLeft);

            Section(setupContent, "玩法", 333);
            for (var i = 0; i < ruleOptions.Length; i++)
            {
                var rule = (PoetryRule)i;
                var x = i % 2 * 336;
                var y = 378 + i / 2 * 92;
                var button = IdiomUi.PageButton(setupContent, "", x, y, 320, 80, () => Select(Players, Goal, rule));
                ruleOptions[i] = button;
                IdiomUi.Label(button.transform, RuleNames[i], 20, 8, 280, 36, 25, UiFactory.Ink, bold: true);
                IdiomUi.Label(button.transform, RuleDescriptions[i], 20, 42, 280, 30, 19, UiFactory.Muted);
            }

            var topic = UiFactory.Panel(setupContent, "Opening prompt", 0, 571, BodyWidth, 237, Color.white);
            IdiomUi.Label(topic.transform, "本局令题", 22, 13, 260, 34, 20, UiFactory.Muted);
            topicTitle = IdiomUi.Label(topic.transform, "", 22, 47, 302, 66, 42, UiFactory.Accent, bold: true);
            topicCondition = IdiomUi.Label(topic.transform, "", 22, 117, 612, 45, 23);
            topicRecord = IdiomUi.Label(topic.transform, "", 22, 169, 612, 35, 20, UiFactory.Muted);
            chooseTopic = IdiomUi.PageButton(topic.transform, "选题", 336, 30, 132, 58, OpenTopicPicker, false, 23);
            changeTopic = IdiomUi.PageButton(topic.transform, "换一题", 483, 30, 151, 58, () => ChangeTopic(), false, 23);
            IdiomUi.Label(setupContent, AnswerUnit, 2, 819, 652, 42, 20, UiFactory.Muted);
            continueButton = IdiomUi.PageButton(setupContent, "继续", 0, 875, 320, 70, ContinueSaved, true, 26);
            startButton = IdiomUi.PageButton(setupContent, "开始学习", 336, 875, 320, 70, StartNew, true, 26);
        }

        private void BuildPlay()
        {
            play = UiFactory.Rect(column, "Feihualing play", 0, 0, 720, 1320);
            var prompt = UiFactory.Panel(play, "Current poetry prompt", 32, 109, BodyWidth, 153, Color.white);
            playMode = IdiomUi.Label(prompt.transform, "", 20, 7, 432, 34, 20, UiFactory.Muted);
            playTitle = IdiomUi.Label(prompt.transform, "", 20, 43, 472, 62, 39, UiFactory.Accent, bold: true);
            playCondition = IdiomUi.Label(prompt.transform, "", 20, 104, 616, 37, 22);
            clock = IdiomUi.Label(prompt.transform, "", 494, 24, 142, 66, 36, UiFactory.Accent, TextAnchor.MiddleRight, true);
            clock.name = "Poetry countdown";
            scoreboard = IdiomUi.Label(play, "", 34, 273, 652, 79, 21, UiFactory.Ink, TextAnchor.MiddleCenter);
            emptyHistory = IdiomUi.Label(play, "", 56, 369, 608, 54, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
            history = new IdiomVirtualList(play, "Feihualing history", HistoryRowHeight, BindHistory, OpenHistory);
            history.Viewport.SetSiblingIndex(emptyHistory.transform.GetSiblingIndex());

            composer = UiFactory.Rect(play, "Feihualing composer", 32, 1080, BodyWidth, 200);
            feedback = IdiomUi.Label(composer, AnswerUnit, 0, 0, BodyWidth, 32, 21, UiFactory.Muted);
            var field = UiFactory.Panel(composer, "Poetry input", 0, 41, 508, 69, Color.white);
            input = field.gameObject.AddComponent<IdiomInputField>();
            input.targetGraphic = field;
            field.raycastTarget = true;
            var inputText = IdiomUi.Label(field.transform, "", 16, 0, 476, 69, 26);
            inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
            inputText.verticalOverflow = VerticalWrapMode.Truncate;
            input.textComponent = inputText;
            input.placeholder = IdiomUi.Label(field.transform, "输入一句诗词…", 16, 0, 476, 69, 24, UiFactory.Muted);
            input.lineType = InputField.LineType.SingleLine;
            input.characterValidation = InputField.CharacterValidation.None;
            input.keyboardType = TouchScreenKeyboardType.Default;
            input.characterLimit = FeihualingProfile.MaximumDraftLength;
            input.shouldHideMobileInput = true;
            input.caretColor = UiFactory.Accent;
            input.customCaretColor = true;
            input.selectionColor = new Color(.36f, .64f, .48f, .28f);
            input.onValueChanged.AddListener(DraftChanged);
            send = IdiomUi.Button(composer, "发送", 523, 41, 133, 69, Submit, true, 25);
            ((IdiomInputField)input).SendButton = send;

            actions = UiFactory.Rect(composer, "Feihualing turn actions", 0, 122, BodyWidth, 56);
            hint = IdiomUi.Button(actions, "提示", 0, 0, 152, 56, OpenHints, false, 22);
            hint.name = "Poetry hint action";
            endLearning = IdiomUi.Button(actions, "结束学习", 488, 0, 168, 56, EndLearning, false, 22);
            pause = IdiomUi.Button(actions, "暂停", 0, 0, 152, 56, () => { if (CanAnswer) PauseMatch(); }, false, 22);
            pass = IdiomUi.Button(actions, "接不上", 488, 0, 168, 56, ConfirmPass, false, 22);

            reviewActions = UiFactory.Rect(play, "Feihualing review actions", 32, 1140, BodyWidth, 70);
            IdiomUi.PageButton(reviewActions, "返回开局", 0, 0, 208, 66, () => { if (CanInteract) ReturnToSetup(); }, false, 21);
            IdiomUi.PageButton(reviewActions, "换一题", 224, 0, 208, 66, () =>
            {
                if (!CanInteract || match?.Phase != PoetryPhase.Finished) return;
                var card = match.Card;
                ReturnToSetup();
                ChangeTopic(card);
            }, false, 21);
            IdiomUi.PageButton(reviewActions, "再来一局", 448, 0, 208, 66, () =>
            {
                if (CanInteract && match?.Phase == PoetryPhase.Finished) StartWithCard(match.Card);
            }, true, 21);
        }

        private void BuildGate()
        {
            var surface = UiFactory.Panel(column, "Feihualing between turns", 32, 292, BodyWidth, 496, Color.white);
            gate = surface.rectTransform;
            gateMode = IdiomUi.Label(gate, "", 28, 33, 600, 66, 21, UiFactory.Accent, TextAnchor.MiddleCenter);
            gateTitle = IdiomUi.Label(gate, "", 28, 104, 600, 74, 40, UiFactory.Ink, TextAnchor.MiddleCenter, true);
            gateBody = IdiomUi.Label(gate, "", 40, 185, 576, 86, 24, UiFactory.Muted, TextAnchor.MiddleCenter);
            gatePrimary = IdiomUi.PageButton(gate, "", 48, 293, 560, 70, () => gateMainAction?.Invoke(), true, 27);
            gateSave = IdiomUi.PageButton(gate, "保存进度返回", 48, 389, 272, 60, () =>
            {
                if (CanInteract) ReturnToSetup();
            }, false, 22);
            gateAbandon = IdiomUi.PageButton(gate, "结束竞技", 336, 389, 272, 60, ConfirmAbandon, false, 22);
        }

        private static void Section(Transform parent, string text, float y) =>
            IdiomUi.Label(parent, text, 0, y, BodyWidth, 37, 23, UiFactory.Ink, bold: true);

        private void Render(bool followHistory = false)
        {
            homeButton.gameObject.SetActive(match == null);
            backButton.gameObject.SetActive(match != null);
            if (match != anchoredHintMatch || match?.Phase != PoetryPhase.Active || match.History.Count != anchoredHintTurn)
                anchoredHintMatch = null;
            var recovery = store != null && store.NeedsRecovery;
            var setupAvailable = catalog != null && profile != null && !recovery;
            var isPlaying = setupAvailable && match != null &&
                (match.Phase == PoetryPhase.Active || match.Phase == PoetryPhase.Finished);
            startup.gameObject.SetActive(!setupAvailable);
            setup.gameObject.SetActive(setupAvailable && match == null);
            play.gameObject.SetActive(isPlaying);
            gate.gameObject.SetActive(setupAvailable && match != null && !isPlaying);

            if (!setupAvailable)
            {
                startupTitle.text = recovery ? "处理本地进度" : loading != null ? "正在打开诗库" : "暂时无法打开诗库";
                startupMessage.text = recovery ? "旧存档已保留。将原文件另存后，可以恢复本地保存。" : loadError ?? initialWarning ??
                    "诗库随应用附带，可离线使用。";
                startupAction.gameObject.SetActive(recovery || loading == null);
                IdiomUi.Caption(startupAction, recovery ? "保留原文件并继续" : "重试");
            }
            else if (match == null) RenderSetup();
            else if (isPlaying) RenderPlay(followHistory);
            else RenderGate();

            UpdateNotice();
            Reflow(true);
        }

        private void RenderSetup()
        {
            for (var i = 0; i < playerOptions.Length; i++) Style(playerOptions[i], i == (int)Players);
            for (var i = 0; i < goalOptions.Length; i++) Style(goalOptions[i], i == (int)Goal);
            for (var i = 0; i < ruleOptions.Length; i++)
                ruleOptions[i].targetGraphic.color = i == (int)Rule ? Sage : Color.white;

            var isDuo = Players == PoetryPlayers.Duo;
            goalHelp.text = GoalHelp(isDuo);

            var card = SelectedCard();
            topicTitle.text = card?.Title ?? "暂无题卡";
            topicCondition.text = card == null ? "" : Condition(card, 1);
            var saved = profile.Matches[Slot];
            var savedCard = saved == null ? null : catalog.FindCard(saved.CardId);
            var best = card == null || isDuo || Goal != PoetryGoal.Speed ? 0 : profile.BestMilliseconds(card.Id);
            topicRecord.text = best > 0 ? "个人最佳 " + FormatSeconds(best) :
                savedCard != null && savedCard.Id != card?.Id ? "已保存题目：" + savedCard.Title : "";
            chooseTopic.interactable = card != null;
            changeTopic.interactable = card != null && Array.Exists(catalog.Cards, candidate => candidate.Rule == Rule && candidate.Id != card.Id);
            continueButton.gameObject.SetActive(saved != null);
            IdiomUi.Caption(continueButton, saved?.Phase == PoetryPhase.Finished ? "查看回顾" : "继续上次");
            IdiomUi.Caption(startButton, saved != null ? "开始新局" : "开始" + GoalNames[(int)Goal]);
            PlaceButton(startButton, saved == null ? 0 : 336, 875, saved == null ? 656 : 320, 70);
            startButton.interactable = card != null;
        }

        private string GoalHelp(bool duo)
        {
            switch (Goal)
            {
                case PoetryGoal.Learning:
                    return "不限时 · 可看线索和原诗\n" + (duo ? "轮流答题，合计合作成果" :
                        AutoCompanionEnabled ? "已开启离线陪练，答对后自动接一句" : "可在设置开启离线陪练");
                case PoetryGoal.Competition:
                    return "不限时 · 每场 " + PoetrySession.CompetitionHintLimit + " 条提示\n" +
                        (duo ? "提示由双方共享 · 接不上输掉小局" : "与离线电脑轮流作答 · 接不上输掉本局");
                default:
                    return (duo ? "每回合 30 秒 · 最多三小局" : "单机 60 秒答 5 句") + "\n独立作答，赛后阅读原诗";
            }
        }

        private void RenderPlay(bool followHistory)
        {
            var finished = match.Phase == PoetryPhase.Finished;
            playMode.text = finished ? "本局回顾 · " + RuleNames[(int)match.Card.Rule] : ModeLabel();
            playTitle.text = match.Card.Title;
            playCondition.text = finished ? ResultLabel() : Condition(match.Card, match.Position);
            scoreboard.text = Scoreboard();
            composer.gameObject.SetActive(!finished && !match.ComputerTurn);
            reviewActions.gameObject.SetActive(finished);
            clock.gameObject.SetActive(Speed && !finished);
            hint.gameObject.SetActive(!finished && !match.ComputerTurn && (Learning || Competition));
            RefreshHintAction();
            endLearning.gameObject.SetActive(Learning);
            pause.gameObject.SetActive(Speed);
            pass.gameObject.SetActive(!Learning && (Duo || !Speed));
            emptyHistory.gameObject.SetActive(match.History.Count == 0);
            emptyHistory.text = finished ? "本局没有作答记录。" : "答出的诗句会出现在这里。";
            EnsureNotebookLoaded();
            history.SetCount(match.History.Count, followHistory && !finished);
            DrawClock(Now);
            ShowInline(inlineMessage);
            RefreshSend();
        }

        private void RenderGate()
        {
            gateMode.text = ModeLabel() + "\n" + Scoreboard(false);
            gateSave.gameObject.SetActive(true);
            gateAbandon.gameObject.SetActive(!Learning);
            if (!Learning) IdiomUi.Caption(gateAbandon, "结束" + GoalNames[(int)match.Goal]);
            PlaceButton(gateSave, 48, 389, Learning ? 560 : 272, 60);
            string caption;
            switch (match.Phase)
            {
                case PoetryPhase.Handoff:
                    gateTitle.text = "请交给" + PlayerName(match.CurrentPlayer);
                    gateBody.text = Speed ? "准备好后开始本回合的 30 秒计时。" : "轮到你接下一句。";
                    caption = "准备好了";
                    gateMainAction = () => Transition(match.Ready);
                    break;
                case PoetryPhase.Paused:
                    gateTitle.text = "已暂停";
                    gateBody.text = "继续后会接上本回合的倒计时。";
                    caption = "继续";
                    gateMainAction = () => Transition(match.Resume);
                    break;
                case PoetryPhase.RoundOver:
                    var round = match.Rounds[match.Rounds.Count - 1];
                    gateTitle.text = round.Winner < 0 ? "本小局平局" : PlayerName(round.Winner) + "赢得小局";
                    gateBody.text = OutcomeLabel(round.Outcome) + "\n下一小局交换先手。";
                    caption = "下一小局";
                    gateMainAction = () => Transition(match.NextRound, true);
                    break;
                default: return;
            }
            IdiomUi.Caption(gatePrimary, caption);
        }

        private void BindHistory(IdiomVirtualList.Row row, int index)
        {
            var entry = match.History[index];
            var line = catalog.Lines[entry.LineId];
            var work = catalog.Works[line.WorkIndex];
            var right = Duo ? entry.Player == 1 : !entry.Companion && entry.Player == 0;
            var x = right ? 90 : 0;
            var computerLine = IsComputerLine(entry);
            row.Button.name = computerLine ? "Poetry computer line" : "";
            row.Button.targetGraphic.color = right ? Sage : Color.white;
            row.Button.interactable = true;
            row.Button.targetGraphic.raycastTarget = true;
            row.Actor.text = entry.Companion ? "陪练" : (Duo ? PlayerName(entry.Player) : entry.Player == 1 ? "电脑" : "你") +
                (Learning ? (entry.Assisted ? " · 辅助" : " · 独立") : Duo ? " · 第 " + entry.Round + " 小局" : "");
            row.Actor.alignment = right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            IdiomUi.Place(row.Actor.rectTransform, x + 12, 0, 540, 28);
            PlaceButton(row.Button, x, 31, 566, 109);
            var saved = notebookStore.Contains(work.Id);
            row.Word.name = "Poetry history answer";
            row.Word.text = entry.Input;
            row.Word.fontSize = entry.Input.Length > 24 ? 22 : 28;
            row.Word.verticalOverflow = VerticalWrapMode.Truncate;
            var starGap = row.Word.fontSize * .5f;
            IdiomUi.Place(row.Word.rectTransform, 18, 4, saved ? 530 - starGap - 17 : 530, 65);
            PlaceHistoryStar(row.Word, row.Note, saved, starGap);
            row.Pinyin.text = "《" + work.Title + "》 · " + work.Author;
            row.Pinyin.fontSize = 18;
            row.Pinyin.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.Pinyin.verticalOverflow = VerticalWrapMode.Truncate;
            IdiomUi.Place(row.Pinyin.rectTransform, 18, 71, 530, 30);
            var press = row.Button.GetComponent<FeihualingComputerLinePress>();
            if (computerLine)
            {
                var boundMatch = match;
                if (press == null) press = row.Button.gameObject.AddComponent<FeihualingComputerLinePress>();
                press.Bind(() => match == boundMatch && CanHoldComputerLine, () => SaveComputerPoem(work));
            }
            else if (press != null) press.Bind(null, null);
        }

        private static void PlaceHistoryStar(Text answer, Text star, bool saved, float gap)
        {
            star.gameObject.SetActive(saved);
            if (!saved) return;
            star.name = "Poetry saved star";
            star.text = "★";
            star.fontSize = 17;
            star.color = UiFactory.Hex("D59C19");
            IdiomUi.Place(star.rectTransform, 0, 0, 24, 30);

            // Align the visible glyphs independently of their font baselines.
            var answerInk = LastHistoryLineInk(answer);
            var starInk = LastHistoryLineInk(star);
            star.rectTransform.anchoredPosition = answer.rectTransform.anchoredPosition +
                new Vector2(answerInk.xMax + gap - starInk.xMin, answerInk.center.y - starInk.center.y);
        }

        private static Rect LastHistoryLineInk(Text label)
        {
            var generator = label.cachedTextGenerator;
            generator.Populate(label.text, label.GetGenerationSettings(label.rectTransform.rect.size));
            var vertices = generator.verts;
            var line = generator.lineCount - 1;
            while (line > 0 && generator.lines[line].startCharIdx * 4 >= vertices.Count) line--;
            var start = line < 0 ? 0 : generator.lines[line].startCharIdx * 4;
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var i = start; i + 3 < vertices.Count; i += 4)
            {
                Vector2 first = vertices[i].position;
                Vector2 opposite = vertices[i + 2].position;
                if (first.x == opposite.x || first.y == opposite.y) continue;
                min = Vector2.Min(min, Vector2.Min(first, opposite));
                max = Vector2.Max(max, Vector2.Max(first, opposite));
            }
            if (float.IsPositiveInfinity(min.x)) return default;
            min /= label.pixelsPerUnit;
            max /= label.pixelsPerUnit;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void LateUpdate()
        {
            if (!visible) return;
            Reflow();
            if (play.gameObject.activeSelf) history.Tick();
            notebookList?.Tick();
        }

        private void Reflow(bool force = false)
        {
            if (!force && (!keyboard.LayoutMeasurementReady || Input.touchCount != 0 || Input.GetMouseButton(0) || Input.GetMouseButtonUp(0)))
                return;
            var width = root.rect.width > 0 ? root.rect.width : 720;
            var height = root.rect.height > 0 ? root.rect.height : 1320;
            var inset = keyboard.BottomInset;
            var keyboardOpen = KeyboardVisible || input.isFocused;
            var phase = match?.Phase;
            if (!force && Mathf.Abs(width - previousWidth) < .1f && Mathf.Abs(height - previousHeight) < .1f &&
                Mathf.Abs(inset - previousInset) < .5f && keyboardOpen == previousKeyboard && phase == previousPhase) return;
            var followHistory = history.NearBottom;
            previousWidth = width; previousHeight = height; previousInset = inset; previousKeyboard = keyboardOpen; previousPhase = phase;
            IdiomUi.Place(column, (width - 720) * .5f, 0, 720, height);
            IdiomUi.Place(setupViewport, 32, 0, BodyWidth, height - 149);
            if (!setup.gameObject.activeSelf) setupScroll.StopMovement();
            IdiomUi.Place(startup, 32, Mathf.Max(108, (height - 340) * .5f), BodyWidth, 340);
            IdiomUi.Place(gate, 32, Mathf.Max(108, (height - 496) * .5f), BodyWidth, 496);

            var finished = phase == PoetryPhase.Finished;
            var showActions = !keyboardOpen;
            actions.gameObject.SetActive(showActions);
            var composerHeight = showActions ? 185 : 118;
            var bottom = Mathf.Max(notice.gameObject.activeSelf ? 48 : 25, inset + 14);
            var composerTop = height - bottom - composerHeight;
            var playHeaderBottom = keyboardOpen ? 300 : 349;
            IdiomUi.Place(scoreboard.rectTransform, 34, keyboardOpen ? 255 : 273, 652, keyboardOpen ? 48 : 79);
            scoreboard.fontSize = keyboardOpen ? 17 : 21;
            scoreboard.verticalOverflow = VerticalWrapMode.Overflow;
            IdiomUi.Place(composer, 32, composerTop, BodyWidth, composerHeight);
            var reviewTop = height - bottom - 70;
            IdiomUi.Place(reviewActions, 32, reviewTop, BodyWidth, 70);
            IdiomUi.Place(history.Viewport, 32, playHeaderBottom, BodyWidth, (finished ? reviewTop : composerTop) - playHeaderBottom - 10);
            IdiomUi.Place(emptyHistory.rectTransform, 56, playHeaderBottom + 8, 608, 54);
            emptyHistory.gameObject.SetActive(play.gameObject.activeSelf && match.History.Count == 0 &&
                history.Viewport.rect.height >= 60);
            IdiomUi.Place(notice.rectTransform, 32, height - 44, BodyWidth, 34);
            if (play.gameObject.activeSelf)
            {
                history.Tick();
                if (followHistory && !finished) history.GoBottom();
            }
        }

        private void RefreshSend() => send.interactable = CanAnswer && !string.IsNullOrWhiteSpace(input.text);

        private bool HasHintForTurn => Learning || (Competition &&
            (match.CompetitionHintsRemaining > 0 || match.HasCurrentCompetitionHint));

        private bool CanOpenHints => CanAnswer && HasHintForTurn;

        private void RefreshHintAction()
        {
            if (!hint.gameObject.activeInHierarchy) return;
            hint.interactable = HasHintForTurn;
            IdiomUi.Caption(hint, Competition ? "提示 " + match.CompetitionHintsRemaining + "/" +
                PoetrySession.CompetitionHintLimit : "提示");
        }

        private void ShowInline(string message)
        {
            inlineMessage = message;
            feedback.text = message ?? AnswerUnit;
            feedback.color = message == null ? UiFactory.Muted : Red;
            RefreshSend();
        }

        private void UpdateNotice()
        {
            notice.text = SaveError ?? initialWarning ?? "";
            notice.gameObject.SetActive(!string.IsNullOrEmpty(notice.text));
            if (visible) setStatus(notice.text);
        }

        private void DrawClock(long now)
        {
            if (match == null || !Speed || match.Phase != PoetryPhase.Active) return;
            var seconds = (match.Remaining(now) + 999) / 1000;
            clock.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            clock.color = seconds <= 10 ? Red : UiFactory.Accent;
        }

        private string ModeLabel() => (Duo ? "同机双人" : "单机") + " · " + GoalNames[(int)match.Goal] +
            " · " + RuleNames[(int)match.Card.Rule];

        private string Scoreboard(bool includeTurn = true)
        {
            if (Learning)
            {
                var left = PlayerScore(0);
                if (!Duo)
                {
                    var companionCount = CompanionCount();
                    return AutoCompanionEnabled || companionCount > 0 ? left + "    陪练 " + companionCount : left;
                }
                var right = PlayerScore(1);
                var total = match.Count(0, false) + match.Count(0, true) + match.Count(1, false) + match.Count(1, true);
                var secondLine = "合计 " + total + " 句";
                if (includeTurn && match.Phase == PoetryPhase.Active) secondLine += " · " + PlayerName(match.CurrentPlayer) + "作答";
                return left + "    " + right + "\n" + secondLine;
            }
            if (!Duo)
            {
                if (Speed) return "有效答句  " + match.Count(0, false) + " / " + PoetrySession.SoloTarget;
                var scoreLine = "你 " + match.Count(0, false) + " 句    电脑 " + match.Count(1, false) + " 句";
                if (match.Phase == PoetryPhase.Active) scoreLine += match.ComputerTurn ? "    电脑正在接令…" : "    轮到你";
                return scoreLine;
            }
            var score = "第 " + match.Round + " / " + PoetrySession.MaximumRounds +
                " 小局    左方 " + match.Wins(0) + " : " + match.Wins(1) + " 右方";
            if (includeTurn && match.Phase == PoetryPhase.Active) score += "    " + PlayerName(match.CurrentPlayer) + "作答";
            return score;
        }

        private string PlayerScore(int player)
        {
            var independent = match.Count(player, false);
            var assisted = match.Count(player, true);
            return (Duo ? PlayerName(player) : "你答") + " " + (independent + assisted) +
                " 句（独立 " + independent + " · 辅助 " + assisted + "）";
        }

        private int CompanionCount() => match.History.Count - match.Count(0, false) - match.Count(0, true);

        private string ResultLabel()
        {
            if (Learning) return match.Outcome == PoetryOutcome.Exhausted ? "本题可答诗句已用尽" : "本局学习已结束";
            if (match.Outcome == PoetryOutcome.Abandoned) return GoalNames[(int)match.Goal] + "已中止";
            if (Duo) return match.Winner < 0 ? "双方平局" : PlayerName(match.Winner) + "赢得整场";
            if (!Speed) return match.Winner == 0 ? "你赢得本局" : match.Winner == 1 ? "电脑赢得本局" : OutcomeLabel(match.Outcome);
            if (match.Outcome == PoetryOutcome.Completed)
                return "挑战完成 · 用时 " + FormatSeconds(match.History[match.History.Count - 1].ElapsedMilliseconds);
            return OutcomeLabel(match.Outcome);
        }

        private static string OutcomeLabel(PoetryOutcome outcome)
        {
            switch (outcome)
            {
                case PoetryOutcome.Timeout: return "作答时间已到";
                case PoetryOutcome.Passed: return "当前方选择接不上";
                case PoetryOutcome.Exhausted: return "本题可答诗句已用尽";
                default: return "本局已结束";
            }
        }

        private static string Condition(PoetryCard card, int position)
        {
            switch (card.Rule)
            {
                case PoetryRule.Character: return "在一句诗词中包含「" + card.Key + "」，字位不限";
                case PoetryRule.Position: return (card.Width == 5 ? "五言" : "七言") + "诗句，第 " + position + " 字是「" + card.Key + "」";
                case PoetryRule.Pair: return "同一句中同时有「" + card.Key + "」和「" + card.SecondKey + "」，顺序不限";
                default: return "答出与「" + card.Title + "」主题相符的句子";
            }
        }

        private static string FormatSeconds(long elapsed) =>
            (elapsed / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " 秒";

        private static void Style(Button button, bool selected)
        {
            button.targetGraphic.color = selected ? UiFactory.Accent : Sage;
            button.GetComponentInChildren<Text>().color = selected ? Color.white : UiFactory.Ink;
        }

        private static void PlaceButton(Button button, float x, float y, float width, float height)
        {
            IdiomUi.Place((RectTransform)button.transform, x, y, width, height);
            IdiomUi.Place(button.GetComponentInChildren<Text>().rectTransform, 12, 0, width - 24, height);
        }
    }
}
