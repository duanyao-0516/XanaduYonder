using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.GoldMiner;

namespace XanaduYonder.Runtime.GoldMiner
{
    internal sealed partial class GoldMinerPage : MonoBehaviour
    {
        private GoldMinerSession session;
        private GoldMinerSaveStore store;
        private GoldMinerLevelCatalog catalog;
        private GoldMinerArtwork art;
        private GoldMinerAudio sound;
        private GoldMinerOrientationScope orientation;
        private GoldMinerInputRouter input;
        private GoldMinerSceneView scene;
        private GoldMinerLayout layout;
        private RectTransform canvasRoot, view, modal;
        private CanvasScaler scaler;
        private Text cashText, targetText, timeText, contributionText, notice;
        private Image noticePanel;
        private Button[] bombButtons;
        private Text[] bombLabels;
        private Action lobby, vibrate;
        private Func<bool> soundEnabled, hapticsEnabled;
        private Action<bool> setSound, setHaptics;
        private bool visible, selecting = true, focused = true, backgrounded, suspending;
        private double accumulator, lastRealTime, lastFrameRealTime, sinceSave;
        private int simulationFrame = -1, stepsThisFrame;
        private Rect previousSafe;
        private Vector2 previousWindow;
        private string message;
        private GoldMinerPhase renderedPhase;
        private Coroutine releaseAssets;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private GoldMinerDiagnostics diagnostics;
#endif
        public string SaveError { get; private set; }
        public event Action Changed;

        public void Initialize(Action goLobby, Func<bool> sound, Func<bool> haptics,
            Action<bool> soundSetter, Action<bool> hapticsSetter, Action haptic)
        {
            lobby = goLobby;
            soundEnabled = sound;
            hapticsEnabled = haptics;
            setSound = soundSetter;
            setHaptics = hapticsSetter;
            vibrate = haptic;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            gameObject.AddComponent<GraphicRaycaster>();
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1320);
            scaler.matchWidthOrHeight = 0;
            canvasRoot = GetComponent<RectTransform>();
            store = new GoldMinerSaveStore(Application.persistentDataPath);
            string warning;
            session = new GoldMinerSession(store.Load(out warning));
            message = warning;
            session.Suspend(GoldMinerSuspendReason.NotVisible);
            gameObject.SetActive(false);
        }

        public void Enter()
        {
            visible = true;
            gameObject.SetActive(true);
            if (art == null) art = new GoldMinerArtwork();
            if (sound == null)
            {
                sound = gameObject.AddComponent<GoldMinerAudio>();
                sound.Initialize(soundEnabled, hapticsEnabled, cue =>
                {
                    if (cue == "gold_collect" || cue == "diamond_collect" || cue == "explosion") vibrate?.Invoke();
                });
            }
            orientation = new GoldMinerOrientationScope();
            session.Resume(GoldMinerSuspendReason.NotVisible);
            session.Suspend(GoldMinerSuspendReason.NeedsExplicitContinue);
            ShowSelection();
        }

        public void Leave()
        {
            if (!visible) return;
            FlushBeforePause();
            session.Suspend(GoldMinerSuspendReason.NotVisible);
            Save();
            visible = false;
            input?.ResetGeneration();
            sound?.StopAll();
            ClearView();
            if (releaseAssets != null) { StopCoroutine(releaseAssets); releaseAssets = null; }
            orientation?.Dispose();
            orientation = null;
            catalog = null;
            if (art != null) { art.Dispose(); art = null; }
            if (sound != null) { sound.Dispose(); Destroy(sound); sound = null; }
            gameObject.SetActive(false);
        }

        public void SetForeground(bool isFocused, bool isPaused)
        {
            if (session == null) return;
            if (!isFocused || isPaused) FlushBeforePause();
            focused = isFocused;
            backgrounded = isPaused;
            SetSuspend(GoldMinerSuspendReason.FocusLost, !focused);
            SetSuspend(GoldMinerSuspendReason.AppPaused, backgrounded);
            if (!focused || backgrounded)
            {
                input?.ResetGeneration();
                sound?.StopAll();
                session.Suspend(GoldMinerSuspendReason.NeedsExplicitContinue);
                Save();
            }
            else if (visible && !selecting && orientation != null && !orientation.IsPending)
            {
                var state = session.GetStateSnapshot();
                if (state != null && (state.Phase == GoldMinerPhase.Running || state.Phase == GoldMinerPhase.Draining)) ShowPause();
            }
            lastRealTime = Time.realtimeSinceStartupAsDouble;
            lastFrameRealTime = lastRealTime;
        }

        public void SettingsChanged()
        {
            if (soundEnabled != null && !soundEnabled()) sound?.StopAll();
        }

        public void Save()
        {
            if (session == null || store == null || store.RequiresRecovery || (!session.Dirty && SaveError == null)) return;
            var revision = session.StateRevision;
            string error;
            if (store.TrySave(session.GetSnapshot(), out error))
            {
                session.MarkSaved(revision);
                if (message == SaveError) SetMessage(null);
                SaveError = null;
            }
            else
            {
                Debug.LogWarning("Gold Miner save: " + error);
                SaveError = "进度暂未保存，请检查设备剩余空间后重试。";
                SetMessage(SaveError);
            }
        }

        public bool HandleBack()
        {
            if (!visible) return false;
            if (modal != null && modalBack != null) { var action = modalBack; action(); return true; }
            if (selecting) { lobby(); return true; }
            ShowPause();
            return true;
        }

        private void Update()
        {
            if (!visible) return;
            orientation?.Tick();
            double now = Time.realtimeSinceStartupAsDouble;
            double delta = lastRealTime <= 0 ? 0 : Math.Max(0, now - lastRealTime);
            double frameDelta = lastFrameRealTime <= 0 ? 0 : Math.Max(0, now - lastFrameRealTime);
            lastRealTime = now;
            lastFrameRealTime = now;
            if (orientation != null && orientation.IsPending) return;
            if (previousSafe != Screen.safeArea || previousWindow != new Vector2(Screen.width, Screen.height))
            {
                previousSafe = Screen.safeArea;
                previousWindow = new Vector2(Screen.width, Screen.height);
                input?.ResetGeneration();
                if (selecting) BuildSelection();
                else RebuildRun();
            }
            if (!selecting && CanAdvance)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                diagnostics?.Frame(frameDelta);
#endif
                accumulator += delta;
                AdvanceAccumulated(8);
                sinceSave += delta;
                if (sinceSave >= .8) { sinceSave = 0; Save(); }
            }
            else accumulator = 0;
            if (!selecting && scene != null)
            {
                var run = session.GetRunSnapshot();
                if (run != null)
                {
                    scene.Render(run, CanAdvance ? (float)Math.Min(frameDelta, .1) : 0);
                    RefreshHud(run.Current);
                }
            }
        }

        private bool CanAdvance
        {
            get
            {
                if (!visible || selecting || backgrounded || !focused || session.IsSuspended ||
                    orientation == null || orientation.IsPending) return false;
                return renderedPhase == GoldMinerPhase.Running || renderedPhase == GoldMinerPhase.Draining;
            }
        }

        private bool CanAct { get { return !suspending && modal == null && accumulator < GoldMinerRules.TickSeconds && CanAdvance; } }

        private bool SynchronizeInputClock()
        {
            if (suspending || modal != null || !CanAdvance) return false;
            // EventSystem order is not a clock boundary. Account for time up to this
            // Down before assigning its command sequence, including a stalled frame.
            double now = Time.realtimeSinceStartupAsDouble;
            double elapsed = Math.Max(0, now - lastRealTime);
            lastRealTime = now;
            accumulator += elapsed;
            sinceSave += elapsed;
            AdvanceAccumulated(8);
            return CanAct;
        }

        private void AdvanceAccumulated(int limit)
        {
            if (simulationFrame != Time.frameCount) { simulationFrame = Time.frameCount; stepsThisFrame = 0; }
            limit = Math.Min(limit, Math.Max(0, 8 - stepsThisFrame));
            int steps = 0;
            while (accumulator >= GoldMinerRules.TickSeconds && steps++ < limit && CanAdvance)
            {
                accumulator -= GoldMinerRules.TickSeconds;
                stepsThisFrame++;
                AdvanceOne();
            }
        }

        private IEnumerator ReleasePreviousViewAssets()
        {
            // Images scheduled by Destroy must disappear before Unity scans roots.
            yield return null;
            yield return Resources.UnloadUnusedAssets();
            releaseAssets = null;
        }

        private void AdvanceOne()
        {
            var result = session.Step();
            foreach (var item in result.Events)
            {
                if (item.Kind == GoldMinerEventKind.Collected)
                {
                    scene?.Collected(item.PlayerId, item.Amount);
                    string cue = "gold_collect";
                    var state = session.GetStateSnapshot();
                    foreach (var entity in state.Entities)
                    {
                        if (entity.Id != item.EntityId) continue;
                        if (entity.Kind == GoldMinerKind.Diamond || entity.Kind == GoldMinerKind.DiamondMole) cue = "diamond_collect";
                        else if (entity.Kind == GoldMinerKind.RockSmall || entity.Kind == GoldMinerKind.RockLarge) cue = "rock_collect";
                        break;
                    }
                    sound?.Play(cue);
                }
                else if (item.Kind == GoldMinerEventKind.Countdown) sound?.Play("countdown");
            }
            if (result.SettlementReady)
            {
                session.Suspend(GoldMinerSuspendReason.BlockingModal);
                if (Apply(() => session.PrepareSettlement()))
                {
                    RebuildRun();
                    sound?.Play(session.GetStateSnapshot().Phase == GoldMinerPhase.Failed ? "failure" : "victory");
                }
                else ShowSaveFailure();
            }
        }

        private void FlushBeforePause()
        {
            if (session == null || suspending) return;
            suspending = true;
            input?.ResetGeneration();
            sound?.StopAll();
            if (CanAdvance)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                accumulator += Math.Max(0, now - lastRealTime);
                lastRealTime = now;
                while (accumulator >= GoldMinerRules.TickSeconds && CanAdvance)
                {
                    accumulator -= GoldMinerRules.TickSeconds;
                    AdvanceOne();
                }
            }
            accumulator = 0;
            suspending = false;
        }

        private void Launch(int player)
        {
            if (!SynchronizeInputClock()) return;
            var run = session.GetRunSnapshot();
            if (run == null || run.LastCommandSeq == long.MaxValue) return;
            if (session.TryLaunch(player, run.LastCommandSeq + 1))
            {
                scene?.Launched(player);
                sound?.Play("hook_launch");
            }
        }

        private void Dynamite(int player)
        {
            if (!SynchronizeInputClock()) return;
            var run = session.GetRunSnapshot();
            if (run == null || run.LastCommandSeq == long.MaxValue) return;
            if (session.TryUseDynamite(player, run.LastCommandSeq + 1))
            {
                scene?.Explode(run, player);
                sound?.Play("explosion");
            }
            else sound?.Play("unavailable");
        }

        private bool Apply(Func<GoldMinerCandidate> prepare)
        {
            try
            {
                var candidate = prepare();
                if (candidate == null)
                {
                    Debug.LogWarning("Gold Miner candidate: " + session.LastError);
                    SetMessage("当前操作暂不可用，请重试。");
                    return false;
                }
                string error;
                if (!store.TrySave(candidate.Snapshot, out error))
                {
                    Debug.LogWarning("Gold Miner transaction save: " + error);
                    SaveError = "操作尚未保存，请检查设备剩余空间后重试。";
                    SetMessage(SaveError);
                    return false;
                }
                if (!session.Commit(candidate)) throw new InvalidOperationException("Stored transaction could not be committed.");
                session.MarkSaved(session.StateRevision);
                SaveError = null;
                message = null;
                Changed?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Gold Miner operation: " + exception);
                SetMessage("暂时无法完成操作，当前进度已保留。");
                return false;
            }
        }

        private void ChooseMode(GoldMinerMode mode, bool restart)
        {
            if (store.RequiresRecovery)
            {
                ShowRecovery(() => ChooseMode(mode, restart));
                return;
            }
            session.EnterMode(mode);
            selecting = false;
            session.Suspend(GoldMinerSuspendReason.OrientationPending);
            PrepareCanvas(mode == GoldMinerMode.Coop);
            ShowWaiting("准备矿场…");
            orientation.Request(mode == GoldMinerMode.Coop, generation =>
            {
                if (!visible || selecting) return;
                session.Resume(GoldMinerSuspendReason.OrientationPending);
                var existing = session.GetRunSnapshot();
                if (restart || existing == null)
                {
                    try
                    {
                        EnsureCatalog();
                        uint seed = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0);
                        if (seed == 0) seed = 1;
                        var start = catalog.CreateStart(mode, 1, seed);
                        var runId = Guid.NewGuid().ToString("N");
                        CreateChallenge(mode, runId, seed, start);
                        return;
                    }
                    catch (Exception error)
                    {
                        Debug.LogError("Gold Miner catalog: " + error);
                        SetMessage("矿场资源无法读取，请重试或重新安装应用。");
                        ShowOperationError(() => ChooseMode(mode, restart));
                        return;
                    }
                }
                RebuildRun();
            }, generation => ShowOperationError(() => ChooseMode(mode, restart)));
        }

        private void CreateChallenge(GoldMinerMode mode, string runId, uint seed, GoldMinerLevelStart start)
        {
            // A failed save retries the same generated checkpoint, never rerolls a mine.
            if (Apply(() => session.PrepareNewRun(mode, runId, seed, start))) RebuildRun();
            else ShowOperationError(() => CreateChallenge(mode, runId, seed, start));
        }

        private void NextLevel()
        {
            try
            {
                EnsureCatalog();
                var run = session.GetRunSnapshot();
                var start = catalog.CreateStart(run.Mode, run.Current.LevelIndex + 1, run.RunSeed, run);
                if (Apply(() => session.PrepareNextLevel(start))) RebuildRun();
            }
            catch (Exception error)
            {
                Debug.LogError("Gold Miner next level: " + error);
                SetMessage("下一关矿场无法读取，购买的补给已保留。请重试。");
            }
        }

        private void EnsureCatalog()
        {
            if (catalog != null) return;
            catalog = new GoldMinerLevelCatalog(key =>
            {
                var text = Resources.Load<TextAsset>(key);
                if (text == null) throw new System.IO.InvalidDataException("Missing " + key);
                var value = text.text;
                Resources.UnloadAsset(text);
                return value;
            });
        }

        private void SetSuspend(GoldMinerSuspendReason reason, bool value)
        {
            if (value) session.Suspend(reason); else session.Resume(reason);
        }

        private void SetMessage(string value)
        {
            message = value;
            if (notice != null) notice.text = value ?? "";
            if (noticePanel != null) noticePanel.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }

        private void OnDestroy()
        {
            Leave();
            orientation?.Dispose();
        }
    }
}
