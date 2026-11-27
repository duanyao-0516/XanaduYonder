using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Feihualing;
using XanaduYonder.Runtime.Idiom;

namespace XanaduYonder.Runtime.Feihualing
{
    public sealed partial class FeihualingPage
    {
        private FeihualingNotebookStore notebookStore;
        private string notebookWarning;
        private IdiomVirtualList notebookList;
        private RectTransform notebookToast;
        private Text notebookToastText;
        private long notebookToastUntil;

        private bool CanHoldComputerLine => CanInteract && !KeyboardVisible && !input.isFocused &&
            !keyboard.JustClosed && Input.touchCount <= 1;

        private void BuildNotebookToast()
        {
            var surface = UiFactory.Panel(column, "Poetry notebook toast", 132, 112, 456, 56, UiFactory.Ink);
            notebookToast = surface.rectTransform;
            notebookToastText = IdiomUi.Label(notebookToast, "", 12, 0, 432, 56, 22, Color.white, TextAnchor.MiddleCenter);
            notebookToastText.name = "Poetry notebook feedback";
            notebookToast.gameObject.SetActive(false);
        }

        private void EnsureNotebookLoaded()
        {
            if (notebookStore != null) return;
            notebookStore = new FeihualingNotebookStore(Application.persistentDataPath);
            notebookStore.LoadNotebook(out notebookWarning);
        }

        private void SaveComputerPoem(PoetryWork work)
        {
            if (!CanHoldComputerLine) return;
            CollectPoem(work, out var message);
            notebookToastText.text = message;
            notebookToastUntil = Now + 1700;
            notebookToast.gameObject.SetActive(true);
        }

        private FeihualingNotebookAddResult CollectPoem(PoetryWork work, out string message)
        {
            EnsureNotebookLoaded();
            var result = notebookStore.Add(work, out message);
            if (result == FeihualingNotebookAddResult.Added && match != null && play.gameObject.activeSelf)
                history.SetCount(match.History.Count, false);
            return result;
        }

        private void OpenNotebook()
        {
            if (!CanInteract) return;
            SuspendForMenu();
            EnsureNotebookLoaded();
            ShowNotebook(0);
        }

        private void ShowNotebook(float scrollOffset)
        {
            var panel = OpenDialog("诗词收藏", DetailHeight());
            var entries = notebookStore.Entries;
            var bottom = panel.rect.height - 82;
            if (!string.IsNullOrEmpty(notebookWarning))
                IdiomUi.Label(panel, notebookWarning, ModalMargin, 107, ModalWidth, 64, 19, UiFactory.Muted);
            if (entries.Count == 0)
            {
                var message = string.IsNullOrEmpty(notebookWarning)
                    ? "还没有收藏。点开任意已答诗句的原诗，即可收藏整首。"
                    : "可以继续点开已答诗句查看原诗。";
                IdiomUi.Label(panel, message, ModalMargin, 183, ModalWidth, 142, 24, UiFactory.Muted, TextAnchor.MiddleCenter);
            }
            else
            {
                var top = string.IsNullOrEmpty(notebookWarning) ? 110 : 174;
                notebookList = new IdiomVirtualList(panel, "Scrollable poetry notebook", 108, BindNotebookRow, OpenNotebookEntry);
                IdiomUi.Place(notebookList.Viewport, ModalMargin, top, ModalWidth, bottom - top - 16);
                notebookList.SetCount(entries.Count, false);
                notebookList.SetOffset(scrollOffset);
            }
            DialogUi.ActionButton(panel, "关闭", ModalMargin, bottom, ModalWidth, 58, CloseDialog, false, 22);
        }

        private void BindNotebookRow(IdiomVirtualList.Row row, int index)
        {
            var entry = notebookStore.Entries[index];
            row.Actor.gameObject.SetActive(false);
            row.Note.gameObject.SetActive(false);
            row.Button.targetGraphic.color = Color.white;
            PlaceButton(row.Button, 0, 0, ModalWidth - 4, 96);
            row.Word.text = "《" + entry.Title + "》";
            row.Word.fontSize = 24;
            row.Word.verticalOverflow = VerticalWrapMode.Truncate;
            IdiomUi.Place(row.Word.rectTransform, 18, 6, ModalWidth - 42, 51);
            row.Pinyin.text = entry.Author;
            row.Pinyin.fontSize = 19;
            IdiomUi.Place(row.Pinyin.rectTransform, 18, 58, ModalWidth - 42, 30);
        }

        private void OpenNotebookEntry(int index)
        {
            if (!modalOpen || notebookList == null || index < 0 || index >= notebookStore.Entries.Count) return;
            var entry = notebookStore.Entries[index];
            var offset = notebookList.Offset;
            var work = new PoetryWork(entry.WorkId, entry.Author, entry.Title, entry.Body);
            OpenPoemDetail(work, (parent, width) => AddPoem(parent, work.Title, work.Author, work.Body, 0, width),
                () => ShowNotebook(offset));
        }
    }
}
