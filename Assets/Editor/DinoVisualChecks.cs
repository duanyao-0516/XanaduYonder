using System;
using System.Collections;
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
    // Renders the production page; only the viewport and sample records are controlled fixtures.
    // No device state, user save, player settings, or running gameplay is written by this check.
    [InitializeOnLoad]
    public static class DinoVisualChecks
    {
        private const string Pending = "Xanadu.DinoVisualChecks.Pending";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly StringBuilder Report = new StringBuilder();
        private static string output;
        private static int assertions;

        static DinoVisualChecks() { EditorApplication.playModeStateChanged += StateChanged; }

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
            new GameObject("Dino visual validation runner").AddComponent<Runner>().StartCoroutine(AfterFrame());
        }

        private static IEnumerator AfterFrame() { yield return null; Execute(); }

        private static void Execute()
        {
            int exit = 0;
            var oldFont = UiFactory.Font;
            assertions = 0; Report.Clear();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/validation/dino-ui"));
            Directory.CreateDirectory(output);
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Require(UiFactory.Font != null, "Bundled shared font loads");
                foreach (var size in new[] { new Vector2Int(720, 1152), new Vector2Int(720, 1320), new Vector2Int(720, 1582),
                    new Vector2Int(1515, 720), new Vector2Int(730, 720) })
                using (var fixture = new Fixture(size.x, size.y))
                {
                    if (size.x < size.y) { fixture.Selection(); fixture.Capture("selection"); }
                    fixture.Ready();
                    fixture.CheckTrackGeometry();
                    fixture.Capture("ready", false, size.x == 1515 ? 0 : -1);
                    if (size.x == 720 && size.y == 1320)
                    {
                        fixture.Open("ShowHelp"); fixture.Capture("help");
                        fixture.Open("ShowSettings"); fixture.Capture("settings");
                        fixture.Ready(); fixture.Pause(); fixture.Capture("paused");
                    }
                    else if (size.x == 1515)
                    {
                        // Reuse the daylight-rendered view so UGUI already owns its cached stencil material.
                        fixture.Night(); fixture.Capture("night", true, 1);
                        fixture.Day(); fixture.Capture("day-restored", false, 0);
                        fixture.SecondNight(); fixture.Capture("second-night", true, 1);
                        fixture.CheckNightResize();
                        fixture.Ready(); fixture.CheckDayReset();
                        fixture.Open("ShowSettings"); fixture.Capture("settings");
                        fixture.Open("ShowHelp"); fixture.Capture("help");
                    }
                    Report.AppendLine("PASS production UI render " + size.x + "x" + size.y);
                }
                using (var fixture = new Fixture(720, 960))
                {
                    fixture.Selection(); fixture.CaptureShortSelection();
                }
                using (var fixture = new Fixture(720, 720, new DateTime(2026, 5, 31)))
                {
                    fixture.Selection(); fixture.CheckBirthdayBanner(); fixture.CaptureShortSelection();
                }
                foreach (var date in new[] { new DateTime(2026, 1, 1), new DateTime(2026, 10, 31),
                    new DateTime(2026, 12, 25), new DateTime(2026, 5, 31), new DateTime(2026, 6, 30) })
                using (var fixture = new Fixture(720, 1320, date))
                {
                    var celebration = DinoCelebrationCalendar.Resolve(date);
                    if (celebration == DinoCelebration.Birthday)
                    {
                        fixture.Selection(); fixture.CheckBirthdayBanner();
                        fixture.Capture("birthday-selection-" + date.ToString("MMdd"));
                    }
                    fixture.Ready(); fixture.CheckCelebration(celebration);
                    fixture.Capture("hat-" + celebration.ToString().ToLowerInvariant() + "-" + date.ToString("MMdd"));
                    if (celebration == DinoCelebration.Christmas)
                    {
                        fixture.Night(); fixture.Capture("christmas-night", true);
                    }
                }
                using (var fixture = new Fixture(1515, 720, new DateTime(2026, 5, 31)))
                {
                    fixture.Ready(); fixture.CheckCelebration(DinoCelebration.Birthday);
                    fixture.Capture("birthday-landscape");
                }
                Report.AppendLine("PASS actual local-date resolution, birthday banners and all four celebration hat renders");
                Report.AppendLine("LIMITS: controlled viewport and sample scores; screenshots use production Unity UGUI / Camera.Render.");
                Report.AppendLine("LIMITS: does not simulate Android rotation, physical touch, audio, or device safe-area delivery.");
                Report.AppendLine("DINO_VISUAL_CHECKS_PASS: " + assertions + " assertions");
            }
            catch (Exception error) { exit = 1; Report.AppendLine("DINO_VISUAL_CHECKS_FAIL: " + error); Debug.LogException(error); }
            finally
            {
                UiFactory.Font = oldFont;
                File.WriteAllText(Path.Combine(output, "report.txt"), Report.ToString());
                Debug.Log(Report.ToString());
                EditorApplication.Exit(exit);
            }
        }

        private static void Require(bool condition, string message)
        { assertions++; if (!condition) throw new InvalidOperationException(message); }

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
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly int width, height;
            private readonly GameObject owner;
            private readonly RectTransform canvasRoot, pageRoot;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly DinoPage page;
            private readonly string directory;
            private RawImage palettePlayer;
            private int initialDayPixels;

            internal Fixture(int width, int height, DateTime? localDate = null)
            {
                this.width = width; this.height = height;
                owner = new GameObject("Dino visual fixture");
                canvasRoot = new GameObject("Dino fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
                canvasRoot.SetParent(owner.transform, false); canvasRoot.sizeDelta = new Vector2(width, height);
                camera = new GameObject("Dino fixture camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(owner.transform, false); camera.transform.position = new Vector3(0, 0, -10);
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = height * .5f;
                camera.aspect = (float)width / height; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiFactory.Paper;
                target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
                var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                pageRoot = UiFactory.Rect(canvasRoot, "Dino production page", 0, 0, width, height);
                page = pageRoot.gameObject.AddComponent<DinoPage>(); page.enabled = false;
                page.Initialize(pageRoot, canvasRoot, () => { }, value => { }, () => true, () => false, () => false, () => { }, () => { });
                directory = Path.Combine(output, "temporary-save-" + Guid.NewGuid().ToString("N"));
                Set(page, "store", new DinoSaveStore(directory));
                Set(page, "profile", new DinoSaveData { BestPortraitScore = 812, BestLandscapeScore = 943 });
                Set(page, "localDate", (Func<DateTime>)(() => localDate ?? new DateTime(2026, 10, 27)));
                page.Enter(); ConfigureViewport();
            }

            private void ConfigureViewport()
            {
                var viewport = Get<object>(page, "viewport");
                Set(viewport, "Width", (float)width); Set(viewport, "Height", (float)height); Set(viewport, "Landscape", width > height);
                Rect safe = Screen.safeArea;
                if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
                Set(viewport, "lastSafe", safe); Set(viewport, "lastPixels", new Vector2(Screen.width, Screen.height));
                Set(viewport, "lastCanvas", canvasRoot.rect.size);
                pageRoot.anchorMin = pageRoot.anchorMax = pageRoot.pivot = new Vector2(.5f, .5f);
                pageRoot.localScale = Vector3.one; pageRoot.sizeDelta = new Vector2(width, height); pageRoot.anchoredPosition = Vector2.zero;
            }

            internal void Selection()
            {
                Set(page, "phase", Enum.Parse(typeof(DinoPage).GetNestedType("Phase", BindingFlags.NonPublic), "Selecting"));
                Invoke(page, "BuildSelection");
            }

            internal void Ready()
            {
                ConfigureViewport(); Set(page, "landscape", width > height);
                Invoke(page, "StartNewGame"); Set(page, "unlockAt", -1f);
                Require(page.CurrentPhase == "Ready", "Requested visual viewport reaches Ready: " + width + "x" + height);
            }

            internal void CaptureShortSelection()
            {
                var content = Get<RectTransform>(page, "content");
                var scroll = content.GetComponentInChildren<ScrollRect>();
                Require(scroll != null && scroll.viewport.rect.height > 0 && scroll.content.rect.height > scroll.viewport.rect.height,
                    "Short selection renders a real scrollable choice area: " + width + "x" + height);
                scroll.verticalNormalizedPosition = 1;
                Capture("selection-scroll-top");
                scroll.verticalNormalizedPosition = 0;
                Capture("selection-scroll-bottom");
                scroll.verticalNormalizedPosition = .35f;
                Canvas.ForceUpdateCanvases();
                scroll.content.Find("Landscape choice").GetComponent<Button>().onClick.Invoke();
                var rebuiltScroll = Get<RectTransform>(page, "content").GetComponentInChildren<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                Require(Math.Abs(rebuiltScroll.verticalNormalizedPosition - .35f) < .001f &&
                    Get<bool>(page, "selectedLandscape") && Get<DinoGame>(page, "game") == null,
                    "Visible choice selection preserves its actual scroll position without starting gameplay");
                Capture("selection-scroll-selected");
                Report.AppendLine("PASS short selection scroll render " + width + "x" + height);
            }

            internal void Pause()
            {
                Invoke(page, "JumpDown", -101);
                var game = Get<DinoGame>(page, "game");
                for (int i = 0; i < 12; i++) game.Step();
                page.Suspend();
                Require(page.CurrentPhase == "Paused", "Visual pause uses production lifecycle");
            }

            internal void Open(string method) { Invoke(page, method); }

            internal void Night()
            {
                var game = Get<DinoGame>(page, "game");
                var track = Get<DinoTrackView>(page, "track");
                Require(track.BackgroundColor == Color.white, "New run begins with a white sky");
                Invoke(page, "JumpDown", -101);
                Require(game.IsStarted, "Day-night replay starts through the page jump callback");
                AdvanceTo(29.9);
                Require(track.BackgroundColor == Color.white, "The first 29.9 running seconds stay white");
                AdvanceTo(30.4);
                var twilight = track.BackgroundColor;
                Require(twilight.r > .35f && twilight.r < .65f && Math.Abs(twilight.r - twilight.g) < .001f &&
                    Math.Abs(twilight.r - twilight.b) < .001f, "First dusk fades through neutral grey rather than switching abruptly");
                long pausedTick = game.Tick;
                for (int i = 0; i < 180; i++) track.Render(game, 0);
                Require(game.Tick == pausedTick && track.BackgroundColor == twilight, "A paused presentation freezes both run time and dusk blend");
                Capture("twilight");
                AdvanceTo(31);
                Require(track.BackgroundColor == Color.black, "First night is fully black by 31 running seconds");
            }

            internal void Day()
            {
                var track = Get<DinoTrackView>(page, "track");
                AdvanceTo(59.9);
                Require(track.BackgroundColor == Color.black, "Night remains black before the 60-second dawn");
                AdvanceTo(60.4);
                Require(track.BackgroundColor.r > .35f && track.BackgroundColor.r < .65f, "Dawn also uses the neutral gradual transition");
                AdvanceTo(61);
                Require(track.BackgroundColor == Color.white, "The same run returns to white by 61 seconds");
                var game = Get<DinoGame>(page, "game");
                while (game.Jumping || game.Ducking) AdvanceTo(game.Elapsed + DinoRules.StepSeconds);
            }

            internal void SecondNight()
            {
                AdvanceTo(91);
                Require(Get<DinoTrackView>(page, "track").BackgroundColor == Color.black, "Day and night continue alternating during one real run");
                Report.AppendLine("PASS actual Game.Step replay: white at 29.9s, dusk at 30.4s, black at 31s, dawn at 60.4s, white at 61s, black at 91s");
            }

            internal void CheckDayReset()
            {
                Require(Get<DinoGame>(page, "game").Tick == 0 && Get<DinoTrackView>(page, "track").BackgroundColor == Color.white,
                    "Restart discards the previous run's night phase");
            }

            internal void CheckNightResize()
            {
                var game = Get<DinoGame>(page, "game");
                var oldTrack = Get<DinoTrackView>(page, "track");
                var oldHeight = oldTrack.GetComponent<RectTransform>().rect.height;
                var oldState = oldTrack.Capture(game);
                long tick = game.Tick;
                page.Suspend();
                // The normal landscape track reaches its height cap, so shrink to exercise a real layout replacement.
                Set(Get<object>(page, "viewport"), "Height", height - 24f);
                Invoke(page, "BuildGame");
                var rebuilt = Get<DinoTrackView>(page, "track");
                var newState = rebuilt.Capture(game);
                Require(!ReferenceEquals(oldTrack, rebuilt) && rebuilt.GetComponent<RectTransform>().rect.height != oldHeight,
                    "Night regression rebuilds a genuinely changed track layout");
                Require(ReferenceEquals(game, Get<DinoGame>(page, "game")) && game.Tick == tick && rebuilt.BackgroundColor == Color.black,
                    "Resizing a paused real night retains the same game and palette");
                Require(Math.Abs(Get<double>(oldState, "AnimationClock") - Get<double>(newState, "AnimationClock")) < 1e-9,
                    "Real night resize preserves presentation animation time");
            }

            internal void CheckCelebration(DinoCelebration expected)
            {
                Require(Get<DinoCelebration>(page, "celebration") == expected, "Page snapshots the real local calendar result: " + expected);
                var track = Get<DinoTrackView>(page, "track");
                var hat = Get<MaskableGraphic>(track, "celebrationHat");
                var player = Get<RawImage>(track, "player");
                Require(hat != null && hat.isActiveAndEnabled && hat.transform.parent == player.transform,
                    "Celebration hat is a visible child of the actual player: " + expected);
            }

            internal void CheckBirthdayBanner()
            {
                Text greeting = null;
                foreach (var text in pageRoot.GetComponentsInChildren<Text>()) if (text.text == "生日快乐") greeting = text;
                Require(greeting != null, "Birthday entry displays the requested greeting");
                Require(Get<RectTransform>(page, "overlay") == null && !greeting.raycastTarget,
                    "Birthday greeting is a small non-blocking page banner");
                var corners = new Vector3[4]; greeting.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                    Require(point.x >= 0 && point.x <= width && point.y >= 0 && point.y <= height,
                        "Birthday greeting remains inside the rendered viewport");
                }
            }

            private void AdvanceTo(double seconds)
            {
                var game = Get<DinoGame>(page, "game");
                var track = Get<DinoTrackView>(page, "track");
                long targetTick = (long)Math.Round(seconds / DinoRules.StepSeconds);
                while (game.Tick < targetTick)
                {
                    bool duck = false;
                    foreach (var obstacle in game.Obstacles)
                        if (obstacle.Kind == DinoObstacleKind.MidBird && game.Tick >= obstacle.RecommendedActionTick && game.Tick < obstacle.RecoveryTick) duck = true;
                    game.SetDuck(duck);
                    foreach (var obstacle in game.Obstacles)
                        if (obstacle.Kind != DinoObstacleKind.MidBird && game.Tick == obstacle.RecommendedActionTick)
                            Require(game.Jump(), "Certified jump is available during visual replay");
                    game.Step();
                    if (game.IsGameOver) throw new InvalidOperationException("Real generated obstacle collision during day-night replay at " + game.Elapsed);
                    track.Render(game, (float)DinoRules.StepSeconds);
                }
                Get<Text>(page, "stateLabel").text = "";
                Invoke(page, "RefreshScore");
            }

            internal void CheckTrackGeometry()
            {
                var track = Get<DinoTrackView>(page, "track").GetComponent<RectTransform>();
                var content = Get<RectTransform>(page, "content");
                var corners = new Vector3[4]; track.GetWorldCorners(corners);
                float left = content.InverseTransformPoint(corners[0]).x;
                float right = content.InverseTransformPoint(corners[2]).x;
                Require(Math.Abs(left - 32) < .1 && Math.Abs(width - right - 32) < .1,
                    "Actual track matches Jump's 32-unit scene margins");
                Require(track.rect.height <= 320.1f, "Track height stays compact");
                Require(Math.Abs(page.WorldWidth - track.rect.width / 1.2) < .01, "Core width uses visible track width");
                var mask = track.GetComponent<Mask>() ?? track.GetComponentInParent<Mask>();
                Require(mask != null && mask.isActiveAndEnabled, "Rounded track has an active UGUI stencil mask");
                foreach (var text in pageRoot.GetComponentsInChildren<Text>())
                {
                    Require(text.font == UiFactory.Font, "Shared font: " + text.text);
                    Require(!text.text.Contains("小恐龙"), "Product UI uses Dino name");
                }
            }

            internal void Capture(string name, bool verifyRoundedNight = false, int spritePalette = -1)
            {
                Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases();
                var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + "-" + width + "x" + height + ".png"), image.EncodeToPNG());
                    foreach (var icon in pageRoot.GetComponentsInChildren<UiIconGraphic>())
                    {
                        var mesh = icon.canvasRenderer.GetMesh();
                        Require(mesh != null && mesh.vertexCount > 0, "Navigation icon submits a visible mesh: " + icon.Kind);
                    }
                    if (verifyRoundedNight) CheckRoundedPixels(image);
                    if (spritePalette >= 0) CheckSpritePalette(image, spritePalette == 1, name);
                    CheckHatPixels(image, name);
                }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }

            private void CheckHatPixels(Texture2D image, string label)
            {
                var track = Get<DinoTrackView>(page, "track");
                if (track == null) return;
                var hat = Get<MaskableGraphic>(track, "celebrationHat");
                if (Get<DinoCelebration>(page, "celebration") == DinoCelebration.None)
                {
                    Require(hat == null || !hat.isActiveAndEnabled, "Ordinary dates have no rendered holiday hat");
                    return;
                }
                Require(hat != null && hat.isActiveAndEnabled, "Dated celebration has an enabled hat graphic");
                var mesh = hat.canvasRenderer.GetMesh();
                Require(mesh != null && mesh.vertexCount > 0, "Hat submits real visible geometry: " + label);
                var corners = new Vector3[4]; hat.rectTransform.GetWorldCorners(corners);
                var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                int pixels = 0;
                for (int y = Mathf.Max(0, Mathf.CeilToInt(min.y)); y <= Mathf.Min(height - 1, Mathf.FloorToInt(max.y)); y++)
                for (int x = Mathf.Max(0, Mathf.CeilToInt(min.x)); x <= Mathf.Min(width - 1, Mathf.FloorToInt(max.x)); x++)
                {
                    var pixel = image.GetPixel(x, y);
                    if (Mathf.Max(pixel.r, pixel.g, pixel.b) - Mathf.Min(pixel.r, pixel.g, pixel.b) > .12f) pixels++;
                }
                Require(pixels >= 20, "Rendered hat contains actual colored pixels: " + label + " (" + pixels + ")");
                Report.AppendLine("PASS celebration pixels " + label + ": colored=" + pixels);
            }

            private void CheckSpritePalette(Texture2D image, bool night, string label)
            {
                var player = Get<RawImage>(Get<DinoTrackView>(page, "track"), "player");
                if (palettePlayer == null) palettePlayer = player;
                Require(ReferenceEquals(player, palettePlayer), "Palette regression uses the same rendered sprite: " + label);

                // Test the material submitted through MaskableGraphic, not only its mutable base material.
                var material = player.materialForRendering;
                Require(material != null && material.HasProperty("_Color"), "Masked sprite has a render tint: " + label);
                float expectedGain = night ? 218f / 83f : 1f;
                var tint = material.GetColor("_Color");
                Require(Math.Abs(tint.r - expectedGain) < .01f && Math.Abs(tint.g - expectedGain) < .01f &&
                    Math.Abs(tint.b - expectedGain) < .01f, "Cached stencil material receives the current palette: " + label);

                var corners = new Vector3[4]; player.rectTransform.GetWorldCorners(corners);
                var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                int left = Mathf.Clamp(Mathf.CeilToInt(min.x), 0, width - 1);
                int right = Mathf.Clamp(Mathf.FloorToInt(max.x), 0, width - 1);
                int bottom = Mathf.Clamp(Mathf.CeilToInt(min.y), 0, height - 1);
                int top = Mathf.Clamp(Mathf.FloorToInt(max.y), 0, height - 1);
                float expectedBody = (night ? 218f : 83f) / 255f;
                int bodyPixels = 0;
                for (int y = bottom; y <= top; y++)
                for (int x = left; x <= right; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    // The source's white outline and the black/white sky are outside this grey range.
                    if (Math.Abs(pixel.r - expectedBody) < .025f && Math.Abs(pixel.g - expectedBody) < .025f &&
                        Math.Abs(pixel.b - expectedBody) < .025f) bodyPixels++;
                }
                Require(bodyPixels >= 100, "Actual dinosaur body pixels use the current palette: " + label + " (" + bodyPixels + ")");
                if (!night && initialDayPixels == 0) initialDayPixels = bodyPixels;
                else if (!night) Require(bodyPixels >= initialDayPixels * .8f,
                    "Returning to day restores the rendered dinosaur body, including cached stencil state");
                Report.AppendLine("PASS sprite pixels " + label + ": body=" + bodyPixels + ", render tint=" + tint.r.ToString("F3"));
            }

            private void CheckRoundedPixels(Texture2D image)
            {
                var track = Get<DinoTrackView>(page, "track").GetComponent<RectTransform>();
                var corners = new Vector3[4]; track.GetWorldCorners(corners);
                var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                var corner = image.GetPixel(Mathf.RoundToInt(min.x) + 2, Mathf.RoundToInt(max.y) - 3);
                var middle = image.GetPixel(Mathf.RoundToInt((min.x + max.x) * .5f), Mathf.RoundToInt((min.y + max.y) * .5f));
                Require(Math.Abs(corner.r - UiFactory.Paper.r) < .025f && Math.Abs(corner.g - UiFactory.Paper.g) < .025f,
                    "Actual rendered corner clips the dark sky and exposes page Paper");
                Require(middle.r < .4f && middle.g < .4f && middle.b < .4f, "Rounded mask retains the actual night sky inside");
            }

            public void Dispose()
            {
                Set(page, "active", false); page.Save();
                camera.targetTexture = null;
                if (RenderTexture.active == target) RenderTexture.active = null;
                Object.DestroyImmediate(owner); target.Release(); Object.DestroyImmediate(target);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
