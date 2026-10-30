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
                Check("held jump repeats on landing and release stops repetition", HeldJump);
                Check("airborne taps do not queue but airborne holds repeat", AirborneHeldJump);
                Check("pointer cancellation and suspension discard held repeat", CancelHeldJump);
                Check("cancel orientation waits for two stable portrait frames", CancelDirection);
                Check("entry immediately presents the actual direction selection", EntryWithoutPlaceholder);
                Check("orientation changes retain the previous page until ready", DirectionWithoutPlaceholder);
                Check("safe resize keeps the same game and permits countdown", SafeResize);
                Check("background resize is applied on return and preserves the paused picture", BackgroundResize);
                Check("unsafe resize preserves progress and refuses continuation", UnsafeResize);
                Check("result uses run-start record and updates the actual shell summary", ResultAndRecords);
                Check("Ready duck uses duck UV and release returns to idle", ReadyDuckSprite);
                Check("new run rejects wrong orientation and insufficient headroom before saving preference", BlockedNewRun);
                Check("near-square landscape separates jump duck and pause targets", NearSquareControls);
                Check("inset rounded track supplies its actual visible width to Core", InsetTrackGeometry);
                Check("portrait score hierarchy and controls follow the approved sketch", SharedUiTokens);
                Check("selection cards and start button stay within the lower reach area", SelectionReach);
                Check("short selection windows scroll both cards without covering fixed actions", ShortSelectionScroll);
                Check("game Back returns to selection before the lobby", ReturnHierarchy);
                Check("portrait and landscape controls stay low and expose pause", ControlsReach);
                Check("help dialog retains readable content and bottom spacing", HelpSpacing);
                Check("birthday entry uses the local lunar date without blocking selection", BirthdayEntry);
                Check("celebration hat follows jump duck and collision without changing the model", CelebrationHatPoses);
                Check("celebration snapshot survives layout replacement and refreshes on re-entry", CelebrationLifecycle);
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
            Require(f.HasText("结束本局并返回选择？"), "Back from pause asks to end the current run and return to selection");
            Require(f.HomeCalls == 0 && ReferenceEquals(game, f.Game) && game.Tick == tick, "Confirmation does not leave or advance");
            f.Page.Back();
            Require(f.HasText("已暂停"), "Back cancels exit confirmation to pause");
            Invoke(f.Page, "Continue");
            Require(f.Page.CurrentPhase == "Countdown" && game.Tick == tick, "Resume begins countdown with frozen tick");
            f.Page.Suspend();
            Require(f.Page.CurrentPhase == "Paused" && game.Tick == tick, "Countdown can be suspended without simulation");
        }

        private static void HeldJump(Fixture f)
        {
            foreach (var size in new[] { new Vector2(720, 1320), new Vector2(1515, 720) })
            foreach (int id in new[] { -1, -101, -102 })
            {
                f.ReadyAt(size.x, size.y);
                Invoke(f.Page, "JumpDown", id);
                Require(f.Game.Jumping, "First held press launches immediately");
                Land(f.Game);
                PageStep(f);
                Require(f.Game.Jumping && f.Game.PlayerVelocity > 0, "Held input launches again on the first grounded fixed step");
                Land(f.Game);
                PageStep(f);
                Require(f.Game.Jumping && f.Game.PlayerVelocity > 0, "One press supports multiple landings");
                Invoke(f.Page, "JumpUp", id);
                Land(f.Game);
                PageStep(f);
                Require(!f.Game.Jumping, "Release cancels all further repeat jumps");
            }
        }

        private static void AirborneHeldJump(Fixture f)
        {
            f.ReadyAt(720, 1320); f.PressJump(); Invoke(f.Page, "JumpUp", 12);
            Invoke(f.Page, "JumpDown", 13); Invoke(f.Page, "JumpUp", 13);
            Land(f.Game); PageStep(f);
            Require(!f.Game.Jumping, "An airborne tap is not buffered after release");
            Invoke(f.Page, "JumpDown", 12); Invoke(f.Page, "JumpUp", 12);
            Invoke(f.Page, "JumpDown", 13);
            Land(f.Game); PageStep(f);
            Require(f.Game.Jumping, "A new airborne hold starts repeating at landing");
            Invoke(f.Page, "DuckDown", 14);
            Land(f.Game); PageStep(f);
            Require(f.Game.Ducking && !f.Game.Jumping, "Held duck takes priority over held jump");
            Invoke(f.Page, "DuckUp", 14); PageStep(f);
            Require(f.Game.Jumping, "Releasing duck allows the still-held jump");
        }

        private static void CancelHeldJump(Fixture f)
        {
            foreach (bool pointerExit in new[] { true, false })
            {
                f.ReadyAt(720, 1320);
                var content = Get<RectTransform>(f.Page, "content");
                var control = content.Find("Dino jump input").GetComponent<DinoTouchControl>();
                var pointer = new UnityEngine.EventSystems.PointerEventData(null) { pointerId = -1 };
                control.OnPointerDown(pointer);
                Require(f.Game.Jumping, "Actual UGUI Down owns the held action");
                if (pointerExit) control.OnPointerExit(pointer); else control.OnCancel(null);
                Land(f.Game); PageStep(f);
                Require(!f.Game.Jumping && !Get<DinoActionOwners>(f.Page, "owners").JumpHeld,
                    "Pointer exit/cancel cannot restart after landing");
            }
            f.ReadyAt(720, 1320); f.PressJump(); f.Page.Suspend();
            Require(!Get<DinoActionOwners>(f.Page, "owners").JumpHeld, "Pause discards held repeat intent");
            Invoke(f.Page, "Continue");
            Set(f.Page, "phase", Enum.Parse(typeof(DinoPage).GetNestedType("Phase", BindingFlags.NonPublic), "Running"));
            Set(f.Page, "unlockAt", -1f);
            Land(f.Game); PageStep(f);
            Require(!f.Game.Jumping, "Resumed run never inherits a stale held jump");
        }

        private static void Land(DinoGame game)
        {
            for (int i = 0; i < 120 && game.Jumping && !game.IsGameOver; i++) game.Step();
            Require(!game.Jumping && !game.IsGameOver, "The unchanged jump physics lands before the obstacle arrives");
        }

        private static void PageStep(Fixture f)
        {
            // Exercise the real Update loop, supplying exactly one fixed step despite Editor frame timing.
            Require(Time.unscaledDeltaTime <= .1f, "Editor frame is within the production delta guard");
            Set(Get<DinoRunClock>(f.Page, "clock"), "accumulated", DinoRules.StepSeconds - Time.unscaledDeltaTime);
            long before = f.Game.Tick;
            Invoke(f.Page, "Update");
            Require(f.Game.Tick == before + 1, "Actual page Update consumes one fixed step");
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

        private static void EntryWithoutPlaceholder(Fixture f)
        {
            f.SelectionAt(720, 1320);
            f.Page.Enter();
            var content = Get<RectTransform>(f.Page, "content");
            Require(content.Find("Portrait choice") != null && content.Find("Landscape choice") != null && content.Find("Dino start") != null,
                "Entry frame already contains the real selection cards and start action");
            Require(!ContainsText(content, "正在调整画面"), "No temporary adjustment page is rendered at entry");
            Require(f.Game == null, "Entry never starts a run while selecting orientation");
            Invoke(f.Page, "Update"); Invoke(f.Page, "Update");
            Require(f.Page.CurrentPhase == "Selecting", "Direction confirmation still completes normally");
        }

        private static void DirectionWithoutPlaceholder(Fixture f)
        {
            f.SelectionAt(720, 1320);
            var before = Get<RectTransform>(f.Page, "content");
            f.BeginTurn(true, "Game");
            Require(ReferenceEquals(before, Get<RectTransform>(f.Page, "content")) && before.gameObject.activeSelf,
                "The current page remains visible while Android changes orientation");
            Require(!ContainsText(before, "正在调整画面"), "No intermediate adjustment text replaces the current page");
            Require(!before.GetComponent<CanvasGroup>().interactable, "Old page actions are disabled during the transition");
            f.FakeViewport(1515, 720, true); f.CacheViewport();
            Invoke(f.Page, "Update");
            Require(f.Page.CurrentPhase == "Turning" && before.gameObject.activeSelf,
                "One stable frame retains the visible old page instead of an empty screen");
            Invoke(f.Page, "Update");
            Require(f.Page.CurrentPhase == "Ready" && !before.gameObject.activeSelf && f.Game != null,
                "Ready layout replaces the old page after direction stability is confirmed");
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
            float restoredWidth = (float)(original.Rules.ViewportWidth * 1.2 + 64);
            float restoredHeight = landscape ? Math.Max(720, physicalHeight) : Math.Max(physicalHeight, restoredWidth + 200);
            f.FakeViewport(restoredWidth, restoredHeight, landscape);
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
                var frame = UiFactory.Rect(shellObject.transform, "Inert lobby frame", 0, 0, 720, 1320);
                Set(shell, "frame", frame); Set(shell, "dinoPage", f.Page);
                Invoke(shell, "BuildLobby"); LobbyDesignChecks.SetModeForCheck(shell, 2);
                Invoke(shell, "RefreshDinoRecord");
                var text = LobbyDesignChecks.RecordForCheck(shell, "Dino");
                Require(text.text == "竖屏最高 " + profile.BestPortraitScore + " · 横屏最高 " + profile.BestLandscapeScore, "Actual shell displays both orientation records");
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
            Set(f.Page, "landscape", false);
            foreach (float height in new[] { 1068f, 1080f, 1107f })
            {
                f.FakeViewport(720, height, false); f.CacheViewport();
                Invoke(f.Page, "StartNewGame");
                Require(f.Page.CurrentPhase == "Unavailable" && f.Game == null, "A compact portrait window cannot overlap status and track: " + height);
            }
            Set(f.Page, "landscape", true);
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
            var jump = (RectTransform)content.Find("Dino jump input");
            var duck = (RectTransform)content.Find("Dino duck input");
            var pause = (RectTransform)content.Find("暂停");
            Require(jump != null && duck != null && pause != null && pause.gameObject.activeSelf, "Actual running touch targets exist");
            Rect jumpArea = RelativeRect(content, jump), duckArea = RelativeRect(content, duck), pauseArea = RelativeRect(content, pause);
            Require(!jumpArea.Overlaps(duckArea) && !jumpArea.Overlaps(pauseArea) && !duckArea.Overlaps(pauseArea),
                "Actual world rectangles do not overlap, whether arranged in one row or separate rows");
            Require(jump.rect.width >= 96 && duck.rect.width >= 96 && pause.rect.width >= 96,
                "Narrow landscape retains useful target widths");
            Require(content.rect.Contains(jumpArea.min) && content.rect.Contains(jumpArea.max) &&
                content.rect.Contains(duckArea.min) && content.rect.Contains(duckArea.max) &&
                content.rect.Contains(pauseArea.min) && content.rect.Contains(pauseArea.max), "All three controls fit the page");
        }

        private static void InsetTrackGeometry(Fixture f)
        {
            foreach (var size in new[] { new Vector2(720, 1152), new Vector2(720, 1320), new Vector2(720, 1582),
                new Vector2(1515, 720), new Vector2(730, 720) })
            {
                f.ReadyAt(size.x, size.y);
                var content = Get<RectTransform>(f.Page, "content");
                var track = Get<DinoTrackView>(f.Page, "track").GetComponent<RectTransform>();
                Rect area = RelativeRect(content, track);
                Require(Math.Abs(area.xMin - 32) < .01 && Math.Abs(size.x - area.xMax - 32) < .01,
                    "Track has the same 32-unit scene inset as Jump at " + size);
                Require(track.rect.height > 0 && track.rect.height <= 320.1f,
                    "Track uses a compact bounded height at " + size);
                Require(Math.Abs(f.Game.Rules.ViewportWidth - track.rect.width / 1.2) < .01,
                    "Rules derive from the real inset track width at " + size);
                Require(Math.Abs(Get<double>(Get<DinoTrackView>(f.Page, "track"), "worldWidth") - f.Game.Rules.ViewportWidth) < .01,
                    "Renderer and Core agree on the same world width");
                var mask = track.GetComponent<Mask>() ?? track.GetComponentInParent<Mask>();
                Require(mask != null && mask.isActiveAndEnabled && mask.graphic != null,
                    "A stencil mask clips the actual track children");
                var image = mask.graphic as Image;
                Require(image != null && image.sprite != null && image.type == Image.Type.Sliced,
                    "Track mask uses the existing rounded sliced sprite");
                Require(track.rect.height / (track.rect.width / f.Game.Rules.ViewportWidth) >= 212,
                    "Compact track retains the full maximum-jump headroom");
            }
        }

        private static void SharedUiTokens(Fixture f)
        {
            f.ReadyAt(720, 1320);
            var content = Get<RectTransform>(f.Page, "content");
            var title = FindText(content, "Dino");
            Require(title != null && title.fontSize == 63 && title.fontStyle == FontStyle.Bold && title.color == UiFactory.Ink,
                "Portrait title uses Jump's 63-point bold Ink style");
            Require(Math.Abs(title.rectTransform.anchoredPosition.x - 56) < .01,
                "Portrait title retains the shared text inset");
            var brand = FindText(content, "XANADU / YONDER");
            Require(brand != null && brand.fontSize == 19 && brand.color == UiFactory.Muted,
                "Navigation uses the shared 19-point muted brand");
            foreach (var kind in new[] { UiIconKind.Back, UiIconKind.Restart, UiIconKind.Book, UiIconKind.Settings })
            {
                UiIconGraphic found = null;
                foreach (var icon in content.GetComponentsInChildren<UiIconGraphic>()) if (icon.Kind == kind) found = icon;
                Require(found != null, "Existing navigation icon is present: " + kind);
                var button = found.GetComponentInParent<Button>().GetComponent<RectTransform>();
                Require(button.rect.size == new Vector2(58, 58), "Navigation retains the shared 58-unit button size");
            }
            foreach (var field in new[] { "scoreLabel", "bestLabel" })
            {
                var value = Get<Text>(f.Page, field);
                var card = value.transform.parent.GetComponent<Image>();
                bool current = field == "scoreLabel";
                int size = current ? 52 : 44;
                Require(value.fontSize == size && value.fontStyle == (current ? FontStyle.Bold : FontStyle.Normal) &&
                    value.color == (current ? UiFactory.Ink : UiFactory.Muted) && value.resizeTextForBestFit &&
                    value.resizeTextMinSize == 14 && value.resizeTextMaxSize == size,
                    "Current score is prominent, best score secondary, and both fit large records");
                Require(card == null || card.color.a == 0, "Sketch scores do not sit inside filled cards");
                var label = FindText(content, current ? "本局分数" : "竖屏最高纪录");
                Require(label != null && label.fontSize == 19 && label.color == UiFactory.Muted,
                    "Score title remains a separate muted label");
            }
            Require(Get<Text>(f.Page, "scoreLabel").text == "0", "Score presentation has no invented leading-zero format");
            var warning = Get<Text>(f.Page, "warningLabel");
            Require(warning.fontSize == 18 && warning.color == UiFactory.Muted, "Saving status reuses the shared muted status style");
            foreach (var text in content.GetComponentsInChildren<Text>())
            {
                Require(text.font == UiFactory.Font, "Every visible UI label uses the shared bundled font");
                Require(!text.text.Contains("小恐龙"), "Visible product name is Dino");
            }
        }

        private static void SelectionReach(Fixture f)
        {
            Set(f.Page, "profile", new DinoSaveData { BestPortraitScore = 1234, BestLandscapeScore = 5678 });
            foreach (float height in new[] { 1320f, 1582f })
            {
                f.SelectionAt(720, height);
                var content = Get<RectTransform>(f.Page, "content");
                var portrait = (RectTransform)content.Find("Portrait choice");
                var landscape = (RectTransform)content.Find("Landscape choice");
                var start = (RectTransform)content.Find("Dino start");
                Require(portrait != null && landscape != null && start != null, "Both choices and the start action exist");
                Rect portraitArea = RelativeRect(content, portrait), landscapeArea = RelativeRect(content, landscape), startArea = RelativeRect(content, start);
                Require(-portraitArea.center.y >= height * .5f && -landscapeArea.center.y > -portraitArea.center.y,
                    "Orientation choices occupy the reachable lower half at " + height);
                Require(-startArea.center.y >= height * .84f && -startArea.yMin <= height - 96,
                    "Start stays near the bottom with footer clearance at " + height);
                Require(!portraitArea.Overlaps(landscapeArea) && !landscapeArea.Overlaps(startArea),
                    "Choice cards and start target do not overlap");
                foreach (var card in new[] { portrait, landscape })
                {
                    var preview = (RectTransform)card.Find("Orientation preview");
                    Require(preview != null && preview.rect.width > 0 && preview.rect.height > 0,
                        "Orientation card contains a real device outline");
                    Require(card == portrait ? preview.rect.height > preview.rect.width : preview.rect.width > preview.rect.height,
                        "Preview aspect ratio depicts its selected orientation");
                    foreach (var graphic in preview.GetComponentsInChildren<Graphic>())
                        Require(!graphic.raycastTarget, "Device preview does not steal the choice tap");
                    var record = FindText(card, card == portrait ? "1234" : "5678");
                    var caption = FindText(card, "最高纪录");
                    Require(record != null && caption != null && record.fontStyle == FontStyle.Bold && record.fontSize == 37 &&
                        record.resizeTextForBestFit && caption.fontSize == 19, "Record has a separate caption and bold numeric line");
                    Require(RelativeRect(card, record.rectTransform).yMax <= RelativeRect(card, caption.rectTransform).yMin + 2,
                        "Record value sits below its caption rather than sharing the same line");
                }
                landscape.GetComponent<Button>().onClick.Invoke();
                Require(Get<bool>(f.Page, "selectedLandscape") && f.Page.CurrentPhase == "Selecting",
                    "Selecting a preview does not start or rotate a run");
                Require(FindText(Get<RectTransform>(f.Page, "content"), "横屏开始") != null,
                    "The lower primary action reflects the chosen direction");
            }
        }

        private static void ShortSelectionScroll(Fixture f)
        {
            Set(f.Page, "profile", new DinoSaveData { BestPortraitScore = 1234, BestLandscapeScore = 5678 });
            foreach (float height in new[] { 960f, 720f })
            {
                Set(f.Page, "localDate", (Func<DateTime>)(() => height == 720 ? new DateTime(2026, 5, 31) : new DateTime(2026, 10, 27)));
                f.SelectionAt(720, height);
                var content = Get<RectTransform>(f.Page, "content");
                var scroll = content.GetComponentInChildren<ScrollRect>();
                Require(scroll != null && scroll.vertical && !scroll.horizontal && scroll.viewport != null && scroll.content != null,
                    "Short selection has a real vertical ScrollRect at " + height);
                Require(scroll.viewport.GetComponent<RectMask2D>() != null && scroll.content.rect.height > scroll.viewport.rect.height &&
                    scroll.viewport.rect.height >= 224, "The masked viewport fits at least one complete direction card");
                Rect viewportArea = RelativeRect(content, scroll.viewport);
                var start = (RectTransform)content.Find("Dino start");
                var description = FindText(content, "选择游玩方向，再向前跑远一点。");
                Require(viewportArea.yMax <= RelativeRect(content, description.rectTransform).yMin &&
                    RelativeRect(content, start).yMax <= viewportArea.yMin,
                    "Scrollable choices stay between the fixed heading and start button");
                var banner = (RectTransform)content.Find("Birthday banner");
                if (height == 720)
                {
                    Require(banner != null && FindText(banner, "生日快乐") != null, "Short birthday window retains the banner");
                    Rect bannerArea = RelativeRect(content, banner);
                    Require(bannerArea.yMax <= RelativeRect(content, description.rectTransform).yMin &&
                        viewportArea.yMax <= bannerArea.yMin, "Birthday banner does not overlap the heading or card viewport");
                }
                var portrait = (RectTransform)scroll.content.Find("Portrait choice");
                var landscape = (RectTransform)scroll.content.Find("Landscape choice");
                Require(portrait != null && landscape != null && portrait.rect.height == 224 && landscape.rect.height == 224,
                    "Short windows preserve both full-size choice cards inside the scroll content");
                scroll.verticalNormalizedPosition = 1;
                Require(scroll.viewport.rect.Contains(RelativeRect(scroll.viewport, FindText(portrait, "竖屏").rectTransform).center),
                    "The upper scroll limit exposes the portrait choice title");
                scroll.verticalNormalizedPosition = 0;
                Require(scroll.viewport.rect.Contains(RelativeRect(scroll.viewport, FindText(landscape, "5678").rectTransform).center),
                    "The lower scroll limit exposes the landscape record");

                // Place the landscape title at the middle of the actual viewport before tapping its card.
                scroll.verticalNormalizedPosition = 1;
                var landscapeTitle = FindText(landscape, "横屏").rectTransform;
                float titleY = -RelativeRect(scroll.viewport, landscapeTitle).center.y;
                float travel = scroll.content.rect.height - scroll.viewport.rect.height;
                float position = Mathf.Clamp01(1 - (titleY - scroll.viewport.rect.height * .5f) / travel);
                scroll.verticalNormalizedPosition = position;
                Require(scroll.viewport.rect.Contains(RelativeRect(scroll.viewport, landscapeTitle).center),
                    "The landscape choice can be visibly reached before selecting it");
                landscape.GetComponent<Button>().onClick.Invoke();
                var rebuilt = Get<RectTransform>(f.Page, "content");
                var rebuiltScroll = rebuilt.GetComponentInChildren<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                Require(Get<bool>(f.Page, "selectedLandscape") && f.Page.CurrentPhase == "Selecting" &&
                    Math.Abs(rebuiltScroll.verticalNormalizedPosition - position) < .001f,
                    "Choosing a direction preserves the scroll position through the real page rebuild");
                Require(FindText(rebuilt, "横屏开始") != null && f.Game == null && f.HomeCalls == 0,
                    "A short-window choice updates the fixed start action without starting a run");
            }
        }

        private static void ReturnHierarchy(Fixture f)
        {
            f.ReadyAt(720, 1320);
            ClickIcon(f.Page, UiIconKind.Back);
            f.FinishPortraitTurn();
            Require(f.Page.CurrentPhase == "Selecting" && f.Game == null && f.HomeCalls == 0,
                "Ready navigation returns to direction selection, not the lobby");

            f.ReadyAt(720, 1320); f.PressJump();
            for (int i = 0; i < 12; i++) f.Game.Step();
            var running = f.Game; long tick = running.Tick;
            f.Page.Back();
            Require(f.Page.CurrentPhase == "Paused" && f.HasText("结束本局并返回选择？") && running.Tick == tick && f.HomeCalls == 0,
                "Running Back freezes and confirms before discarding this run");
            var confirm = FindText(Get<RectTransform>(f.Page, "overlay"), "返回方向选择");
            Require(confirm != null && confirm.GetComponentInParent<Button>() != null, "Return confirmation has a concrete selection action");
            confirm.GetComponentInParent<Button>().onClick.Invoke();
            f.FinishPortraitTurn();
            Require(f.Page.CurrentPhase == "Selecting" && f.Game == null && f.HomeCalls == 0,
                "Confirmed game Back returns only one navigation level");

            f.ReadyAt(720, 1320); f.PressJump();
            for (int i = 0; i < 2400 && !f.Game.IsGameOver; i++) f.Game.Step();
            Require(f.Game.IsGameOver, "Result return test reaches an actual collision");
            Invoke(f.Page, "EndRun"); f.Page.Back(); f.FinishPortraitTurn();
            Require(f.Page.CurrentPhase == "Selecting" && f.HomeCalls == 0, "Result Back also returns to selection");
            ClickIcon(f.Page, UiIconKind.Home); f.FinishPortraitTurn();
            Require(f.HomeCalls == 1, "Only Back from the selection page leaves for the lobby");
        }

        private static void ControlsReach(Fixture f)
        {
            foreach (var size in new[] { new Vector2(720, 1152), new Vector2(720, 1320), new Vector2(720, 1582), new Vector2(1515, 720), new Vector2(730, 720) })
            {
                f.ReadyAt(size.x, size.y);
                var content = Get<RectTransform>(f.Page, "content");
                var jump = (RectTransform)content.Find("Dino jump input");
                var duck = (RectTransform)content.Find("Dino duck input");
                var pause = (RectTransform)content.Find("暂停");
                Require(jump != null && duck != null && pause != null && pause.gameObject.activeSelf,
                    "Ready always offers jump, duck and pause");
                Require(FindText(content, "更换方向") == null, "There is no redundant direction action on the game page");
                var jumpArea = RelativeRect(content, jump); var duckArea = RelativeRect(content, duck);
                var pauseArea = RelativeRect(content, pause);
                Require(FindText(pause, "暂停").fontStyle == FontStyle.Bold, "Pause has the requested bold emphasis");
                Require(FindText(content, "玩法说明") == null, "Help is not duplicated in the bottom controls");
                if (size.x > size.y)
                {
                    Require(Math.Abs(pauseArea.center.y - jumpArea.center.y) < .01 && Math.Abs(pauseArea.center.y - duckArea.center.y) < .01,
                        "Landscape pause is vertically centered with both action cards");
                    Require(!pauseArea.Overlaps(RelativeRect(content, Get<Text>(f.Page, "warningLabel").rectTransform)),
                        "Saving status does not overlap the centered pause control");
                }
                else Require(Math.Abs(size.y + pauseArea.yMin - 88) < .01, "Portrait pause retains its prior bottom offset");
                Require(jump.rect.width > duck.rect.width && jump.GetComponent<Image>().color == UiFactory.Accent,
                    "The larger right jump target is the primary action");
                Require(-jumpArea.center.y > size.y * .72f && -duckArea.center.y > size.y * .72f,
                    "Both action targets occupy the lower thumb area at " + size);
                Require(!jumpArea.Overlaps(duckArea) && !jumpArea.Overlaps(pauseArea) && !duckArea.Overlaps(pauseArea),
                    "Jump, duck and pause are separate touch targets");
                Require(Math.Abs(duckArea.xMin - 32) < .01 && Math.Abs(size.x - jumpArea.xMax - 32) < .01,
                    "Action cards align with the 32-unit playfield margins");
                Require(ContainsText(jump, "轻按") && ContainsText(jump, "按住") && ContainsText(duck, "按住"),
                    "Action instructions are contained in their associated cards");
            }
        }

        private static void HelpSpacing(Fixture f)
        {
            foreach (var size in new[] { new Vector2(720, 1320), new Vector2(1515, 720), new Vector2(730, 720) })
            {
                f.ReadyAt(size.x, size.y);
                ClickIcon(f.Page, UiIconKind.Book);
                var overlay = Get<RectTransform>(f.Page, "overlay");
                var panel = (RectTransform)overlay.Find("Dino dialog");
                var done = FindText(panel, "知道了").GetComponentInParent<Button>().GetComponent<RectTransform>();
                var license = FindText(panel, "素材与许可").GetComponentInParent<Button>().GetComponent<RectTransform>();
                Rect doneArea = RelativeRect(panel, done), licenseArea = RelativeRect(panel, license);
                float bottomGap = panel.rect.height + doneArea.yMin;
                Require(bottomGap >= 24 && bottomGap <= 40, "Help primary action has a normal 24..40 unit bottom margin");
                Require(licenseArea.yMin - doneArea.yMax >= 12 && !doneArea.Overlaps(licenseArea), "Help actions remain separated");
                Require(panel.rect.height <= size.y - 32 && panel.rect.Contains(doneArea.min) && panel.rect.Contains(doneArea.max),
                    "Help primary action fits the dialog in either orientation");
                done.GetComponent<Button>().onClick.Invoke();
                Require(Get<RectTransform>(f.Page, "overlay") == null && f.Page.CurrentPhase == "Ready", "Help closes back to the prepared game");
            }
        }

        private static void ClickIcon(DinoPage page, UiIconKind kind)
        {
            var content = Get<RectTransform>(page, "content");
            foreach (var icon in content.GetComponentsInChildren<UiIconGraphic>())
                if (icon.Kind == kind) { icon.GetComponentInParent<Button>().onClick.Invoke(); return; }
            throw new InvalidOperationException("Missing navigation icon " + kind);
        }

        private static Rect RelativeRect(RectTransform ancestor, RectTransform child)
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var min = ancestor.InverseTransformPoint(corners[0]);
            var max = ancestor.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void BirthdayEntry(Fixture f)
        {
            foreach (var date in new[] { new DateTime(2026, 5, 31), new DateTime(2026, 6, 30) })
            {
                Set(f.Page, "localDate", (Func<DateTime>)(() => date));
                f.Page.Enter(); f.FakeViewport(720, 1320, false); f.CacheViewport();
                Invoke(f.Page, "Update"); Invoke(f.Page, "Update");
                Require(f.Page.CurrentPhase == "Selecting", "Birthday reaches normal direction selection");
                var content = Get<RectTransform>(f.Page, "content");
                var greeting = FindText(content, "生日快乐");
                Require(Get<DinoCelebration>(f.Page, "celebration") == DinoCelebration.Birthday && greeting != null,
                    "Both requested lunar birthdays show the greeting through the real calendar");
                Require(Get<RectTransform>(f.Page, "overlay") == null && !greeting.raycastTarget,
                    "Greeting does not open a blocking dialog or consume touch input");
                Require(greeting.font == UiFactory.Font && greeting.color == UiFactory.Ink,
                    "Birthday banner keeps the page's shared font and text color");
            }
            Set(f.Page, "localDate", (Func<DateTime>)(() => new DateTime(2026, 10, 27)));
            f.Page.Enter(); f.FakeViewport(720, 1320, false); f.CacheViewport();
            Invoke(f.Page, "Update"); Invoke(f.Page, "Update");
            Require(Get<DinoCelebration>(f.Page, "celebration") == DinoCelebration.None &&
                FindText(Get<RectTransform>(f.Page, "content"), "生日快乐") == null,
                "Ordinary-day re-entry removes the old birthday banner");
        }

        private static void CelebrationHatPoses(Fixture f)
        {
            Set(f.Page, "localDate", (Func<DateTime>)(() => new DateTime(2026, 5, 31)));
            f.ReadyAt(730, 720);
            var game = f.Game;
            var track = Get<DinoTrackView>(f.Page, "track");
            var player = Get<RawImage>(track, "player");
            var hat = Get<MaskableGraphic>(track, "celebrationHat");
            Require(hat != null && hat.isActiveAndEnabled && hat.transform.parent == player.transform && !hat.raycastTarget,
                "Birthday hat is a non-interactive child of the real player");
            var standing = hat.rectTransform.anchoredPosition;
            double modelWidth = game.Rules.PlayerWidth, modelHeight = game.Rules.PlayerHeight;
            long tick = game.Tick; double distance = game.Distance;
            track.Render(game, 0);
            Require(game.Tick == tick && game.Distance == distance && game.Rules.PlayerWidth == modelWidth && game.Rules.PlayerHeight == modelHeight,
                "Rendering a hat does not enlarge collision geometry or advance simulation");
            game.SetDuck(true); track.Render(game, 0);
            var duck = hat.rectTransform.anchoredPosition;
            Require(duck.x > standing.x + 10 && duck.y < standing.y - 10,
                "Ducking moves the hat to the lowered forward head instead of leaving it floating");
            game.SetDuck(false); track.Render(game, 0);
            Require(Vector2.Distance(hat.rectTransform.anchoredPosition, standing) < .01f,
                "Releasing duck restores the standing head anchor");
            f.PressJump(); double highest = 0;
            for (int i = 0; i < 90; i++)
            {
                game.Step(); track.Render(game, (float)DinoRules.StepSeconds);
                highest = Math.Max(highest, game.PlayerY);
                AssertHatInsideTrack(track, hat);
            }
            Require(highest > 100 && !game.Jumping && !game.IsGameOver,
                "A complete held jump keeps the real hat inside the compact landscape track");
            Require(Vector2.Distance(hat.rectTransform.anchoredPosition, standing) < .01f,
                "Running and jumping retain the head-relative hat anchor");
            for (int i = 0; i < 2400 && !game.IsGameOver; i++) game.Step();
            Require(game.IsGameOver, "Uncontrolled run reaches an actual collision");
            track.Render(game, 0);
            Require(hat.isActiveAndEnabled && Vector2.Distance(hat.rectTransform.anchoredPosition, standing) < .01f,
                "Collision uses the standing head anchor and retains its cosmetic hat");
        }

        private static void CelebrationLifecycle(Fixture f)
        {
            var localDate = new DateTime(2026, 12, 25);
            Set(f.Page, "localDate", (Func<DateTime>)(() => localDate));
            f.ReadyAt(720, 1320); f.PressJump();
            for (int i = 0; i < 12; i++) f.Game.Step();
            var game = f.Game; long tick = game.Tick;
            f.Page.Suspend(); localDate = new DateTime(2026, 10, 27);
            var oldTrack = Get<DinoTrackView>(f.Page, "track");
            f.FakeViewport(720, 1582, false); f.CacheViewport(); Invoke(f.Page, "BuildGame");
            var rebuilt = Get<DinoTrackView>(f.Page, "track");
            Require(!ReferenceEquals(oldTrack, rebuilt) && ReferenceEquals(game, f.Game) && game.Tick == tick,
                "Paused layout replacement retains the same dated game");
            Require(Get<DinoCelebration>(f.Page, "celebration") == DinoCelebration.Christmas &&
                Get<MaskableGraphic>(rebuilt, "celebrationHat").isActiveAndEnabled,
                "The current entry keeps its sampled hat when the clock or layout changes");
            f.ReadyAt(720, 1320);
            var ordinaryHat = Get<MaskableGraphic>(Get<DinoTrackView>(f.Page, "track"), "celebrationHat");
            Require(Get<DinoCelebration>(f.Page, "celebration") == DinoCelebration.None &&
                (ordinaryHat == null || !ordinaryHat.isActiveAndEnabled), "Re-entry rechecks local date and removes the stale hat");
        }

        private static void AssertHatInsideTrack(DinoTrackView track, MaskableGraphic hat)
        {
            var trackRect = track.GetComponent<RectTransform>();
            var trackBounds = trackRect.rect;
            var bounds = RelativeRect(trackRect, hat.rectTransform);
            Require(bounds.xMin >= trackBounds.xMin - .1f && bounds.xMax <= trackBounds.xMax + .1f &&
                bounds.yMin >= trackBounds.yMin - .1f && bounds.yMax <= trackBounds.yMax + .1f,
                "Full hat geometry stays within the clipped track through the jump");
        }

        private static Text FindText(Transform parent, string value)
        {
            foreach (var text in parent.GetComponentsInChildren<Text>()) if (text.text == value) return text;
            return null;
        }

        private static bool ContainsText(Transform parent, string value)
        {
            foreach (var text in parent.GetComponentsInChildren<Text>()) if (text.text.Contains(value)) return true;
            return false;
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
                Set(Page, "localDate", (Func<DateTime>)(() => new DateTime(2026, 10, 27)));
            }
            internal void Ready()
            {
                Page.Enter();
                Set(Page, "landscape", Get<bool>(Viewport, "Landscape"));
                Invoke(Page, "StartNewGame"); Set(Page, "unlockAt", -1f);
                Require(Page.CurrentPhase == "Ready" && Game != null, "Prepared real page");
            }
            internal void ReadyAt(float width, float height)
            {
                Page.Enter(); Set(Page, "landscape", width > height);
                FakeViewport(width, height, width > height); CacheViewport();
                Invoke(Page, "StartNewGame"); Set(Page, "unlockAt", -1f);
                Require(Page.CurrentPhase == "Ready" && Game != null, "Prepared real page at " + width + "x" + height);
            }
            internal void SelectionAt(float width, float height)
            {
                Page.Enter(); FakeViewport(width, height, false); CacheViewport();
                Invoke(Page, "Update"); Invoke(Page, "Update");
                Require(Page.CurrentPhase == "Selecting", "Reached selection through the real entry flow");
            }
            internal void FinishPortraitTurn()
            {
                FakeViewport(720, 1320, false); CacheViewport();
                Invoke(Page, "Update"); Invoke(Page, "Update");
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
