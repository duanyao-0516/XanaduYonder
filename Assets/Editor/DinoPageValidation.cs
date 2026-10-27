using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Dino;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Dino;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    /// <summary>
    /// Production page/state/UGUI regression checks. Play mode permits the page's deferred Destroy
    /// calls; fixtures disable automatic Update and call it explicitly with controlled viewport data.
    /// Initialize only reads the normal save. Before any callback, each fixture replaces both the
    /// profile and store with a fresh temporary store. AppController.Awake is never called.
    /// </summary>
    [InitializeOnLoad]
    public static class DinoPageValidation
    {
        private const string Pending = "Xanadu.DinoPageValidation.Pending";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int assertions, cases, failed;
        private static readonly StringBuilder Report = new StringBuilder();

        static DinoPageValidation() { EditorApplication.playModeStateChanged += StateChanged; }

        public static void Run()
        {
            SessionState.SetBool(Pending, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            new GameObject("Dino page validation runner").AddComponent<Runner>().StartCoroutine(AfterFrame());
        }

        private static IEnumerator AfterFrame() { yield return null; Execute(); }

        private static void Execute()
        {
            var oldFont = UiFactory.Font;
            assertions = cases = failed = 0; Report.Clear();
            try {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Require(UiFactory.Font != null, "Bundled CJK font loads");
                Check("pause Back opens exit confirmation and freezes the session", PauseAndBack);
                Check("cancel orientation waits for two stable portrait frames", CancelDirection);
                Check("safe resize keeps the same game and permits countdown", SafeResize);
                Check("background resize is applied on return and preserves the paused picture", BackgroundResize);
                Check("unsafe resize preserves progress and refuses continuation", UnsafeResize);
                Check("result uses run-start record and updates the actual shell summary", ResultAndRecords);
                Check("Ready duck uses duck UV and release returns to idle", ReadyDuckSprite);
                Check("new run rejects wrong orientation and insufficient headroom before saving preference", BlockedNewRun);
                Check("near-square landscape separates jump duck and pause targets", NearSquareControls);
                Report.AppendLine("LIMITS: real Unity objects, page methods, UGUI and render UV; viewport readiness is controlled by reflection.");
                Report.AppendLine("LIMITS: no Android rotation, physical pointer, native Back, audio output or device visual evidence.");
                Report.AppendLine((failed == 0 ? "DINO_PAGE_CHECKS_PASS: " : "DINO_PAGE_CHECKS_FAIL: ") + cases + " cases, " + assertions + " assertions, " + failed + " failures");
            }
            catch (Exception error) { failed++; Report.AppendLine("DINO_PAGE_CHECKS_FAIL: " + error); Debug.LogException(error); }
            finally {
                UiFactory.Font = oldFont;
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/logs"));
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "dino-page-checks.txt"), Report.ToString());
                Debug.Log(Report.ToString());
                EditorApplication.Exit(failed == 0 ? 0 : 1);
            }
        }

        private static void PauseAndBack(Fixture f)
        {
            f.Ready(); f.PressJump();
            DinoGame game = f.Game;
            for (int i = 0; i < 9; i++) game.Step();
            long tick = game.Tick; double y = game.PlayerY, velocity = game.PlayerVelocity;
            f.Page.Suspend();
            Require(f.Page.CurrentPhase == "Paused" && f.HasText("已暂停"), "Pause opens pause menu");
            Require(!Get<DinoActionOwners>(f.Page, "owners").JumpHeld, "Physical jump ownership clears on suspend");
            Require(game.PlayerY == y && game.PlayerVelocity == velocity, "Suspend retains current jump trajectory");
            f.Page.Back();
            Require(f.HasText("结束本局并返回？"), "Back from pause asks to end the current run");
            Require(f.HomeCalls == 0 && ReferenceEquals(game, f.Game) && game.Tick == tick, "Confirmation does not leave or advance");
            f.Page.Back();
            Require(f.HasText("已暂停"), "Back cancels exit confirmation to pause");
            Invoke(f.Page, "Continue");
            Require(f.Page.CurrentPhase == "Countdown" && game.Tick == tick, "Resume begins countdown with frozen tick");
            f.Page.Suspend();
            Require(f.Page.CurrentPhase == "Paused" && game.Tick == tick, "Countdown can be suspended without simulation");
        }

        private static void CancelDirection(Fixture f)
        {
            f.Page.Enter();
            f.FakeViewport(720, 1320, false); f.CacheViewport();
            Invoke(f.Page, "Update"); Invoke(f.Page, "Update");
            Require(f.Page.CurrentPhase == "Selecting", "Entry waits then shows selection");
            f.BeginTurn(true, "Game");
            Require(f.Requests[f.Requests.Count - 1] && f.Page.CurrentPhase == "Turning", "Landscape requests do not directly start game");
            f.FakeViewport(1320, 720, true); f.CacheViewport();
            f.Page.Back();
            Require(!f.Requests[f.Requests.Count - 1] && f.Page.CurrentPhase == "Turning", "Cancel requests portrait and remains Turning");
            f.FakeViewport(1320, 720, true); f.CacheViewport();
            Invoke(f.Page, "Update");
            Require(f.Page.CurrentPhase == "Turning", "Old landscape viewport cannot finish cancellation");
            f.FakeViewport(720, 1320, false); f.CacheViewport();
            Invoke(f.Page, "Update");
            Require(f.Page.CurrentPhase == "Turning", "One stable portrait frame is insufficient");
            Invoke(f.Page, "Update");
            Require(f.Page.CurrentPhase == "Selecting" && f.HomeCalls == 0 && f.Game == null, "Second stable frame returns to selection without stale start");
        }

        private static void SafeResize(Fixture f)
        {
            f.Ready(); f.PressJump();
            for (int i = 0; i < 12; i++) f.Game.Step();
            DinoGame original = f.Game; long tick = original.Tick; double distance = original.Distance;
            // Force the real Update -> viewport.Refresh -> changed branch, ending at the real screen geometry.
            Set(f.Viewport, "lastCanvas", Vector2.zero);
            Invoke(f.Page, "Update");
            Require(ReferenceEquals(original, f.Game) && original.Tick == tick && original.Distance == distance, "Resize preserves exact model and clock");
            Require(f.Page.CurrentPhase == "Paused" && !Get<bool>(f.Page, "windowBlocked"), "Safe resize pauses without blocking");
            Invoke(f.Page, "Continue");
            Require(f.Page.CurrentPhase == "Countdown" && original.Tick == tick, "Safe original world can resume after countdown");
        }

        private static void UnsafeResize(Fixture f)
        {
            f.Ready();
            float physicalWidth = Get<float>(f.Viewport, "Width"), physicalHeight = Get<float>(f.Viewport, "Height");
            bool landscape = Get<bool>(f.Viewport, "Landscape");
            var original = new DinoGame(new DinoRules(Math.Min(4096, physicalWidth * 1.5)), 7);
            Set(f.Page, "game", original); Invoke(f.Page, "BuildGame"); f.PressJump();
            for (int i = 0; i < 15; i++) original.Step();
            long tick = original.Tick; double y = original.PlayerY;
            Set(f.Viewport, "lastCanvas", Vector2.zero);
            Invoke(f.Page, "Update");
            Require(ReferenceEquals(original, f.Game) && original.Tick == tick && original.PlayerY == y, "Unsafe resize retains existing positions and input trajectory");
            Require(Get<bool>(f.Page, "windowBlocked") && f.HasText("等待恢复游戏窗口"), "Window below legible scale is blocked");
            Invoke(f.Page, "Continue");
            Require(f.Page.CurrentPhase == "Paused" && f.HasText("等待恢复游戏窗口") && original.Tick == tick, "Continue cannot bypass unsafe viewport");
            // Return to a legible, sufficiently tall window. This validates the real recheck and Continue
            // methods separately from platform-specific Screen/rotation delivery.
            f.FakeViewport((float)(original.Rules.ViewportWidth * 1.2), Math.Max(900, physicalHeight), landscape);
            Invoke(f.Page, "ShowWindowChanged"); Invoke(f.Page, "Continue");
            Require(!Get<bool>(f.Page, "windowBlocked") && f.Page.CurrentPhase == "Countdown" && ReferenceEquals(original, f.Game), "Restored window can resume the existing run");
        }

        private static void BackgroundResize(Fixture f)
        {
            f.Ready(); f.PressJump();
            for (int i = 0; i < 12; i++) f.Game.Step();
            DinoGame game = f.Game; long tick = game.Tick;
            var originalTrack = Get<DinoTrackView>(f.Page, "track");
            Set(originalTrack, "animationClock", 3.25); Set(originalTrack, "nightAmount", .68f);
            Set(f.Page, "foreground", (Func<bool>)(() => false));
            Set(f.Viewport, "lastCanvas", Vector2.zero);
            Invoke(f.Page, "Update");
            Require(f.Page.CurrentPhase == "Paused" && game.Tick == tick, "Background resize suspends without simulation");
            Require(ReferenceEquals(originalTrack, Get<DinoTrackView>(f.Page, "track")), "Background defers the expensive rebuild");
            Set(f.Page, "foreground", (Func<bool>)(() => true));
            Invoke(f.Page, "Update");
            var rebuilt = Get<DinoTrackView>(f.Page, "track");
            Require(!ReferenceEquals(originalTrack, rebuilt), "Foreground consumes the deferred geometry change");
            Require(ReferenceEquals(game, f.Game) && game.Tick == tick && f.Page.CurrentPhase == "Paused", "Rebuild keeps paused session and tick");
            Require(Math.Abs(Get<double>(rebuilt, "animationClock") - 3.25) < 1e-9 && Math.Abs(Get<float>(rebuilt, "nightAmount") - .68f) < .0001,
                "Rebuild preserves animation phase and day-night blend");
        }

        private static void ResultAndRecords(Fixture f)
        {
            f.Ready(); f.PressJump();
            for (int i = 0; i < 2400 && !f.Game.IsGameOver; i++) f.Game.Step();
            Require(f.Game.IsGameOver && f.Game.Score > 0, "Real collision produces a scored terminal run");
            long score = f.Game.Score;
            Invoke(f.Page, "UpdateRecord"); Invoke(f.Page, "EndRun");
            Require(f.Page.CurrentPhase == "Finished" && f.HasText("新纪录！"), "Result compares against run-start record after saving");
            bool landscape = Get<bool>(f.Page, "landscape");
            var profile = Get<DinoSaveData>(f.Page, "profile");
            Require(profile.GetBest(landscape) == score && profile.GetBest(!landscape) == 0, "Only the current orientation record changes");
            string warning; var persisted = f.Store.Load(out warning);
            Require(warning == null && persisted.GetBest(landscape) == score, "New record reaches the isolated real save file");
            var shellObject = new GameObject("Inert real app shell"); shellObject.SetActive(false);
            var shell = shellObject.AddComponent<AppController>();
            try {
                var text = UiFactory.Label(f.Canvas, "", 0, 0, 600, 40, 20, Color.black);
                Set(shell, "dinoPage", f.Page); Set(shell, "lobbyDinoRecordText", text);
                Invoke(shell, "RefreshDinoRecord");
                Require(text.text == "竖屏最高 " + profile.BestPortraitScore + "  ·  横屏最高 " + profile.BestLandscapeScore, "Actual shell displays both orientation records");
            }
            finally { Object.DestroyImmediate(shellObject); }
            Invoke(f.Page, "StartNewGame");
            Require(Get<long>(f.Page, "startingBest") == score && !f.Game.IsStarted, "Restart starts fresh and remembers the previous record baseline");
        }

        private static void ReadyDuckSprite(Fixture f)
        {
            f.Ready();
            Invoke(f.Page, "DuckDown", 31);
            var track = Get<DinoTrackView>(f.Page, "track"); track.Render(f.Game, 0);
            var sprite = Get<RawImage>(track, "player");
            Require(f.Game.Ducking && !f.Game.IsStarted && f.Page.CurrentPhase == "Ready", "Ready duck changes posture without starting");
            Require(Math.Abs(sprite.uvRect.x - 2206f / 2446) < .00001f && Math.Abs(sprite.uvRect.width - 118f / 2446) < .00001f,
                "Ready duck uses the actual first duck frame, not idle plus adjacent pixels");
            Invoke(f.Page, "JumpDown", 32);
            Require(!f.Game.IsStarted, "Real held duck blocks jump");
            Invoke(f.Page, "DuckUp", 31); track.Render(f.Game, 0);
            Require(!f.Game.Ducking && Math.Abs(sprite.uvRect.x - 1678f / 2446) < .00001f && Math.Abs(sprite.uvRect.width - 88f / 2446) < .00001f,
                "Duck release restores standing idle UV");
        }

        private static void BlockedNewRun(Fixture f)
        {
            f.Page.Enter();
            Set(f.Page, "landscape", true);
            f.FakeViewport(720, 1320, false); f.CacheViewport();
            Invoke(f.Page, "StartNewGame");
            Require(f.Page.CurrentPhase == "Unavailable" && f.Game == null && f.HasText("请调整游戏窗口"), "Wrong orientation cannot create a run");
            var profile = Get<DinoSaveData>(f.Page, "profile");
            Require(profile.PreferredOrientation == "portrait", "Failed entry does not commit the requested preference");
            f.FakeViewport(1320, 520, true); f.CacheViewport();
            Invoke(f.Page, "StartNewGame");
            Require(f.Page.CurrentPhase == "Unavailable" && f.Game == null, "Insufficient vertical clearance cannot create a run");
            f.FakeViewport(1320, 720, true); f.CacheViewport();
            Invoke(f.Page, "StartNewGame");
            Require(f.Page.CurrentPhase == "Ready" && f.Game != null && !f.Game.IsStarted, "Adequate restored window reaches Ready");
            Require(profile.PreferredOrientation == "landscape", "Only successful entry commits preference");
        }

        private static void NearSquareControls(Fixture f)
        {
            f.Page.Enter(); Set(f.Page, "landscape", true);
            f.FakeViewport(730, 720, true); f.CacheViewport();
            Invoke(f.Page, "StartNewGame"); f.PressJump();
            var content = Get<RectTransform>(f.Page, "content");
            var jump = (RectTransform)content.Find("跳跃\n轻按短跳 · 按住长跳");
            var duck = (RectTransform)content.Find("下蹲\n按住");
            var pause = (RectTransform)content.Find("暂停");
            Require(jump != null && duck != null && pause != null && pause.gameObject.activeSelf, "Actual running touch targets exist");
            Require(jump.anchoredPosition.x >= pause.anchoredPosition.x + pause.rect.width + 23.9f, "Jump retains a visible gap after centered pause");
            Require(pause.anchoredPosition.x >= duck.anchoredPosition.x + duck.rect.width, "Duck cannot overlap centered pause");
            Require(jump.rect.width >= 96 && duck.rect.width >= 96 && pause.rect.width >= 96, "Narrow landscape retains accessible targets");
        }

        private static void Check(string name, Action<Fixture> body)
        {
            cases++;
            try { using (var fixture = new Fixture()) body(fixture); Report.AppendLine("PASS " + name); }
            catch (Exception error) { failed++; Report.AppendLine("FAIL " + name + "\n" + error); Debug.LogException(error); }
        }
        private static void Require(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException(message); }
        private static T Get<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, Private);
            if (field != null) return (T)field.GetValue(target);
            return (T)target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        }
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, Private);
            if (field != null) field.SetValue(target, value);
            else target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        }
        private static object Invoke(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly RectTransform Canvas;
            internal readonly DinoPage Page;
            internal readonly DinoSaveStore Store;
            internal readonly List<bool> Requests = new List<bool>();
            internal int HomeCalls;
            private readonly GameObject root;
            private readonly string directory;
            internal object Viewport { get { return Get<object>(Page, "viewport"); } }
            internal DinoGame Game { get { return Get<DinoGame>(Page, "game"); } }

            internal Fixture()
            {
                root = new GameObject("Dino page fixture", typeof(RectTransform));
                Canvas = (RectTransform)root.transform; Canvas.sizeDelta = new Vector2(720, 1320);
                var pageRoot = UiFactory.Rect(Canvas, "Dino production page", 0, 0, 720, 1320);
                Page = pageRoot.gameObject.AddComponent<DinoPage>(); Page.enabled = false;
                Page.Initialize(pageRoot, Canvas, () => HomeCalls++, Requests.Add, () => true, () => false, () => false, () => { }, () => { });
                directory = Path.Combine(Path.GetTempPath(), "dino-page-validation-" + Guid.NewGuid().ToString("N"));
                Store = new DinoSaveStore(directory);
                Set(Page, "store", Store); Set(Page, "profile", new DinoSaveData());
            }
            internal void Ready()
            {
                Page.Enter();
                Set(Page, "landscape", Get<bool>(Viewport, "Landscape"));
                Invoke(Page, "StartNewGame"); Set(Page, "unlockAt", -1f);
                Require(Page.CurrentPhase == "Ready" && Game != null, "Prepared real page");
            }
            internal void PressJump() { Set(Page, "unlockAt", -1f); Invoke(Page, "JumpDown", 12); Require(Game.IsStarted && Page.CurrentPhase == "Running", "Real jump callback starts run"); }
            internal void FakeViewport(float width, float height, bool landscape) { Set(Viewport, "Width", width); Set(Viewport, "Height", height); Set(Viewport, "Landscape", landscape); }
            internal void CacheViewport()
            {
                Rect safe = Screen.safeArea; if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
                Set(Viewport, "lastSafe", safe); Set(Viewport, "lastPixels", new Vector2(Screen.width, Screen.height)); Set(Viewport, "lastCanvas", Canvas.rect.size);
            }
            internal void BeginTurn(bool landscape, string destination)
            {
                Type type = typeof(DinoPage).GetNestedType("Destination", BindingFlags.NonPublic);
                Invoke(Page, "BeginTurn", landscape, Enum.Parse(type, destination));
            }
            internal bool HasText(string expected)
            {
                var overlay = Get<RectTransform>(Page, "overlay");
                if (overlay == null) return false;
                foreach (var text in overlay.GetComponentsInChildren<Text>()) if (text.text == expected) return true;
                return false;
            }
            public void Dispose()
            {
                Set(Page, "active", false); Page.Save(); Object.DestroyImmediate(root);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
