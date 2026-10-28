using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Sheep;

namespace XanaduYonder.Runtime.Sheep
{
    internal sealed partial class SheepPage : MonoBehaviour
    {
        private SheepSession session;
        private SheepGame game => session == null ? null : session.Game;
        private SheepSaveData data => session.Data;
        private SheepSaveStore store;
        private SheepBoardView board;
        private GameFeedback feedback;
        private Func<bool> shellAcceptsInput, soundEnabled, hapticsEnabled;
        private Action<string> setStatus;
        private Action<string, string, string, Action, string, Action> showDialog;
        private Func<string, float, Action, RectTransform> openModal;
        private Action closeModal, returnLobby;
        private Action<Action> commonSettings;
        private Text levelText, progress, hint, resultTitle, resultMessage;
        private Button removeButton, undoButton, shuffleButton, resultPrimary;
        private Text removeLabel, undoLabel, shuffleLabel, resultPrimaryLabel;
        private GameObject resultPanel, playArea;
        private bool visible, saveFailed, animating;
        private string warning;
        public event Action Changed;
        public long CompletedRuns => session == null ? 0 : data.CompletedRuns;
        public long EndlessBest => session == null ? 0 : data.EndlessBest;
        public string SaveError => saveFailed ? warning : null;

        public void Initialize(RectTransform root, Action lobby, Action<Action> settings, Func<bool> acceptsInput,
            Func<bool> sound, Func<bool> haptics, GameFeedback commonFeedback, Action<string> status,
            Action<string, string, string, Action, string, Action> dialog,
            Func<string, float, Action, RectTransform> modal, Action dismiss)
        {
            returnLobby = lobby; commonSettings = settings; shellAcceptsInput = acceptsInput;
            soundEnabled = sound; hapticsEnabled = haptics; feedback = commonFeedback;
            setStatus = status; showDialog = dialog; openModal = modal; closeModal = dismiss;
            store = new SheepSaveStore(Application.persistentDataPath);
            session = new SheepSession(store.Load(out warning) ?? new SheepSaveData());
            BuildLayout(root);
            Refresh();
            Debug.Log("XanaduYonder sheep ready; save=" + store.SavePath);
        }

        private static uint NewSeed()
        {
            var seed = unchecked((uint)DateTime.UtcNow.Ticks ^ (uint)Environment.TickCount);
            return seed == 0 ? 1u : seed;
        }

        public void Enter()
        {
            visible = true; gameObject.SetActive(true);
            Refresh(); ShowModes();
            setStatus(warning ?? "选择第二关挑战，或按消除数量计分的无尽模式。");
        }

        public void Leave() { Suspend(); Save(); visible = false; gameObject.SetActive(false); }

        public void Suspend()
        {
            StopAllCoroutines(); animating = false;
            if (game != null && board != null) board.FinishAnimations(game);
            if (resultPanel != null) Refresh();
        }

        private bool AcceptsInput => visible && shellAcceptsInput();

        private void Pick(int id)
        {
            if (!AcceptsInput || game == null || !game.CanPick(id)) return;
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
            animating = false; Refresh(); CheckEndState();
        }

        private void UseTool(SheepTool tool)
        {
            if (!AcceptsInput || game == null || !game.CanUseTool(tool)) return;
            Suspend();
            var moved = Math.Min(3, game.GetSnapshot().Tray.Length);
            if (!game.UseTool(tool)) return;
            Save(); Refresh(); feedback.Play(false, soundEnabled(), false);
            setStatus(warning ?? (tool == SheepTool.RemoveThree || tool == SheepTool.Revive
                ? "已移出 " + moved + " 张，点击暂存牌可放回卡槽。"
                : tool == SheepTool.Undo ? "上一张牌已退回原位。" : "场上图案已重新排列。"));
            CheckEndState();
        }

        private void Refresh()
        {
            playArea.SetActive(game != null);
            if (game == null) { resultPanel.SetActive(false); return; }
            board.Render(game); RefreshLabels(); RenderResult();
        }

        private void RefreshLabels()
        {
            if (game == null) return;
            var snapshot = game.GetSnapshot();
            levelText.text = snapshot.Mode == SheepMode.Tutorial ? "第 1 关 · 热身" : snapshot.Mode == SheepMode.Endless
                ? "无尽 · 第 " + snapshot.Wave + " 波" : "第 2 关 · " + DifficultyName(snapshot.Difficulty);
            progress.text = snapshot.Mode == SheepMode.Endless ? "已消除 " + snapshot.Score + " 个" : "已消 " + game.ClearedCount + " / 余 " + game.RemainingCount;
            UpdateTool(removeButton, removeLabel, "移出", SheepTool.RemoveThree);
            UpdateTool(undoButton, undoLabel, "撤回", SheepTool.Undo);
            UpdateTool(shuffleButton, shuffleLabel, "洗牌", SheepTool.Shuffle);
            hint.text = snapshot.Tray.Length >= 6 ? "卡槽将满，留意下一组三消" : "只取未被盖住的牌";
            if (session.IsCustomRun) hint.text += snapshot.Mode == SheepMode.Endless ? " · 自定义，不计最高" : " · 自定义规则";
            if (game.Phase == SheepPhase.Lost) hint.text = (HasRescue ? "可以尝试道具或复活" : "本局已结束，可重新开始") + (session.IsCustomRun ? " · 自定义规则" : "");
            Changed?.Invoke();
        }

        private void UpdateTool(Button button, Text label, string title, SheepTool tool)
        {
            var canUse = game.CanUseTool(tool);
            button.interactable = canUse;
            label.text = title + "  " + game.GetToolRemaining(tool);
            label.color = canUse ? UiFactory.Ink : UiFactory.Muted;
        }

        private bool HasRescue => game.CanUseTool(SheepTool.RemoveThree) || game.CanUseTool(SheepTool.Undo) || game.CanUseTool(SheepTool.Revive);

        private void RenderResult()
        {
            if (game == null) { resultPanel.SetActive(false); return; }
            var snapshot = game.GetSnapshot();
            var won = game.Phase == SheepPhase.Won;
            var show = visible && !animating && game.Phase != SheepPhase.Playing && !(won && snapshot.Mode == SheepMode.Endless);
            resultPanel.SetActive(show);
            if (show) resultPanel.transform.SetAsLastSibling();
            if (!show) return;
            resultTitle.text = won ? (snapshot.Mode == SheepMode.Tutorial ? "热身完成！" : "旸了个旸，过关！") : "卡槽满了，再想一想";
            resultMessage.text = won ? (snapshot.Mode == SheepMode.Tutorial ? "接下来，进入第二关挑战。" : "这一轮的牌已经全部消除。\n累计通关 " + data.CompletedRuns + " 次")
                : (snapshot.Mode == SheepMode.Endless ? "本局已消除 " + snapshot.Score + " 个\n" : "")
                + (game.CanUseTool(SheepTool.Revive) ? "免费复活可移出前三张。" : snapshot.ReviveUsed ? "本局复活已使用。" : "移出区空间不足以复活。")
                + (game.CanUseTool(SheepTool.RemoveThree) || game.CanUseTool(SheepTool.Undo) ? "\n下方可用道具仍能解困。" : HasRescue ? "\n点击下方按钮继续。" : "\n重新开始，再挑战一局。");
            resultPrimary.interactable = won || game.CanUseTool(SheepTool.Revive);
            resultPrimaryLabel.text = won ? (snapshot.Mode == SheepMode.Tutorial ? (data.Challenge != null && new SheepGame(data.Challenge).Phase != SheepPhase.Won ? "继续第 2 关" : "挑战第 2 关") : "再闯一轮")
                : snapshot.ReviveUsed ? "复活已使用" : "免费复活 ×1";
        }

        public void CheckEndState()
        {
            if (!visible || animating || !shellAcceptsInput() || game == null) return;
            if (session.AdvanceWave())
            {
                Save(); Refresh(); setStatus(warning ?? "进入第 " + game.GetSnapshot().Wave + " 波 · 道具余量沿用本局");
            }
            RenderResult();
        }

        private void ResultAction()
        {
            if (!AcceptsInput || game == null) return;
            if (game.Phase == SheepPhase.Lost) { UseTool(SheepTool.Revive); return; }
            if (game.Phase == SheepPhase.Won)
            {
                if (game.GetSnapshot().Mode == SheepMode.Tutorial && data.Challenge != null && new SheepGame(data.Challenge).Phase != SheepPhase.Won)
                    Resume(SheepMode.Challenge);
                else ChooseChallenge();
            }
        }

        private void Begin(SheepMode mode, SheepDifficulty difficulty)
        {
            closeModal(); Suspend(); session.Start(mode, difficulty, NewSeed());
            Save(); Refresh(); setStatus(warning ?? (mode == SheepMode.Endless ? "消除一个计 1 分，道具与复活次数整局共用。" : "先观察叠层，再安排卡槽。"));
        }

        private void Resume(SheepMode mode)
        {
            closeModal(); Suspend();
            if (!session.Resume(mode)) return;
            Save(); Refresh(); CheckEndState();
            setStatus(warning ?? "已继续原有对局，难度和道具余量保持不变。");
        }

        private void ConfirmRestart()
        {
            if (!AcceptsInput || game == null) return;
            var mode = game.GetSnapshot().Mode;
            showDialog(mode == SheepMode.Endless ? "重新开始无尽挑战？" : "重新开始本关？",
                "当前模式的进度将清空，\n新局使用设置中的道具次数。", "重新开始", () => {
                    if (mode == SheepMode.Challenge) ChooseChallenge(); else Begin(mode, SheepDifficulty.Normal);
                }, "继续本局", CheckEndState);
        }

        private void ShowHelp()
        {
            if (!AcceptsInput) return;
            Action finish = () => { closeModal(); CheckEndState(); };
            var panel = openModal("旸了个旸 · 玩法", 652, finish);
            UiFactory.Label(panel, "只取未被盖住的牌，三个相同即消除。\n卡槽满 7 张失败，移出区最多暂存 6 张。\n移出：把卡槽前三张放到移出区。\n撤回：退回最近一张尚未消除的牌。\n洗牌：重排棋盘图案，不影响卡槽。\n每局可免费复活一次，移出前三张。\n无尽每消除一个计 1 分，清空后换波。\n道具整局共用，设置只影响新开对局。",
                32, 116, 532, 376, 23, UiFactory.Muted, TextAnchor.MiddleLeft);
            UiFactory.Button(panel, "知道了", 32, 550, 532, 64, finish, true);
        }

        public void Save()
        {
            if (store == null || session == null) return;
            session.Capture();
            if (!store.TrySave(data, out var error))
            {
                saveFailed = true; warning = "旸了个旸进度暂未保存，请检查剩余空间。";
                Debug.LogWarning("XanaduYonder sheep save failed: " + error);
            }
            else if (saveFailed) { saveFailed = false; warning = null; }
            if (visible && warning != null) setStatus(warning);
            Changed?.Invoke();
        }

        private void LateUpdate()
        {
            if (!visible || game == null || animating) return;
            board.RefreshLayout(game);
            if (resultPanel.activeSelf) resultPanel.transform.SetAsLastSibling();
        }
        private void OnDisable() { if (session != null) Suspend(); }
    }
}
