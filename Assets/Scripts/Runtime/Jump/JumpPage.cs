using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core.Jump;

namespace XanaduYonder.Runtime.Jump
{
    // Owns this game's session; the app shell owns navigation, modal presentation and settings.
    internal sealed class JumpPage : MonoBehaviour
    {
        private JumpGame game;
        private JumpSaveStore store;
        private JumpSaveData data;
        private JumpSceneView scene;
        private GameFeedback feedback;
        private readonly ChargeGesture gesture = new ChargeGesture();
        private Func<bool> shellAcceptsInput;
        private Func<bool> soundEnabled;
        private Func<bool> hapticsEnabled;
        private Action<string> setStatus;
        private Action<string, string, string, Action, string, Action> showDialog;
        private Action returnLobby;
        private Text score;
        private Text best;
        private Text hint;
        private Text combo;
        private Image chargeFill;
        private double chargeStarted;
        private bool animating;
        private bool visible;
        private bool saveFailed;
        private string warning;
        public long BestScore => data == null ? 0 : data.BestScore;
        public string SaveError => saveFailed ? warning : null;
        public event Action Changed;

        public void Initialize(RectTransform root, Action lobby, Action settings, Func<bool> acceptsInput,
            Func<bool> sound, Func<bool> haptics, GameFeedback commonFeedback, Action<string> status,
            Action<string, string, string, Action, string, Action> dialog)
        {
            returnLobby = lobby;
            shellAcceptsInput = acceptsInput;
            soundEnabled = sound;
            hapticsEnabled = haptics;
            feedback = commonFeedback;
            setStatus = status;
            showDialog = dialog;
            store = new JumpSaveStore(Application.persistentDataPath);
            data = store.Load(out warning);
            if (data == null) data = new JumpSaveData { Game = NewGame().GetSnapshot() };
            game = new JumpGame(data.Game);

            var nav = UiFactory.Band(root, "Jump navigation", 0, 92, 0);
            UiFactory.Button(nav, "‹  大厅", 56, 34, 150, 58, lobby);
            UiFactory.Label(nav, "XANADU / YONDER", 226, 34, 278, 58, 19, UiFactory.Muted, TextAnchor.MiddleCenter);
            UiFactory.Button(nav, "设置", 552, 34, 112, 58, settings);
            var heading = UiFactory.Band(root, "Jump heading", 112, 104, .08f);
            UiFactory.Label(heading, "跳一跳", 56, 0, 400, 88, 63, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(heading, "02 / 轻巧向前", 440, 30, 224, 42, 22, UiFactory.Muted, TextAnchor.MiddleRight);
            var scores = UiFactory.Band(root, "Jump scores", 230, 96, .16f);
            score = ScorePanel(scores, "本局分数", 56);
            best = ScorePanel(scores, "最高纪录", 368);

            var stageBand = UiFactory.Band(root, "Jump stage", 346, 676, .45f);
            var stage = UiFactory.Rect(stageBand, "3D play area", 32, 0, 656, 676);
            var raw = stage.gameObject.AddComponent<RawImage>();
            raw.color = Color.white;
            raw.raycastTarget = true;
            var touch = stage.gameObject.AddComponent<JumpTouchInput>();
            touch.Initialize(BeginCharge, ReleaseCharge);
            scene = gameObject.AddComponent<JumpSceneView>();
            scene.Initialize(raw);
            combo = UiFactory.Label(stage, "", 24, 22, 608, 42, 25, UiFactory.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            hint = UiFactory.Label(stage, "按住蓄力，松手起跳", 24, 608, 608, 44, 25, UiFactory.Ink, TextAnchor.MiddleCenter);

            var controls = UiFactory.Band(root, "Jump charge", 1044, 28, .70f);
            UiFactory.Panel(controls, "Charge track", 56, 0, 608, 10, UiFactory.Hex("DDE5DA"));
            chargeFill = UiFactory.Panel(controls, "Charge amount", 56, 0, 0, 10, UiFactory.Accent);
            var actions = UiFactory.Band(root, "Jump actions", 1098, 76, .88f);
            UiFactory.Button(actions, "重新开始", 56, 0, 296, 76, ConfirmRestart);
            UiFactory.Button(actions, "玩法说明", 368, 0, 296, 76, ShowHelp);
            var tip = UiFactory.Band(root, "Jump instructions", 1200, 42, 1);
            UiFactory.Label(tip, "连续落在中心，加分更多。", 56, 0, 608, 42, 23, UiFactory.Muted, TextAnchor.MiddleCenter);
            scene.Render(game.GetSnapshot());
            RefreshScores();
            Debug.Log("XanaduYonder jump ready; save=" + store.SavePath);
        }

        private static Text ScorePanel(Transform root, string title, float x)
        {
            var panel = UiFactory.Panel(root, title, x, 0, 296, 96, UiFactory.Hex("E7EDE4"));
            UiFactory.Label(panel.transform, title, 20, 8, 256, 30, 19, UiFactory.Muted);
            var value = UiFactory.Label(panel.transform, "0", 20, 36, 256, 54, 37, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 14;
            value.resizeTextMaxSize = 37;
            return value;
        }

        private static JumpGame NewGame()
        {
            var seed = unchecked((uint)DateTime.UtcNow.Ticks ^ (uint)Environment.TickCount);
            return new JumpGame(seed == 0 ? 1u : seed);
        }

        public void Enter()
        {
            visible = true;
            gameObject.SetActive(true);
            scene.SetVisible(true);
            scene.Render(game.GetSnapshot());
            RefreshScores();
            hint.text = "按住蓄力，松手起跳";
            setStatus(warning ?? "距离不同，试着找到自己的节奏。");
            Save();
            CheckEndState();
        }

        public void Leave()
        {
            Suspend();
            Save();
            visible = false;
            scene.SetVisible(false);
            gameObject.SetActive(false);
        }

        public void Suspend()
        {
            CancelCharge();
            StopAllCoroutines();
            animating = false;
            gesture.FinishFlight();
            if (game != null && scene != null) scene.Suspend(game.GetSnapshot());
        }

        private bool AcceptsInput => visible && !game.GetSnapshot().IsGameOver && shellAcceptsInput();

        private void BeginCharge(int pointer)
        {
            var now = Time.realtimeSinceStartupAsDouble;
            if (!AcceptsInput || !gesture.Begin(pointer, now)) return;
            chargeStarted = now;
            hint.text = "松手，跳向下一个平台";
            feedback.BeginCharge(soundEnabled());
        }

        private void ReleaseCharge(int pointer, bool cancelled)
        {
            double seconds;
            if (!gesture.Release(pointer, Time.realtimeSinceStartupAsDouble, cancelled || !AcceptsInput, out seconds))
            {
                if (!gesture.IsCharging) ResetChargePresentation();
                return;
            }
            ResetChargePresentation();
            var before = game.GetSnapshot();
            var result = game.Jump(seconds);
            Save();
            RefreshScores();
            animating = true;
            gesture.StartFlight();
            feedback.Jump(soundEnabled());
            StartCoroutine(Animate(before, result));
        }

        private IEnumerator Animate(JumpSnapshot before, JumpResult result)
        {
            yield return scene.Animate(before, result, game.GetSnapshot());
            animating = false;
            gesture.FinishFlight();
            if (result.Outcome == JumpOutcome.Missed)
            {
                hint.text = "差一点，再试一次";
                RefreshScores();
                CheckEndState();
                // Opening a modal cancels transient sounds; play this result after it is open.
                feedback.Miss(soundEnabled(), hapticsEnabled());
                yield break;
            }
            else
            {
                feedback.Land(result.Outcome == JumpOutcome.Perfect, soundEnabled(), hapticsEnabled());
                if (!gesture.IsCharging)
                    hint.text = result.Outcome == JumpOutcome.Stayed ? "还在原平台，再多蓄力一点" :
                        result.Outcome == JumpOutcome.Perfect ? "正中中心！  +" + result.DeltaScore : "稳稳落地  +" + result.DeltaScore;
            }
            RefreshScores();
            CheckEndState();
        }

        private void CancelCharge()
        {
            gesture.Cancel();
            ResetChargePresentation();
        }

        private void ResetChargePresentation()
        {
            feedback.EndCharge();
            if (scene != null) scene.SetCharge(0);
            if (chargeFill != null) chargeFill.rectTransform.sizeDelta = new Vector2(0, 10);
            if (hint != null) hint.text = "按住蓄力，松手起跳";
        }

        private void Update()
        {
            if (!visible) return;
            if (!shellAcceptsInput()) { if (gesture.IsCharging) CancelCharge(); return; }
            if (Input.GetKeyDown(KeyCode.Space)) BeginCharge(-100);
            if (Input.GetKeyUp(KeyCode.Space)) ReleaseCharge(-100, false);
            if (!gesture.IsCharging) return;
            var amount = Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble - chargeStarted) / JumpGame.MaxChargeSeconds));
            scene.SetCharge(amount);
            feedback.Charge(amount);
            chargeFill.rectTransform.sizeDelta = new Vector2(608 * amount, 10);
        }

        public void CheckEndState()
        {
            if (!visible || animating || !shellAcceptsInput() || !game.GetSnapshot().IsGameOver) return;
            showDialog("下一次，再远一点。", "本局得分  " + game.GetSnapshot().Score + "\n最高纪录  " + data.BestScore,
                "再来一局", Restart, "返回大厅", returnLobby);
        }

        private void ConfirmRestart()
        {
            showDialog("重新开始这一局？", "当前跳跃进度将被清空，\n最高纪录和设置会保留。", "重新开始", Restart, "继续当前游戏", CheckEndState);
        }

        private void Restart()
        {
            Suspend();
            game = NewGame();
            Save();
            Enter();
        }

        private void ShowHelp()
        {
            showDialog("找到跳跃的节奏", "按住游戏区域蓄力，松手起跳。\n按得越久，跳得越远。\n落台得 1 分；连续中心得 2、4、6…分。\n落空结束，短跳留在原台可继续。", "知道了", CheckEndState, null, null);
        }

        private void RefreshScores()
        {
            var snapshot = game.GetSnapshot();
            data.BestScore = Math.Max(data.BestScore, snapshot.Score);
            score.text = snapshot.Score.ToString();
            best.text = data.BestScore.ToString();
            combo.text = snapshot.Streak > 0 ? "连续中心  × " + snapshot.Streak : "";
            Changed?.Invoke();
        }

        public void Save()
        {
            if (store == null || game == null) return;
            data.Game = game.GetSnapshot();
            data.BestScore = Math.Max(data.BestScore, data.Game.Score);
            string error;
            if (!store.TrySave(data, out error))
            {
                saveFailed = true;
                warning = "跳一跳进度暂未保存，请检查剩余空间。";
                Debug.LogWarning("XanaduYonder jump save failed: " + error);
            }
            else if (saveFailed)
            {
                saveFailed = false;
                warning = null;
                if (visible) setStatus("跳一跳进度已保存。");
            }
            if (visible && warning != null) setStatus(warning);
        }

        private void OnDisable() { if (game != null) Suspend(); }
    }

    internal sealed class JumpTouchInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Action<int> begin;
        private Action<int, bool> release;
        public void Initialize(Action<int> onBegin, Action<int, bool> onRelease) { begin = onBegin; release = onRelease; }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) begin(data.pointerId);
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            var cancelled = data.pointerId >= 0;
            for (var i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.fingerId == data.pointerId) { cancelled = touch.phase == TouchPhase.Canceled; break; }
            }
            release(data.pointerId, cancelled);
        }
    }
}
