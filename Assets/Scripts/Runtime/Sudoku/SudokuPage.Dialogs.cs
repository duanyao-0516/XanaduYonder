using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Sudoku;

namespace XanaduYonder.Runtime.Sudoku
{
    internal sealed partial class SudokuPage
    {
        private RectTransform ShowDialog(string title, string message, string primary, Action primaryAction,
            string secondary = null, Action secondaryAction = null, bool opaque = false)
        {
            CloseDialog();
            // Disable navigation as well as pointer input behind the modal.
            gameInput.interactable = false;
            if (selectionInput == null) selectionInput = selectionRoot.AddComponent<CanvasGroup>();
            selectionInput.interactable = false;
            var overlay = UiFactory.Panel(root, "Sudoku dialog overlay", 0, 0, 720, 1320,
                opaque ? UiFactory.Paper : new Color(.06f, .14f, .1f, .35f), false);
            UiFactory.Stretch(overlay.rectTransform); overlay.raycastTarget = true; dialog = overlay.gameObject;
            float height = Mathf.Clamp(300 + (message.Length / 21 + message.Split('\n').Length) * 34, 480, 860);
            var panel = UiFactory.Panel(overlay.transform, "Sudoku dialog", 0, 0, 596, height, UiFactory.Paper).rectTransform;
            UiFactory.Center(panel);
            UiFactory.Label(panel, title, 32, 30, 532, 62, 32, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            var viewport = UiFactory.Rect(panel, "Explanation viewport", 32, 110, 532, height - 300);
            viewport.gameObject.AddComponent<RectMask2D>();
            var surface = viewport.gameObject.AddComponent<Image>(); surface.color = Color.clear; surface.raycastTarget = true;
            var body = UiFactory.Label(viewport, message, 0, 0, 532, height - 300, 24, UiFactory.Muted, TextAnchor.UpperLeft);
            body.rectTransform.sizeDelta = new Vector2(532, Mathf.Max(height - 300, body.preferredHeight + 12));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = body.rectTransform;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            UiFactory.Button(panel, primary, 32, height - 170, 532, 66, () => { CloseDialog(); primaryAction?.Invoke(); }, true);
            if (secondary != null) UiFactory.Button(panel, secondary, 32, height - 88, 532, 58, () => { CloseDialog(); secondaryAction?.Invoke(); });
            dismissDialog = () => { CloseDialog(); if (secondaryAction != null) secondaryAction(); else primaryAction?.Invoke(); };
            return panel;
        }
        private void CloseDialog()
        {
            if (dialog != null) { dialog.SetActive(false); Destroy(dialog); dialog = null; }
            dismissDialog = null;
            if (gameInput != null) gameInput.interactable = true;
            if (selectionInput != null) selectionInput.interactable = true;
        }
        public void Back()
        {
            if (difficultyDropdown.IsExpanded) { difficultyDropdown.Hide(); return; }
            if (dialog != null) { dismissDialog?.Invoke(); return; }
            if (choosing) returnLobby(); else ReturnToSelection();
        }
        private void Pause()
        {
            if (!visible || choosing) return;
            session.Pause(); CancelHint(); ClearFlash(); Save(); ShowPause();
        }
        private void ShowPause()
        {
            var panel = ShowDialog("已暂停", "盘面已被迷雾遮住，计时已停止。\n\n准备好后，点击继续。", "继续", () => {
                if (!session.Foreground) { ShowPause(); return; }
                session.Resume(); Refresh(); CheckEndState();
            }, "返回关卡", ReturnToSelection, true);
            UiFactory.Button(panel, "重开", 440, 36, 124, 48, ConfirmRestart);
        }
        private void ConfirmRestart()
        {
            if (!visible || choosing) return;
            CancelHint();
            bool paused = session.Paused;
            ShowDialog("重新开始这一局？", "回到本题最初的盘面，清空填写与草稿。\n\n错误、计时和三次提示重新开始；历史通关记录保留。", "重新开始", () => StartLevel(session.Game.Puzzle, true),
                "取消", () => { if (paused) ShowPause(); else CheckEndState(); }, paused);
        }
        private void ShowRules()
        {
            if (!visible || dialog != null || !acceptsInput() || !session.Foreground) return;
            difficultyDropdown.Hide();
            bool resume = !choosing && !session.Paused;
            if (!choosing) { session.Pause(); CancelHint(); ClearFlash(); Save(); }
            ShowDialog("数独玩法", "基础规则\n每行、每列及每个九宫格中，1–9 各出现一次。每道题都有唯一解，不包含特殊变体。\n\n进度与按钮\n顶部的两个数为已填写格数 / 剩余空格数；只统计可填写的格子，错误数字计入已填写，铅笔草稿不计入。数字按钮下方的数值表示该数字还需正确填写的个数。工具依次为撤回、橡皮、铅笔和铃铛提示；铅笔按钮变为深绿色时表示已开启，铃铛旁是剩余提示次数。\n\n填写与高亮\n普通模式先选格，再选数字。选中格子的行、列和宫会变深；点击已有数字，可查看相同的数字与候选。题目给出的数字不可修改，你填写的正确数字也能擦除、替换和撤回。\n\n铅笔草稿\n开启铅笔模式时输入小候选数字，再点一次即可取消。与行、列、宫已有数字冲突的候选无法写入，红色闪动三次但不计错误。填入正确数字后，会自动删除同行、同列、同宫中的对应候选。橡皮可清空该格的候选。\n\n快速输入\n打开快速模式后先选数字，再连续点空格；已有数字不会被覆盖。快速模式也可以配合铅笔使用。\n\n错误与提示\n正式填数出错会标红，累计 3 次结束本局。每局有 3 次提示，先定位并解释，必要时可以揭晓或填入一格；同一条提示重复查看不扣次。撤回不会退还错误或提示次数。\n\n难度与进度\n难度按解题需要的逻辑技巧划分，不按空格多少。每档 500 关，每个难度最多保存 1 场未完成对局；切换难度会保留其他对局。同一难度另开一关，会先确认替换。\n\n暂停与保存\n离开对局、切到后台或查看本页会停止计时。暂停时盘面被遮住；进度、草稿和已使用提示会自动保存。",
                choosing ? "返回关卡" : "返回对局", () => {
                    if (choosing) return;
                    if (!session.Foreground) { ShowPause(); return; }
                    if (resume) { session.Resume(); Refresh(); CheckEndState(); }
                    else ShowPause();
                });
        }
        public void CheckEndState()
        {
            if (!visible || choosing || session == null || dialog != null || !session.Foreground || session.Paused || resultDismissed) return;
            if (session.Game.Phase == SudokuPhase.Playing) return;
            CancelHint(); Save();
            var puzzle = session.Game.Puzzle;
            bool won = session.Game.Phase == SudokuPhase.Won;
            string message = Difficulties[puzzle.Difficulty] + " · 第 " + puzzle.Level + " 关\n用时 " + FormatTime(session.ElapsedSeconds) +
                "\n错误 " + session.Game.Errors + "/3  ·  已用提示 " + (3 - session.Game.HintsRemaining) + "/3\n\n" +
                (won ? "每个数字都找到了自己的位置。" : "已累计三次错误，本局结束。\n重新挑战会重置本局。 ");
            var panel = ShowDialog(won ? "恭喜通关" : "本局结束", message,
                won ? puzzle.Level < 500 ? "下一关" : "返回关卡" : "重新挑战", () => {
                    if (won && puzzle.Level == 500) ReturnToSelection();
                    else StartLevel(won ? catalog.Get(puzzle.Difficulty, puzzle.Level + 1) : puzzle, true);
                }, "查看盘面", () => { resultDismissed = true; Refresh(); });
            UiFactory.Button(panel, "关卡", 440, 36, 124, 48, ReturnToSelection);
            if (won) UiFactory.Button(panel, "重玩", 32, 36, 124, 48, () => StartLevel(puzzle, true));
        }

        private void OpenHint()
        {
            if (!Interactive) return;
            ClearFlash(); inspectionDigit = 0; Refresh();
            int error = session.FirstError();
            if (error >= 0)
            {
                session.Controls.SelectedCell = error; Refresh();
                ShowDialog("先处理红色数字", "请先用橡皮擦除盘面中的红色数字，再查看解题提示。\n\n这次提醒不消耗提示次数。", "知道了", () => { }); return;
            }
            if (session.CurrentHint != null) { ShowHintCard(); return; }
            if (session.Game.HintsRemaining == 0)
            { ShowDialog("提示已用完", "本局的三次提示已经使用完毕。\n可以继续用候选草稿推导，或重新挑战这一关。", "继续思考", () => { }); return; }
            ShowDialog("查看一步解题帮助", "首次展示这条帮助会使用 1 次提示。\n\n定位、解题解释和填入这一格合计一次；同一盘面重新查看不重复扣次。\n\n当前剩余 " + session.Game.HintsRemaining + " 次。",
                "查看提示", StartHint, "取消", () => { });
        }
        private void StartHint()
        {
            CancelHint();
            var owner = session; var request = owner.BeginHint(owner.Controls.SelectedCell);
            if (request == null) { Refresh(); return; }
            var cancellation = new CancellationTokenSource(); hintCancellation = cancellation;
            var token = cancellation.Token;
            var task = Task.Run(() => SudokuHintEngine.Find(request.Puzzle, request.Values, request.PreferredCell, token, 200), token);
            task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            ShowDialog("正在推导下一步", "正在分析当前盘面，稍等片刻。\n取消不会消耗提示次数。", "取消", () => { CancelHint(); Refresh(); });
            hintRoutine = StartCoroutine(WaitForHint(task, owner, request, cancellation));
        }
        private IEnumerator WaitForHint(Task<SudokuHint> task, SudokuSession owner, SudokuHintRequest request, CancellationTokenSource cancellation)
        {
            while (!task.IsCompleted) yield return null;
            hintRoutine = null;
            bool cancelled = cancellation.IsCancellationRequested;
            if (hintCancellation == cancellation) hintCancellation = null;
            cancellation.Dispose();
            if (cancelled || task.IsCanceled || owner != session || !visible || choosing) yield break;
            CloseDialog();
            if (task.IsFaulted)
            {
                Debug.LogWarning("Sudoku hint failed: " + task.Exception);
                ShowDialog("暂时未能生成提示", "本次没有消耗次数，请稍后重试。", "知道了", () => { }); yield break;
            }
            if (!owner.AcceptHint(request, task.Result)) { Refresh(); yield break; }
            Save(); Refresh(); ShowHintCard();
        }
        private void CancelHint()
        {
            session?.CancelHint();
            if (hintCancellation != null) { hintCancellation.Cancel(); hintCancellation.Dispose(); hintCancellation = null; }
            if (hintRoutine != null) { StopCoroutine(hintRoutine); hintRoutine = null; }
        }
        private void ShowHintCard()
        {
            var help = session.CurrentHint;
            if (help == null || !session.CanPlay) return;
            string location = "第 " + (help.Cell / 9 + 1) + " 行，第 " + (help.Cell % 9 + 1) + " 列";
            string text = "技巧：" + help.Technique + "\n\n" + help.Explanation;
            if (help.Revealed) text = location + " 可以填写 " + help.Digit + "。\n\n" + text;
            text += "\n\n本条帮助已计入使用次数；查看和应用不再扣次。";
            bool needReveal = help.IsDirect && !help.Revealed;
            Action reveal = () => ShowDialog("显示这一格的答案？", "将显示 " + location + " 的正确数字。\n不会额外消耗提示次数。", "揭晓", () => {
                session.RevealHint(); Save(); ShowHintCard();
            }, "返回解释", ShowHintCard);
            var panel = ShowDialog("一步解题提示", text, needReveal ? "直接揭晓这一格" : "填入这一格", () => {
                if (needReveal) reveal();
                else { inspectionDigit = 0; Move(session.ApplyHint()); }
            }, "继续思考", () => { Refresh(); });
            if (!help.Revealed && !needReveal) UiFactory.Button(panel, "揭晓", 440, 36, 124, 48, reveal);
            RenderBoard(help.Evidence);
        }
    }
}
