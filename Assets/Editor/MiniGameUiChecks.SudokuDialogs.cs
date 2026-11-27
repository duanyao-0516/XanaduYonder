using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Sudoku;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Sudoku;

namespace XanaduYonder.EditorTools
{
    public static partial class MiniGameUiChecks
    {
        private static void CheckSudokuDialogBranches(Fixture f, SudokuPage page, SudokuPuzzle puzzle)
        {
            GameObject Dialog() => Get<GameObject>(page, "dialog");
            void Capture(string slug, string expectedTitle)
            {
                Require(Dialog() != null && Dialog().GetComponentsInChildren<Text>().Any(label => label.text == expectedTitle),
                    "Sudoku renders the production dialog " + expectedTitle);
                var panel = (RectTransform)Dialog().transform.Find("Sudoku dialog");
                var actions = panel.GetComponentsInChildren<Button>().Select(button => (RectTransform)button.transform)
                    .OrderByDescending(action => action.anchoredPosition.y).ToArray();
                var body = (RectTransform)panel.Find("Reading area");
                float bottomInset = panel.rect.height + actions.Last().anchoredPosition.y - actions.Last().rect.height;
                float topInset = -panel.GetComponentInChildren<Text>().rectTransform.anchoredPosition.y;
                Require(Mathf.Abs(topInset - bottomInset) < .5f && bottomInset >= 24 && bottomInset <= 36,
                    expectedTitle + " reserves matching top and bottom margins around its actual actions");
                float bodyToAction = -actions[0].anchoredPosition.y + body.anchoredPosition.y - body.rect.height;
                Require(bodyToAction >= 16 && bodyToAction <= 32, expectedTitle + " places its first action next to the reading area");
                var scroll = body.GetComponent<ScrollRect>();
                if (slug == "rules-selection") Require(scroll != null && scroll.content.rect.height > body.rect.height,
                    "The full Sudoku rules remain scrollable above their action");
                if (scroll == null) Require(Mathf.Abs(body.rect.height - Mathf.Ceil(body.GetComponentInChildren<Text>().preferredHeight)) < 1,
                    expectedTitle + " allocates only the measured short reading height");
                f.Capture("sudoku-dialog-" + slug);
            }
            void Click(string caption) => f.Click((RectTransform)FindCaption(Dialog().transform, caption).transform);
            void ClickToolsHint() => f.Click((RectTransform)Get<Button>(page, "hintButton").transform);
            SudokuSession StartFresh()
            {
                Invoke(page, "StartLevel", puzzle, true);
                return Get<SudokuSession>(page, "session");
            }

            var session = StartFresh();
            var game = Get<GameObject>(page, "gameRoot");
            f.Click((RectTransform)Icon(game.transform, UiIconKind.Restart).transform);
            Capture("restart-playing", "重新开始这一局？");
            Click("取消");
            f.Click((RectTransform)Get<Button>(page, "pauseButton").transform);
            Capture("pause", "已暂停");
            Click("重开");
            Capture("restart-paused", "重新开始这一局？");
            Click("取消");
            Require(session.Paused, "Cancel from paused restart preserves the paused session");
            Click("继续");

            var profile = Get<SudokuProfile>(page, "profile");
            profile.Capture(session.Export());
            Invoke(page, "ReturnToSelection");
            var selection = Get<GameObject>(page, "selectionRoot");
            f.Click((RectTransform)Icon(selection.transform, UiIconKind.Book).transform);
            Capture("rules-selection", "数独玩法");
            Click("返回关卡");
            f.Click((RectTransform)Get<Button[]>(page, "levelButtons")[1].transform);
            Capture("replace-level", "替换这个难度的对局？");
            Click("取消");
            Require(profile.GetSlot(puzzle.Difficulty).Game.Puzzle.Id == puzzle.Id, "Cancelling replacement preserves the stored attempt");

            session = StartFresh();
            int cell = Array.FindIndex(puzzle.Givens, digit => digit == 0);
            Invoke(page, "Pick", cell);
            Invoke(page, "Number", puzzle.Solution[cell] % 9 + 1);
            ClickToolsHint();
            Capture("hint-error-cell", "先处理红色数字");
            Click("知道了");

            session = StartFresh();
            ClickToolsHint();
            Capture("hint-request", "查看一步解题帮助");
            bool sawLoading = false;
            void CaptureLoading(Transform overlay, bool visible)
            {
                if (!visible || overlay.gameObject != Dialog() ||
                    !overlay.GetComponentsInChildren<Text>().Any(label => label.text == "正在推导下一步")) return;
                // A fast solver can finish before StartCoroutine first yields. Observe the complete
                // production loading card when the overlay binds its input, before starting that coroutine.
                Capture("hint-computing", "正在推导下一步");
                sawLoading = true;
            }
            DialogFocusScope.VisibilityChanged += CaptureLoading;
            try { Click("查看提示"); }
            finally { DialogFocusScope.VisibilityChanged -= CaptureLoading; }
            Require(sawLoading, "The solver shows its cancellable loading dialog");
            Invoke(page, "CancelHint");
            Invoke(page, "CloseDialog");

            session = StartFresh();
            var failedRequest = session.BeginHint(-1);
            var failure = Task.FromException<SudokuHint>(new InvalidOperationException("Editor test of failed hint computation"));
            var failed = (IEnumerator)Invoke(page, "WaitForHint", failure, session, failedRequest, new CancellationTokenSource());
            Require(!failed.MoveNext(), "The completed failure follows the production solver failure path");
            Capture("hint-failure", "暂时未能生成提示");
            Click("知道了");
            Require(session.Game.HintsRemaining == 3, "Solver failure leaves the hint budget intact");

            for (int use = 0; use < 3; use++)
            {
                bool direct = use == 1;
                var request = session.BeginHint(-1);
                Require(request != null, "A fresh board revision accepts a new hint");
                var hint = SudokuHintEngine.Find(request.Puzzle, request.Values, request.PreferredCell,
                    CancellationToken.None, direct ? 0 : -1);
                Require(hint != null && hint.IsDirect == direct && session.AcceptHint(request, hint),
                    "The Sudoku engine and session accept the requested hint presentation");
                Invoke(page, "Refresh");
                ClickToolsHint();
                if (use < 2)
                {
                    string kind = direct ? "direct" : "logical";
                    Capture("hint-" + kind, "一步解题提示");
                    Click(direct ? "直接揭晓这一格" : "揭晓");
                    Capture("hint-reveal-" + kind, "显示这一格的答案？");
                    Click("揭晓");
                    Capture("hint-revealed-" + kind, "一步解题提示");
                }
                Click("填入这一格");
                Require(session.Game.IsCorrect(hint.Cell) && session.Game.HintsRemaining == 2 - use,
                    "Applying an accepted hint fills its cell without a second charge");
            }
            ClickToolsHint();
            Capture("hint-exhausted", "提示已用完");
            Click("继续思考");
        }
    }
}
