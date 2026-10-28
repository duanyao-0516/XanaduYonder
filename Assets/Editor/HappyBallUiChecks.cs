using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core.HappyBall;
using XanaduYonder.Runtime;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Production page, real UGUI camera render, disposable test storage only.
    [InitializeOnLoad]
    public static class HappyBallUiChecks
    {
        private const string Pending = "Xanadu.HappyBallChecks.Pending";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly StringBuilder Report = new StringBuilder();
        private static int checks;
        private static string output;
        static HappyBallUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }
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
            new GameObject("HappyBall UI runner").AddComponent<Runner>().StartCoroutine(Execute());
        }
        private static IEnumerator Execute()
        {
            yield return null;
            int exit = 0;
            output = Path.GetFullPath("artifacts/validation/happy-ball-ui");
            Directory.CreateDirectory(output);
            Report.Clear(); checks = 0;
            var oldFont = UiFactory.Font;
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Require(UiFactory.Font != null, "Noto font is present");
                var pageType = typeof(AppController).Assembly.GetType("XanaduYonder.Runtime.HappyBall.HappyBallPage");
                Require(pageType != null, "Production HappyBall page exists");
                foreach (int height in new[] { 1320, 1582 })
                using (var fixture = new Fixture(height, pageType))
                {
                    fixture.CheckLayout();
                    fixture.CheckLifecycle();
                    fixture.CheckDeathFeedback();
                    fixture.CheckReferenceSequence();
                    fixture.CheckHeavySequence();
                    fixture.CheckHighWallSequence();
                    fixture.Capture("normal-720x" + height);
                    fixture.CheckSplit();
                    fixture.Capture("split-720x" + height);
                    fixture.CheckSceneContracts();
                    fixture.ShowProgressedScene();
                    fixture.Capture("progressed-720x" + height);
                    fixture.CheckNoColliders();
                }
                Report.AppendLine("HAPPY_BALL_UI_CHECKS_PASS: " + checks + " assertions");
            }
            catch (Exception error) { exit = 1; Report.AppendLine("HAPPY_BALL_UI_CHECKS_FAIL\n" + error); Debug.LogException(error); }
            finally
            {
                UiFactory.Font = oldFont;
                File.WriteAllText(Path.Combine(output, "report.txt"), Report.ToString());
                Debug.Log(Report.ToString());
                EditorApplication.Exit(exit);
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks++; Report.AppendLine("PASS " + message); }
        private static object Get(object value, string name) { return value.GetType().GetField(name, Members).GetValue(value); }
        private static object Invoke(object value, string method, params object[] args)
        { return value.GetType().GetMethod(method, Members).Invoke(value, args); }
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root;
            private readonly Camera camera;
            private readonly RenderTexture texture;
            private readonly RectTransform frame;
            private readonly Component page;
            private readonly string directory;
            private readonly int height;
            public Fixture(int height, Type pageType)
            {
                this.height = height;
                directory = Path.Combine(output, "store-" + Guid.NewGuid().ToString("N"));
                Root = new GameObject("HappyBall UI fixture");
                camera = new GameObject("Fixture UI camera").AddComponent<Camera>();
                camera.transform.SetParent(Root.transform); camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiFactory.Paper;
                camera.cullingMask = 1 << 5; camera.orthographic = true; camera.enabled = false;
                camera.orthographicSize = height * .5f; camera.aspect = 720f / height;
                camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                texture = new RenderTexture(720, height, 24); texture.Create(); camera.targetTexture = texture;
                var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(Root.transform);
                var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                var rootRect = (RectTransform)canvasObject.transform;
                rootRect.sizeDelta = new Vector2(720, height);
                frame = UiFactory.Rect(rootRect, "Fixture page", 0, 0, 720, height);
                UiFactory.Stretch(frame);
                if (EventSystem.current == null) new GameObject("Events", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(Root.transform);
                page = frame.gameObject.AddComponent(pageType);
                var assembly = typeof(AppController).Assembly;
                var storeType = assembly.GetType("XanaduYonder.Runtime.HappyBall.HappyBallSaveStore");
                var saveStore = Activator.CreateInstance(storeType, directory);
                Action<string, string, string, Action, string, Action> dialog = (a, b, c, d, e, f) => { };
                Invoke(page, "Initialize", frame, (Action)(() => { }), (Action<Action>)(after => after()),
                    (Func<bool>)(() => true), (Func<bool>)(() => false), (Func<bool>)(() => false), null,
                    (Action<string>)(_ => { }), dialog, saveStore, null);
                SetUiLayer(canvasObject.transform);
                Canvas.ForceUpdateCanvases();
                Invoke(page, "Enter");
            }
            private static void SetUiLayer(Transform root)
            { root.gameObject.layer = 5; foreach (Transform child in root) SetUiLayer(child); }
            public void CheckLayout()
            {
                Canvas.ForceUpdateCanvases();
                var texts = frame.GetComponentsInChildren<Text>(true);
                Require(texts.Count(t => t.text == "欢乐球球") == 1, "One centered game title");
                Require(!texts.Any(t => t.text.Contains("XANADU") || t.text.Contains("YONDER") || t.text.Contains("08 /")), "No brand or separate subtitle");
                Require(!texts.Any(t => t.text.Contains("按住左右滑动") || t.text.Contains("穿过后")), "No footer captions or tutorial text");
                var title = texts.Single(t => t.text == "欢乐球球");
                Require(title.alignment == TextAnchor.MiddleCenter && Math.Abs(title.rectTransform.anchoredPosition.y + 34) < .1, "Title uses existing navigation row");
                var icons = frame.GetComponentsInChildren<UiIconGraphic>(true);
                foreach (var kind in new[] { UiIconKind.Home, UiIconKind.Restart, UiIconKind.Book, UiIconKind.Settings })
                    Require(icons.Count(i => i.Kind == kind) == 1, "Existing navigation icon " + kind);
                var score = (Text)Get(page, "scoreText"); var best = (Text)Get(page, "bestText");
                Require(score.horizontalOverflow == HorizontalWrapMode.Overflow && best.horizontalOverflow == HorizontalWrapMode.Overflow, "Score labels do not wrap");
                Require(Math.Abs(score.rectTransform.anchoredPosition.y - best.rectTransform.anchoredPosition.y) < .1, "Score and best share baseline");
                var stage = (RectTransform)Get(page, "stageRect");
                Require(stage.offsetMin.y == 0 && stage.offsetMax.y == -180, "Stage fills all height below compact header, no footer reserve");
                Require(stage.rect.height >= height - 181, "Full scene height preserved " + height);
            }
            public void CheckLifecycle()
            {
                Require(!(bool)page.GetType().GetProperty("IsRunning").GetValue(page), "Entry awaits explicit start/resume");
                Invoke(page, "Resume");
                Require((bool)page.GetType().GetProperty("IsRunning").GetValue(page), "Explicit start runs game");
                Canvas.ForceUpdateCanvases(); camera.Render();
                var stage = (RectTransform)Get(page, "stageRect");
                var center = RectTransformUtility.WorldToScreenPoint(camera, stage.TransformPoint(stage.rect.center));
                var input = Get(page, "input");
                Require((bool)Invoke(input, "TopHitIsPlayArea", 1, center), "Rendered game region owns gameplay raycast");
                Require(Math.Abs((double)Invoke(input, "ViewportWidth") - 720) < .1, "Drag normalization uses rendered scene width");
                Invoke(page, "Suspend"); Canvas.ForceUpdateCanvases(); camera.Render();
                Require(!(bool)Invoke(input, "TopHitIsPlayArea", 1, center), "Pause overlay blocks acquisition through itself");
                Invoke(page, "Resume");
                Invoke(page, "Advance", .06f);
                var game = Get(page, "game"); var before = (HappyBallSnapshot)Invoke(game, "GetSnapshot");
                Invoke(page, "Suspend"); Invoke(page, "Advance", .1f);
                var after = (HappyBallSnapshot)Invoke(game, "GetSnapshot");
                Require(before.BallY == after.BallY && before.TowerAngle == after.TowerAngle, "Suspended game cannot advance");
                Invoke(page, "SetForeground", false); Invoke(page, "SetForeground", true);
                Require(!(bool)page.GetType().GetProperty("IsRunning").GetValue(page), "Focus return does not resume automatically");
                Invoke(page, "Resume"); Invoke(page, "Save");
                page.GetType().GetField("pendingRotation", Members).SetValue(page, .4d);
                Invoke(Get(page, "input"), "Cancel");
                Require((double)Get(page, "pendingRotation") == 0, "Input cancellation clears pending substep rotation");
                Require(File.Exists(Path.Combine(directory, "happy-ball-v1.json")), "Production page saves to isolated store");
                Invoke(page, "SetHostSaveWarning", "公共设置暂未保存");
                Require(((Text)Get(page, "errorText")).text.Contains("公共设置"), "Shared save warning remains visible without footer");
                Invoke(page, "SetHostSaveWarning", (object)null);
                Invoke(page, "ShowHelp");
                Require(!(bool)page.GetType().GetProperty("IsRunning").GetValue(page), "Help pauses simulation");
                Invoke(page, "Resume"); Invoke(page, "OpenSettings");
                Require(!(bool)page.GetType().GetProperty("IsRunning").GetValue(page), "Closing settings requires explicit resume");
                Invoke(page, "Resume");
            }
            public void CheckDeathFeedback()
            {
                var scene = Get(page, "scene");
                var original = (HappyBallSnapshot)Invoke(Get(page, "game"), "GetSnapshot");
                var terminal = original.Copy();
                terminal.BallY = HappyBallRules.BallRadius + .025; terminal.BallVelocity = -3;
                terminal.Layers[0].Sectors = new[] { new HappyBallSector { StartAngle = 0, SweepAngle = HappyBallGeometry.Tau, Dangerous = true } };
                terminal.Layers[0].Walls = new HappyBallWall[0];
                page.GetType().GetField("game", Members).SetValue(page, new HappyBallGame(terminal));
                Invoke(scene, "Reset", terminal); Invoke(page, "Resume");
                Invoke(page, "Advance", .02f);
                var game = (HappyBallGame)Get(page, "game");
                Require(game.GameOver, "Red impact freezes gameplay immediately");
                Require(!((GameObject)Get(page, "pauseOverlay")).activeSelf, "Death contact remains visible before result overlay");
                Require((bool)Get(Get(page, "data"), "ResultRecorded"), "Death result is recorded before visual delay");
                var contact = game.GetSnapshot();
                Capture("death-contact-720x" + height);
                Invoke(page, "Advance", .16f);
                Require(!((GameObject)Get(page, "pauseOverlay")).activeSelf, "Death splash has readable feedback time");
                Require(game.GetSnapshot().BallY == contact.BallY, "Death feedback advances visuals only");
                Capture("death-splash-720x" + height);
                Invoke(page, "Advance", .18f);
                Require(((GameObject)Get(page, "pauseOverlay")).activeSelf, "Result appears after short death feedback");
                Capture("death-result-720x" + height);
                page.GetType().GetField("game", Members).SetValue(page, new HappyBallGame(terminal));
                Get(page, "data").GetType().GetField("ResultRecorded", Members).SetValue(Get(page, "data"), false);
                Invoke(scene, "Reset", terminal); Invoke(page, "Resume"); Invoke(page, "Advance", .02f);
                Invoke(page, "SetForeground", false); Invoke(page, "SetForeground", true);
                Require(((GameObject)Get(page, "pauseOverlay")).activeSelf, "Background during death commits terminal overlay immediately");
                Invoke(page, "Advance", .2f);
                Require(!(bool)page.GetType().GetProperty("IsRunning").GetValue(page), "Foreground return cannot resume a terminal game");
                page.GetType().GetField("game", Members).SetValue(page, new HappyBallGame(original));
                Get(page, "data").GetType().GetField("ResultRecorded", Members).SetValue(Get(page, "data"), false);
                Invoke(scene, "Reset", original); Invoke(page, "Resume");
            }
            public void CheckReferenceSequence()
            {
                var scene = Get(page, "scene");
                var original = (HappyBallSnapshot)Invoke(Get(page, "game"), "GetSnapshot");
                var state = original.Copy();
                state.BallY = HappyBallRules.BallRadius + .025; state.BallVelocity = -3;
                state.TowerAngle = 0;
                state.Layers[0].Sectors = new[] { new HappyBallSector { StartAngle = 0, SweepAngle = HappyBallGeometry.Tau } };
                state.Layers[0].Walls = new HappyBallWall[0];
                var model = new HappyBallGame(state);
                Invoke(scene, "Reset", state);
                var view = (Camera)scene.GetType().GetProperty("SceneCamera").GetValue(scene);
                var stage = (Transform)scene.GetType().GetProperty("SceneRoot").GetValue(scene);
                Require(!view.orthographic, "Scene perspective provides near-to-far size change");
                float contactY = 0, apexY = 0, radiusPixels = 0;
                for (int frameIndex = 0; frameIndex <= 32; frameIndex++)
                {
                    var result = model.Step(1d / 48, 0);
                    var snapshot = model.GetSnapshot();
                    Invoke(scene, "Render", snapshot, result); Invoke(scene, "Tick", 1f / 48);
                    var ball = (Transform)Get(scene, "ball");
                    var point = view.WorldToViewportPoint(ball.position);
                    Require(point.y > .15f && point.y < .97f, "Full bounce remains inside scene frame " + frameIndex);
                    if (frameIndex == 0)
                    {
                        contactY = point.y;
                        Require((int)scene.GetType().GetProperty("ActiveDropletCount").GetValue(scene) > 0, "Landing emits visible liquid droplets");
                        var drops = stage.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("HappyBall liquid droplet ")).ToArray();
                        var alpha = new MaterialPropertyBlock();
                        Require(drops.All(r => { r.GetPropertyBlock(alpha); return alpha.GetFloat("_Opacity") > .8f; }), "Fresh liquid droplets start opaque enough to read");
                        var mark = stage.GetComponentsInChildren<MeshFilter>().First(m => m.name == "HappyBall stain 0");
                        Require(mark.sharedMesh.vertices.Any(v => v.x < -.05f) && mark.sharedMesh.vertices.Any(v => v.x > .05f), "Paint footprint spans both sides of the angular wrap boundary");
                        var right = view.WorldToViewportPoint(ball.position + view.transform.right * (float)HappyBallRules.BallRadius);
                        radiusPixels = Math.Abs(right.x - point.x) * 720;
                    }
                    if (frameIndex == 4) Require(ball.localScale.y > ball.localScale.x * 1.05f, "Rising ball stretches after leaving contact instead of staying flattened");
                    apexY = Math.Max(apexY, point.y);
                    if (frameIndex % 4 == 0) Capture("bounce-" + frameIndex.ToString("D2") + "-720x" + height);
                }
                float risePixels = (apexY - contactY) * (height - 180);
                Require(risePixels > radiusPixels * 7, "Bounce has at least 3.5 visible ball diameters of travel");
                Report.AppendLine("MEASURE bounce rise=" + risePixels.ToString("F1") + "px contact diameter=" + (radiusPixels * 2).ToString("F1") + "px viewport=" + height);
                var topLeft = view.WorldToViewportPoint(stage.TransformPoint(new Vector3(-1, 0, 0)));
                var topRight = view.WorldToViewportPoint(stage.TransformPoint(new Vector3(1, 0, 0)));
                var farLeft = view.WorldToViewportPoint(stage.TransformPoint(new Vector3(-1, -5, 0)));
                var farRight = view.WorldToViewportPoint(stage.TransformPoint(new Vector3(1, -5, 0)));
                Require(topRight.x - topLeft.x > (farRight.x - farLeft.x) * 1.08f, "Column visibly narrows at deeper floors");
                int lowerVisible = 0;
                for (int i = 1; i <= 5; i++)
                {
                    var point = view.WorldToViewportPoint(stage.TransformPoint(new Vector3(0, -(float)HappyBallRules.LayerSpacing * i, -(float)HappyBallRules.OrbitRadius)));
                    if (point.y > .02f && point.y < .95f) lowerVisible++;
                }
                Require(lowerVisible >= 3, "At least three lower floors are previewed");
                int visibleFloors = 0;
                for (int floor = 0; floor < HappyBallRules.WindowLayers; floor++)
                {
                    float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                    // Back edges can project into view while fully hidden behind the
                    // pillar and nearer floors. Count the readable front half only.
                    for (int pointIndex = 0; pointIndex <= 16; pointIndex++)
                    {
                        float angle = -Mathf.PI * .5f + pointIndex * Mathf.PI / 16;
                        var point = view.WorldToViewportPoint(stage.TransformPoint(new Vector3(
                            Mathf.Sin(angle) * (float)HappyBallRules.OuterRadius,
                            -floor * (float)HappyBallRules.LayerSpacing,
                            -Mathf.Cos(angle) * (float)HappyBallRules.OuterRadius)));
                        minY = Math.Min(minY, point.y); maxY = Math.Max(maxY, point.y);
                    }
                    if (maxY > 0 && minY < 1) visibleFloors++;
                }
                Report.AppendLine("MEASURE visible floor heights=" + visibleFloors + " spacing=" + HappyBallRules.LayerSpacing + " viewport=" + height);
                Require(visibleFloors >= 4 && visibleFloors <= 5, "Reference framing shows four to five front floor heights, including partial edge plates");
                Require((int)scene.GetType().GetProperty("ActiveStainCount").GetValue(scene) >= 1, "Landing leaves persistent paint marks");
                var rotated = model.GetSnapshot(); rotated.TowerAngle += .7;
                Invoke(scene, "Render", rotated, null);
                Capture("paint-rotated-720x" + height);
                rotated.Layers[0].State = HappyBallLayerState.Passed;
                rotated.BallY = rotated.Layers[0].Y - .7; rotated.PassedLayers = 1; rotated.NaturalStreak = 1; rotated.Score = 1;
                Invoke(scene, "Render", rotated, new HappyBallStepResult { Events = new[] { new HappyBallEvent {
                    Kind = HappyBallEventKind.LayerPassed, LayerIndex = 0, ScoreDelta = 1,
                    HasContact = true, ContactTowerAngle = rotated.TowerAngle, ContactOriginLayer = rotated.OriginLayer, ContactBallY = rotated.BallY
                } } });
                Invoke(scene, "Tick", .16f);
                var attached = stage.GetComponentsInChildren<MeshFilter>().Where(m => m.name.StartsWith("HappyBall stain ")).ToArray();
                Require(attached.Length > 0 && attached.All(m => m.transform.parent.name.Contains("split")), "Paint transfers onto its detached plate halves");
                Capture("paint-split-720x" + height);
                Invoke(scene, "Reset", original);
                Require((int)scene.GetType().GetProperty("ActiveStainCount").GetValue(scene) == 0, "Restoring does not replay paint history");
            }
            public void CheckHeavySequence()
            {
                var scene = Get(page, "scene");
                var original = (HappyBallSnapshot)Invoke(Get(page, "game"), "GetSnapshot");
                var state = new HappyBallGame(159).GetSnapshot();
                state.PassedLayers = 3; state.NaturalStreak = 3; state.Score = 6; state.Power = HappyBallPower.Armed;
                for (int i = 0; i < 3; i++) state.Layers[i].State = HappyBallLayerState.Passed;
                var impact = state.Layers[3];
                impact.Sectors = new[] { new HappyBallSector { StartAngle = 0, SweepAngle = HappyBallGeometry.Tau, Dangerous = true } };
                impact.Walls = new HappyBallWall[0];
                state.BallY = impact.Y + HappyBallRules.BallRadius + .025; state.BallVelocity = -6;
                var model = new HappyBallGame(state);
                Invoke(scene, "Reset", state);
                float apex = (float)state.BallY;
                for (int frameIndex = 0; frameIndex <= 40; frameIndex++)
                {
                    var result = model.Step(1d / 48, 0);
                    var snapshot = model.GetSnapshot();
                    Invoke(scene, "Render", snapshot, result); Invoke(scene, "Tick", 1f / 48);
                    ((Text)Get(page, "scoreText")).text = snapshot.Score.ToString();
                    ((Text)Get(page, "bestText")).text = snapshot.Score.ToString();
                    if (frameIndex == 0)
                    {
                        Require(!snapshot.GameOver && snapshot.BallVelocity > 0, "Armed red impact rebounds without death");
                        Require(snapshot.Score == 10, "Heavy impact immediately continues 1+2+3+4 scoring");
                        Require(((Text)Get(scene, "scorePopup")).text == "+4", "Heavy contact shows its reward immediately");
                        Require((int)scene.GetType().GetProperty("ActiveSplitCount").GetValue(scene) == 1, "Heavy impact detaches one pair of pieces");
                        var root = (Transform)scene.GetType().GetProperty("SceneRoot").GetValue(scene);
                        var pieces = root.GetComponentsInChildren<MeshFilter>().Where(m => m.name.Contains("split") && m.sharedMesh.vertexCount > 0).ToArray();
                        Require(pieces.Length == 2 && pieces.All(m => m.sharedMesh.colors.All(c => c.g > .8f && c.b < .25f)), "Heavy broken surfaces turn bright green including red regions");
                    }
                    apex = Math.Max(apex, (float)snapshot.BallY);
                    if (frameIndex % 4 == 0) Capture("heavy-" + frameIndex.ToString("D2") + "-720x" + height);
                }
                Require(apex > impact.Y + HappyBallRules.BallRadius * 8, "Heavy rebound is visually substantial");
                Require(model.GetSnapshot().PassedLayers >= 4, "Rebound returns through removed plate");
                Invoke(scene, "Reset", original); Invoke(page, "RefreshScores");
            }
            public void CheckHighWallSequence()
            {
                var scene = Get(page, "scene");
                var original = (HappyBallSnapshot)Invoke(Get(page, "game"), "GetSnapshot");
                var state = new HappyBallGame(159).GetSnapshot();
                state.TowerAngle = 0;
                state.Layers[0].Sectors = new[] { new HappyBallSector { StartAngle = 0, SweepAngle = HappyBallGeometry.Tau } };
                state.Layers[0].Walls = new[] { new HappyBallWall { Angle = 0, WidthAngle = .2, Height = HappyBallRules.MaximumWallHeight } };
                state.BallY = HappyBallRules.MaximumWallHeight + HappyBallRules.BallRadius + .025;
                state.BallVelocity = -3;
                var model = new HappyBallGame(state);
                Invoke(scene, "Reset", state);
                var view = (Camera)scene.GetType().GetProperty("SceneCamera").GetValue(scene);
                HappyBallSnapshot apex = state;
                float highestTop = 0;
                for (int frameIndex = 0; frameIndex <= 36; frameIndex++)
                {
                    var result = model.Step(1d / 48, 0);
                    var snapshot = model.GetSnapshot();
                    Invoke(scene, "Render", snapshot, result); Invoke(scene, "Tick", 1f / 48);
                    if (frameIndex == 0)
                        Require(result.Events.Any(e => e.Kind == HappyBallEventKind.Bounce) && snapshot.BallVelocity > 0, "Maximum-height black wall top rebounds without death");
                    var ball = (Transform)Get(scene, "ball");
                    float top = view.WorldToViewportPoint(ball.position + view.transform.up * ball.localScale.y * .5f).y;
                    float bottom = view.WorldToViewportPoint(ball.position - view.transform.up * ball.localScale.y * .5f).y;
                    highestTop = Math.Max(highestTop, top);
                    Require(!snapshot.GameOver && top < .99f && bottom > .05f, "Entire ball stays visible during high-wall bounce " + frameIndex);
                    if (snapshot.BallY > apex.BallY) apex = snapshot;
                    if (frameIndex % 6 == 0) Capture("high-wall-" + frameIndex.ToString("D2") + "-720x" + height);
                }
                Report.AppendLine("MEASURE maximum-wall bounce top=" + highestTop.ToString("F3") + " viewport=" + height);
                Invoke(scene, "Reset", apex);
                var restoredBall = (Transform)Get(scene, "ball");
                float restoredTop = view.WorldToViewportPoint(restoredBall.position + view.transform.up * restoredBall.localScale.y * .5f).y;
                Require(restoredTop < .99f && restoredTop > .1f, "Restoring a high-wall apex keeps ball inside the scene");
                Capture("high-wall-restored-720x" + height);
                var crossed = new HappyBallGame(apex);
                crossed.Step(HappyBallRules.FixedStep, .65);
                var crossedState = crossed.GetSnapshot();
                Require(!crossed.GameOver && crossedState.TowerAngle > .6, "Ball can rotate beyond the tall wall at its apex");
                Invoke(scene, "Reset", crossedState);
                restoredBall = (Transform)Get(scene, "ball");
                restoredTop = view.WorldToViewportPoint(restoredBall.position + view.transform.up * restoredBall.localScale.y * .5f).y;
                Require(restoredTop < .99f && restoredTop > .1f, "Restoring after clearing a high wall retains bounce headroom");
                Capture("high-wall-crossed-restored-720x" + height);
                Invoke(scene, "Reset", original);
            }
            public void CheckSplit()
            {
                var scene = Get(page, "scene");
                var game = Get(page, "game"); var snapshot = (HappyBallSnapshot)Invoke(game, "GetSnapshot");
                snapshot.Layers[0].State = HappyBallLayerState.Passed;
                snapshot.BallY = snapshot.Layers[0].Y - .7;
                snapshot.PassedLayers = 1; snapshot.NaturalStreak = 1; snapshot.Score = 1;
                var result = new HappyBallStepResult { Events = new[] { new HappyBallEvent { Kind = HappyBallEventKind.LayerPassed, LayerIndex = snapshot.Layers[0].Index, ScoreDelta = 1 } } };
                Invoke(scene, "Render", snapshot, result); Invoke(scene, "Tick", .16f);
                Require((int)scene.GetType().GetProperty("ActiveFragmentCount").GetValue(scene) == 2, "Pass produces two large fragments");
                // Scene has no gameplay callbacks: replaying a visual event cannot change the model.
                var before = (HappyBallSnapshot)Invoke(game, "GetSnapshot");
                float popupTime = (float)Get(scene, "scorePopupTime");
                Invoke(scene, "Render", snapshot, result);
                var after = (HappyBallSnapshot)Invoke(game, "GetSnapshot");
                Require(before.Score == after.Score && before.PassedLayers == after.PassedLayers, "Visual split cannot mutate score");
                Require((int)scene.GetType().GetProperty("ActiveSplitCount").GetValue(scene) == 1, "Repeated pass event cannot split twice");
                Require((float)Get(scene, "scorePopupTime") == popupTime, "Repeated event cannot restart score popup");
                Require(((Text)Get(scene, "scorePopup")).text == "+1" && !((Text)Get(scene, "scorePopup")).raycastTarget, "Passing score popup is transient and cannot intercept drag");
            }
            public void CheckSceneContracts()
            {
                var scene = Get(page, "scene");
                var root = (Transform)scene.GetType().GetProperty("SceneRoot").GetValue(scene);
                var fragments = root.GetComponentsInChildren<MeshFilter>().Where(m => m.name.Contains("split")).ToArray();
                Require(fragments.Length == 2 && fragments.All(m => m.sharedMesh.vertexCount > 30), "Both detached halves contain substantial plate geometry");
                Require(fragments[0].transform.localPosition.x * fragments[1].transform.localPosition.x < 0, "Halves separate to screen left and right");
                var rotations = fragments.Select(m => m.transform.localRotation).ToArray();
                var positions = fragments.Select(m => m.transform.localPosition).ToArray();
                var snapshot = ((HappyBallSnapshot)Get(scene, "rendered")).Copy();
                snapshot.TowerAngle += .7;
                Invoke(scene, "Render", snapshot, null);
                Require(fragments.Select((m, i) => m.transform.localRotation == rotations[i]).All(equal => equal), "Later rotation does not rotate detached halves");
                float shift = 64 * (float)HappyBallRules.LayerSpacing;
                snapshot = snapshot.Copy(); snapshot.OriginLayer += 64; snapshot.BallY += shift;
                foreach (var layer in snapshot.Layers) layer.Y += shift;
                Invoke(scene, "Render", snapshot, null);
                Require(fragments.Select((m, i) => Vector3.Distance(m.transform.localPosition, positions[i] + Vector3.up * shift) < .001f).All(equal => equal), "Origin shift preserves detached half positions relative to tower");
                Invoke(scene, "Tick", .25f); Invoke(scene, "Tick", .25f);
                Require((int)scene.GetType().GetProperty("ActiveSplitCount").GetValue(scene) == 0, "Fragments expire and return to pool");
                var normal = (HappyBallSnapshot)Invoke(Get(page, "game"), "GetSnapshot");
                Invoke(scene, "Reset", normal);
                var broken = normal.Copy(); broken.Layers[0].State = HappyBallLayerState.Broken;
                Invoke(scene, "Render", broken, new HappyBallStepResult { Events = new[] { new HappyBallEvent { Kind = HappyBallEventKind.LayerBroken, LayerIndex = broken.Layers[0].Index } } });
                Invoke(scene, "Render", broken, new HappyBallStepResult { Events = new[] { new HappyBallEvent { Kind = HappyBallEventKind.LayerPassed, LayerIndex = broken.Layers[0].Index } } });
                Require((int)scene.GetType().GetProperty("ActiveSplitCount").GetValue(scene) == 1, "Smash splits at contact; later pass does not replay");
                Require(root.GetComponentsInChildren<Transform>().Any(t => t.name == "HappyBall intact layer 1"), "Next intact layer survives previous layer split");
                Invoke(scene, "SetVisible", false);
                Require((int)scene.GetType().GetProperty("ActiveSplitCount").GetValue(scene) == 0, "Leaving clears visual effects");
                Invoke(scene, "SetVisible", true); Invoke(scene, "Reset", broken);
                Require((int)scene.GetType().GetProperty("ActiveSplitCount").GetValue(scene) == 0, "Restoring snapshot does not replay historical split");
                Invoke(scene, "Render", broken, new HappyBallStepResult { Events = new[] { new HappyBallEvent { Kind = HappyBallEventKind.LayerPassed, LayerIndex = broken.Layers[0].Index } } });
                Require((int)scene.GetType().GetProperty("ActiveSplitCount").GetValue(scene) == 0, "Restored Spent layer passing never replays its earlier smash");
                Require(root.GetComponentsInChildren<MeshFilter>(true).Count(m => m.name.Contains("split")) == 16, "Fragment pool is bounded to sixteen meshes");
            }
            public void ShowProgressedScene()
            {
                var game = new HappyBallGame(159); double sinceBounce = -1;
                for (int step = 0; step < 20000 && game.GetSnapshot().PassedLayers < 20 && !game.GameOver; step++)
                {
                    var state = game.GetSnapshot(); var layer = state.Layers.First(l => l.Index == state.PassedLayers);
                    double difference = HappyBallGeometry.NormalizeAngle(HappyBallGenerator.GapCenter(layer) + state.TowerAngle + Math.PI) - Math.PI;
                    double rotation = sinceBounce >= HappyBallGenerator.RouteReactionTime || Math.Abs(difference) < .1
                        ? -Math.Sign(difference) * Math.Min(Math.Abs(difference), HappyBallGenerator.RouteAngularSpeed * HappyBallRules.FixedStep) : 0;
                    foreach (var item in game.Step(HappyBallRules.FixedStep, rotation).Events)
                    {
                        if (item.Kind == HappyBallEventKind.Bounce) sinceBounce = 0;
                        if (item.Kind == HappyBallEventKind.LayerPassed) sinceBounce = -1;
                    }
                    if (sinceBounce >= 0) sinceBounce += HappyBallRules.FixedStep;
                }
                var snapshot = game.GetSnapshot();
                Require(!game.GameOver && snapshot.PassedLayers == 20, "Real generated gameplay reaches red plates and vertical walls");
                Require(snapshot.Layers.Any(l => l.Walls.Length > 0) && snapshot.Layers.Any(l => l.Sectors.Any(s => s.Dangerous)), "Progressed fixture includes both hazard kinds");
                Require(snapshot.Layers.SelectMany(l => l.Walls).Any(w => w.Height >= .85), "Generated later floors contain visibly tall fins with authoritative collision height");
                ((Text)Get(page, "scoreText")).text = snapshot.Score.ToString();
                ((Text)Get(page, "bestText")).text = snapshot.Score.ToString();
                Invoke(Get(page, "scene"), "Reset", snapshot);
            }
            public void CheckNoColliders()
            {
                var scene = (Component)Get(page, "scene");
                Require(scene != null, "Production 3D scene exists");
                // This project intentionally does not enable Unity 3D Physics; model owns collisions.
                Require(!frame.GetComponentsInChildren<Component>(true).Any(c => c != null && c.GetType().Name.Contains("Collider")), "UI has no accidental collision components");
            }
            public void Capture(string name)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var view in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (view != camera && view.enabled && view.targetTexture != null) view.Render();
                camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = texture;
                var image = new Texture2D(720, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 720, height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image); RenderTexture.active = previous;
            }
            public void Dispose()
            {
                Invoke(page, "Leave");
                Object.DestroyImmediate(Root); texture.Release(); Object.DestroyImmediate(texture);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
