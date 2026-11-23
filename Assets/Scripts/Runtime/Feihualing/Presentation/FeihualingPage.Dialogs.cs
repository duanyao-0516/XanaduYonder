using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Feihualing;
using XanaduYonder.Runtime.Idiom;

namespace XanaduYonder.Runtime.Feihualing
{
    public sealed partial class FeihualingPage
    {
        private const float ModalMargin = 32;
        private const float ModalWidth = 532;
        private RectTransform hintViewport, hintContent;
        private ScrollRect hintScroll;
        private Text hintNotice;
        private Button hintRefresh;
        private IReadOnlyList<int> hintCandidates;
        private int expandedLine = -1;
        private PoetrySession anchoredHintMatch;
        private int anchoredHintTurn, anchoredHintLine;
        private float anchoredHintOffset;
        private Action modalBack;
        private RectTransform poemScreen;

        private RectTransform OpenDialog(string title, float height, Action back = null)
        {
            HideKeyboard();
            CloseDialog();
            var panel = openModal(title, height, DismissDialog);
            modalOpen = true;
            modalBack = back;
            UiFactory.CircleButton(panel, UiIconKind.Close, panel.rect.width - 72, 43, 48, DismissDialog);
            RefreshSend();
            return panel;
        }

        private void DismissDialog()
        {
            if (!modalOpen) return;
            var back = modalBack;
            CloseDialog();
            back?.Invoke();
        }

        private void CloseDialog()
        {
            if (!modalOpen) return;
            if (hintContent != null && match?.Goal == PoetryGoal.Competition)
            {
                anchoredHintMatch = match;
                anchoredHintTurn = match.History.Count;
                anchoredHintLine = expandedLine;
                anchoredHintOffset = hintContent.anchoredPosition.y;
            }
            modalOpen = false;
            modalBack = null;
            notebookList = null;
            hintCandidates = null;
            hintRefresh = null;
            hintContent = null;
            hintNotice = null;
            hintScroll = null;
            hintViewport = null;
            expandedLine = -1;
            if (poemScreen != null)
            {
                poemScreen.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(poemScreen.gameObject);
                else DestroyImmediate(poemScreen.gameObject);
                poemScreen = null;
            }
            else closeModal();
            RefreshSend();
            RefreshHintAction();
        }

        private void ShowRules()
        {
            if (!CanInteract) return;
            SuspendForMenu();
            var panel = OpenDialog("飞花令 · 玩法说明", DetailHeight());
            IdiomUi.ScrollText(panel,
                "先选人数、目标和令题。每次只发送一个诗词分句，例如「床前明月光」。句末标点可省略。同一小局已答过的诗句不能重复，收录的繁简或异文写法也算同一句。\n\n" +
                "四种令题\n" +
                "单字令 · 诗句包含指定字，位置不限。\n" +
                "字位令 · 按题卡要求答五言或七言，令字须在当前指定位置。每答对一句就移到下一位，最后一位之后回到第一位。\n" +
                "双字令 · 同一句同时包含两个指定字，顺序不限。\n" +
                "主题令 · 按令题作答，以诗库审核收录的主题诗句为准。\n\n" +
                "单机 · 学习\n不限时，可看提示和原诗。可在设置开启离线陪练，答对后陪练自动接一句。\n\n" +
                "同机双人 · 学习\n不限时。轮流答题，交给下一位后由对方点「准备好了」。分别记录独立和辅助作答，并统计合计。\n\n" +
                "单机 · 竞技\n与离线电脑轮流接句。不限时，接不上就输掉本局。\n\n" +
                "同机双人 · 竞技\n两人轮流接句。不限时，接不上就输掉这小局。\n\n" +
                "单机 · 竞速\n在 " + (PoetrySession.SoloMilliseconds / 1000) + " 秒内答出 " + PoetrySession.SoloTarget + " 个不同的有效分句。\n\n" +
                "同机双人 · 竞速\n每回合 " + (PoetrySession.DuoMilliseconds / 1000) + " 秒，点「准备好了」才开始计时。超时或接不上输掉这小局，题库用尽的小局记平。\n\n" +
                "双人竞技和竞速每小局交换先手，先赢两小局获胜。最多 " + PoetrySession.MaximumRounds + " 小局，最后按胜场结算，胜场相同则为平局。\n\n" +
                "竞技每场共 " + PoetrySession.CompetitionHintLimit + " 条提示，双人共享。每条只显示线索，同一回合重开已看的线索不扣额度。竞速没有提示。\n\n" +
                "点开历史里的任意答句即可阅读完整原诗并收藏。单机竞技也可长按电脑答句直接收藏。已收藏的诗词在答句末尾标有黄星，右上角可打开收藏。\n\n" +
                "竞速中打开说明或原诗会暂停，返回后点「继续」再计时。",
                ModalMargin, 116, ModalWidth, panel.rect.height - 218);
            IdiomUi.Button(panel, "知道了", ModalMargin, panel.rect.height - 84, ModalWidth, 62, CloseDialog, true);
        }

        private void Confirm(string title, string description, string caption, Action confirm)
        {
            var panel = OpenDialog(title, 452);
            IdiomUi.Label(panel, description, ModalMargin, 112, ModalWidth, 121, 24, UiFactory.Muted, TextAnchor.MiddleCenter);
            IdiomUi.Button(panel, caption, ModalMargin, 259, ModalWidth, 70, () =>
            {
                if (!modalOpen) return;
                CloseDialog();
                if (CanInteract) confirm();
            }, true, 25);
            IdiomUi.Button(panel, "取消", ModalMargin, 347, ModalWidth, 63, CloseDialog, false, 23);
        }

        private void StartupAction()
        {
            if (!CanInteract) return;
            if (store == null || !store.NeedsRecovery)
            {
                BeginLoad();
                return;
            }
            Confirm("处理本地进度？", profile == null ? "系统会另存旧记录，并创建新的飞花令进度。" :
                "系统会另存旧记录，再保存此刻的飞花令进度。", "保留原文件并继续", () =>
            {
                if (store.TryResetProtected(out var error))
                {
                    if (profile == null) profile = new FeihualingProfile();
                    initialWarning = null;
                    SaveError = null;
                    Commit();
                }
                else { SaveError = error; UpdateNotice(); }
            });
        }

        private void EndLearning()
        {
            if (!CanAnswer || !Learning) return;
            Confirm("结束这局学习？", "已答的句子会保留，结束后可以逐句回顾。", "结束并回顾", () => Transition(match.End, true));
        }

        private void ConfirmPass()
        {
            if (!CanAnswer || Learning || (Speed && !Duo)) return;
            var result = Duo ? "对方将赢得这一小局。" : "电脑将赢得本局。";
            Confirm("这一句接不上？", result + (Speed ? "确认前，本回合的倒计时继续。" : ""), "确认接不上",
                () => Transition(match.Pass, true));
        }

        private void ConfirmAbandon()
        {
            if (!CanInteract || match == null || Learning) return;
            Confirm("结束整场" + GoalNames[(int)match.Goal] + "？", "已发生的作答和小局结果会保留。", "确认结束",
                () => Transition(match.Abandon, true));
        }

        private void OpenTopicPicker()
        {
            if (!CanInteract || profile == null || match != null) return;
            var selected = SelectedCard();
            var panel = OpenDialog("选择令题", DetailHeight());
            IdiomUi.Scroll(panel, "Scrollable poetry topics", out var viewport, out var content);
            var bottom = panel.rect.height - 82;
            IdiomUi.Place(viewport, ModalMargin, 106, ModalWidth, bottom - 122);
            var index = 0;
            foreach (var candidate in catalog.Cards)
            {
                var card = candidate;
                if (card.Rule != Rule) continue;
                var x = index % 2 * 274;
                var y = index / 2 * 72;
                var caption = card.Rule == PoetryRule.Position ? card.Title + " · " + (card.Width == 5 ? "五言" : "七言") : card.Title;
                IdiomUi.Button(content, caption, x, y, 258, 60, () =>
                {
                    CloseDialog();
                    SelectTopic(card);
                }, selected?.Id == card.Id, 23);
                if (selected?.Id == card.Id)
                    content.anchoredPosition = new Vector2(0, y);
                index++;
            }
            var height = Mathf.Max(viewport.rect.height, ((index + 1) / 2) * 72);
            content.sizeDelta = new Vector2(ModalWidth, height);
            content.anchoredPosition = new Vector2(0, Mathf.Min(content.anchoredPosition.y, height - viewport.rect.height));
            IdiomUi.Button(panel, "关闭", ModalMargin, bottom, ModalWidth, 58, CloseDialog, false, 22);
        }

        private const string HintInstruction = "从相邻或相关诗句回想要接的那一句。";

        private void OpenHints()
        {
            if (!CanOpenHints) return;
            var saveHint = Learning || !match.HasCurrentCompetitionHint;
            var candidates = match.Hints();
            if (candidates.Count == 0)
            {
                ShowInline("暂时没有可用提示，可以继续自己作答。");
                return;
            }
            var panel = OpenDialog("诗句线索", DetailHeight());
            hintCandidates = candidates;
            hintNotice = IdiomUi.Label(panel, HintCaption(), ModalMargin, 104, ModalWidth, 47, 21, UiFactory.Muted);
            hintScroll = IdiomUi.Scroll(panel, "Scrollable Hint Accordion", out hintViewport, out hintContent);
            var bottom = panel.rect.height - 82;
            IdiomUi.Place(hintViewport, ModalMargin, 159, ModalWidth, bottom - 171);
            hintRefresh = IdiomUi.Button(panel, Competition ? "换一条" : "换一批", ModalMargin, bottom, 258, 58, RefreshHints, false, 22);
            IdiomUi.Button(panel, "关闭", ModalMargin + 274, bottom, 258, 58, CloseDialog, false, 22);
            var restoreAnchor = match == anchoredHintMatch && match.History.Count == anchoredHintTurn;
            if (restoreAnchor) expandedLine = anchoredHintLine;
            RenderHints(offset: restoreAnchor ? anchoredHintOffset : (float?)null);
            ExposeVisibleClues();
            if (saveHint) SaveExposure();
            RefreshHintControls();
        }

        private float DetailHeight() => Mathf.Clamp(root.rect.height * .76f, 650, 1000);

        private string HintCaption() => Competition
            ? "本场剩余 " + match.CompetitionHintsRemaining + " 条提示"
            : HintInstruction;

        private void RefreshHintControls()
        {
            if (hintRefresh != null)
                hintRefresh.interactable = !Competition || match.CompetitionHintsRemaining > 0;
            RefreshHintAction();
        }

        private void RefreshHints()
        {
            if (!modalOpen || hintCandidates == null || (Competition && match.CompetitionHintsRemaining == 0)) return;
            var candidates = match.Hints(true);
            if (candidates.Count == 0 || SameCandidates(candidates, hintCandidates))
            {
                hintNotice.text = Competition ? HintCaption() + " · 暂无新线索" : "暂时没有新线索，仍可查看当前提示。";
                return;
            }
            hintCandidates = candidates;
            hintNotice.text = HintCaption();
            expandedLine = -1;
            RenderHints();
            ExposeVisibleClues();
            SaveExposure();
            RefreshHintControls();
        }

        private static bool SameCandidates(IReadOnlyList<int> first, IReadOnlyList<int> second)
        {
            if (first.Count != second.Count) return false;
            for (var i = 0; i < first.Count; i++) if (first[i] != second[i]) return false;
            return true;
        }

        private void RenderHints(int focusLine = -1, float? offset = null)
        {
            for (var i = hintContent.childCount - 1; i >= 0; i--)
            {
                var row = hintContent.GetChild(i).gameObject;
                row.SetActive(false);
                if (Application.isPlaying) Destroy(row);
                else DestroyImmediate(row);
            }
            var y = 0f;
            var focusY = 0f;
            foreach (var candidate in hintCandidates)
            {
                var lineId = candidate;
                if (lineId == focusLine) focusY = y;
                var card = UiFactory.Panel(hintContent, "Hint candidate " + lineId, 0, y, ModalWidth - 4, 1, Color.white);
                var clueY = 14f;
                var context = match.ClueLines(lineId);
                var workLines = catalog.WorkLineIds(catalog.Lines[lineId].WorkIndex);
                var targetIndex = 0;
                while (targetIndex < workLines.Count && workLines[targetIndex] != lineId) targetIndex++;
                foreach (var clueId in context)
                {
                    var label = targetIndex > 0 && workLines[targetIndex - 1] == clueId ? "上句" :
                        targetIndex + 1 < workLines.Count && workLines[targetIndex + 1] == clueId ? "下句" : "相关句";
                    clueY = AddOriginalText(card.transform, label + " · " + match.MaskClue(catalog.Text(clueId), lineId),
                        clueY, 492, 23, UiFactory.Ink);
                }
                if (context.Count == 0)
                    clueY = AddOriginalText(card.transform, "可展开原诗查看上下文。", clueY, 492, 23, UiFactory.Muted);
                var expanded = lineId == expandedLine;
                IdiomUi.Button(card.transform, expanded ? "收起原诗" : "展开原诗", 16, clueY + 8, 220, 49,
                    () => ToggleOriginal(lineId), false, 21);
                IdiomUi.Place(card.rectTransform, 0, y, ModalWidth - 4, clueY + 69);
                y += clueY + 73;
                if (expanded) y += AddOriginal(hintContent, lineId, y, ModalWidth - 4, true);
                y += 12;
            }
            var height = Mathf.Max(hintViewport.rect.height, y);
            hintContent.sizeDelta = new Vector2(ModalWidth, height);
            hintScroll.StopMovement();
            hintContent.anchoredPosition = new Vector2(0, Mathf.Clamp(offset ?? focusY, 0, height - hintViewport.rect.height));
        }

        private void ToggleOriginal(int lineId)
        {
            if (!modalOpen || hintCandidates == null) return;
            expandedLine = expandedLine == lineId ? -1 : lineId;
            RenderHints(lineId);
            if (expandedLine != lineId || !Learning) return;
            var changed = false;
            foreach (var candidate in catalog.WorkLineIds(catalog.Lines[lineId].WorkIndex))
                changed |= ExposeVisibleLine(candidate, lineId);
            if (changed) SaveExposure();
        }

        private void ExposeVisibleClues()
        {
            if (!Learning) return;
            foreach (var target in hintCandidates)
                foreach (var clue in match.ClueLines(target))
                    ExposeVisibleLine(clue, target);
        }

        private bool ExposeVisibleLine(int lineId, int target)
        {
            var text = catalog.Text(lineId);
            return string.Equals(text, match.MaskClue(text, target), StringComparison.Ordinal) && match.ExposeLine(lineId);
        }

        private bool IsComputerLine(PoetryEntry entry) =>
            Competition && !Duo && entry.Player == 1 && !entry.Companion;

        private void OpenHistory(int index)
        {
            if (!CanInteract || match == null || index < 0 || index >= match.History.Count) return;
            var entry = match.History[index];
            var workIndex = catalog.Lines[entry.LineId].WorkIndex;
            OpenPoemDetail(catalog.Works[workIndex], (parent, width) => AddOriginal(parent, entry.LineId, 0, width));
            if (match.Phase == PoetryPhase.Active && match.ExposeWork(workIndex)) SaveExposure();
        }

        private void OpenPoemDetail(PoetryWork work, Func<Transform, float, float> render, Action back = null)
        {
            SuspendForMenu();
            EnsureNotebookLoaded();
            CloseDialog();
            var paper = UiFactory.Panel(root, "Feihualing full poem", 0, 0, 720, 1320, UiFactory.Paper, false);
            paper.raycastTarget = true;
            poemScreen = paper.rectTransform;
            UiFactory.Stretch(poemScreen);
            modalOpen = true;
            modalBack = back;
            RefreshSend();

            var panel = UiFactory.Rect(poemScreen, "Poem reading column", 0, 0, 720, 1320);
            panel.anchorMin = new Vector2(.5f, 0);
            panel.anchorMax = new Vector2(.5f, 1);
            panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(720, 0);
            UiFactory.IconButton(panel, UiIconKind.Back, 56, 34, 58, 58, DismissDialog);
            IdiomUi.Label(panel, "原诗", 200, 31, 320, 66, 35, UiFactory.Ink, TextAnchor.MiddleCenter, true);

            IdiomUi.Scroll(panel, "Scrollable poem", out var viewport, out var content);
            UiFactory.Stretch(viewport);
            viewport.offsetMin = new Vector2(32, 139);
            viewport.offsetMax = new Vector2(-32, -112);
            var height = render(content, BodyWidth - 4);
            IdiomUi.Place(content, 0, 0, BodyWidth, Mathf.Max(viewport.rect.height, height));

            var feedback = IdiomUi.Label(panel, "", 32, 0, BodyWidth, 34, 20, Red, TextAnchor.MiddleCenter);
            AnchorPoemFooter(feedback.rectTransform, 32, 96);
            var currentScreen = poemScreen;
            Button collect = null;
            collect = IdiomUi.Button(panel, "", 32, 0, BodyWidth, 68, () =>
            {
                if (!modalOpen || poemScreen != currentScreen) return;
                var result = CollectPoem(work, out var message);
                feedback.text = result == FeihualingNotebookAddResult.Failed ? message : "";
                ShowCollectionState(collect, work.Id);
            }, true, 24);
            AnchorPoemFooter((RectTransform)collect.transform, 32, 24);
            ShowCollectionState(collect, work.Id);
        }

        private void ShowCollectionState(Button button, string workId)
        {
            var saved = notebookStore.Contains(workId);
            IdiomUi.Caption(button, saved ? "已收藏" : "收藏整首");
            button.interactable = !saved;
        }

        private static void AnchorPoemFooter(RectTransform rect, float left, float bottom)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(left, bottom);
        }

        private float AddOriginal(Transform parent, int lineId, float y, float width, bool maskTargets = false)
        {
            var work = catalog.Works[catalog.Lines[lineId].WorkIndex];
            if (maskTargets)
                return AddPoem(parent, match.MaskClue(work.Title, lineId), match.MaskClue(work.Author, lineId),
                    match.MaskClue(work.Body, lineId), y, width, "待答诗句已遮盖", UiFactory.Muted);
            match.Card.Reasons.TryGetValue(lineId, out var reason);
            return AddPoem(parent, work.Title, work.Author, work.Body, y, width, "原句 · " + catalog.Text(lineId),
                UiFactory.Accent, reason);
        }

        private float AddPoem(Transform parent, string title, string author, string body, float y, float width,
            string note = null, Color? noteColor = null, string reason = null)
        {
            var original = UiFactory.Panel(parent, "Expanded original", 0, y, width, 1, Pale);
            var contentWidth = width - 36;
            var at = AddOriginalText(original.transform, "《" + title + "》", 15, contentWidth, 27, UiFactory.Ink, true);
            at = AddOriginalText(original.transform, author, at + 2, contentWidth, 20, UiFactory.Muted);
            if (!string.IsNullOrEmpty(note))
            {
                var highlight = noteColor == UiFactory.Accent;
                at = AddOriginalText(original.transform, note, at + 12, contentWidth, highlight ? 23 : 20,
                    noteColor ?? UiFactory.Muted, highlight);
            }
            if (!string.IsNullOrEmpty(reason))
                at = AddOriginalText(original.transform, "归类依据 · " + reason, at + 8, contentWidth, 20, UiFactory.Accent);

            // Keep long source text scrollable without asking legacy UGUI Text to
            // put a whole unusually long poem into one vertex buffer.
            foreach (var stanza in body.Split('\n'))
            {
                at += 8;
                for (var start = 0; start < stanza.Length;)
                {
                    var count = Mathf.Min(420, stanza.Length - start);
                    if (start + count < stanza.Length && char.IsHighSurrogate(stanza[start + count - 1])) count--;
                    at = AddOriginalText(original.transform, stanza.Substring(start, count), at, contentWidth, 25, UiFactory.Ink);
                    start += count;
                }
            }
            IdiomUi.Place(original.rectTransform, 0, y, width, at + 18);
            return at + 18;
        }

        private static float AddOriginalText(Transform parent, string text, float y, float width, int size, Color color, bool bold = false)
        {
            var label = IdiomUi.Label(parent, text, 18, y, width, 1, size, color, TextAnchor.UpperLeft, bold);
            var height = Mathf.Max(size * 1.45f, label.preferredHeight);
            IdiomUi.Place(label.rectTransform, 18, y, width, height);
            return y + height;
        }

        private void SaveExposure()
        {
            dirty = true;
            Save();
        }
    }
}
