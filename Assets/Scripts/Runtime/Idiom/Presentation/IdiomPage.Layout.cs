using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Idiom;

namespace XanaduYonder.Runtime.Idiom
{
    internal sealed partial class IdiomPage
    {
        private RectTransform gameRoot, bookRoot, footer, inputRow, endedRow, starter, messagePanel;
        private Text target, countText, inlineError, messageTitle, messageBody, bookCount, bookEmpty;
        private Text pageNotice, notebookNotice, composerRule, starterRule, starterDescription;
        private Button computerMatch, localMatch, leftStart, rightStart, send, hint, bookButton, newReplies, messageAction;
        private IdiomRuleSwitch ruleSwitch;
        private Button notebookRecovery;
        private InputField input;
        private IdiomVirtualList chat, bookList;
        private float layoutWidth, layoutHeight, layoutInset = -1;

        private void BuildLayout()
        {
            gameRoot = UiFactory.Rect(root, "Idiom game", 0, 0, 720, 1320); UiFactory.Stretch(gameRoot);
            bookRoot = UiFactory.Rect(root, "Idiom notebook", 0, 0, 720, 1320); UiFactory.Stretch(bookRoot);
            UiFactory.IconButton(gameRoot, UiIconKind.Home, 56, 34, 58, 58, () => { if (CanNavigate) goLobby(); });
            var title = IdiomUi.Label(gameRoot, "成语接龙", 200, 30, 320, 66, 35, UiFactory.Ink, TextAnchor.MiddleCenter, true);
            title.name = "Idiom centered title";
            bookButton = UiFactory.IconButton(gameRoot, UiIconKind.Notebook, 536, 34, 58, 58, () => { if (CanNavigate) OpenNotebook(false); });
            UiFactory.IconButton(gameRoot, UiIconKind.Settings, 606, 34, 58, 58, () => { if (CanNavigate) ShowMenu(); });
            computerMatch = IdiomUi.Button(gameRoot, "人机对战", 32, 108, 320, 58, () => ChangeMatchKind(IdiomMatchKind.Computer));
            localMatch = IdiomUi.Button(gameRoot, "双人本地对战", 368, 108, 320, 58, () => ChangeMatchKind(IdiomMatchKind.Local));
            countText = IdiomUi.Label(gameRoot, "", 210, 184, 300, 52, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
            countText.name = "Idiom centered count";
            ruleSwitch = IdiomRuleSwitch.Create(gameRoot, 552, 184, () =>
                ChangeMode(data.ActiveMode == IdiomMode.SameCharacter ? IdiomMode.SameSound : IdiomMode.SameCharacter));
            target = IdiomUi.Label(gameRoot, "", 32, 244, 656, 40, 23, UiFactory.Accent, TextAnchor.MiddleCenter, true);
            countText.horizontalOverflow = HorizontalWrapMode.Overflow;
            countText.verticalOverflow = VerticalWrapMode.Truncate;
            chat = new IdiomVirtualList(gameRoot, "Idiom conversation", 164, BindChatRow, index =>
            {
                if (AcceptsInput && index < Game.History.Count) ShowDefinition(Game.History[index], null, null);
            });
            newReplies = IdiomUi.Button(gameRoot, "新回复 ↓", 260, 900, 200, 48, () =>
            {
                if (!AcceptsInput) return;
                chat.GoBottom(); newReplies.gameObject.SetActive(false);
            }, true, 21);
            newReplies.gameObject.SetActive(false);

            footer = UiFactory.Rect(gameRoot, "Idiom composer", 32, 1088, 656, 152);
            inlineError = IdiomUi.Label(footer, "", 0, 0, 656, 40, 21, UiFactory.Hex("9B5143"));
            inputRow = UiFactory.Rect(footer, "Active composer", 0, 46, 656, 70);
            hint = IdiomUi.Button(inputRow, "提示 0/3", 0, 0, 132, 70, RequestHint, false, 21);
            var field = UiFactory.Panel(inputRow, "Idiom input", 144, 0, 368, 70, Color.white);
            field.raycastTarget = true;
            input = field.gameObject.AddComponent<InputField>();
            input.targetGraphic = field;
            var text = IdiomUi.Label(field.transform, "", 18, 0, 332, 70, 27);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            var placeholder = IdiomUi.Label(field.transform, "输入四字成语…", 18, 0, 332, 70, 24, UiFactory.Muted);
            input.textComponent = text; input.placeholder = placeholder;
            input.contentType = InputField.ContentType.Standard;
            input.lineType = InputField.LineType.SingleLine;
            input.characterValidation = InputField.CharacterValidation.None;
            input.keyboardType = TouchScreenKeyboardType.Default;
            input.characterLimit = 128;
            input.shouldHideMobileInput = true;
            input.caretColor = UiFactory.Accent;
            input.customCaretColor = true;
            input.selectionColor = new Color(.36f, .64f, .48f, .28f);
            input.onValueChanged.AddListener(DraftChanged);
            // Selection confirmation, endEdit and losing focus never submit a turn.
            send = IdiomUi.Button(inputRow, "发送", 524, 0, 132, 70, Submit, true, 25);
            composerRule = IdiomUi.Label(footer, "", 0, 122, 656, 30, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
            endedRow = UiFactory.Rect(footer, "Finished composer", 0, 38, 656, 76);
            IdiomUi.Button(endedRow, "本轮小结", 0, 0, 320, 72, () => { if (AcceptsInput) ShowSummary(); });
            IdiomUi.Button(endedRow, "再来一局", 336, 0, 320, 72, () => { if (AcceptsInput) Restart(); }, true);

            starter = UiFactory.Rect(gameRoot, "Choose who starts", 32, 330, 656, 300);
            IdiomUi.Label(starter, "谁来开个头？", 0, 0, 656, 70, 40, UiFactory.Ink, TextAnchor.MiddleCenter, true);
            starterDescription = IdiomUi.Label(starter, "", 0, 88, 656, 50, 26, UiFactory.Muted, TextAnchor.MiddleCenter);
            leftStart = IdiomUi.Button(starter, "机器开局", 0, 172, 320, 80, () => StartGame(IdiomActor.Left), true, 28);
            rightStart = IdiomUi.Button(starter, "人先开始", 336, 172, 320, 80, () => StartGame(IdiomActor.Right), false, 28);
            starterRule = IdiomUi.Label(starter, "", 0, 270, 656, 38, 21, UiFactory.Muted, TextAnchor.MiddleCenter);

            messagePanel = UiFactory.Rect(gameRoot, "Idiom loading or recovery", 32, 340, 656, 330);
            messageTitle = IdiomUi.Label(messagePanel, "", 0, 0, 656, 70, 34, UiFactory.Ink, TextAnchor.MiddleCenter, true);
            messageBody = IdiomUi.Label(messagePanel, "", 20, 88, 616, 132, 24, UiFactory.Muted, TextAnchor.UpperCenter);
            messageAction = IdiomUi.Button(messagePanel, "重试", 128, 238, 400, 72, () =>
            {
                if (!CanNavigate) return;
                if (store != null && store.NeedsRecovery && loaded) ConfirmGameRecovery();
                else BeginLoad();
            }, true);

            IdiomUi.Button(bookRoot, "‹ 返回", 56, 34, 130, 58, () => { if (CanNavigate) ReturnFromNotebook(); });
            IdiomUi.Label(bookRoot, "生词本", 210, 30, 300, 66, 35, UiFactory.Ink, TextAnchor.MiddleCenter, true);
            bookCount = IdiomUi.Label(bookRoot, "", 500, 40, 188, 50, 22, UiFactory.Accent, TextAnchor.MiddleRight);
            IdiomUi.Label(bookRoot, "适时复习 · 点开后可标记“我记住了”", 32, 108, 656, 50, 23, UiFactory.Muted);
            bookList = new IdiomVirtualList(bookRoot, "Saved idiom list", 158, BindNotebookRow, index =>
            {
                if (CanNavigate && notebookVisible && index < notebookItems.Count)
                    ShowDefinition(notebookItems[index].Id, null, notebookItems[index]);
            });
            bookEmpty = IdiomUi.Label(bookRoot, "", 60, 300, 600, 170, 28, UiFactory.Muted, TextAnchor.MiddleCenter);
            notebookRecovery = IdiomUi.Button(bookRoot, "保留旧档，重新创建", 160, 510, 400, 70,
                () => { if (CanNavigate) ConfirmNotebookRecovery(null); }, true);
            pageNotice = IdiomUi.Label(gameRoot, "", 32, 1272, 656, 40, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
            notebookNotice = IdiomUi.Label(bookRoot, "", 32, 1272, 656, 40, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
            bookRoot.gameObject.SetActive(false);
            Layout();
        }

        private void Update()
        {
            if (!visible) return;
            Layout();
            if (draftDirty && Time.unscaledTime >= draftSaveAt) Save();
            send.interactable = AcceptsInput && Game.IsStarted && !Game.IsEnded && !string.IsNullOrWhiteSpace(input.text);
            chat.Tick();
            if (notebookVisible) bookList.Tick();
            if (newReplies.gameObject.activeSelf && chat.NearBottom) newReplies.gameObject.SetActive(false);
            var notice = SaveError ?? initialWarning ?? "";
            pageNotice.text = notebookNotice.text = notice;
            pageNotice.color = notebookNotice.color = SaveError == null ? UiFactory.Muted : UiFactory.Hex("9B5143");
        }

        private void Layout()
        {
            // UGUI deselects InputField on pointer DOWN, dismissing the IME before
            // Button receives pointer UP. Reflowing in between moves the hit target
            // and cancels an otherwise valid click. Keep geometry through release.
            if (layoutWidth > 0 && (Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))) return;
            var width = root.rect.width > 0 ? root.rect.width : 720;
            var height = root.rect.height > 0 ? root.rect.height : 1320;
            var inset = keyboard == null ? 0 : keyboard.BottomInset;
            if (Mathf.Abs(width - layoutWidth) < .1f && Mathf.Abs(height - layoutHeight) < .1f && Mathf.Abs(inset - layoutInset) < .5f) return;
            layoutWidth = width; layoutHeight = height; layoutInset = inset;
            // The design frame is at least 720 wide. Keep controls in its centered
            // 720-unit column while giving the conversation all vertical space.
            var left = (width - 720) * .5f;
            gameRoot.offsetMin = new Vector2(left, 0); gameRoot.offsetMax = new Vector2(-left, 0);
            bookRoot.offsetMin = new Vector2(left, 0); bookRoot.offsetMax = new Vector2(-left, 0);
            var bottom = Mathf.Max(64, inset + 14);
            var composerTop = Mathf.Max(0, height - bottom - 156);
            IdiomUi.Place(footer, 32, composerTop, 656, 156);
            IdiomUi.Place(chat.Viewport, 24, 296, 672, Mathf.Max(1, composerTop - 310));
            IdiomUi.Place(newReplies.GetComponent<RectTransform>(), 260, composerTop - 58, 200, 48);
            IdiomUi.Place(starter, 32, Mathf.Min(360, Mathf.Max(270, height * .27f)), 656, 320);
            IdiomUi.Place(messagePanel, 32, Mathf.Min(360, height * .28f), 656, 330);
            var recoveringBook = notebook != null && store != null && store.NeedsRecovery;
            IdiomUi.Place(bookList.Viewport, 32, 180, 656, height - (recoveringBook ? 328 : 248));
            IdiomUi.Place(notebookRecovery.GetComponent<RectTransform>(), 160, recoveringBook ? height - 140 : 510, 400, 70);
            IdiomUi.Place(pageNotice.rectTransform, 32, height - 48, 656, 40);
            IdiomUi.Place(notebookNotice.rectTransform, 32, height - 48, 656, 40);
        }

        private void Refresh(bool bottom = false)
        {
            var ready = loaded && Game != null;
            gameRoot.gameObject.SetActive(!notebookVisible);
            bookRoot.gameObject.SetActive(notebookVisible);
            bookButton.interactable = loaded;
            computerMatch.interactable = localMatch.interactable = ready;
            PaintMode(computerMatch, !IsLocal);
            PaintMode(localMatch, IsLocal);
            ruleSwitch.SetState(data != null && data.ActiveMode == IdiomMode.SameSound, ready);
            starterDescription.text = IsLocal ? "两个人，一部手机，轮流接下去。" : "你一言，我一语，边接边学。";
            IdiomUi.Caption(leftStart, IsLocal ? "左方先开始" : "机器开局");
            IdiomUi.Caption(rightStart, IsLocal ? "右方先开始" : "人先开始");
            composerRule.text = starterRule.text = HintRule;
            starter.gameObject.SetActive(ready && !Game.IsStarted);
            chat.Viewport.gameObject.SetActive(ready && Game.IsStarted);
            footer.gameObject.SetActive(ready && Game.IsStarted);
            inputRow.gameObject.SetActive(ready && Game.IsStarted && !Game.IsEnded);
            endedRow.gameObject.SetActive(ready && Game.IsEnded);
            messagePanel.gameObject.SetActive(!ready);
            target.text = ready ? TargetLabel() : "离线接龙 · 随手学成语";
            countText.text = ready ? "本轮完成 " + CompletedCount + " 个" : "";
            countText.fontSize = 23;
            if (countText.preferredWidth > countText.rectTransform.rect.width)
                countText.fontSize = Mathf.Max(16, Mathf.FloorToInt(23 * countText.rectTransform.rect.width / countText.preferredWidth));
            if (ready)
            {
                writingDraft = true; input.SetTextWithoutNotify(ActiveSave.Draft ?? ""); writingDraft = false;
                input.interactable = Game.IsStarted && !Game.IsEnded;
                var hintCount = IsLocal ? Game.HintCountFor(Game.NextActor) : Game.HintCount;
                IdiomUi.Caption(hint, Game.CurrentHint == null ? "提示 " + hintCount + "/3" : "查看提示");
                var leftTurn = IsLocal && Game.NextActor == IdiomActor.Left;
                IdiomUi.Place(send.GetComponent<RectTransform>(), leftTurn ? 0 : 524, 0, 132, 70);
                IdiomUi.Place(hint.GetComponent<RectTransform>(), leftTurn ? 524 : 0, 0, 132, 70);
                ((Text)input.placeholder).text = IsLocal ? TurnName + "输入成语…" : "输入四字成语…";
                chat.SetCount(Game.History.Count, bottom);
                if (Game.IsEnded) inlineError.text = "本轮已完成，点开成语继续学习。";
            }
            else
            {
                newReplies.gameObject.SetActive(false);
                var recovery = loaded && store != null && store.NeedsRecovery;
                messageTitle.text = loading ? "正在打开本地词库" : recovery ? "你的存档已保留" : "词库暂时未打开";
                messageBody.text = loading ? "首次载入需要片刻，之后无需联网。" : recovery ?
                    "原存档暂时无法继续。\n可以先保留原文件，再重新创建\n成语对局、生词本与个人学习记录。" : loadError ?? "点下方按钮，重新载入本地词库。";
                messageAction.gameObject.SetActive(!loading);
                IdiomUi.Caption(messageAction, recovery ? "保留旧档，重新开始" : "重新载入");
            }
            if (notebookVisible) RefreshNotebook();
            Layout();
        }

        private static void PaintMode(Button button, bool selected)
        {
            button.GetComponent<Image>().color = selected ? UiFactory.Accent : UiFactory.Hex("E7EDE4");
            button.GetComponentInChildren<Text>().color = selected ? Color.white : UiFactory.Ink;
        }

        private string TargetLabel()
        {
            if (Game == null || !Game.IsStarted) return "选择谁来开个头";
            if (Game.IsEnded) return "这一轮接到这里";
            var turn = IsLocal ? "轮到" + TurnName + " · " : "";
            if (Game.LastEntry == null) return turn + "先说一个四字成语";
            return turn + (Game.Mode == IdiomMode.SameCharacter ? "请接“" + Game.LastEntry.LastCharacter + "”字开头" :
                "请接读音“" + Game.LastEntry.LastKey.Replace("v", "ü") + "”，声调不限");
        }

        private void BindChatRow(IdiomVirtualList.Row row, int index)
        {
            IdiomEntry entry;
            if (Game == null || index >= Game.History.Count || !lexicon.TryGetById(Game.History[index], out entry)) return;
            var player = index % 2 == 0 ? Game.FirstActor == IdiomActor.Player : Game.FirstActor != IdiomActor.Player;
            var x = player ? 190 : 8;
            IdiomUi.Place(row.Button.GetComponent<RectTransform>(), x, 27, 474, 122);
            IdiomUi.Place(row.Actor.rectTransform, player ? 514 : 12, 0, 146, 25);
            row.Actor.alignment = player ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            row.Actor.text = IsLocal ? (player ? "右方" : "左方") : (player ? "你" : "APP");
            row.Button.GetComponent<Image>().color = player ? UiFactory.Accent : UiFactory.Hex("E5EDDA");
            row.Word.text = entry.Word; row.Word.color = player ? Color.white : UiFactory.Ink;
            row.Pinyin.text = entry.Pinyin; row.Pinyin.color = player ? UiFactory.Hex("E1EDDB") : UiFactory.Accent;
            row.Note.gameObject.SetActive(false);
        }

        private void BindNotebookRow(IdiomVirtualList.Row row, int index)
        {
            var item = notebookItems[index];
            IdiomUi.Place(row.Button.GetComponent<RectTransform>(), 0, 4, 656, 140);
            row.Button.GetComponent<Image>().color = UiFactory.Hex("E8EDDF");
            row.Actor.gameObject.SetActive(false);
            IdiomUi.Place(row.Word.rectTransform, 22, 5, 600, 50);
            IdiomUi.Place(row.Pinyin.rectTransform, 22, 55, 600, 32);
            IdiomUi.Place(row.Note.rectTransform, 22, 92, 600, 36);
            row.Word.text = item.Word; row.Pinyin.text = item.Pinyin;
            row.Note.text = (item.Meaning ?? "").Replace("\r", " ").Replace("\n", " ");
            row.Note.gameObject.SetActive(true);
        }
    }
}
