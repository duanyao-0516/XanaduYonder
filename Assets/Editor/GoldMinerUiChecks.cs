using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Core.GoldMiner;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.GoldMiner;
using XanaduYonder.Runtime.Jump;
using XanaduYonder.Runtime.Sheep;
using XanaduYonder.Runtime.Sudoku;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Real page callbacks, production Core, UGUI and persistence in Play Mode.
    // Never calls AppController.Awake or page.Initialize: fixture-owned stores are
    // installed before the first callback. No player save directory is accessed.
    [InitializeOnLoad]
    public static class GoldMinerUiChecks
    {
        private const string Pending = "Xanadu.GoldMinerUiChecks.Pending";
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly StringBuilder Report = new StringBuilder();
        private static int assertions;
        private static string output;
        private static Fixture fixture;
        private static Font previousFont;

        static GoldMinerUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }

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
            new GameObject("Gold Miner checks runner").AddComponent<Runner>().StartCoroutine(Guarded());
        }

        private static IEnumerator Guarded()
        {
            output = Path.GetFullPath("artifacts/gold-miner-development/ui");
            Directory.CreateDirectory(output);
            previousFont = UiFactory.Font;
            var routines = new Stack<IEnumerator>();
            routines.Push(Execute());
            while (routines.Count > 0)
            {
                object current = null;
                bool next = false;
                Exception failure = null;
                try { next = routines.Peek().MoveNext(); if (next) current = routines.Peek().Current; }
                catch (Exception error) { failure = error; }
                if (failure != null) { Finish(failure); yield break; }
                if (!next) { (routines.Pop() as IDisposable)?.Dispose(); continue; }
                var nested = current as IEnumerator;
                if (nested != null) { routines.Push(nested); continue; }
                yield return current;
            }
            Finish(null);
        }

        private static void Finish(Exception failure)
        {
            try { fixture?.Dispose(); }
            catch (Exception error) { failure = failure ?? error; }
            UiFactory.Font = previousFont;
            Report.AppendLine(failure == null ? "GOLD_MINER_UI_CHECKS_PASS: " + assertions + " assertions" : "GOLD_MINER_UI_CHECKS_FAIL after " + assertions + " assertions\n" + failure);
            Report.AppendLine("Scope: Unity Play Mode, real UGUI raycasts and production rules/store; Android physical multi-touch and OS rotation require separate device validation.");
            File.WriteAllText(Path.Combine(output, "report.txt"), Report.ToString());
            if (failure != null) Debug.LogException(failure);
            Debug.Log(Report.ToString());
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static IEnumerator Execute()
        {
            assertions = 0; Report.Clear();
            UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            Require(UiFactory.Font != null, "Bundled font loads");
            CheckLayoutFixtures();
            fixture = new Fixture();
            fixture.Size(720, 1320);
            for (int i = 0; i < 8; i++) yield return null;
            Require(Screen.width == 720 && Screen.height == 1320, "GameView uses the requested portrait size");
            fixture.BuildShell();
            fixture.CheckLobby();
            yield return fixture.Click(fixture.Button("选择单人 / 双人  →", fixture.Lobby.transform));
            for (int i = 0; i < 8; i++) yield return null;
            var page = fixture.Page;
            Require(Get<bool>(page, "visible") && Get<bool>(page, "selecting"), "Real lobby button enters Gold Miner selection");
            Require(!fixture.Frame.gameObject.activeSelf, "Gold Miner owns a separate full-window canvas");
            fixture.Capture("01-selection");
            fixture.CheckButtonsInsideSafeArea();
            Require(Get<RectTransform>(page, "view").Find("黄金矿工").GetComponent<Image>().sprite.name == "gold-miner-wordmark-v2", "Mode selection uses the approved gold wordmark artwork");

            foreach (GoldMinerMode mode in new[] { GoldMinerMode.Solo, GoldMinerMode.Coop })
            {
                bool coop = mode == GoldMinerMode.Coop;
                string prefix = coop ? "coop" : "solo";
                fixture.Size(coop ? 1320 : 720, coop ? 720 : 1320);
                Invoke(page, "ChooseMode", mode, true);
                for (int i = 0; i < 10; i++) yield return null;
                Require(!Get<GoldMinerOrientationScope>(page, "orientation").IsPending, prefix + " orientation stable gate completes");
                var session = Get<GoldMinerSession>(page, "session");
                Require(session.ActiveMode == mode && session.GetStateSnapshot().Phase == GoldMinerPhase.Ready, prefix + " starts Ready");
                fixture.CheckButtonsInsideSafeArea();
                fixture.Capture(prefix + "-ready");
                fixture.CheckReadyArtwork(mode);
                fixture.CheckHeaderArtworkGeometry();
                yield return fixture.CheckResponsiveHeaderArtwork(mode, "ready");
                int readyTick = session.GetStateSnapshot().SimulationTick;
                for (int i = 0; i < 3; i++) yield return null;
                Require(session.GetStateSnapshot().SimulationTick == readyTick, prefix + " Ready does not advance time");
                yield return fixture.Click(fixture.Button("开始采矿"));
                Require(session.GetStateSnapshot().Phase == GoldMinerPhase.Running && !session.IsSuspended, prefix + " Start opens explicit gate");
                Require(Get<RectTransform>(page, "view").Find("Mine world").gameObject.activeSelf && Get<RectTransform>(page, "view").Find("Mine HUD").gameObject.activeSelf,
                    "Starting a level restores the live mine and HUD after the illustrated preview");
                // A batch GameView can render four frames in < 1/60 second.
                // Wait for real elapsed time, without changing the production clock.
                double tickDeadline = Time.realtimeSinceStartupAsDouble + 2;
                while (session.GetStateSnapshot().SimulationTick == 0 && Time.realtimeSinceStartupAsDouble < tickDeadline)
                    yield return null;
                Require(session.GetStateSnapshot().SimulationTick > 0, prefix + " real frame loop advances fixed ticks");
                fixture.Capture(prefix + "-game");
                fixture.CheckReleasedView("GoldMiner/backgrounds/bg-mode-select");
                fixture.CheckMineGeometry(mode);
                fixture.CheckDesignFidelityGeometry(mode);
                Invoke(page, "ShowPause");
                int pausedTick = session.GetStateSnapshot().SimulationTick;
                for (int i = 0; i < 3; i++) yield return null;
                Require(session.IsSuspended && session.GetStateSnapshot().SimulationTick == pausedTick, prefix + " pause freezes simulation");
                fixture.Capture(prefix + "-pause");
                fixture.CheckModalControlGeometry("pause");
                if (coop) yield return fixture.CheckLongScreenPause();
                yield return fixture.CheckExitConfirmation(prefix);
                yield return fixture.Click(fixture.Button("玩法说明"));
                Require(Get<RectTransform>(page, "modal") != null && session.IsSuspended, prefix + " help stays paused");
                fixture.Capture(prefix + "-help");
                fixture.CheckHelpBackdrop(mode);
                yield return fixture.Click(fixture.Button("知道了"));
                yield return fixture.Click(fixture.Button("继续游戏"));
                Require(!session.IsSuspended, prefix + " continue resumes");
                page.SetForeground(false, false);
                Require(session.IsSuspended, prefix + " focus loss suspends");
                page.SetForeground(true, false);
                Require(session.IsSuspended && Get<RectTransform>(page, "modal") != null, prefix + " foreground requires explicit continue");
                yield return fixture.Click(fixture.Button("继续游戏"));

                // Stop the MonoBehaviour clock only while advancing exact deterministic
                // ticks below; all commands, mutations and UI transitions remain real.
                page.enabled = false;
                Invoke(page, "ShowPause");
                yield return fixture.Click(fixture.Button("重新开始"));
                yield return fixture.Click(fixture.Button("重试本关", Get<RectTransform>(page, "modal")));
                Require(session.GetStateSnapshot().Phase == GoldMinerPhase.Ready && session.GetRunSnapshot().AttemptIndex == 1, prefix + " confirmed retry restores checkpoint");
                yield return fixture.Click(fixture.Button("开始采矿"));
                yield return fixture.CheckDeadlineGuards(mode);
                yield return fixture.CheckPointerRouting(mode);
                fixture.CheckAutosaveFailure();
                fixture.CheckFrameClockIndependence();
                page.Save();
                string warning;
                var reloaded = fixture.Store.Load(out warning);
                Require(reloaded != null && warning == null, prefix + " real store reload succeeds");
                Require(GoldMinerSaveCodec.Encode(reloaded) == GoldMinerSaveCodec.Encode(session.GetSnapshot()), prefix + " in-flight root survives codec and disk roundtrip exactly");
                Require(reloaded.GetRun(mode).Current.Hooks[0].State == GoldMinerHookState.Extending, prefix + " saved in-flight hook is preserved");
                var restored = new GoldMinerSession(reloaded);
                Set(page, "session", restored); session = restored;
                Invoke(page, "RebuildRun");
                Require(session.IsSuspended && Get<RectTransform>(page, "modal") != null, prefix + " restored active state shows pause gate");
                yield return fixture.Click(fixture.Button("继续游戏"));
                Invoke(page, "ShowPause");
                yield return fixture.Click(fixture.Button("重新开始"));
                yield return fixture.Click(fixture.Button("重试本关", Get<RectTransform>(page, "modal")));
                yield return fixture.Click(fixture.Button("开始采矿"));
                fixture.ReplaySelectedWitness();
                Require(session.GetStateSnapshot().Phase == GoldMinerPhase.Won, prefix + " actual witness reaches saved Won state");
                fixture.Capture(prefix + "-result");
                fixture.CheckHeaderArtworkGeometry();
                yield return fixture.CheckResponsiveHeaderArtwork(mode, "result");
                yield return fixture.Click(fixture.Button("进入商店"));
                Require(session.GetStateSnapshot().Phase == GoldMinerPhase.Shop, prefix + " result enters shop");
                fixture.Capture(prefix + "-shop");
                fixture.CheckReleasedView("GoldMiner/backgrounds/" + (coop ? "bg-coop" : "bg-solo"));
                fixture.CheckButtonsInsideSafeArea();
                fixture.CheckShopTextGeometry();
                yield return fixture.CheckResponsiveShop(mode);
                if (!coop) yield return fixture.CheckResponsiveShop(mode, true);
                long cash = session.GetStateSnapshot().Cash;
                int player = coop ? 1 : 0;
                int bombs = session.GetStateSnapshot().Players[player].Bombs;
                long transaction = session.GetSnapshot().TxnSeq;
                fixture.CheckPurchaseFailure(GoldMinerItem.Dynamite, player);
                Invoke(page, "Purchase", GoldMinerItem.Dynamite, player);
                Require(session.GetStateSnapshot().Cash == cash - GoldMinerRules.Price(GoldMinerItem.Dynamite, 1), prefix + " shop deducts exact price");
                Require(session.GetStateSnapshot().Players[player].Bombs == bombs + 1, prefix + " shop credits chosen inventory");
                Require(session.GetSnapshot().TxnSeq == transaction + 1 && page.SaveError == null,
                    prefix + " failed purchase retry commits exactly once and clears save error");
                Require(File.ReadAllText(fixture.Store.SavePath) == GoldMinerSaveCodec.Encode(session.GetSnapshot()),
                    prefix + " successful purchase retry persists the committed root");
                string firstPattern = session.GetStateSnapshot().PatternId;
                yield return fixture.Click(fixture.Button(coop ? "一起进入下一关  ›" : "进入下一关"));
                Require(session.GetStateSnapshot().Phase == GoldMinerPhase.Ready && session.GetStateSnapshot().LevelIndex == 2, prefix + " shop advances to next Ready");
                Require(session.GetStateSnapshot().PatternId != firstPattern, prefix + " next mine changes pattern");
                yield return fixture.Click(fixture.Button("开始采矿"));
                for (int i = 0; i < GoldMinerRules.LevelTicks; i++) Invoke(page, "AdvanceOne");
                Require(session.GetStateSnapshot().Phase == GoldMinerPhase.Failed, prefix + " no-click round reaches real Failed result");
                fixture.Capture(prefix + "-failed");
                fixture.CheckFailureArtGeometry();
                yield return fixture.CheckResponsiveHeaderArtwork(mode, "failed");
                yield return fixture.Click(fixture.Button("重新挑战"));
                Require(session.GetStateSnapshot().Phase == GoldMinerPhase.Ready, prefix + " failed round retries");
                yield return fixture.Click(fixture.Button("返回模式"));
                fixture.Size(720, 1320);
                page.enabled = true;
                for (int i = 0; i < 10; i++) yield return null;
                Require(Get<bool>(page, "selecting") && !Get<GoldMinerOrientationScope>(page, "orientation").IsPending, prefix + " mode selection returns to portrait");
                Require(Get<GoldMinerSession>(page, "session").GetSnapshot().GetRun(mode).Current.LevelIndex == 2, prefix + " selection retains mode progress");
                Report.AppendLine("PASS " + prefix + ": real fixed ticks, ready/pause/help/lifecycle, down routing, disk restore, retry, witness win, shop, next-level, failure and portrait return.");
            }
            var root = Get<GoldMinerSession>(page, "session").GetSnapshot();
            Require(root.Solo != null && root.Coop != null && root.Solo.RunId != root.Coop.RunId, "Both modes preserve independent challenges");
            yield return fixture.Click(fixture.Button("‹ 大厅"));
            Require(!Get<bool>(page, "visible") && fixture.Frame.gameObject.activeSelf && fixture.Lobby.activeSelf, "Real return callback restores the lobby");
            Require(Screen.orientation == fixture.PriorOrientation, "Leave restores prior screen orientation");
            DateTime savedAt = File.GetLastWriteTimeUtc(fixture.Store.SavePath);
            yield return new WaitForSecondsRealtime(.05f);
            page.Save();
            Require(File.GetLastWriteTimeUtc(fixture.Store.SavePath) == savedAt, "Unchanged inactive Gold Miner is not rewritten by shared shell saves");
            fixture.CheckStoreIsolation();
            yield return Resources.UnloadUnusedAssets();
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long beforeBytes = Profiler.GetTotalAllocatedMemoryLong();
            int beforeTextures = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            for (int cycle = 0; cycle < 20; cycle++)
            {
                fixture.Size(720, 1320);
                page.Enter();
                for (int frame = 0; frame < 5; frame++) yield return null;
                bool coop = cycle % 2 == 1;
                fixture.Size(coop ? 1320 : 720, coop ? 720 : 1320);
                Invoke(page, "ChooseMode", coop ? GoldMinerMode.Coop : GoldMinerMode.Solo, false);
                for (int frame = 0; frame < 5; frame++) yield return null;
                Require(!Get<GoldMinerOrientationScope>(page, "orientation").IsPending, "Re-entry cycle reaches stable orientation " + cycle);
                page.Leave();
                yield return null;
                Require(Get<GoldMinerArtwork>(page, "art") == null && Get<GoldMinerSceneView>(page, "scene") == null,
                    "Leave releases page-owned artwork and scene " + cycle);
            }
            yield return Resources.UnloadUnusedAssets();
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long afterBytes = Profiler.GetTotalAllocatedMemoryLong();
            int afterTextures = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            Report.AppendLine("RESOURCE_SAMPLE after 20 real enter/leave cycles + Resources.UnloadUnusedAssets + full GC: allocated " + beforeBytes + " -> " + afterBytes +
                " bytes (delta " + (afterBytes - beforeBytes) + "); Texture2D objects " + beforeTextures + " -> " + afterTextures + ". Recorded evidence, no arbitrary memory threshold.");
        }

        private static void CheckLayoutFixtures()
        {
            foreach (var size in new[] { new Vector2(720, 1320), new Vector2(720, 1582), new Vector2(1320, 720), new Vector2(1582, 720) })
            {
                bool coop = size.x > size.y;
                var mode = coop ? GoldMinerMode.Coop : GoldMinerMode.Solo;
                var safe = coop ? new Rect(60, 0, size.x - 84, size.y - 24) : new Rect(0, 36, size.x, size.y - 60);
                var metrics = new XanaduYonder.Runtime.GoldMiner.GoldMinerLayout(size.x, size.y, safe, mode);
                Require(metrics.Mine.xMin >= safe.xMin && metrics.Mine.xMax <= safe.xMax, "Layout mine respects horizontal inset at " + size);
                Require(metrics.Mine.yMin >= metrics.Hud.yMax && metrics.Mine.yMax <= safe.yMax - 90, "Layout mine respects controls at " + size);
                var left = metrics.World(0, 0); var right = metrics.World(GoldMinerRules.Width(mode), GoldMinerRules.Height(mode));
                Require(Vector2.Distance(left, metrics.Mine.position) < .01f && Vector2.Distance(right, metrics.Mine.max) < .01f,
                    "World endpoints preserve world geometry at " + size);
                Require(metrics.Hud.xMin >= safe.xMin && metrics.Hud.xMax <= safe.xMax && metrics.Hud.yMin >= safe.yMin,
                    "HUD remains inside inset safe area at " + size);
            }
            Report.AppendLine("PASS four pure layout fixtures: 720x1320, 720x1582, 1320x720, 1582x720; portrait top/bottom and landscape asymmetric cutout insets. Full UI flow renders standard portrait/landscape; shop fixtures additionally render both long-screen sizes with real text bounds checks.");
        }

        private sealed class Runner : MonoBehaviour { }
        [Serializable] private sealed class WitnessShard { public WitnessLayout[] layouts; }
        [Serializable] private sealed class WitnessLayout { public string id; public Witness witness; }
        [Serializable] private sealed class Witness { public WitnessInput[] inputs; public int endTick; public long expectedIncome; }
        [Serializable] private sealed class WitnessInput { public int tick; public int playerId; public string action; }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Owner;
            public readonly GoldMinerPage Page;
            public readonly GoldMinerSaveStore Store;
            public readonly ScreenOrientation PriorOrientation;
            public RectTransform Frame;
            public GameObject Lobby;
            private readonly string directory;
            private readonly FaultFiles files = new FaultFiles();
            private readonly Canvas canvas;
            private readonly EventSystem events;
            private readonly Camera camera;
            private readonly GameViewSizeScope sizes = new GameViewSizeScope();
            private readonly Dictionary<string, Rect> sourceAlphaBounds = new Dictionary<string, Rect>();
            private AppController shell;
            private Canvas lobbyCanvas;

            public Fixture()
            {
                PriorOrientation = Screen.orientation;
                directory = Path.Combine(Path.GetTempPath(), "xanadu-gold-ui-" + Guid.NewGuid().ToString("N"));
                Store = new GoldMinerSaveStore(directory, files);
                Owner = new GameObject("Gold Miner isolated UI fixture");
                events = Child("Fixture EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>();
                camera = Child("Fixture camera", typeof(Camera)).GetComponent<Camera>();
                camera.enabled = false; camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiFactory.Paper;
                var pageObject = Child("Gold Miner fixture page", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = pageObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
                var scaler = pageObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1320); scaler.matchWidthOrHeight = 0;
                Page = pageObject.AddComponent<GoldMinerPage>();
                Set(Page, "canvasRoot", pageObject.GetComponent<RectTransform>()); Set(Page, "scaler", scaler);
                Set(Page, "store", Store); Set(Page, "session", new GoldMinerSession());
                Set(Page, "soundEnabled", (Func<bool>)(() => false)); Set(Page, "hapticsEnabled", (Func<bool>)(() => false));
                Set(Page, "setSound", (Action<bool>)(_ => { })); Set(Page, "setHaptics", (Action<bool>)(_ => { }));
                Set(Page, "vibrate", (Action)(() => { }));
                pageObject.SetActive(false);
            }

            public void BuildShell()
            {
                var inert = Child("Inert shell"); inert.SetActive(false);
                shell = inert.AddComponent<AppController>();
                var backdrop = Child("Lobby fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                lobbyCanvas = backdrop.GetComponent<Canvas>(); lobbyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scale = backdrop.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scale.referenceResolution = new Vector2(720, 1320); scale.matchWidthOrHeight = 0;
                Frame = UiFactory.Rect(backdrop.transform, "Safe area design frame", 0, 0, 720, 1320);
                Frame.gameObject.AddComponent<SafeDesignFrame>().Initialize(Frame, (RectTransform)backdrop.transform);
                Set(shell, "frame", Frame);
                var feedback = inert.AddComponent<GameFeedback>(); Set(shell, "feedback", feedback);
                var jumpRoot = Child("Inert jump"); jumpRoot.SetActive(false);
                var jump = jumpRoot.AddComponent<JumpPage>();
                Set(jump, "scene", jumpRoot.AddComponent<JumpSceneView>()); Set(jump, "feedback", feedback); Set(shell, "jumpPage", jump);
                var sheepRoot = Child("Inert sheep"); sheepRoot.SetActive(false); Set(shell, "sheepPage", sheepRoot.AddComponent<SheepPage>());
                var sudokuRoot = Child("Inert sudoku"); sudokuRoot.SetActive(false); Set(shell, "sudokuPage", sudokuRoot.AddComponent<SudokuPage>());
                var game = new Game2048(Get<IRandomSource>(shell, "random"));
                Set(shell, "game", game); Set(shell, "data", new SaveData { Game = game.GetSnapshot() });
                Set(shell, "gamePage", Child("Inert 2048 page"));
                Set(shell, "scoreText", UiFactory.Label(inert.transform, "", 0, 0, 1, 1, 10, Color.white));
                Set(shell, "bestText", UiFactory.Label(inert.transform, "", 0, 0, 1, 1, 10, Color.white));
                Set(shell, "goldMinerPage", Page); Set(Page, "lobby", (Action)(() => Invoke(shell, "ShowLobby")));
                Invoke(shell, "BuildLobby"); Lobby = Get<GameObject>(shell, "lobbyPage");
                Canvas.ForceUpdateCanvases();
            }

            public void CheckLobby()
            {
                var content = (RectTransform)Lobby.transform.Find("Lobby list viewport/Lobby list content");
                string[] names = { "2048", "Jump", "Sheep", "Watermelon", "Idiom", "Sudoku", "Gold Miner" };
                for (int i = 0; i < names.Length; i++)
                {
                    var card = (RectTransform)content.Find(names[i] + " game card");
                    Require(card != null && Mathf.Abs(card.rect.height - 350) < .1f, "Lobby preserves fixed card height: " + names[i]);
                    Require(card.GetComponentInChildren<Text>().text.StartsWith((i + 1).ToString("00")), "Lobby ordinal " + names[i]);
                    Require(Mathf.Abs(card.anchoredPosition.y + 216 + i * 374) < .1f, "Lobby fixed card spacing: " + names[i]);
                }
                var goldCard = content.Find("Gold Miner game card");
                var celebrating = goldCard.Find("Gold Miner celebrating miner").GetComponent<Image>();
                Require(celebrating.sprite != null && celebrating.sprite.name == "success", "Gold Miner lobby card uses the celebrating miner state");
                Require(goldCard.Find("Gold Miner prize").GetComponent<Image>().sprite != null, "Celebrating miner displays his gold prize");
                var viewport = (RectTransform)content.parent;
                content.anchoredPosition = new Vector2(0, Mathf.Max(0, content.rect.height - viewport.rect.height));
                Require(content.rect.height > viewport.rect.height, "Seven cards scroll without shrinking");
                Capture("00-lobby-gold-card");
            }

            public void Size(int width, int height) { sizes.Set(width, height); }

            public Button Button(string title, Transform scope = null)
            {
                if (scope == null) scope = Page.transform;
                foreach (var button in scope.GetComponentsInChildren<Button>())
                {
                    if (!button.gameObject.activeInHierarchy) continue;
                    var label = button.GetComponentInChildren<Text>();
                    if (label != null && label.text == title) return button;
                }
                throw new InvalidOperationException("Missing button: " + title);
            }

            public IEnumerator Click(Button button)
            {
                // Let Unity destroy the previous modal and render the replacement.
                // ForceUpdateCanvases alone does not establish Graphic.depth for a
                // newly-created graphic, so same-frame synthetic clicks can hit the
                // underlying mine even though the replacement will be drawn next frame.
                yield return null;
                yield return null;
                Require(button.interactable, "Button is enabled: " + button.name);
                Canvas.ForceUpdateCanvases();
                var point = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
                var data = Down(point, -1);
                var target = data.pointerPress;
                if (target != null) ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(data.pointerCurrentRaycast.gameObject);
                Require(clicked == button.gameObject, "Top production raycast resolves intended button: " + button.name +
                    "; actualHit=" + data.pointerCurrentRaycast.gameObject.name + "; clickTarget=" + (clicked == null ? "null" : clicked.name) + "; point=" + point);
                ExecuteEvents.Execute(clicked, data, ExecuteEvents.pointerClickHandler);
            }

            private PointerEventData Down(Vector2 point, int id)
            {
                var pointer = new PointerEventData(events) { position = point, pointerId = id, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
                Require(hits.Count > 0, "Real raycaster finds point " + point);
                pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
                pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
                return pointer;
            }

            public IEnumerator CheckPointerRouting(GoldMinerMode mode)
            {
                var session = Get<GoldMinerSession>(Page, "session");
                Set(Page, "accumulator", 0d);
                Set(Page, "lastRealTime", Time.realtimeSinceStartupAsDouble);
                var first = Down(new Vector2(Screen.width * .25f, Screen.height * .35f), 101);
                Require(session.GetStateSnapshot().Hooks[0].State == GoldMinerHookState.Extending, "P1 Down immediately launches");
                long sequence = session.GetRunSnapshot().LastCommandSeq;
                if (mode == GoldMinerMode.Coop)
                {
                    var second = Down(new Vector2(Screen.width * .75f, Screen.height * .35f), 102);
                    Require(session.GetStateSnapshot().Hooks[1].State == GoldMinerHookState.Extending, "Distinct simultaneous P2 Down launches second hook");
                    sequence = session.GetRunSnapshot().LastCommandSeq;
                    ExecuteEvents.Execute(second.pointerPress, second, ExecuteEvents.pointerUpHandler);
                }
                first.position = new Vector2(Screen.width * .8f, first.position.y);
                ExecuteEvents.Execute(first.pointerPress, first, ExecuteEvents.pointerUpHandler);
                Require(session.GetRunSnapshot().LastCommandSeq == sequence, "Cross-half Up never launches again");
                Set(Page, "lastRealTime", Time.realtimeSinceStartupAsDouble);
                Invoke(Page, "ShowPause");
                var blocked = Down(new Vector2(Screen.width * .08f, Screen.height * .25f), 103);
                Require(session.GetRunSnapshot().LastCommandSeq == sequence, "Modal consumes mine pointer without a command");
                if (blocked.pointerPress != null) ExecuteEvents.Execute(blocked.pointerPress, blocked, ExecuteEvents.pointerUpHandler);
                yield return Click(Button("继续游戏"));
            }

            public IEnumerator CheckDeadlineGuards(GoldMinerMode mode)
            {
                var session = Get<GoldMinerSession>(Page, "session");
                AdvanceToLastTick(session);
                long sequence = session.GetRunSnapshot().LastCommandSeq;
                Set(Page, "lastRealTime", Time.realtimeSinceStartupAsDouble - 1d);
                Set(Page, "accumulator", 0d);
                var pointer = Down(new Vector2(Screen.width * .25f, Screen.height * .35f), 201);
                var terminal = session.GetStateSnapshot();
                Require(terminal.Phase == GoldMinerPhase.Failed && terminal.RemainingTicks == 0,
                    mode + " I13 late UGUI PointerDown settles overdue final tick");
                Require(session.GetRunSnapshot().LastCommandSeq == sequence,
                    mode + " I13 stale frame cannot create a post-deadline command");
                foreach (var hook in terminal.Hooks) Require(hook.State == GoldMinerHookState.Swinging,
                    mode + " I13 late Down creates no Extending hook");
                if (pointer.pointerPress != null) ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
                Require(Button("重新挑战", Get<RectTransform>(Page, "modal")) != null,
                    mode + " I13 overdue input preserves actual result UI");
                yield return Click(Button("重新挑战"));
                yield return Click(Button("开始采矿"));

                AdvanceToLastTick(session);
                Set(Page, "lastRealTime", Time.realtimeSinceStartupAsDouble - 1d);
                Set(Page, "accumulator", 0d);
                Invoke(Page, "ShowPause");
                Require(session.GetStateSnapshot().Phase == GoldMinerPhase.Failed && session.GetStateSnapshot().RemainingTicks == 0,
                    mode + " pause request consumes overdue final tick");
                Require(Button("重新挑战", Get<RectTransform>(Page, "modal")) != null,
                    mode + " deadline result is not replaced by stale pause controls");
                yield return Click(Button("重新挑战"));
                yield return Click(Button("开始采矿"));
                Require(session.GetStateSnapshot().Phase == GoldMinerPhase.Running && session.GetStateSnapshot().SimulationTick == 0,
                    mode + " deadline checks restore checkpoint for later scenarios");
                Report.AppendLine("PASS " + mode + " I13: overdue PointerDown and pause flush the real final tick, reject late launch, preserve result UI, then retry checkpoint.");
            }

            public void CheckAutosaveFailure()
            {
                var session = Get<GoldMinerSession>(Page, "session");
                Require(session.GetStateSnapshot().Phase == GoldMinerPhase.Running && !session.IsSuspended,
                    "Autosave failure starts in the actual Running page");
                Page.Save();
                Require(!session.Dirty && Page.SaveError == null, "Autosave baseline is committed");
                string beforeDisk = File.ReadAllText(Store.SavePath);
                Require(session.Step().Advanced && session.Dirty, "Real simulation creates unsaved state");
                string unsavedRoot = GoldMinerSaveCodec.Encode(session.GetSnapshot());
                long revision = session.StateRevision;
                int attempts = files.WriteAttempts;
                files.FailWrites = true;
                try { Page.Save(); }
                finally { files.FailWrites = false; }
                Require(files.WriteAttempts == attempts + 1, "Dirty Page.Save reaches real filesystem boundary");
                Require(session.Dirty && !string.IsNullOrEmpty(Page.SaveError), "Failed autosave keeps dirty state and visible error");
                Require(session.StateRevision == revision && GoldMinerSaveCodec.Encode(session.GetSnapshot()) == unsavedRoot,
                    "Failed autosave preserves the complete live root");
                Require(File.ReadAllText(Store.SavePath) == beforeDisk, "Failed autosave preserves the previous main file bytes");
                Capture(session.ActiveMode.ToString().ToLowerInvariant() + "-save-error");
                CheckNoticeGeometry("Running save failure");
                Page.Save();
                Require(!session.Dirty && Page.SaveError == null, "Recovered autosave clears dirty and error");
                Require(GoldMinerSaveCodec.Encode(session.GetSnapshot()) == unsavedRoot && File.ReadAllText(Store.SavePath) == unsavedRoot,
                    "Recovered autosave stores precisely the retained live root");
                Report.AppendLine("PASS " + session.ActiveMode + " real Page.Save write failure: retained live state/dirty/error and old file; next successful save persists it and clears error.");
            }

            public void CheckPurchaseFailure(GoldMinerItem item, int player)
            {
                var session = Get<GoldMinerSession>(Page, "session");
                Require(session.GetStateSnapshot().Phase == GoldMinerPhase.Shop, "Purchase failure starts in the actual shop");
                string beforeRoot = GoldMinerSaveCodec.Encode(session.GetSnapshot());
                string beforeDisk = File.ReadAllText(Store.SavePath);
                long revision = session.StateRevision;
                bool dirty = session.Dirty;
                int attempts = files.WriteAttempts;
                files.FailWrites = true;
                try { Invoke(Page, "Purchase", item, player); }
                finally { files.FailWrites = false; }
                Require(files.WriteAttempts == attempts + 1 && !string.IsNullOrEmpty(Page.SaveError),
                    "Shop purchase attempts save and exposes filesystem failure");
                Require(session.StateRevision == revision && session.Dirty == dirty && GoldMinerSaveCodec.Encode(session.GetSnapshot()) == beforeRoot,
                    "Failed purchase does not commit cash, inventory, shop stock or transaction sequence");
                Require(File.ReadAllText(Store.SavePath) == beforeDisk, "Failed purchase preserves committed disk root");
                Capture(session.ActiveMode.ToString().ToLowerInvariant() + "-shop-save-error");
                CheckNoticeGeometry("Shop purchase save failure");
                Report.AppendLine("PASS " + session.ActiveMode + " shop write failure: candidate remains uncommitted with unchanged cash/inventory/stock/txn/file; following normal purchase verifies one successful retry.");
            }

            public void CheckFrameClockIndependence()
            {
                var diagnostics = Get<GoldMinerDiagnostics>(Page, "diagnostics");
                Require(diagnostics != null, "Real page diagnostics are present in editor validation");
                var samples = Get<List<double>>(diagnostics, "milliseconds");
                int before = samples.Count;
                double frameTime = Time.realtimeSinceStartupAsDouble - .25d;
                Set(Page, "lastFrameRealTime", frameTime);
                Set(Page, "lastRealTime", Time.realtimeSinceStartupAsDouble - .03d);
                Set(Page, "accumulator", 0d);
                Invoke(Page, "SynchronizeInputClock");
                Require(Get<double>(Page, "lastFrameRealTime") == frameTime,
                    "Input catch-up cannot overwrite the independent render-frame timestamp");
                Invoke(Page, "Update");
                Require(samples.Count == before + 1 && samples[samples.Count - 1] >= 249,
                    "Frame diagnostics retain the complete simulated 250ms gap after input-clock catch-up");
                Report.AppendLine("PASS separate clock accounting: injected 250ms frame gap remains visible after input clock catches up 30ms; timing injection tests accounting, not device performance.");
            }

            private static void AdvanceToLastTick(GoldMinerSession session)
            {
                Require(!session.IsSuspended && session.GetStateSnapshot().Phase == GoldMinerPhase.Running,
                    "Deadline scenario starts from a real active round");
                while (session.GetStateSnapshot().RemainingTicks > 1)
                    Require(session.Step().Advanced, "Production Core advances toward deadline");
                Require(session.GetStateSnapshot().RemainingTicks == 1 && session.GetStateSnapshot().SimulationTick == GoldMinerRules.LevelTicks - 1,
                    "Production Core reaches exactly one remaining tick");
            }

            public void ReplaySelectedWitness()
            {
                var session = Get<GoldMinerSession>(Page, "session");
                var run = session.GetRunSnapshot();
                string key = "GoldMiner/Levels/" + GoldMinerRules.ModeId(run.Mode) + "-" + run.Current.LevelIndex.ToString("00");
                var shard = JsonUtility.FromJson<WitnessShard>(Resources.Load<TextAsset>(key).text);
                Witness witness = null;
                foreach (var candidate in shard.layouts) if (candidate.id == run.Current.LayoutId) witness = candidate.witness;
                Require(witness != null, "Chosen production layout has a witness");
                int input = 0;
                for (int tick = 0; tick <= witness.endTick; tick++)
                {
                    while (input < witness.inputs.Length && witness.inputs[input].tick == tick)
                    {
                        var command = witness.inputs[input++];
                        Require(command.action == "launch", "Witness contains only launch commands");
                        long before = session.GetRunSnapshot().LastCommandSeq;
                        // This scenario already drives all simulation time explicitly.
                        // Event-clock catch-up is separately tested above with a stale
                        // timestamp; do not add test harness CPU time to this witness.
                        Set(Page, "lastRealTime", Time.realtimeSinceStartupAsDouble);
                        Set(Page, "accumulator", 0d);
                        Invoke(Page, "Launch", command.playerId);
                        Require(session.GetRunSnapshot().LastCommandSeq == before + 1, "Production page accepts witness launch at tick " + tick);
                    }
                    Invoke(Page, "AdvanceOne");
                    var state = session.GetStateSnapshot();
                    if (state.Phase == GoldMinerPhase.Won || state.Phase == GoldMinerPhase.Completed) break;
                }
                Require(input == witness.inputs.Length && session.GetStateSnapshot().Cash >= session.GetStateSnapshot().Target, "Witness earns actual target without fabricated cash");
            }

            public void CheckMineGeometry(GoldMinerMode mode)
            {
                var metrics = Get<XanaduYonder.Runtime.GoldMiner.GoldMinerLayout>(Page, "layout");
                Require(metrics.Mine.xMin >= metrics.Safe.xMin && metrics.Mine.xMax <= metrics.Safe.xMax, "Mine width remains in safe area");
                Require(metrics.Mine.yMin >= metrics.Hud.yMax && metrics.Mine.yMax <= metrics.Safe.yMax - 90, "Mine does not overlap HUD or lower controls");
                Require(Math.Abs(metrics.Mine.width / metrics.Mine.height - GoldMinerRules.Width(mode) / GoldMinerRules.Height(mode)) < .001, "World scale stays uniform");
                CheckButtonsInsideSafeArea();
            }

            public void CheckDesignFidelityGeometry(GoldMinerMode mode)
            {
                var metrics = Get<XanaduYonder.Runtime.GoldMiner.GoldMinerLayout>(Page, "layout");
                var view = Get<RectTransform>(Page, "view");
                var hud = view.Find("Mine HUD") as RectTransform;
                Require(hud != null && hud.rect.height <= 84, "Both gameplay HUDs occupy one compact row from the approved mockup");
                var cash = Get<Text>(Page, "cashText");
                var target = Get<Text>(Page, "targetText");
                var time = Get<Text>(Page, "timeText");
                Require(cash != null && target != null && time != null, "HUD retains real data-bound funds, target and timer");
                Require(hud.Find("资金") != null && hud.Find("目标") != null, "HUD keeps readable funds and target labels beside real artwork icons");
                RequireNoOverlap(ScreenRect(cash.rectTransform), ScreenRect(target.rectTransform), "HUD funds versus target");
                RequireNoOverlap(ScreenRect(target.rectTransform), ScreenRect(time.rectTransform), "HUD target versus timer");
                var pause = hud.Find("Pause button") as RectTransform;
                Require(pause != null && Mathf.Abs(pause.rect.width - pause.rect.height) < .01f, "Pause uses the circular control in the approved mockup");
                Require(pause.Find("Pause icon") != null, "Pause uses icon geometry, not a text substitute");
                if (mode == GoldMinerMode.Coop)
                {
                    Require(view.Find("Player badge P1") != null && view.Find("Player badge P2") != null, "Coop identifies miners with amber and teal badges");
                    Require(view.Find("左半屏点击出钩") != null && view.Find("右半屏点击出钩") != null, "Coop keeps separate hints at their matching input zones");
                }
                Require(metrics.Mine.yMin > hud.anchoredPosition.y + hud.rect.height, "Compact HUD does not consume the mine field");
                Report.AppendLine("PASS " + mode + " design geometry: compact HUD, visible labels, real icons, circular pause, non-overlapping bound values and player control hints.");
            }

            public void CheckButtonsInsideSafeArea()
            {
                Canvas.ForceUpdateCanvases();
                foreach (var button in Page.GetComponentsInChildren<Button>())
                {
                    var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var p = RectTransformUtility.WorldToScreenPoint(null, corner);
                        Require(p.x >= Screen.safeArea.xMin - 1 && p.x <= Screen.safeArea.xMax + 1 && p.y >= Screen.safeArea.yMin - 1 && p.y <= Screen.safeArea.yMax + 1,
                            "Button inside actual safe area: " + button.name + " at " + p);
                    }
                }
            }

            public IEnumerator CheckResponsiveShop(GoldMinerMode mode, bool shortPortrait = false)
            {
                bool cooperative = mode == GoldMinerMode.Coop;
                int width = cooperative ? 1582 : 720;
                int height = cooperative ? 720 : shortPortrait ? 1280 : 1582;
                Require(!Page.enabled, "Long-screen shop fixture owns manual page layout refresh");
                var session = Get<GoldMinerSession>(Page, "session");
                string before = GoldMinerSaveCodec.Encode(session.GetSnapshot());
                Require(session.GetStateSnapshot().Phase == GoldMinerPhase.Shop, "Long-screen fixture starts from the real shop");
                Size(width, height);
                for (int frame = 0; frame < 8; frame++) yield return null;
                Require(Screen.width == width && Screen.height == height, "Long-screen GameView reaches " + width + "x" + height);
                Invoke(Page, "RebuildRun");
                for (int frame = 0; frame < 2; frame++) yield return null;
                Capture((cooperative ? "coop" : "solo") + (shortPortrait ? "-shop-short-screen" : "-shop-long-screen"));
                CheckButtonsInsideSafeArea();
                CheckShopTextGeometry();
                Require(GoldMinerSaveCodec.Encode(session.GetSnapshot()) == before,
                    "Long-screen rendering preserves complete shop state");
                Report.AppendLine("PASS actual " + width + "x" + height + " shop: live GameView, screenshot, safe-area buttons, non-overlapping text regions and complete generated text.");
                Size(cooperative ? 1320 : 720, cooperative ? 720 : 1320);
                for (int frame = 0; frame < 8; frame++) yield return null;
                Invoke(Page, "RebuildRun");
                for (int frame = 0; frame < 2; frame++) yield return null;
            }

            public void CheckModalControlGeometry(string context)
            {
                var modal = Get<RectTransform>(Page, "modal");
                var panel = modal.Find("Dialog panel") as RectTransform;
                Require(panel != null, context + " has a real dialog panel");
                var buttons = panel.GetComponentsInChildren<Button>();
                for (int i = 0; i < buttons.Length; i++)
                for (int j = i + 1; j < buttons.Length; j++)
                    RequireNoOverlap(ScreenRect((RectTransform)buttons[i].transform), ScreenRect((RectTransform)buttons[j].transform),
                        context + " interactive controls " + buttons[i].name + " versus " + buttons[j].name);
                var labels = panel.GetComponentsInChildren<Text>();
                for (int i = 0; i < labels.Length; i++)
                {
                    if (string.IsNullOrEmpty(labels[i].text)) continue;
                    for (int j = i + 1; j < labels.Length; j++)
                        if (!string.IsNullOrEmpty(labels[j].text))
                            RequireNoOverlap(ScreenRect(labels[i].rectTransform), ScreenRect(labels[j].rectTransform),
                                context + " text " + labels[i].text + " versus " + labels[j].text);
                    CheckCompleteText(labels[i], context);
                }
                CheckButtonsInsideSafeArea();
            }

            public IEnumerator CheckLongScreenPause()
            {
                var session = Get<GoldMinerSession>(Page, "session");
                string before = GoldMinerSaveCodec.Encode(session.GetSnapshot());
                bool wasEnabled = Page.enabled;
                Page.enabled = false;
                Size(1582, 720);
                for (int frame = 0; frame < 8; frame++) yield return null;
                Invoke(Page, "RebuildRun");
                for (int frame = 0; frame < 2; frame++) yield return null;
                Capture("coop-pause-long-screen");
                CheckModalControlGeometry("long-screen pause");
                Require(GoldMinerSaveCodec.Encode(session.GetSnapshot()) == before, "Responsive pause rendering preserves the entire run");
                Size(1320, 720);
                for (int frame = 0; frame < 8; frame++) yield return null;
                Invoke(Page, "RebuildRun");
                for (int frame = 0; frame < 2; frame++) yield return null;
                Page.enabled = wasEnabled;
                Report.AppendLine("PASS 1582x720 pause: all actual text regions and interactive buttons remain separate and inside the safe area.");
            }

            public IEnumerator CheckExitConfirmation(string prefix)
            {
                Invoke(Page, "ConfirmExit", Get<Action>(Page, "lobby"));
                for (int frame = 0; frame < 2; frame++) yield return null;
                Capture(prefix + "-confirm-exit");
                CheckButtonsInsideSafeArea();
                yield return Click(Button("继续游戏"));
                Require(Get<GoldMinerSession>(Page, "session").IsSuspended, "Cancel exit restores the paused game without advancing time");
            }

            public void CheckHelpBackdrop(GoldMinerMode mode)
            {
                var view = Get<RectTransform>(Page, "view");
                Require(!view.Find("Mine HUD").gameObject.activeSelf, "Help heading replaces the HUD instead of overlapping it");
                if (mode == GoldMinerMode.Solo)
                {
                    Require(!view.Find("Dynamite P1").gameObject.activeSelf, "Portrait help CTA has a clear bottom control band");
                    Require(!view.Find("Mine touch hint").gameObject.activeSelf, "Portrait help does not cover underlying hint text");
                }
                CheckButtonsInsideSafeArea();
            }

            public void CheckReadyArtwork(GoldMinerMode mode)
            {
                var view = Get<RectTransform>(Page, "view");
                Require(!view.Find("Mine HUD").gameObject.activeSelf && !view.Find("Mine world").gameObject.activeSelf,
                    "Ready artwork is shown over the empty mine backdrop without a duplicate miner or HUD");
                if (mode == GoldMinerMode.Solo)
                    Require(Get<RectTransform>(Page, "modal").Find("Dialog panel/Ready miner").GetComponent<Image>().sprite.name == "ready-thumbsup-miner-v2",
                        "Portrait Ready uses the approved thumbs-up miner artwork");
            }

            public void CheckFailureArtGeometry()
            {
                var panel = Get<RectTransform>(Page, "modal").Find("Dialog panel");
                var miner = panel.Find("Retry miner").GetComponent<Image>();
                Require(miner.sprite.name == "result-failed-miner-sign-v2", "Failure uses the approved miner and blank wooden sign artwork");
                var heading = panel.Find("Result heading") as RectTransform;
                Rect imageRect = FittedImageRect(miner);
                Rect visible = CheckArtworkInsideSafeArea(miner);
                RequireNoOverlap(visible, ScreenRect(panel.Find("这次还差一点") as RectTransform),
                    "Failure visible artwork versus subtitle");
                // Authored blank board: x 11%-89%, top-down y 73.5%-95%.
                // The gloved hands finish above this rectangle; the dynamic title belongs inside it.
                var board = new Rect(imageRect.x + imageRect.width * .11f, imageRect.y + imageRect.height * .05f,
                    imageRect.width * .78f, imageRect.height * .215f);
                Rect text = ScreenRect(heading);
                Require(text.xMin >= board.xMin - 1 && text.xMax <= board.xMax + 1 && text.yMin >= board.yMin - 1 && text.yMax <= board.yMax + 1,
                    "Failure heading remains entirely inside the wooden sign below the miner's hands");
                CheckCompleteText(heading.GetComponent<Text>(), "Failure sign title");
            }

            public void CheckHeaderArtworkGeometry()
            {
                var panel = Get<RectTransform>(Page, "modal").Find("Dialog panel");
                var state = Get<GoldMinerSession>(Page, "session").GetStateSnapshot();
                if (state.Phase == GoldMinerPhase.Failed) { CheckFailureArtGeometry(); return; }
                if (state.Phase == GoldMinerPhase.Ready)
                {
                    var image = panel.Find(Get<GoldMinerSession>(Page, "session").ActiveMode == GoldMinerMode.Coop ? "Ready miners" : "Ready miner").GetComponent<Image>();
                    CheckArtworkInsideSafeArea(image);
                }
                else
                {
                    var image = panel.Find("Victory badge").GetComponent<Image>();
                    Rect visible = CheckArtworkInsideSafeArea(image);
                    RequireNoOverlap(visible, ScreenRect(panel.Find("Result heading") as RectTransform),
                        "Victory visible artwork versus heading");
                }
            }

            public IEnumerator CheckResponsiveHeaderArtwork(GoldMinerMode mode, string phase)
            {
                bool cooperative = mode == GoldMinerMode.Coop;
                var session = Get<GoldMinerSession>(Page, "session");
                string before = GoldMinerSaveCodec.Encode(session.GetSnapshot());
                bool wasEnabled = Page.enabled;
                Page.enabled = false;
                var dimensions = cooperative
                    ? new[] { new Vector2Int(1582, 720), new Vector2Int(2373, 1080), new Vector2Int(2400, 1080) }
                    : new[] { new Vector2Int(720, 1280), new Vector2Int(720, 1582) };
                foreach (var size in dimensions)
                {
                    Size(size.x, size.y);
                    for (int frame = 0; frame < 8; frame++) yield return null;
                    Require(Screen.width == size.x && Screen.height == size.y, "Header artwork GameView reaches " + size);
                    Invoke(Page, "RebuildRun");
                    for (int frame = 0; frame < 2; frame++) yield return null;
                    Capture((cooperative ? "coop" : "solo") + "-" + phase + "-responsive");
                    CheckHeaderArtworkGeometry();
                    CheckButtonsInsideSafeArea();
                    Require(GoldMinerSaveCodec.Encode(session.GetSnapshot()) == before,
                        "Responsive " + phase + " artwork preserves the entire run");
                }
                Size(cooperative ? 1320 : 720, cooperative ? 720 : 1320);
                for (int frame = 0; frame < 8; frame++) yield return null;
                Invoke(Page, "RebuildRun");
                for (int frame = 0; frame < 2; frame++) yield return null;
                Page.enabled = wasEnabled;
                Report.AppendLine("PASS " + mode + " " + phase + " source-alpha bounds: all visible pixels remain in safe area at each live responsive GameView size.");
            }

            private Rect CheckArtworkInsideSafeArea(Image image)
            {
                Rect normalized = SourceAlphaBounds(image.sprite);
                Rect fitted = FittedImageRect(image);
                var visible = new Rect(fitted.x + normalized.x * fitted.width, fitted.y + normalized.y * fitted.height,
                    normalized.width * fitted.width, normalized.height * fitted.height);
                Rect safe = Screen.safeArea;
                Require(visible.xMin >= safe.xMin - .5f && visible.xMax <= safe.xMax + .5f &&
                    visible.yMin >= safe.yMin - .5f && visible.yMax <= safe.yMax + .5f,
                    image.name + " real PNG alpha stays inside all four safe edges at " + Screen.width + "x" + Screen.height +
                    ": visible=" + visible + ", safe=" + safe);
                return visible;
            }

            private Rect SourceAlphaBounds(Sprite sprite)
            {
                string path = AssetDatabase.GetAssetPath(sprite.texture);
                Rect bounds;
                if (sourceAlphaBounds.TryGetValue(path, out bounds)) return bounds;
                Require(!string.IsNullOrEmpty(path) && File.Exists(path), "Artwork alpha check reads the actual source PNG");
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Require(ImageConversion.LoadImage(texture, File.ReadAllBytes(path)), "Artwork source PNG decodes for alpha verification");
                    var pixels = texture.GetPixels32();
                    int left = texture.width, bottom = texture.height, right = 0, top = 0;
                    for (int y = 0; y < texture.height; y++)
                    for (int x = 0; x < texture.width; x++)
                    {
                        if (pixels[y * texture.width + x].a == 0) continue;
                        left = Math.Min(left, x); bottom = Math.Min(bottom, y);
                        right = Math.Max(right, x + 1); top = Math.Max(top, y + 1);
                    }
                    Require(right > left && top > bottom, "Artwork PNG contains visible alpha pixels");
                    bounds = Rect.MinMaxRect(left / (float)texture.width, bottom / (float)texture.height,
                        right / (float)texture.width, top / (float)texture.height);
                    sourceAlphaBounds.Add(path, bounds);
                    return bounds;
                }
                finally { Object.DestroyImmediate(texture); }
            }

            private static Rect FittedImageRect(Image image)
            {
                Rect rect = ScreenRect(image.rectTransform);
                float height = rect.width * image.sprite.rect.height / image.sprite.rect.width;
                if (height < rect.height)
                    return new Rect(rect.x, rect.y + (rect.height - height) * .5f, rect.width, height);
                float width = rect.height * image.sprite.rect.width / image.sprite.rect.height;
                return new Rect(rect.x + (rect.width - width) * .5f, rect.y, width, rect.height);
            }

            public void CheckShopTextGeometry()
            {
                var view = Get<RectTransform>(Page, "view");
                Require(Get<GoldMinerSession>(Page, "session").GetStateSnapshot().Phase == GoldMinerPhase.Shop,
                    "Text geometry check targets actual shop cards");
                foreach (string name in new[] { "炸药", "力量药水", "幸运四叶草", "石头收藏书", "钻石油" })
                {
                    var card = view.Find(name);
                    Require(card != null, "Shop renders card " + name);
                    var labels = card.GetComponentsInChildren<Text>();
                    Require(labels.Length >= 4, "Shop card contains name, description, price and purchase text: " + name);
                    for (int i = 0; i < labels.Length; i++)
                    {
                        var label = labels[i];
                        if (string.IsNullOrEmpty(label.text)) continue;
                        Rect rect = ScreenRect(label.rectTransform);
                        for (int j = i + 1; j < labels.Length; j++)
                        {
                            if (string.IsNullOrEmpty(labels[j].text)) continue;
                            Rect other = ScreenRect(labels[j].rectTransform);
                            float overlapX = Mathf.Min(rect.xMax, other.xMax) - Mathf.Max(rect.xMin, other.xMin);
                            float overlapY = Mathf.Min(rect.yMax, other.yMax) - Mathf.Max(rect.yMin, other.yMin);
                            Require(overlapX <= .5f || overlapY <= .5f,
                                "Shop text regions do not overlap at " + Screen.width + "x" + Screen.height + ": " + name + " [" + label.text + "] vs [" + labels[j].text + "] overlap=" + overlapX + "x" + overlapY);
                        }
                        CheckCompleteText(label, "Shop " + name);
                    }
                }
            }

            private void CheckNoticeGeometry(string context)
            {
                var notice = Get<Text>(Page, "notice");
                Require(notice != null && notice.gameObject.activeInHierarchy && !string.IsNullOrEmpty(notice.text),
                    context + " renders a non-empty error notice");
                Require(notice.text == Page.SaveError, context + " displays the complete current save error");
                Rect bounds = ScreenRect(notice.rectTransform);
                Rect safe = Screen.safeArea;
                Require(bounds.xMin >= safe.xMin - .5f && bounds.xMax <= safe.xMax + .5f &&
                    bounds.yMin >= safe.yMin - .5f && bounds.yMax <= safe.yMax + .5f,
                    context + " notice stays inside actual screen safe area");
                foreach (var button in Page.GetComponentsInChildren<Button>())
                    RequireNoOverlap(bounds, ScreenRect((RectTransform)button.transform), context + " notice versus button " + button.name);
                var contribution = Get<Text>(Page, "contributionText");
                if (contribution != null && contribution.gameObject.activeInHierarchy && !string.IsNullOrEmpty(contribution.text))
                    RequireNoOverlap(bounds, ScreenRect(contribution.rectTransform), context + " notice versus contribution/instructions");
                CheckCompleteText(notice, context + " notice");
                Report.AppendLine("PASS " + Get<GoldMinerSession>(Page, "session").ActiveMode + " " + context + ": real non-empty notice fits its line, remains in safe area and does not overlap buttons or contribution text.");
            }

            private static void RequireNoOverlap(Rect first, Rect second, string context)
            {
                float width = Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin);
                float height = Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin);
                Require(width <= .5f || height <= .5f, context + " overlaps " + width + "x" + height + " pixels");
            }

            private static void CheckCompleteText(Text label, string context)
            {
                // Follow UGUI Text.preferredHeight exactly: author font size and
                // best-fit limits stay in UI units, while scaleFactor is applied by
                // TextGenerator. Its fontSizeUsedForBestFit must not be fed back as
                // an author size: that result can already be scaled to physical pixels.
                // Source: com.unity.ugui/Runtime/UGUI/UI/Core/Text.cs,
                // GetGenerationSettings, OnPopulateMesh and preferredHeight.
                Require(label.cachedTextGenerator.lineCount > 0, context + " has actual generated text: " + label.text);
                var settings = label.GetGenerationSettings(new Vector2(label.GetPixelAdjustedRect().width, 0));
                using (var generator = new TextGenerator())
                {
                    float requiredHeight = generator.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
                    Require(Mathf.Abs(requiredHeight - label.preferredHeight) <= .01f,
                        context + " measurement agrees with production UGUI preferredHeight");
                    Require(requiredHeight <= label.rectTransform.rect.height + .5f,
                        context + " text is complete at " + Screen.width + "x" + Screen.height + ": [" + label.text + "] requires " + requiredHeight + " but has " + label.rectTransform.rect.height);
                    if (context == "Shop 炸药" && label.text == "炸药")
                        CheckTitleScaleUnits(label, requiredHeight);
                }
            }

            private static void CheckTitleScaleUnits(Text label, float measuredHeight)
            {
                var generated = label.cachedTextGenerator;
                Require(generated.lineCount == 1, "Scale-unit self-check uses one real rendered title line");
                float lineHeight = generated.lines[0].height / label.pixelsPerUnit;
                Require(Mathf.Abs(lineHeight - measuredHeight) <= 1f / label.pixelsPerUnit,
                    "Measured title height matches real lineInfo after a single pixel-to-UI conversion: measured=" + measuredHeight + " line=" + lineHeight);
                var mesh = label.canvasRenderer.GetMesh();
                Require(mesh != null && mesh.vertexCount > 0 && generated.verts.Count > 0,
                    "Scale-unit self-check has actual CanvasRenderer and TextGenerator geometry");
                float lower = float.PositiveInfinity, upper = float.NegativeInfinity;
                foreach (var vertex in generated.verts)
                {
                    lower = Mathf.Min(lower, vertex.position.y);
                    upper = Mathf.Max(upper, vertex.position.y);
                }
                float generatorMeshHeight = (upper - lower) / label.pixelsPerUnit;
                Require(Mathf.Abs(generatorMeshHeight - mesh.bounds.size.y) <= .02f,
                    "Actual title mesh height matches generated pixel geometry converted once: generated=" + generatorMeshHeight + " mesh=" + mesh.bounds.size.y);
                Report.AppendLine("TEXT_SCALE_SELF_CHECK screen=" + Screen.width + "x" + Screen.height +
                    " authorFont=" + label.fontSize + " pixelsPerUnit=" + label.pixelsPerUnit +
                    " generatedBestFit=" + generated.fontSizeUsedForBestFit + " measuredUiHeight=" + measuredHeight +
                    " lineUiHeight=" + lineHeight + " generatedMeshUiHeight=" + generatorMeshHeight + " actualMeshUiHeight=" + mesh.bounds.size.y);
            }

            private static Rect ScreenRect(RectTransform transform)
            {
                var corners = new Vector3[4]; transform.GetWorldCorners(corners);
                var lower = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                var upper = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                return Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
            }

            public void Capture(string name)
            {
                int width = Screen.width, height = Screen.height;
                var texture = new RenderTexture(width, height, 24); texture.Create();
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                var oldActive = RenderTexture.active;
                try
                {
                    camera.targetTexture = texture; camera.orthographicSize = height * .5f; camera.aspect = width / (float)height;
                    foreach (var c in new[] { canvas, lobbyCanvas }) if (c != null)
                    { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 1; }
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    RenderTexture.active = texture;
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + "-" + width + "x" + height + ".png"), image.EncodeToPNG());
                }
                finally
                {
                    foreach (var c in new[] { canvas, lobbyCanvas }) if (c != null) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
                    camera.targetTexture = null; RenderTexture.active = oldActive;
                    Object.DestroyImmediate(image); texture.Release(); Object.DestroyImmediate(texture); Canvas.ForceUpdateCanvases();
                }
            }

            public void CheckStoreIsolation()
            {
                Require(Store.SavePath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Fixture store is rooted in a unique temporary directory");
                Require(!Store.SavePath.StartsWith(Application.persistentDataPath + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Fixture never writes the application player save directory");
                foreach (string file in Directory.GetFiles(directory)) Require(Path.GetFileName(file).StartsWith("gold-miner-v1.json", StringComparison.Ordinal), "Fixture writes only Gold Miner files");
            }

            public void CheckReleasedView(string previousBackground)
            {
                var artwork = Get<GoldMinerArtwork>(Page, "art");
                var cache = Get<Dictionary<string, Dictionary<string, Sprite>>>(artwork, "cache");
                Require(!cache.ContainsKey(previousBackground), "New view releases previous background cache: " + previousBackground);
                long textureBytes = 0;
                var textures = new HashSet<Texture>();
                foreach (var sprites in cache.Values)
                    foreach (var sprite in sprites.Values)
                        if (textures.Add(sprite.texture)) textureBytes += Profiler.GetRuntimeMemorySizeLong(sprite.texture);
                Report.AppendLine("VIEW_RESOURCE_SAMPLE " + Get<GoldMinerSession>(Page, "session").GetStateSnapshot().Phase +
                    " mode=" + Get<GoldMinerSession>(Page, "session").ActiveMode + " cacheTextures=" + textures.Count + " textureBytes=" + textureBytes);
            }

            private GameObject Child(string name, params Type[] components)
            {
                var obj = new GameObject(name, components); obj.transform.SetParent(Owner.transform, false); return obj;
            }

            public void Dispose()
            {
                if (Page != null) Page.Leave();
                if (Owner != null) Object.DestroyImmediate(Owner);
                sizes.Dispose();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        // Delegates every operation to the production filesystem except the
        // explicitly armed write failure. This keeps real files, codecs and atomic
        // replacement paths in the integration test rather than faking a save result.
        private sealed class FaultFiles : IGoldMinerSaveFileSystem
        {
            private readonly GoldMinerSaveFileSystem real = new GoldMinerSaveFileSystem();
            public bool FailWrites;
            public int WriteAttempts;
            public bool Exists(string path) { return real.Exists(path); }
            public byte[] ReadBounded(string path, int maximumLength) { return real.ReadBounded(path, maximumLength); }
            public void CreateDirectory(string path) { real.CreateDirectory(path); }
            public void WriteAndFlush(string path, byte[] bytes)
            {
                WriteAttempts++;
                if (FailWrites) throw new IOException("Deliberate Gold Miner UI fixture write failure.");
                real.WriteAndFlush(path, bytes);
            }
            public void Replace(string temporary, string destination, string backup) { real.Replace(temporary, destination, backup); }
            public void Move(string source, string destination) { real.Move(source, destination); }
        }

        // Fixed-resolution GameView drives the real Screen API in editor tests.
        // Added sizes and the selected index are restored when the fixture exits.
        private sealed class GameViewSizeScope : IDisposable
        {
            private readonly EditorWindow window;
            private readonly object group;
            private readonly Type groupType;
            private readonly PropertyInfo selected;
            private readonly int oldIndex;
            private readonly List<int> added = new List<int>();

            public GameViewSizeScope()
            {
                var assembly = typeof(Editor).Assembly;
                var viewType = assembly.GetType("UnityEditor.GameView", true);
                window = EditorWindow.GetWindow(viewType); window.Show();
                selected = viewType.GetProperty("selectedSizeIndex", Instance);
                oldIndex = (int)selected.GetValue(window, null);
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes = singleton.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
                var enumType = assembly.GetType("UnityEditor.GameViewSizeGroupType", true);
                string target = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget).ToString();
                object enumValue = Enum.Parse(enumType, Enum.IsDefined(enumType, target) ? target : "Standalone");
                group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { enumValue });
                groupType = group.GetType();
            }

            public void Set(int width, int height)
            {
                var assembly = typeof(Editor).Assembly;
                var sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
                var enumType = assembly.GetType("UnityEditor.GameViewSizeType", true);
                var value = Activator.CreateInstance(sizeType, Instance, null,
                    new[] { Enum.Parse(enumType, "FixedResolution"), (object)width, height, "Gold Miner UI " + width + "x" + height }, null);
                int index = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { value }); added.Add(index);
                selected.SetValue(window, index, null); window.Repaint();
            }

            public void Dispose()
            {
                selected.SetValue(window, oldIndex, null);
                for (int i = added.Count - 1; i >= 0; i--) groupType.GetMethod("RemoveCustomSize").Invoke(group, new object[] { added[i] });
                window.Repaint();
            }
        }

        private static object Invoke(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Instance).Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Instance).GetValue(target); }
        private static void Set(object target, string name, object value) { target.GetType().GetField(name, Instance).SetValue(target, value); }
        private static void Require(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException(message); }
    }
}
