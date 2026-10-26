using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Sheep;

namespace XanaduYonder.Runtime.Sheep
{
    internal sealed class SheepPage : MonoBehaviour
    {
        private SheepGame game;
        private SheepSaveStore store;
        private SheepSaveData data;
        private SheepBoardView board;
        private GameFeedback feedback;
        private Func<bool> shellAcceptsInput, soundEnabled, hapticsEnabled;
        private Action<string> setStatus;
        private Action<string, string, string, Action, string, Action> showDialog;
        private Action returnLobby;
        private Text levelText, progress, hint, resultTitle, resultMessage;
        private Button removeButton, undoButton, shuffleButton, resultPrimary;
        private Text removeLabel, undoLabel, shuffleLabel, resultPrimaryLabel;
        private GameObject resultPanel;
        private bool visible, saveFailed, animating;
        private string warning;
        public event Action Changed;
        public long CompletedRuns => data == null ? 0 : data.CompletedRuns;
        public string SaveError => saveFailed ? warning : null;

        public void Initialize(RectTransform root, Action lobby, Action settings, Func<bool> acceptsInput,
            Func<bool> sound, Func<bool> haptics, GameFeedback commonFeedback, Action<string> status,
            Action<string, string, string, Action, string, Action> dialog)
        {
            returnLobby = lobby; shellAcceptsInput = acceptsInput;
            soundEnabled = sound; hapticsEnabled = haptics; feedback = commonFeedback;
            setStatus = status; showDialog = dialog;
            store = new SheepSaveStore(Application.persistentDataPath);
            data = store.Load(out warning);
            if (data == null) data = new SheepSaveData { Game = NewGame(1).GetSnapshot() };
            game = new SheepGame(data.Game);

            var nav = UiFactory.Band(root, "Sheep navigation", 0, 92, 0);
            UiFactory.Button(nav, "‹  大厅", 56, 34, 150, 58, lobby);
            UiFactory.Label(nav, "XANADU / YONDER", 226, 34, 278, 58, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Button(nav, "设置", 552, 34, 112, 58, settings);
            var heading = UiFactory.Band(root, "Sheep heading", 110, 104, .04f);
            UiFactory.Label(heading, "旸了个旸", 56, 0, 440, 88, 60, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(heading, "03 / 层层三消", 464, 24, 200, 42, 20, UiFactory.Muted, TextAnchor.MiddleRight);
            var stats = UiFactory.Band(root, "Sheep progress", 214, 44, .12f);
            levelText = UiFactory.Label(stats, "", 56, 0, 300, 44, 25, UiFactory.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            progress = UiFactory.Label(stats, "", 340, 0, 324, 44, 21, UiFactory.Muted, TextAnchor.MiddleRight);
            var stageBand = UiFactory.Band(root, "Sheep board", 276, 540, .30f);
            var stage = UiFactory.Panel(stageBand, "Layered tile board", 32, 0, 656, 540, UiFactory.Hex("E9EEDB")).rectTransform;
            var reserveBand = UiFactory.Band(root, "Sheep reserve", 832, 110, .62f);
            var reserve = UiFactory.Rect(reserveBand, "Move-out reserve", 56, 0, 608, 110);
            var trayBand = UiFactory.Band(root, "Sheep tray", 958, 126, .78f);
            var tray = UiFactory.Panel(trayBand, "Seven-slot tray", 48, 0, 624, 124, UiFactory.Hex("E1E8CE")).rectTransform;
            board = new SheepBoardView(root, stage, tray, reserve, Pick);

            var tools = UiFactory.Band(root, "Sheep tools", 1104, 76, .92f);
            removeButton = UiFactory.Button(tools, "移出 ×1", 56, 0, 192, 70, () => UseTool(SheepTool.RemoveThree));
            undoButton = UiFactory.Button(tools, "撤回 ×1", 264, 0, 192, 70, () => UseTool(SheepTool.Undo));
            shuffleButton = UiFactory.Button(tools, "洗牌 ×1", 472, 0, 192, 70, () => UseTool(SheepTool.Shuffle));
            removeLabel = removeButton.GetComponentInChildren<Text>();
            undoLabel = undoButton.GetComponentInChildren<Text>();
            shuffleLabel = shuffleButton.GetComponentInChildren<Text>();
            var actions = UiFactory.Band(root, "Sheep actions", 1194, 46, 1);
            UiFactory.Button(actions, "重开本关", 56, 0, 190, 46, ConfirmRestart);
            UiFactory.Button(actions, "玩法", 534, 0, 130, 46, ShowHelp);
            hint = UiFactory.Label(actions, "只取未被盖住的牌", 252, 0, 278, 46, 20, UiFactory.Muted, TextAnchor.MiddleCenter);

            // Result controls occupy the board only. Available rescue tools and reserve stay reachable below.
            var resultBand = UiFactory.Band(root, "Sheep result", 276, 540, .30f);
            resultPanel = resultBand.gameObject;
            var panel = UiFactory.Panel(resultBand, "Round result", 88, 78, 544, 390, UiFactory.Paper);
            panel.raycastTarget = true;
            resultTitle = UiFactory.Label(panel.transform, "", 24, 24, 496, 64, 36, UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            resultMessage = UiFactory.Label(panel.transform, "", 32, 100, 480, 94, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
            resultPrimary = UiFactory.Button(panel.transform, "", 32, 210, 480, 68, ResultAction, true);
            resultPrimaryLabel = resultPrimary.GetComponentInChildren<Text>();
            UiFactory.Button(panel.transform, "重开本关", 32, 300, 232, 60, RestartLevel);
            UiFactory.Button(panel.transform, "返回大厅", 280, 300, 232, 60, lobby);
            Refresh();
            Debug.Log("XanaduYonder sheep ready; save=" + store.SavePath);
        }

        private static SheepGame NewGame(int level)
        {
            var seed = unchecked((uint)DateTime.UtcNow.Ticks ^ (uint)Environment.TickCount);
            return new SheepGame(level, seed == 0 ? 1u : seed);
        }

        public void Enter()
        {
            visible = true; gameObject.SetActive(true);
            Refresh(); Save();
            setStatus(warning ?? "三张相同图案会自动消除，留意卡槽余量。");
            CheckEndState();
        }

        public void Leave()
        {
            Suspend(); Save(); visible = false; gameObject.SetActive(false);
        }

        public void Suspend()
        {
            StopAllCoroutines(); animating = false;
            if (game != null && board != null) board.FinishAnimations(game);
            if (game != null && resultPanel != null) Refresh();
        }

        private bool AcceptsInput => visible && shellAcceptsInput();

        private void Pick(int id)
        {
            if (!AcceptsInput || !game.CanPick(id)) return;
            // Commit immediately. A fast next tap finishes the visual transition instead of being discarded.
            Suspend();
            var before = game.GetSnapshot();
            var move = game.Pick(id);
            if (!move.Changed) return;
            Save(); RefreshLabels();
            feedback.Play(move.MatchedIds.Length > 0, soundEnabled(), hapticsEnabled());
            animating = true;
            StartCoroutine(AnimatePick(before, move));
        }

        private IEnumerator AnimatePick(SheepSnapshot before, SheepMove move)
        {
            yield return board.AnimatePick(game, before, move);
            animating = false;
            Refresh();
            CheckEndState();
        }

        private void UseTool(SheepTool tool)
        {
            if (!AcceptsInput || !game.CanUseTool(tool)) return;
            Suspend();
            if (!game.UseTool(tool)) return;
            Save(); Refresh();
            feedback.Play(false, soundEnabled(), false);
            setStatus(warning ?? (tool == SheepTool.RemoveThree || tool == SheepTool.Revive
                ? "前三张已移出，点击暂存牌可放回卡槽。"
                : tool == SheepTool.Undo ? "上一张牌已退回原位。" : "场上图案已重新排列。"));
            CheckEndState();
        }

        private void Refresh()
        {
            board.Render(game);
            RefreshLabels();
            RenderResult();
        }

        private void RefreshLabels()
        {
            var snapshot = game.GetSnapshot();
            levelText.text = snapshot.Level == 1 ? "第 1 关 · 热身" : "第 2 关 · 挑战";
            progress.text = "已消 " + game.ClearedCount + "  /  余 " + game.RemainingCount;
            UpdateTool(removeButton, removeLabel, "移出", snapshot.RemoveUsed, game.CanUseTool(SheepTool.RemoveThree));
            UpdateTool(undoButton, undoLabel, "撤回", snapshot.UndoUsed, game.CanUseTool(SheepTool.Undo));
            UpdateTool(shuffleButton, shuffleLabel, "洗牌", snapshot.ShuffleUsed, game.CanUseTool(SheepTool.Shuffle));
            hint.text = snapshot.Tray.Length >= 6 ? "卡槽只剩 1 格！" : "只取未被盖住的牌";
            if (game.Phase == SheepPhase.Lost) hint.text = "可用道具解困";
            else if (game.Phase == SheepPhase.Won) hint.text = "全部清空！";
            Changed?.Invoke();
        }

        private static void UpdateTool(Button button, Text label, string title, bool used, bool canUse)
        {
            button.interactable = canUse;
            label.text = title + (used ? " · 已用" : " ×1");
            label.color = canUse ? UiFactory.Ink : UiFactory.Muted;
        }

        private void RenderResult()
        {
            var show = visible && !animating && game.Phase != SheepPhase.Playing;
            resultPanel.SetActive(show);
            if (!show) return;
            var snapshot = game.GetSnapshot();
            var won = game.Phase == SheepPhase.Won;
            resultTitle.text = won ? (snapshot.Level == 1 ? "热身完成！" : "旸了个旸，过关！") : "卡槽满了，再想一想";
            resultMessage.text = won ? (snapshot.Level == 1 ? "接下来，挑战更多图案与叠层。" : "这一轮的牌已经全部消除。\n累计通关 " + data.CompletedRuns + " 次")
                : (game.CanUseTool(SheepTool.Revive) ? "免费复活可移出前三张。\n下方尚未使用的移出、撤回也可解困。" : "本关的复活机会已用完。\n仍可尝试下方可用道具，或重新挑战。");
            resultPrimary.interactable = won || game.CanUseTool(SheepTool.Revive);
            resultPrimaryLabel.text = won ? (snapshot.Level == 1 ? "进入第 2 关  →" : "再闯一轮")
                : (snapshot.ReviveUsed ? "复活已使用" : "免费复活 ×1");
        }

        public void CheckEndState()
        {
            if (!visible || animating || !shellAcceptsInput()) return;
            RenderResult();
        }

        private void ResultAction()
        {
            if (!AcceptsInput) return;
            if (game.Phase == SheepPhase.Lost) { UseTool(SheepTool.Revive); return; }
            if (game.Phase != SheepPhase.Won) return;
            StartLevel(game.GetSnapshot().Level == 1 ? 2 : 1);
        }

        private void StartLevel(int level)
        {
            Suspend(); game = NewGame(level); data.WinRecorded = false;
            Save(); Refresh();
            setStatus(warning ?? (level == 1 ? "从热身开始，找齐三张相同图案。" : "挑战开始：先观察叠层，再安排卡槽。"));
        }

        private void RestartLevel() { if (AcceptsInput) StartLevel(game.GetSnapshot().Level); }

        private void ConfirmRestart()
        {
            if (!AcceptsInput) return;
            showDialog("重新开始本关？", "本关棋盘和道具将重置，\n累计通关次数会保留。", "重新开始", RestartLevel, "继续本关", CheckEndState);
        }

        private void ShowHelp()
        {
            if (!AcceptsInput) return;
            showDialog("旸了个旸 · 玩法", "只取未被盖住的牌，三个相同即消除。\n卡槽装满 7 张失败，清空全部牌过关。\n移出前三张；撤回最近未消除的一张。\n洗牌重排场上图案，道具每关各一次。\n免费复活一次，第2关全清记一次通关。", "知道了", CheckEndState, null, null);
        }

        public void Save()
        {
            if (store == null || game == null) return;
            data.Game = game.GetSnapshot();
            if (data.Game.Level == 2 && game.Phase == SheepPhase.Won && !data.WinRecorded)
            {
                if (data.CompletedRuns < long.MaxValue) data.CompletedRuns++;
                data.WinRecorded = true;
                Changed?.Invoke();
            }
            string error;
            if (!store.TrySave(data, out error))
            {
                saveFailed = true;
                warning = "旸了个旸进度暂未保存，请检查剩余空间。";
                Debug.LogWarning("XanaduYonder sheep save failed: " + error);
            }
            else if (saveFailed)
            {
                saveFailed = false; warning = null;
                if (visible) setStatus("旸了个旸进度已保存。");
            }
            if (visible && warning != null) setStatus(warning);
        }

        private void OnDisable() { if (game != null) Suspend(); }
    }
}
