using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Idiom;

namespace XanaduYonder.Runtime.Idiom
{
    internal sealed partial class IdiomPage
    {
        private Text dialogNotice;

        private RectTransform Dialog(string title, float height, Action back)
        {
            Save(); HideKeyboard(); Suspend();
            CloseDialog();
            var panel = openModal(title, height, ReturnFromDialog);
            modalOpen = true; modalBack = back;
            dialogNotice = null;
            foreach (var text in panel.GetComponentsInChildren<Text>()) text.supportRichText = false;
            UiFactory.CircleButton(panel, UiIconKind.Close, panel.rect.width - 72, 43, 48, ReturnFromDialog);
            return panel;
        }

        private void CloseDialog()
        {
            if (modalOpen) closeModal();
            modalOpen = false; modalBack = null;
            dialogNotice = null;
        }

        private void ReturnFromDialog()
        {
            if (!modalOpen) return;
            var back = modalBack;
            CloseDialog();
            if (visible) back?.Invoke();
        }

        private void RequestHint()
        {
            if (!AcceptsInput || !Game.IsStarted || Game.IsEnded) return;
            var candidate = Game.RequestHint();
            if (candidate == null)
            {
                inlineError.text = "暂时没有可用提示，试试自己接一个词。";
                CheckEndState(); return;
            }
            Save(); Refresh(); Changed?.Invoke();
            if (Game.IsEnded) ShowSummary(); else ShowHint(candidate);
        }

        private void ShowHint(IdiomHint candidate)
        {
            if (candidate == null || Game == null || Game.IsEnded) return;
            var hintCount = IsLocal ? Game.HintCountFor(Game.NextActor) : Game.HintCount;
            var panel = Dialog((IsLocal ? TurnName + " · " : "本轮") + "第 " + hintCount + " 次提示", 282 + candidate.EntryIds.Count * 98, null);
            IdiomUi.Label(panel, "点选填入，或先看看释义。", 32, 104, 532, 44, 23, UiFactory.Muted);
            for (var i = 0; i < candidate.EntryIds.Count; i++)
            {
                var id = candidate.EntryIds[i];
                IdiomEntry entry;
                if (!lexicon.TryGetById(id, out entry)) continue;
                var y = 162 + i * 98;
                var choose = IdiomUi.Button(panel, "", 32, y, 414, 84, () =>
                {
                    CloseDialog(); SetDraft(entry.Word); draftDirty = true;
                    Save(); Refresh(); input.ActivateInputField();
                });
                IdiomUi.Label(choose.transform, entry.Word, 18, 1, 378, 48, 30, UiFactory.Ink, bold: true);
                IdiomUi.Label(choose.transform, entry.Pinyin, 18, 48, 378, 30, 19, UiFactory.Accent);
                IdiomUi.Button(panel, "释义", 460, y, 104, 84,
                    () => ShowDefinition(id, () => ShowHint(candidate), null), false, 23);
            }
            IdiomUi.Button(panel, "先想一想", 32, panel.rect.height - 86, 532, 62, ReturnFromDialog);
        }

        private void ShowSummary()
        {
            if (Game == null || !Game.IsEnded || !visible) return;
            announcedResults.Add(Game);
            var candidate = Game.EndReason == IdiomEndReason.HintLimit ? Game.CurrentHint : null;
            var panel = Dialog("本轮回顾", candidate == null ? 620 : 840, null);
            IdiomUi.Label(panel, CompletedCount.ToString(), 32, 104, 532, 98, 70, UiFactory.Accent, TextAnchor.MiddleCenter, true);
            IdiomUi.Label(panel, IsLocal ? "左方 " + Game.LeftCount + " 个 · 右方 " + Game.RightCount + " 个" : "个成语，由你接成", 32, 200, 532, 42, 26, UiFactory.Ink, TextAnchor.MiddleCenter);
            var encouragement = CompletedCount == 0 ? "这次先认识几个新成语，下轮再试试。" :
                "把新认识的词收进生词本，\n下次又能多接几个。";
            if (IsLocal && Game.EndReason == IdiomEndReason.HintLimit)
                encouragement = TurnName + "的第 3 次提示已送达。\n一起把新成语收进生词本吧。";
            if (!IsLocal && Game.EndReason == IdiomEndReason.AppNoMoves) encouragement = "这个词把我难住了！\n" + encouragement;
            if (Game.EndReason == IdiomEndReason.RecommendationExhausted)
                encouragement = "这一轮适合的词先接到这里。\n一起回顾收获，下次再来相遇。";
            if (Game.EndReason == IdiomEndReason.DictionaryExhausted) encouragement = "词库挑战完成！\n" + encouragement;
            if (ActiveSave.NewBestInRun) encouragement = "新纪录！ " + encouragement;
            IdiomUi.Label(panel, encouragement, 36, 250, 524, candidate == null ? 120 : 100, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
            if (candidate != null)
            {
                for (var i = 0; i < candidate.EntryIds.Count; i++)
                {
                    var id = candidate.EntryIds[i];
                    IdiomEntry entry;
                    if (!lexicon.TryGetById(id, out entry)) continue;
                    var row = IdiomUi.Button(panel, "", 32, 364 + i * 82, 532, 72,
                        () => ShowDefinition(id, ShowSummary, null));
                    IdiomUi.Label(row.transform, entry.Word, 18, 0, 300, 42, 28, UiFactory.Ink, bold: true);
                    IdiomUi.Label(row.transform, entry.Pinyin, 18, 39, 420, 28, 18, UiFactory.Accent);
                    IdiomUi.Label(row.transform, "释义 ›", 414, 14, 100, 42, 20, UiFactory.Accent, TextAnchor.MiddleRight);
                }
            }
            var bottom = panel.rect.height - 170;
            IdiomUi.Button(panel, "再来一局", 32, bottom, 258, 68, () => { CloseDialog(); Restart(); }, true);
            IdiomUi.Button(panel, "生词本", 306, bottom, 258, 68, () => OpenNotebook(true));
            IdiomUi.Button(panel, "回看本轮", 32, bottom + 84, 532, 62, ReturnFromDialog);
        }

        private void ShowDefinition(string id, Action back, IdiomNotebookItem saved, bool expanded = false)
        {
            if (!visible || !loaded) return;
            IdiomEntry entry;
            lexicon.TryGetById(id, out entry);
            IdiomDefinition definition;
            string detailError;
            var found = loader.TryGetDefinition(id, out definition, out detailError);
            if (saved != null && found && definition.SenseId != saved.SenseId) found = false;
            var word = saved != null ? saved.Word : entry == null ? id : entry.Word;
            var pinyin = saved != null ? saved.Pinyin : entry == null ? "" : entry.Pinyin;
            if (found) { word = definition.Word; pinyin = definition.Pinyin; }
            if ((found || saved != null) && entry != null && Game != null) Game.RecordDefinitionViewed(id);
            var panel = Dialog(word, Mathf.Clamp(root.rect.height * .74f, 680, 1000), back);
            IdiomUi.Label(panel, pinyin, 32, 104, 532, 42, 25, UiFactory.Accent, TextAnchor.MiddleCenter);
            var added = FindNotebookItem(id) != null;
            var canRemove = added;
            var footerTop = panel.rect.height - (canRemove ? 196 : 132);
            var text = found ? DefinitionText(definition, expanded) : saved != null ?
                "已保存的释义\n" + saved.Meaning + "\n\n当前资料暂不可用，仍为你保留加入时的内容。" :
                "详情暂不可用。\n请重试，接龙进度不会受影响。";
            var longStories = found && StoryLength(definition) > 480;
            var expandHeight = longStories || !found ? 58 : 0;
            IdiomUi.ScrollText(panel, text, 32, 164, 532, footerTop - 184 - expandHeight);
            if (longStories)
                IdiomUi.Button(panel, expanded ? "收起长典故" : "展开典故", 32, footerTop - 62, 532, 48,
                    () => ShowDefinition(id, back, saved, !expanded), false, 21);
            else if (!found)
                IdiomUi.Button(panel, "重新读取资料", 32, footerTop - 62, 532, 48,
                    () => ShowDefinition(id, back, saved, expanded), false, 21);
            var add = IdiomUi.Button(panel, added ? "我记住了" : "加入生词本", 32, footerTop, 340, 64, () =>
            {
                if (store == null || !store.CanWrite || notebook == null)
                {
                    ConfirmNotebookRecovery(() => ShowDefinition(id, back, saved, expanded)); return;
                }
                if (added)
                {
                    if (entry == null) return;
                    var remembered = CopyNotebook();
                    remembered.Entries.RemoveAll(item => item.Id == id);
                    if (SaveNotebook(remembered, () => learning.Remember(id)))
                    {
                        setStatus("已记住“" + word + "”，今后也会出现在熟悉词中。");
                        ReturnFromDialog(); RefreshNotebook();
                    }
                    return;
                }
                if (!found || definition == null) return;
                var updated = CopyNotebook();
                var addedAt = UtcNow();
                updated.Entries.Add(new IdiomNotebookItem
                {
                    Id = definition.Id, SenseId = definition.SenseId, Word = definition.Word, Pinyin = definition.Pinyin,
                    Meaning = definition.Meaning, DictionaryVersion = lexicon.Version,
                    AddedAt = addedAt
                });
                if (SaveNotebook(updated, () => learning.AddNotebook(id, addedAt))) ShowDefinition(id, back, saved, expanded);
            }, true);
            add.interactable = added ? entry != null : found;
            IdiomUi.Button(panel, "复制成语", 388, footerTop, 176, 64, () =>
            {
                GUIUtility.systemCopyBuffer = word; setStatus("已复制“" + word + "”。");
            }, false, 21);
            if (canRemove)
                IdiomUi.Button(panel, "移出生词本", 32, footerTop + 76, 532, 48, () =>
                {
                    if (notebook == null || store == null || !store.CanWrite)
                    {
                        ConfirmNotebookRecovery(() => ShowDefinition(id, back, saved, expanded)); return;
                    }
                    var updated = CopyNotebook();
                    updated.Entries.RemoveAll(item => item.Id == id);
                    if (SaveNotebook(updated, () => learning.RemoveNotebook(id))) { ReturnFromDialog(); RefreshNotebook(); }
                }, false, 21);
            var source = found ? definition.SourceLabel : saved != null ? "显示加入生词本时保存的资料" : "本地离线资料";
            var sourceLabel = IdiomUi.Label(panel, source, 32, panel.rect.height - 48, 532, 32, 16, UiFactory.Muted, TextAnchor.MiddleCenter);
            sourceLabel.verticalOverflow = VerticalWrapMode.Truncate;
            dialogNotice = sourceLabel;
        }

        private static int StoryLength(IdiomDefinition definition)
        {
            var count = 0;
            if (definition.Stories != null) foreach (var story in definition.Stories) count += (story ?? "").Length;
            return count;
        }

        private static string DefinitionText(IdiomDefinition definition, bool expanded)
        {
            var text = new StringBuilder();
            text.Append("词义\n").Append(definition.Meaning);
            if (definition.Stories != null && definition.Stories.Length > 0)
            {
                text.Append("\n\n典故\n");
                var stories = string.Join("\n\n", definition.Stories);
                if (!expanded && stories.Length > 480)
                {
                    var cut = 480;
                    if (char.IsHighSurrogate(stories[cut - 1])) cut--;
                    text.Append(stories.Substring(0, cut)).Append("…");
                }
                else text.Append(stories);
            }
            AppendCitations(text, "出处", definition.Origins);
            AppendCitations(text, "例句", definition.Examples);
            return text.ToString();
        }

        private static void AppendCitations(StringBuilder text, string title, IdiomCitation[] values)
        {
            if (values == null || values.Length == 0) return;
            text.Append("\n\n").Append(title);
            foreach (var value in values)
            {
                if (value == null) continue;
                text.Append("\n").Append(value.Text);
                if (!string.IsNullOrEmpty(value.Reference)) text.Append("\n—— ").Append(value.Reference);
                text.Append('\n');
            }
        }

        private void ShowMenu()
        {
            Save();
            var recoverCurrent = Game != null && store != null && store.NeedsRecovery;
            var panel = Dialog("成语接龙", recoverCurrent ? 526 : 430, null);
            IdiomUi.Button(panel, "玩法说明", 32, 116, 532, 72, ShowHelp);
            var finish = IdiomUi.Button(panel, "结束本轮", 32, 212, 258, 72, () =>
            {
                Confirm("结束这一轮？", "已完成的成语仍可回看，\n生词本会一直保留。", "回顾本轮", () =>
                {
                    Game.Finish(); Save(); Refresh(); Changed?.Invoke(); ShowSummary();
                }, ShowMenu);
            });
            finish.interactable = Game != null && Game.IsStarted && !Game.IsEnded;
            var restart = IdiomUi.Button(panel, "新一局", 306, 212, 258, 72, () => { CloseDialog(); Restart(); }, true);
            restart.interactable = Game != null;
            if (recoverCurrent)
                IdiomUi.Button(panel, "保留旧档，保存当前进度", 32, 316, 532, 72, ConfirmCurrentGameRecovery, false, 23);
            IdiomUi.Button(panel, "继续", 32, panel.rect.height - 104, 532, 62, ReturnFromDialog);
        }

        private void ShowHelp()
        {
            var panel = Dialog("成语接龙 · 玩法", 750, ShowMenu);
            IdiomUi.ScrollText(panel,
                "同字：上一词末字，接下一词首字。\n同音：首尾读音相同即可，声调不限。\n\n" +
                "人机对战可选机器或你先开始；双人本地对战可选左方或右方先开始，之后轮流接词。双方本局用过的成语都不能重复。\n\n" +
                "提示一直免费。每个回合只给一组，反复查看不重复计次。人机模式第 3 次新提示后回顾本轮；双人模式双方各 3 次，任一方用满即结算。\n\n" +
                "人机模式统计你接出的成语；双人模式统计总数和双方各自的数量。没有倒计时，也没有错误次数限制。\n\n" +
                "点开成语查看释义、典故，加入生词本。APP 会适时带你复习生词，同一个词的自动复习间隔至少一天。\n\n" +
                "人机对战中，生词本外独立接出的词会进入个人熟悉词；本回合提示过或查看过释义的词不算独立输入。生词仍由你点击“我记住了”确认。双人不会积累个人熟悉词。\n\n" +
                "生词本中的“我记住了”会把词转为熟悉词；单纯移出只取消收藏。四种对局各自续玩，生词本共用。",
                32, 116, 532, panel.rect.height - 218);
            IdiomUi.Button(panel, "知道了", 32, panel.rect.height - 84, 532, 62, ReturnFromDialog, true);
        }

        private void Confirm(string title, string message, string action, Action confirmed, Action cancelled)
        {
            var panel = Dialog(title, 430, cancelled);
            IdiomUi.Label(panel, message, 32, 116, 532, 148, 25, UiFactory.Muted, TextAnchor.MiddleCenter);
            IdiomUi.Button(panel, "取消", 32, 316, 258, 70, ReturnFromDialog);
            IdiomUi.Button(panel, action, 306, 316, 258, 70, () => { CloseDialog(); confirmed(); }, true);
        }

        private void Restart()
        {
            if (Game == null) return;
            if (Game.IsStarted && !Game.IsEnded)
            {
                Confirm("开始新的一局？", "当前这一局将结束，\n最佳记录和生词本会保留。", "新一局", ResetMode, null);
            }
            else ResetMode();
        }

        private void ResetMode()
        {
            HideKeyboard(); Suspend();
            var mode = data.ActiveMode;
            var replacement = NewMode(mode, data.ActiveMatchKind); replacement.Best = ActiveSave.Best;
            announcedResults.Remove(Game);
            if (IsLocal)
            {
                if (mode == IdiomMode.SameCharacter)
                { data.LocalCharacter = replacement; localCharacterGame = RestoreGame(replacement.Game); }
                else { data.LocalSound = replacement; localSoundGame = RestoreGame(replacement.Game); }
            }
            else if (mode == IdiomMode.SameCharacter)
            {
                data.Character = replacement; characterGame = RestoreGame(replacement.Game);
            }
            else { data.Sound = replacement; soundGame = RestoreGame(replacement.Game); }
            chatOffsets[ActiveSlot] = 0; inlineError.text = "";
            newReplies.gameObject.SetActive(false);
            Save(); Refresh(true); Changed?.Invoke();
        }

        private void ConfirmGameRecovery()
        {
            Confirm("保留旧档，重新开始？", "原文件先保留为诊断备份。\n所有接龙模式、生词本与\n个人学习记录将重新创建。", "重新开始", () =>
            {
                string error;
                if (!store.TryResetProtected(out error))
                {
                    saveError = "旧档暂未能归档，原文件仍保留，请稍后重试。";
                    setStatus(saveError); Refresh(); return;
                }
                data = NewSave(); RestoreGames(); notebook = data.Notebook; RebuildNotebookItems(); announcedResults.Clear();
                notebookError = null;
                saveError = null; saveFailed = false; initialWarning = null;
                Save(); Refresh(true); Changed?.Invoke();
            }, null);
        }

        private void ConfirmCurrentGameRecovery()
        {
            ConfirmCurrentGameRecoveryWithReturn(ShowMenu);
        }

        private void ConfirmCurrentGameRecoveryWithReturn(Action back)
        {
            Confirm("恢复保存当前进度？", "旧文件先保留为诊断备份，\n再保存所有模式的当前进度。\n生词本不受影响。", "保留并保存", () =>
            {
                string error;
                if (!store.TryResetProtected(out error))
                {
                    saveError = error ?? "旧档暂未能归档，请重试。";
                    setStatus(saveError); Refresh(); return;
                }
                initialWarning = null;
                Save(); Refresh(); Changed?.Invoke(); back?.Invoke();
            }, back);
        }

        private void OpenNotebook(bool fromSummary)
        {
            if (!loaded || !visible) return;
            Save(); HideKeyboard(); Suspend(); CloseDialog();
            if (Game != null) chatOffsets[ActiveSlot] = chat.Offset;
            notebookFromSummary = fromSummary; notebookVisible = true;
            Refresh();
            if (notebook == null) ConfirmNotebookRecovery(null);
        }

        private void ReturnFromNotebook()
        {
            if (!notebookVisible) return;
            var summary = notebookFromSummary;
            notebookVisible = false; notebookFromSummary = false;
            Refresh();
            if (Game != null) chat.SetOffset(chatOffsets[ActiveSlot]);
            if (summary) ShowSummary();
        }

        private void RebuildNotebookItems()
        {
            notebookItems.Clear();
            if (notebook != null && notebook.Entries != null) notebookItems.AddRange(notebook.Entries);
            notebookItems.Sort((a, b) =>
            {
                var order = b.AddedAt.CompareTo(a.AddedAt);
                return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
            });
        }

        private void RefreshNotebook()
        {
            bookCount.text = notebook == null ? "暂不可读取" : "已收录 " + notebookItems.Count + " 个";
            bookEmpty.gameObject.SetActive(notebookItems.Count == 0);
            bookEmpty.text = notebook == null ? "生词本暂不可读取，\n原文件已为你保留。" : "点开成语，加入想记住的词。";
            bookList.SetCount(notebookItems.Count, false);
            notebookRecovery.gameObject.SetActive(notebook == null || store != null && store.NeedsRecovery);
            IdiomUi.Caption(notebookRecovery, notebook == null ? "保留旧档，重新创建" : "保留旧档，保存当前生词本");
            layoutHeight = -1;
            Layout();
        }

        private IdiomNotebookItem FindNotebookItem(string id)
        {
            if (notebook != null && notebook.Entries != null)
                foreach (var item in notebook.Entries) if (item.Id == id) return item;
            return null;
        }

        private IdiomNotebookData CopyNotebook()
        {
            return new IdiomNotebookData { Entries = new List<IdiomNotebookItem>(notebook.Entries) };
        }

        private bool SaveNotebook(IdiomNotebookData proposed, Action updateLearning)
        {
            // Persist notebook, learner and all four games together; failed edits
            // roll back the same profile instance shared by every live game.
            CaptureSave();
            var previousProfile = learning.GetSnapshot();
            updateLearning();
            data.Profile = learning.GetSnapshot();
            data.Notebook = proposed;
            string error;
            if (!store.TrySave(data, out error))
            {
                learning.ReplaceSnapshot(previousProfile);
                data.Profile = previousProfile;
                data.Notebook = notebook;
                notebookError = error ?? "生词本暂未保存，请重试。"; setStatus(notebookError);
                if (dialogNotice != null) { dialogNotice.text = notebookError; dialogNotice.color = UiFactory.Hex("9B5143"); }
                if (notebookVisible) RefreshNotebook();
                Debug.LogWarning("XanaduYonder idiom notebook save failed: " + error);
                return false;
            }
            notebook = proposed; notebookError = null; saveError = null; saveFailed = false; draftDirty = false;
            RebuildNotebookItems();
            if (notebookVisible) RefreshNotebook();
            setStatus(saveError ?? "生词本已保存。");
            return true;
        }

        private void ConfirmNotebookRecovery(Action back)
        {
            if (store == null) return;
            if (data == null) ConfirmGameRecovery();
            else ConfirmCurrentGameRecoveryWithReturn(back);
        }
    }
}
