using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Core.Jump;
using XanaduYonder.Core.Sheep;
using XanaduYonder.Core.Sudoku;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Jump;
using XanaduYonder.Runtime.Sheep;
using XanaduYonder.Runtime.Sudoku;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Production UGUI rendering and behavior checks. Sudoku never opens a store;
    // Jump initializes with a read-only load, then its store is detached before callbacks.
    [InitializeOnLoad]
    public static partial class MiniGameUiChecks
    {
        private const string Pending = "Xanadu.MiniGameUiChecks.Pending";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int checks;
        private static string output;
        private static readonly StringBuilder report = new StringBuilder();

        static MiniGameUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }

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
            new GameObject("Mini game checks runner").AddComponent<Runner>().StartCoroutine(RunAfterFrame());
        }

        private static IEnumerator RunAfterFrame()
        {
            yield return null;
            Execute();
        }

        private static void Execute()
        {
            int exit = 0;
            var oldFont = UiFactory.Font;
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/validation/minigame-ui"));
            Directory.CreateDirectory(output);
            foreach (var stale in Directory.GetFiles(output, "terminal-*.png")) File.Delete(stale);
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Require(UiFactory.Font != null, "Bundled font loads");
                DialogSheetGeometryChecks.Run();
                DialogTypographyChecks.Run();
                foreach (int height in new[] { 1320, 1582 })
                using (var fixture = new Fixture(height))
                {
                    CheckOutsideTap(fixture);
                    CheckLobby(fixture);
                    CheckZumaHostInput(fixture);
                    CheckJump(fixture);
                    CheckSudoku(fixture);
                    for (int difficulty = 0; difficulty < 4; difficulty++)
                    {
                        CheckSudokuTerminal(fixture, true, 1, difficulty);
                        CheckSudokuTerminal(fixture, false, 1, difficulty);
                        CheckSudokuTerminal(fixture, true, 500, difficulty);
                    }
                    Check2048Terminals(fixture);
                    CheckWatermelonTerminals(fixture);
                    CheckIcons(fixture);
                    CheckSheep(fixture);
                    report.AppendLine("PASS 720x" + height + ": lobby order, jump callbacks, Sudoku counters/modal/input/timer, icon meshes, sheep renders");
                }
                report.AppendLine("MINIGAME_UI_CHECKS_PASS: " + checks + " assertions");
            }
            catch (Exception error)
            {
                exit = 1;
                report.AppendLine("MINIGAME_UI_CHECKS_FAIL after " + checks + " assertions\n" + error);
                Debug.LogException(error);
            }
            finally
            {
                UiFactory.Font = oldFont;
                File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
                Debug.Log(report.ToString());
                EditorApplication.Exit(exit);
            }
        }

        private static void CheckZumaHostInput(Fixture f)
        {
            var inert = new GameObject("Inert Zuma host input shell"); inert.SetActive(false);
            var shell = inert.AddComponent<AppController>();
            var canvas = new GameObject("Retained lobby canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            try
            {
                Set(shell, "canvasRoot", (RectTransform)canvas.transform);
                int actions = 0;
                var button = UiFactory.Button(canvas.transform, "Enter another game", 0, 0, 240, 80, () => actions++);
                var transition = button.transition;
                var gate = typeof(AppController).GetMethod("SetLandscapeHostInput", Private);
                Require(gate != null, "Zuma transition gates all host input, not only raycasting");
                gate.Invoke(shell, new object[] { false });
                Require(canvas.activeSelf && !canvas.GetComponent<GraphicRaycaster>().enabled && !button.IsInteractable(),
                    "Retained lobby remains visible but rejects touch and keyboard submission");
                Require(button.transition == Selectable.Transition.None, "Input gating does not flash disabled button tints");
                button.OnSubmit(new BaseEventData(EventSystem.current));
                Require(actions == 0, "Keyboard Submit cannot enter another game while Zuma rotates");
                gate.Invoke(shell, new object[] { true });
                Require(button.IsInteractable() && canvas.GetComponent<GraphicRaycaster>().enabled && button.transition == transition,
                    "Returning from Zuma restores original host input and visual transitions");
                button.OnSubmit(new BaseEventData(EventSystem.current));
                Require(actions == 1, "Restored lobby accepts normal keyboard submission");
            }
            finally { Object.DestroyImmediate(canvas); Object.DestroyImmediate(inert); }
        }

        private static void CheckLobby(Fixture f)
        {
            var inert = new GameObject("Inert shell"); inert.SetActive(false);
            var shell = inert.AddComponent<AppController>();
            Set(shell, "frame", f.Root);
            Set(shell, "canvasRoot", f.Root);
            Invoke(shell, "BuildLobby");
            LobbyDesignChecks.SetModeForCheck(shell, 2);
            var lobby = Get<GameObject>(shell, "lobbyPage");
            try
            {
                var game = new Game2048(Get<IRandomSource>(shell, "random"));
                Set(shell, "game", game);
                Set(shell, "data", new SaveData { Game = game.GetSnapshot(), BestScore = 12345, BestTile = 512 });
                Set(shell, "scoreText", UiFactory.Label(inert.transform, "", 0, 0, 1, 1, 10, Color.white));
                Set(shell, "bestText", UiFactory.Label(inert.transform, "", 0, 0, 1, 1, 10, Color.white));
                Set(shell, "jumpPage", inert.AddComponent<JumpPage>());
                Set(shell, "sheepPage", inert.AddComponent<SheepPage>());
                var sudoku = inert.AddComponent<SudokuPage>();
                Set(sudoku, "profile", new SudokuProfile { CompletedIds = new[] { "se-v1-d0-l001", "se-v1-d0-l002", "se-v1-d1-l001", "se-v1-d2-l001", "se-v1-d3-l001" } });
                Set(shell, "sudokuPage", sudoku);
                Invoke(shell, "RefreshScores");
                var scoreRecord = LobbyDesignChecks.RecordForCheck(shell, "2048");
                Require(scoreRecord.text.Contains("12345") && scoreRecord.text.Contains("512"), "2048 lobby shows independent score and maximum tile records");
                string sudokuSummary = LobbyDesignChecks.RecordForCheck(shell, "Sudoku").text;
                Require(sudokuSummary.Contains("5/2000") && sudokuSummary.Contains("入门 2") && sudokuSummary.Contains("困难 1"), "Sudoku lobby summarizes global and per-difficulty progress");
                var content = lobby.GetComponentInChildren<ScrollRect>().content;
                var names = LobbyDesignChecks.ExpectedNames;
                var routes = LobbyDesignChecks.ExpectedRoutes;
                float lastBottom = 0;
                for (int i = 0; i < names.Count; i++)
                {
                    var card = LobbyDesignChecks.FindCard(content, names[i]);
                    float top = LobbyDesignChecks.CardTop(content, card);
                    Require(i == 0 || Math.Abs(top - lastBottom - 13) < .5f, "Single lobby keeps the approved thirteen-unit gap at " + names[i]);
                    lastBottom = top + card.rect.height;
                    Require(Mathf.Abs(card.rect.width - 354) < .5f && Mathf.Abs(card.rect.height - 143) < .5f,
                        names[i] + " uses the approved single-card dimensions");
                    var entry = card.GetComponent<Button>();
                    Require(entry != null && card.GetComponentsInChildren<Button>().Length == 1, names[i] + " has one whole-card entry");
                    LobbyInputTestDriver.CheckBinding(entry, shell, routes[i], Require);
                    f.ShowLobbyCard(content, card);
                    f.CheckCardGestures(content.parent.GetComponent<ScrollRect>(), entry);
                }
                int cardCount = 0;
                foreach (var button in content.GetComponentsInChildren<Button>())
                    if (button.name.EndsWith(" game card", StringComparison.Ordinal)) cardCount++;
                Require(cardCount == names.Count, "Lobby contains exactly " + names.Count + " ordered game cards");
                Require(Array.Exists(LobbyDesignChecks.FindCard(content, "BakeryBasket").GetComponentsInChildren<Text>(),
                    label => label.text == "抓大鹅"), "Lobby displays the renamed Goose game");
                var fruit = LobbyDesignChecks.FindCard(content, "LinkMatch").GetComponentsInChildren<RawImage>();
                Require(fruit.Length == 4, "Link Match lobby preview shows four fruit tiles");
                foreach (var tile in fruit) Require(tile.texture != null && !tile.raycastTarget, "Decorative Link Match fruit loads without intercepting input");
                Require(fruit[0].texture == fruit[2].texture && fruit[0].texture != fruit[1].texture && fruit[1].texture != fruit[3].texture,
                    "Link Match preview retains the approved strawberry pair, banana and grapes");
                CheckLobbyPreviews(f, content);
                content.anchoredPosition = Vector2.zero; f.Capture("lobby-top");
                CheckLobbyConfirmations(f, shell, content);
                CheckLobbySettings(f, shell, content);
                f.ShowLobbyCard(content, LobbyDesignChecks.FindCard(content, "Watermelon")); f.Capture("lobby-watermelon");
                var status = Get<Text>(shell, "lobbyStatus");
                Require(status.transform.parent == content, "Lobby status belongs to scrolling content");
                Invoke(shell, "SetStatus", "所有玩法离线可用 · 进度自动保存");
                var viewport = (RectTransform)content.parent;
                content.anchoredPosition = new Vector2(0, Mathf.Max(0, content.rect.height - viewport.rect.height));
                f.Capture("lobby-footer");
                var finalCard = LobbyDesignChecks.FindCard(content, names[names.Count - 1]);
                Require(-status.rectTransform.anchoredPosition.y > LobbyDesignChecks.CardTop(content, finalCard) + finalCard.rect.height,
                    "Lobby footer follows the final card without overlap");
                var ids = new string[2000];
                for (int d = 0; d < 4; d++) for (int level = 1; level <= 500; level++) ids[d * 500 + level - 1] = "se-v1-d" + d + "-l" + level.ToString("000");
                Set(sudoku, "profile", new SudokuProfile { CompletedIds = ids }); Invoke(shell, "RefreshSudokuRecord");
                var maximum = LobbyDesignChecks.RecordForCheck(shell, "Sudoku");
                Require(maximum.text.Contains("2000/2000") && maximum.text.Contains("入门 500") && maximum.text.Contains("简单 500") &&
                    maximum.text.Contains("中等 500") && maximum.text.Contains("困难 500"), "Maximum completed counts retain all four difficulty totals");
                f.ShowLobbyCard(content, LobbyDesignChecks.FindCard(content, "Sudoku"));
                CheckSingleLineSummary(maximum); f.Capture("lobby-complete-summary");
            }
            finally { Object.DestroyImmediate(lobby); Object.DestroyImmediate(inert); }
        }

        private static void CheckSingleLineSummary(Text summary)
        {
            Require(!summary.text.Contains("四档难度") && summary.text.IndexOfAny(new[] { '\r', '\n' }) < 0,
                "Sudoku summary omits the redundant difficulty heading and explicit line breaks");
            Require(summary.cachedTextGenerator.lineCount == 1, "Maximum Sudoku summary actually generates one rendered line");
            var mesh = summary.canvasRenderer.GetMesh();
            Require(mesh != null && mesh.vertexCount > 0 && mesh.bounds.size.x <= summary.rectTransform.rect.width + 1,
                "Maximum Sudoku summary submits a visible mesh inside its available width");
            var settings = summary.GetGenerationSettings(summary.rectTransform.rect.size);
            if (summary.resizeTextForBestFit) settings.fontSize = summary.cachedTextGenerator.fontSizeUsedForBestFit;
            settings.resizeTextForBestFit = false;
            using (var generator = new TextGenerator())
                Require(generator.GetPreferredWidth(summary.text, settings) / summary.pixelsPerUnit <= summary.rectTransform.rect.width + 1,
                    "The complete maximum Sudoku summary fits at its actual rendered font size without truncation");
        }

        private static void CheckLobbyPreviews(Fixture f, RectTransform content)
        {
            var idiom = LobbyDesignChecks.FindCard(content, "Idiom");
            Require(idiom.Find("Game logo") == null, "Idiom card retains its text-only presentation");
            f.ShowLobbyCard(content, idiom); f.Capture("lobby-idiom-without-preview");
            string[] cards = { "2048", "Jump", "Watermelon", "Sudoku", "Minesweeper", "Zuma", "Dino", "HappyBall", "SlidingPuzzle", "Tetris", "FlappyBird" };
            var scroll = content.parent.GetComponent<ScrollRect>();
            foreach (string name in cards)
            {
                var card = LobbyDesignChecks.FindCard(content, name); f.ShowLobbyCard(content, card);
                var preview = card.Find("Game logo") as RectTransform;
                var painted = f.CheckPreview(preview, scroll, name);
                Require(Mathf.Abs(preview.rect.width - 63) < .5f && Mathf.Abs(preview.rect.height - 63) < .5f,
                    name + " uses the approved large-logo slot");
                if (name == "Sudoku" || name == "Minesweeper")
                {
                    for (int cell = 0; cell < 9; cell++)
                    {
                        RectTransform square = null;
                        foreach (var candidate in preview.GetComponentsInChildren<RectTransform>())
                            if (candidate.name == name + " preview " + cell) square = candidate;
                        Require(square != null, name + " preview retains all nine cells");
                        var corners = new Vector3[4]; square.GetWorldCorners(corners);
                        float width = Vector3.Distance(corners[0], corners[3]); float height = Vector3.Distance(corners[0], corners[1]);
                        Require(width > 0 && Mathf.Abs(width - height) < .5f, name + " cell " + cell + " remains square in the rendered layout");
                        var mesh = square.GetComponent<Graphic>().canvasRenderer.GetMesh();
                        Require(mesh != null && mesh.vertexCount >= 4 && Mathf.Abs(mesh.bounds.size.x - mesh.bounds.size.y) < .5f,
                            name + " cell " + cell + " submits square render geometry");
                    }
                }
                if (name == "Tetris") CheckTetrisPreview(preview.GetComponentInChildren<XanaduYonder.Runtime.Tetris.TetrisGraphic>());
                if (name == "Zuma" || name == "HappyBall")
                {
                    var artwork = preview.GetComponentInChildren<RawImage>();
                    Require(artwork != null && artwork.texture == Resources.Load<Texture2D>("Lobby/" + name + "Logo"),
                        name + " uses the supplied lobby artwork resource");
                }
                if (name == "Watermelon" || name == "FlappyBird") f.CheckPaintedCenter(preview, painted, name);
                f.Capture("lobby-preview-" + name.ToLowerInvariant());
            }
        }

        private static Rect RenderedBounds(RectTransform relativeTo, Graphic[] graphics)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            int count = 0;
            foreach (var graphic in graphics)
            {
                var mesh = graphic.canvasRenderer.GetMesh();
                if (mesh == null) continue;
                foreach (var vertex in mesh.vertices)
                {
                    Vector2 point = relativeTo.InverseTransformPoint(graphic.transform.TransformPoint(vertex));
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point); count++;
                }
            }
            Require(count > 0, "Visible content submits geometry for its alignment check");
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void CheckTetrisPreview(Graphic graphic)
        {
            Require(graphic != null, "Tetris preview has a rendered Graphic");
            var mesh = graphic.canvasRenderer.GetMesh();
            var vertices = mesh.vertices; var triangles = mesh.triangles;
            // Recover separate drawn shapes from triangle connectivity, rather than inspecting
            // the production piece enum or repeating the preview's tile coordinates.
            var groups = new int[vertices.Length];
            for (int i = 0; i < groups.Length; i++) groups[i] = i;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int root = MeshGroup(groups, triangles[i]);
                groups[MeshGroup(groups, triangles[i + 1])] = root;
                groups[MeshGroup(groups, triangles[i + 2])] = root;
            }
            var bounds = new Dictionary<int, Bounds>();
            var areas = new Dictionary<int, float>();
            for (int i = 0; i < vertices.Length; i++)
            {
                int group = MeshGroup(groups, i);
                if (!bounds.TryGetValue(group, out var box)) box = new Bounds(vertices[i], Vector3.zero);
                box.Encapsulate(vertices[i]); bounds[group] = box;
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int group = MeshGroup(groups, triangles[i]);
                float area = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                    vertices[triangles[i + 2]] - vertices[triangles[i]]).magnitude * .5f;
                areas.TryGetValue(group, out float previous); areas[group] = previous + area;
            }
            Require(bounds.Count == 4, "Tetris preview draws a four-block piece");
            foreach (var pair in bounds)
            {
                var size = pair.Value.size;
                Require(size.x > 0 && Mathf.Abs(size.x - size.y) < .5f && areas[pair.Key] >= size.x * size.y * .9f,
                    "Each Tetris block has square bounds and filled corners instead of a circular pad");
            }
        }

        private static int MeshGroup(int[] groups, int index)
        {
            while (groups[index] != index) index = groups[index];
            return index;
        }

        private static void CheckJump(Fixture f)
        {
            var root = f.Page("Jump fixture");
            var page = root.gameObject.AddComponent<JumpPage>();
            var feedback = root.gameObject.AddComponent<GameFeedback>();
            string dialog = null;
            Action onOutside = null;
            var host = new GameObject("Jump dialog host");
            host.SetActive(false);
            host.transform.SetParent(root, false);
            var shell = host.AddComponent<AppController>();
            Set(shell, "frame", root);
            Set(shell, "canvasRoot", root);
            Set(shell, "data", new SaveData());
            void CloseHost() => Invoke(shell, "CloseModal");
            page.Initialize(root, () => { }, () =>
                typeof(AppController).GetMethod("OpenSettings", Private, null, new[] { typeof(Action) }, null)
                    .Invoke(shell, new object[] { (Action)(() => { }) }),
                () => Get<GameObject>(shell, "modal") == null, () => false, () => false, feedback, _ => { },
                (title, message, primary, action, secondary, cancel, outside) =>
                {
                    dialog = title;
                    Invoke(shell, "ShowMessageDialog", title, message, primary, action, secondary, cancel, false, outside);
                    var owner = Get<GameObject>(shell, "modal");
                    onOutside = outside == null ? null : (Action)(() =>
                    {
                        var graphic = owner.GetComponentInChildren<DialogBackdrop>();
                        var hit = new RaycastResult { gameObject = graphic == null ? owner : graphic.gameObject };
                        owner.GetComponent<DialogOutsideTap>().OnPointerClick(new PointerEventData(EventSystem.current)
                            { pointerPressRaycast = hit, pointerCurrentRaycast = hit });
                    });
                });
            Set(page, "store", null);
            Set(page, "game", new JumpGame(123));
            Set(page, "data", new JumpSaveData { Game = new JumpGame(123).GetSnapshot() });
            try
            {
                page.Enter();
                var nav = root.Find("Jump navigation");
                var restart = Icon(nav, UiIconKind.Restart);
                var book = Icon(nav, UiIconKind.Book);
                f.Click((RectTransform)restart.transform);
                Require(dialog == "重新开始这一局？", "Jump restart icon opens existing confirmation");
                CloseHost();
                f.Click((RectTransform)book.transform);
                Require(dialog == "找到跳跃的节奏", "Jump book icon opens existing rules");
                CloseHost();
                f.Click((RectTransform)Icon(nav, UiIconKind.Settings).transform);
                Require(Get<GameObject>(shell, "modal").GetComponentInChildren<DialogSheetLayout>() != null,
                    "Jump's settings entry uses the production bottom sheet.");
                CloseHost();
                var scene = Get<JumpSceneView>(page, "scene");
                Get<Camera>(scene, "sceneCamera").Render();
                f.Capture("jump");
                var missed = new JumpGame(123).GetSnapshot();
                missed.PlayerX = -2;
                missed.IsGameOver = true;
                var terminal = new JumpGame(missed);
                Set(page, "game", terminal);
                page.CheckEndState();
                Require(onOutside != null, "A terminal jump has a separate outside action");
                onOutside();
                dialog = null;
                page.CheckEndState();
                Require(dialog == null && ReferenceEquals(Get<JumpGame>(page, "game"), terminal),
                    "Dismissing a jump result keeps the same completed run available for inspection");
                f.Click((RectTransform)Get<Button>(page, "reviewResult").transform);
                Require(dialog == "下一次，再远一点。", "The score can be reopened from the completed jump");
            }
            finally
            {
                // Release owned resources between synchronous fixtures before the
                // next camera renders, instead of waiting for the next player frame.
                var scene = Get<JumpSceneView>(page, "scene");
                var texture = Get<RenderTexture>(scene, "texture");
                Get<Camera>(scene, "sceneCamera").targetTexture = null;
                if (texture != null) { texture.Release(); Object.DestroyImmediate(texture); Set(scene, "texture", null); }
                Object.DestroyImmediate(Get<GameObject>(scene, "stage")); Set(scene, "stage", null);
                foreach (var material in Get<List<Material>>(scene, "materials")) Object.DestroyImmediate(material);
                foreach (var mesh in Get<List<Mesh>>(scene, "meshes")) Object.DestroyImmediate(mesh);
                Get<List<Material>>(scene, "materials").Clear(); Get<List<Mesh>>(scene, "meshes").Clear();
                Set(page, "game", null);
                Object.DestroyImmediate(root.gameObject);
            }
        }

        private static void CheckSudoku(Fixture f)
        {
            var root = f.Page("Sudoku fixture");
            var page = root.gameObject.AddComponent<SudokuPage>();
            var profile = new SudokuProfile();
            var catalog = SudokuCatalog.Parse(Resources.Load<TextAsset>("Sudoku/puzzles").text);
            var puzzle = catalog.Get(0, 1);
            var session = new SudokuSession(profile.CreateGame(puzzle), false);
            int left = 0, settings = 0;
            Set(page, "root", root); Set(page, "profile", profile); Set(page, "catalog", catalog); Set(page, "session", session);
            Set(page, "acceptsInput", (Func<bool>)(() => true)); Set(page, "returnLobby", (Action)(() => left++));
            Set(page, "openSettings", (Action)(() => settings++)); Set(page, "setStatus", (Action<string>)(_ => { }));
            Set(page, "soundEnabled", (Func<bool>)(() => false)); Set(page, "hapticsEnabled", (Func<bool>)(() => false));
            Set(page, "feedback", root.gameObject.AddComponent<GameFeedback>());
            Invoke(page, "BuildGame"); Invoke(page, "BuildSelection");
            try
            {
                page.Enter();
                f.Capture("sudoku-selection");
                var previousPage = Get<Button>(page, "previousPage");
                var nextPage = Get<Button>(page, "nextPage");
                foreach (var turn in new[] { previousPage, nextPage })
                {
                    var background = turn.GetComponent<Image>();
                    Require(background.sprite == UiFactory.Disc && background.alphaHitTestMinimumThreshold == 0,
                        "Sudoku pagination shares the circle image and keeps rectangular hit targets");
                }
                Require(!previousPage.interactable && nextPage.interactable, "Only forward pagination is active on the first page");
                f.Click((RectTransform)nextPage.transform);
                Require(Get<int>(page, "levelPage") == 1 && previousPage.interactable, "Next opens the following level page");
                f.Click((RectTransform)previousPage.transform);
                Require(Get<int>(page, "levelPage") == 0 && !previousPage.interactable, "Previous restores the first level page");
                var dropdown = Get<SudokuDifficultyDropdown>(page, "difficultyDropdown");
                // This complete page is created and exercised synchronously before
                // Unity's next Start phase; initialize Dropdown's own tween runner.
                typeof(Dropdown).GetMethod("Start", Private).Invoke(dropdown, null);
                dropdown.alphaFadeSpeed = 0;
                f.Click(dropdown.GetComponent<RectTransform>());
                Require(dropdown.IsExpanded, "Sudoku difficulty selector opens with real pointer input");
                var options = Get<GameObject>(dropdown, "expandedList");
                var items = options.GetComponentsInChildren<Toggle>();
                Require(items.Length == 4, "Difficulty selector exposes all four choices");
                foreach (var item in items) Require(item.GetComponent<Image>().sprite == null, "Difficulty option uses a square surface");
                f.Capture("sudoku-difficulty");
                var blocker = (GameObject)typeof(Dropdown).GetField("m_Blocker", Private).GetValue(dropdown);
                dropdown.Hide();
                // Hide normally destroys these at frame end; isolate subsequent
                // synchronous clicks while preserving the real open-list screenshot.
                options.SetActive(false);
                if (blocker != null) blocker.SetActive(false);
                var selection = Get<GameObject>(page, "selectionRoot");
                f.Click((RectTransform)Icon(selection.transform, UiIconKind.Home).transform);
                Require(left == 1, "Sudoku shared home navigates");
                f.Click((RectTransform)Icon(selection.transform, UiIconKind.Settings).transform);
                Require(settings == 1, "Sudoku shared settings opens");
                Invoke(page, "StartLevel", puzzle, true);
                session = Get<SudokuSession>(page, "session");
                int empty = 0, first = -1;
                for (int i = 0; i < 81; i++) if (!session.Game.IsGiven(i)) { empty++; if (first < 0) first = i; }
                CheckCounter(page, 0, empty, "fresh puzzle excludes givens");
                Invoke(page, "Pick", first);
                Invoke(page, "Number", puzzle.Solution[first]);
                CheckCounter(page, 1, empty - 1, "correct entry");
                f.Click((RectTransform)Get<Button>(page, "eraseButton").transform);
                CheckCounter(page, 0, empty, "erase entry");
                f.Click((RectTransform)Get<Button>(page, "undoButton").transform);
                CheckCounter(page, 1, empty - 1, "undo erase");
                f.Click((RectTransform)Get<Button>(page, "eraseButton").transform);
                f.Click((RectTransform)Get<Button>(page, "pencilButton").transform);
                Invoke(page, "Number", puzzle.Solution[first]);
                Require(session.Game.NotesAt(first) != 0, "Pencil icon writes candidate");
                CheckCounter(page, 0, empty, "candidate not filled");
                f.Click((RectTransform)Get<Button>(page, "pencilButton").transform);
                Invoke(page, "Number", puzzle.Solution[first] % 9 + 1);
                CheckCounter(page, 1, empty - 1, "incorrect entry remains an occupied editable cell");
                Invoke(page, "StartLevel", puzzle, true);
                session = Get<SudokuSession>(page, "session");
                Invoke(page, "Pick", first);
                Invoke(page, "Number", puzzle.Solution[first]);
                var game = Get<GameObject>(page, "gameRoot");
                f.Capture("sudoku-play");
                var rules = Icon(game.transform, UiIconKind.Book);
                session.Tick(2);
                double seconds = session.ElapsedSeconds;
                f.Click((RectTransform)rules.transform);
                var overlay = Get<GameObject>(page, "dialog");
                Require(overlay != null, "Rules icon opens a dialog");
                Require(overlay.GetComponent<Image>().color.a > 0 && overlay.GetComponent<Image>().color.a < 1, "Rules backdrop is translucent");
                var panel = (RectTransform)overlay.transform.Find("Sudoku dialog");
                Require(panel.rect.width < f.Root.rect.width && panel.rect.height < f.Root.rect.height, "Rules panel fits within visible game");
                Require(Vector2.Distance(Center(panel), Center(f.Root)) < .1f, "Rules dialog centered");
                session.Tick(5);
                Require(session.Paused && session.ElapsedSeconds == seconds, "Rules pause game time");
                var restart = Icon(game.transform, UiIconKind.Restart);
                Require(!Get<CanvasGroup>(page, "gameInput").interactable, "Modal disables underlying keyboard navigation");
                f.Capture("sudoku-help");
                f.Click((RectTransform)restart.transform);
                Require(Get<GameObject>(page, "dialog") == null && Get<SudokuSession>(page, "session") == session &&
                    session.CanPlay && session.ElapsedSeconds == seconds, "Outside tap resumes the original puzzle without activating restart");
                f.Click((RectTransform)rules.transform);
                overlay = Get<GameObject>(page, "dialog");
                Button close = null;
                foreach (var button in overlay.GetComponentsInChildren<Button>())
                    if (button.GetComponentInChildren<Text>()?.text == "返回对局") close = button;
                Require(close != null, "Rules exposes return action");
                f.Click((RectTransform)close.transform);
                Require(Get<GameObject>(page, "dialog") == null && session.CanPlay, "Closing help resumes live game");
                session.Tick(1);
                Require(session.ElapsedSeconds == seconds + 1, "Timer advances after rules close");
                CheckSudokuQuickInput(f, page, puzzle);
                CheckSudokuDialogBranches(f, page, puzzle);
                Require(Get<SudokuProfileStore>(page, "store") == null, "Sudoku fixture never accesses player saves");
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        private static void CheckSudokuQuickInput(Fixture f, SudokuPage page, SudokuPuzzle puzzle)
        {
            const string helpKey = "sudoku.quick-input-help-seen";
            bool hadHelpKey = PlayerPrefs.HasKey(helpKey);
            int storedHelp = PlayerPrefs.GetInt(helpKey);
            try
            {
                PlayerPrefs.DeleteKey(helpKey);
                Invoke(page, "StartLevel", puzzle, true);
                var session = Get<SudokuSession>(page, "session");
                var cells = Get<Image[]>(Get<SudokuBoardView>(page, "board"), "backgrounds");
                var digits = Get<Button[]>(page, "digits");
                var quick = (RectTransform)Get<Button>(page, "quickButton").transform;
                int given = Array.IndexOf(puzzle.Givens, 9);
                int same = Array.IndexOf(puzzle.Givens, 9, given + 1);
                int rowPeer = given / 9 * 9 + (given + 1) % 9;
                int colPeer = (given + 9) % 81;
                f.Click(cells[given].rectTransform);
                Color matching = cells[same].color, row = cells[rowPeer].color, col = cells[colPeer].color;
                Require(matching == UiFactory.Hex("C7DEC8") && row == UiFactory.Hex("EDF1E8") && col == row,
                    "Selecting a given highlights matching digits plus its row and column");
                f.Click(quick);
                var overlay = Get<GameObject>(page, "dialog");
                Require(overlay != null, "First quick-mode use opens help");
                f.Capture("sudoku-dialog-quick-input-help");
                Button enable = null;
                foreach (var button in overlay.GetComponentsInChildren<Button>())
                    if (button.GetComponentInChildren<Text>().text == "开启") enable = button;
                Require(enable != null, "Quick input help can confirm enabling");
                f.Click((RectTransform)enable.transform);
                Require(session.Controls.Quick && cells[given].GetComponent<Outline>().enabled &&
                    cells[same].color == matching && cells[rowPeer].color == row && cells[colPeer].color == col,
                    "Enabling quick mode preserves the existing digit and peer highlights");
                f.Click(quick);
                Require(!session.Controls.Quick && cells[given].GetComponent<Outline>().enabled &&
                    cells[same].color == matching && cells[rowPeer].color == row && cells[colPeer].color == col,
                    "Disabling quick mode preserves the existing digit and peer highlights");
                Invoke(page, "StartLevel", puzzle, true);
                session = Get<SudokuSession>(page, "session");
                f.Click(quick);
                Require(session.Controls.Quick && Get<GameObject>(page, "dialog") == null,
                    "A new round enables quick mode without showing help again");

                var eights = new List<int>();
                for (int i = 0; i < 81; i++)
                    if (puzzle.Solution[i] == 8 && !session.Game.IsGiven(i)) eights.Add(i);
                f.Click((RectTransform)digits[7].transform);
                f.Click(cells[eights[0]].rectTransform);
                Require(cells[eights[0]].GetComponent<Outline>().enabled, "Quick fill focuses the newly filled cell");
                f.Click((RectTransform)digits[8].transform);
                Require(!cells[eights[0]].GetComponent<Outline>().enabled && cells[eights[0]].color != matching && cells[same].color == matching,
                    "Selecting 9 highlights the 9s and clears the former 8 cell focus");

                for (int i = 1; i < eights.Count - 1; i++) session.Game.EnterValue(eights[i], 8);
                f.Click((RectTransform)digits[7].transform);
                int lastEight = eights[eights.Count - 1];
                f.Click(cells[lastEight].rectTransform);
                Require(session.Game.RemainingDigit(8) == 0 && session.Controls.QuickDigit == 9 &&
                    !cells[lastEight].GetComponent<Outline>().enabled && cells[same].color == matching,
                    "Finishing digit 8 advances to 9 without keeping the last 8 highlighted");
                f.Capture("sudoku-quick-next-digit");
            }
            finally
            {
                if (hadHelpKey) PlayerPrefs.SetInt(helpKey, storedHelp);
                else PlayerPrefs.DeleteKey(helpKey);
                PlayerPrefs.Save();
            }
        }

        private static void CheckSudokuTerminal(Fixture f, bool won, int level, int difficulty)
        {
            var root = f.Page("Sudoku terminal fixture");
            var footer = UiFactory.Label(UiFactory.Band(f.Root, "Status", 1278, 34, 1), "", 56, 0, 608, 34, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
            var page = root.gameObject.AddComponent<SudokuPage>();
            var profile = new SudokuProfile();
            var catalog = SudokuCatalog.Parse(Resources.Load<TextAsset>("Sudoku/puzzles").text);
            var puzzle = catalog.Get(difficulty, level);
            Set(page, "root", root); Set(page, "profile", profile); Set(page, "catalog", catalog);
            Set(page, "session", new SudokuSession(profile.CreateGame(puzzle), false));
            Set(page, "acceptsInput", (Func<bool>)(() => true)); Set(page, "returnLobby", (Action)(() => {}));
            Set(page, "openSettings", (Action)(() => {})); Set(page, "setStatus", (Action<string>)(value => footer.text = value));
            Set(page, "soundEnabled", (Func<bool>)(() => false)); Set(page, "hapticsEnabled", (Func<bool>)(() => false));
            Set(page, "feedback", root.gameObject.AddComponent<GameFeedback>());
            Invoke(page, "BuildGame"); Invoke(page, "BuildSelection");
            try
            {
                page.Enter(); Invoke(page, "StartLevel", puzzle, true);
                var session = Get<SudokuSession>(page, "session");
                session.Tick(87);
                for (int i = 0; i < 81 && session.CanPlay; i++)
                {
                    if (session.Game.IsGiven(i)) continue;
                    session.SelectCell(i); session.SelectDigit(won ? puzzle.Solution[i] : puzzle.Solution[i] % 9 + 1);
                }
                Require(session.Game.Phase == (won ? SudokuPhase.Won : SudokuPhase.Lost), "Sudoku reaches its real terminal rules state");
                Invoke(page, "Refresh"); page.CheckEndState();
                var results = page.GetComponent<ResultPresentation>();
                string name = "terminal-sudoku-difficulty-" + difficulty + "-" + (won ? "won" : "lost") + "-level-" + level;
                f.Capture(name + "-waiting");
                results.Advance(0); results.Advance(.81f);
                f.Capture(name + "-result");
                if (won && level == 1 && difficulty == 0)
                {
                    f.Click((RectTransform)FindCaption(root, "更多操作").transform);
                    Require(FindCaption(root, "重玩") != null, "A completed ordinary level exposes its own actions");
                    f.Capture("sudoku-dialog-result-actions");
                    f.Click((RectTransform)FindCaption(root, "返回结算").transform);
                }
                f.Click((RectTransform)FindCaption(root, "查看盘面").transform);
                f.Capture(name + "-inspect");
                f.CheckReviewLayout(root);
                f.Click((RectTransform)FindCaption(root, "查看结算").transform);
                f.Capture(name + "-reopen");
                Require(Get<GameObject>(page, "dialog") != null && !session.CanPlay, "Sudoku reopen preserves terminal state");
            }
            finally { Object.DestroyImmediate(root.gameObject); Object.DestroyImmediate(footer.transform.parent.gameObject); }
        }

        private static Button FindCaption(Transform root, string caption)
        {
            foreach (var button in root.GetComponentsInChildren<Button>())
                if (button.GetComponentInChildren<Text>()?.text == caption) return button;
            throw new InvalidOperationException("Missing button: " + caption);
        }

        private static void CheckCounter(SudokuPage page, int filled, int remaining, string context)
        {
            Require(Get<Text>(page, "remainingText").text == filled + "/" + remaining, "Filled/remaining " + context);
        }

        private static void CheckIcons(Fixture f)
        {
            var root = f.Page("Icon fixture");
            int i = 0;
            foreach (UiIconKind kind in Enum.GetValues(typeof(UiIconKind)))
            {
                int x = 40 + i % 6 * 110, y = 80 + i / 6 * 160;
                UiFactory.IconButton(root, kind, x, y, 58, 58, () => { });
                UiFactory.Label(root, kind.ToString(), x - 15, y + 70, 88, 40, 18, UiFactory.Ink, TextAnchor.MiddleCenter);
                i++;
            }
            f.Capture("shared-icons", true);
            Object.DestroyImmediate(root.gameObject);
        }

        private static void CheckSheep(Fixture f)
        {
            for (int level = 1; level <= 2; level++)
            {
                var root = f.Page("Sheep fixture");
                var stage = UiFactory.Rect(root, "Stage", 32, 100, 656, f.Height - 400);
                var tray = UiFactory.Rect(root, "Tray", 56, f.Height - 270, 608, 110);
                var reserve = UiFactory.Rect(root, "Reserve", 180, f.Height - 150, 360, 120);
                var board = new SheepBoardView(root, stage, tray, reserve, _ => { });
                board.Render(new SheepGame(level, 123));
                f.Capture("sheep-level-" + level);
                Object.DestroyImmediate(root.gameObject);
            }

            var directory = Path.Combine(Path.GetTempPath(), "xanadu-sheep-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var pageRoot = f.Page("Sheep settings fixture");
            var page = pageRoot.gameObject.AddComponent<SheepPage>();
            var store = new SheepSaveStore(directory);
            var session = new SheepSession(new SheepSaveData());
            var inert = new GameObject("Sheep modal host");
            inert.transform.SetParent(pageRoot, false);
            inert.SetActive(false);
            var shell = inert.AddComponent<AppController>();
            Set(shell, "canvasRoot", pageRoot);
            RectTransform panel = null;
            GameObject layer = null;
            void Close()
            {
                Invoke(shell, "CloseModal");
                layer = null; panel = null;
            }
            RectTransform Open(string title, float height, Action back)
            {
                panel = (RectTransform)Invoke(shell, "OpenSheepModal", title, height, back);
                layer = Get<GameObject>(shell, "modal");
                return panel;
            }
            RectTransform OpenSheet(string title, float height, Action back)
            {
                panel = (RectTransform)Invoke(shell, "CreateSheet", title, height, back);
                layer = Get<GameObject>(shell, "modal");
                return panel;
            }
            void Click(string caption)
            {
                var control = (RectTransform)FindCaption(panel, caption).transform;
                control.GetComponentInParent<DialogSheetLayout>()?.Reveal(control);
                f.Click(control);
                if (layer == null) return;
                var sheet = layer.GetComponentInChildren<DialogSheetLayout>();
                if (sheet != null && sheet.IsDismissing) sheet.AdvanceDismissal(1f);
            }
            try
            {
                Set(shell, "data", new SaveData());
                Set(page, "commonSettings", (Action<RectTransform, DialogSheetSections>)((body, sections) =>
                    Invoke(shell, "AddFeedbackControls", body, sections)));
                Set(page, "store", store);
                Set(page, "session", session);
                Set(page, "setStatus", (Action<string>)(_ => { }));
                Set(page, "shellAcceptsInput", (Func<bool>)(() => true));
                Set(page, "showDialog", (Action<string, string, string, Action, string, Action>)((title, body, yes, confirm, no, cancel) =>
                {
                    var confirmation = Open(title, 440, cancel);
                    DialogUi.ActionButton(confirmation, yes, 32, 216, 532, 64, confirm, true);
                    DialogUi.ActionButton(confirmation, no, 32, 304, 532, 64, cancel);
                }));
                Set(page, "openModal", (Func<string, float, Action, RectTransform>)Open);
                Set(page, "openSheet", (Func<string, float, Action, RectTransform>)OpenSheet);
                Set(page, "closeModal", (Action)Close);
                Invoke(page, "ShowModes");
                Click("玩法设置");
                f.Capture("sheep-settings");
                f.Capture("sheep-settings-difficulty");
                Click("困难");
                Click("直接使用默认难度");
                var settingsOwner = layer;
                panel.GetComponentInParent<DialogSheetLayout>().Reveal((RectTransform)FindCaption(panel, "+").transform);
                f.Capture("sheep-settings-tools");
                Click("+");
                Require(layer == settingsOwner && session.Data.Preferences.DefaultDifficulty == SheepDifficulty.Normal && session.Data.Preferences.RemoveLimit == 2,
                    "All groups edit the same unsaved draft in place");
                Click("取消修改");
                Click("玩法设置");
                Click("困难");
                Click("直接使用默认难度");
                Click("+");
                Click("保存玩法设置");
                var saved = store.Load(out _).Preferences;
                Require(saved.DefaultDifficulty == SheepDifficulty.Hard && saved.SkipDifficultyPrompt &&
                    saved.RemoveLimit == 3 && saved.UndoLimit == 3 && saved.ShuffleLimit == 1,
                    "Saving persists values edited in both sections of one sheet");
                Click("玩法设置");
                Click("恢复玩法默认值");
                Click("保存玩法设置");
                saved = store.Load(out _).Preferences;
                Require(saved.DefaultDifficulty == SheepDifficulty.Normal && !saved.SkipDifficultyPrompt && saved.IsStandardTools,
                    "Reset restores the difficulty, prompt and all three tool defaults");

                Invoke(page, "BuildLayout", pageRoot);
                session.Start(SheepMode.Challenge, SheepDifficulty.Hard, 123);
                session.Start(SheepMode.Endless, SheepDifficulty.Normal, 456);
                uint challengeState = session.Data.Challenge.RandomState;
                uint endlessState = session.Data.Endless.RandomState;
                Invoke(page, "ShowModes");
                Require(panel.GetComponentsInChildren<Button>().Length == 5, "Both saved slots share one row per mode");
                Click("第二关挑战");
                Click("另开新局");
                Click("取消");
                Require(session.Data.Challenge.RandomState == challengeState && session.Data.Endless.RandomState == endlessState,
                    "Cancelling replacement retains both saved slots");
                Click("继续第二关");
                Require(session.Game.GetSnapshot().Difficulty == SheepDifficulty.Hard &&
                    session.Game.GetSnapshot().RandomState == challengeState, "Continue restores the original challenge");
                Invoke(page, "ShowModes");
                Click("第二关挑战");
                Click("另开新局");
                Click("开始新局");
                Click("开始挑战");
                var restarted = store.Load(out _);
                Require(restarted.Challenge.Difficulty == SheepDifficulty.Normal && restarted.Endless.RandomState == endlessState,
                    "A confirmed replacement affects only its chosen slot");
                Invoke(page, "ShowModes");
                Click("无尽模式");
                Click("继续无尽");
                Require(session.Game.GetSnapshot().RandomState == endlessState, "The other saved mode still resumes");
                Set(page, "visible", true);
                Invoke(page, "ShowHelp");
                Require(panel != null && FindCaption(panel, "知道了") != null, "Sheep opens the rules above the current round.");
                DialogScreenshotAudit.CaptureCurrent(panel);
                Click("知道了");
                foreach (var mode in new[] { SheepMode.Tutorial, SheepMode.Challenge })
                {
                    int previous = mode == SheepMode.Challenge ? session.Data.Challenge.LayoutId : -1;
                    var generated = SheepLevelGenerator.Generate(mode, SheepDifficulty.Normal, 19, previousLayoutId: previous);
                    session.Start(mode, SheepDifficulty.Normal, 19);
                    foreach (int id in generated.SolutionIds) Require(session.Game.Pick(id).Changed, "The production layout solution remains playable.");
                    Require(session.Game.Phase == SheepPhase.Won, "Completing the deal reaches its real result.");
                    Invoke(page, "Refresh");
                    f.Capture("sheep-terminal-" + mode);
                }
                session.Start(SheepMode.Endless, SheepDifficulty.Normal, 1);
                while (session.Game.Phase == SheepPhase.Playing)
                {
                    var snapshot = session.Game.GetSnapshot();
                    var pick = Enumerable.Range(0, snapshot.Kinds.Length).Where(session.Game.CanPick)
                        .OrderBy(id => snapshot.Tray.Count(slot => snapshot.Kinds[slot] == snapshot.Kinds[id])).First();
                    session.Game.Pick(pick);
                }
                Require(session.Game.Phase == SheepPhase.Lost, "A full tray reaches the recoverable loss panel.");
                Invoke(page, "Refresh");
                f.Capture("sheep-terminal-full-tray");
            }
            finally
            {
                Close();
                Object.DestroyImmediate(pageRoot.gameObject);
                Directory.Delete(directory, true);
            }
        }

        private static Button Icon(Transform root, UiIconKind kind)
        {
            foreach (var icon in root.GetComponentsInChildren<UiIconGraphic>())
                if (icon.Kind == kind) return icon.GetComponentInParent<Button>();
            throw new InvalidOperationException("Missing active icon " + kind + " under " + root.name);
        }
        private static object Invoke(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
        private static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Private).GetValue(target); }
        private static void Set(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }
        private static Vector2 Center(RectTransform rect) { return rect.TransformPoint(rect.rect.center); }
        private static void Require(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }

        private sealed class Runner : MonoBehaviour { }

        private sealed class Fixture : IDisposable
        {
            public readonly int Height;
            public readonly RectTransform Root;
            private readonly GameObject owner;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly EventSystem events;

            public Fixture(int height)
            {
                Height = height;
                owner = new GameObject("Mini game UI fixture");
                Root = new GameObject("Fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
                Root.SetParent(owner.transform, false);
                Root.sizeDelta = new Vector2(720, height);
                camera = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(owner.transform, false);
                camera.enabled = false; camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = height * .5f; camera.aspect = 720f / height;
                camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = UiFactory.Paper; camera.cullingMask = ~(1 << 31);
                target = new RenderTexture(720, height, 24); target.Create(); camera.targetTexture = target;
                var canvas = Root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                events = new GameObject("Fixture events", typeof(EventSystem)).GetComponent<EventSystem>(); events.transform.SetParent(owner.transform, false);
            }
            public RectTransform Page(string name) { var rect = UiFactory.Rect(Root, name, 0, 0, 720, Height); UiFactory.Stretch(rect); return rect; }
            private void Flush()
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                Canvas.ForceUpdateCanvases();
            }
            public void ShowLobbyCard(RectTransform content, RectTransform card)
            {
                Require(card != null, "The requested screenshot card exists");
                var viewport = (RectTransform)content.parent;
                var scroll = viewport.GetComponent<ScrollRect>();
                scroll.StopMovement();
                Flush();
                float top = LobbyDesignChecks.CardTop(content, card);
                float offset = top - (viewport.rect.height - card.rect.height) * .5f;
                content.anchoredPosition = new Vector2(content.anchoredPosition.x,
                    Mathf.Clamp(offset, 0, Mathf.Max(0, content.rect.height - viewport.rect.height)));
                Flush();
                var visible = ScreenRect(viewport);
                var drawn = ScreenRect(card);
                Require(drawn.xMin >= visible.xMin - 1 && drawn.xMax <= visible.xMax + 1 &&
                    drawn.yMin >= visible.yMin - 1 && drawn.yMax <= visible.yMax + 1,
                    card.name + " is wholly inside the viewport before pixel checks and screenshots");
            }
            public void CheckCardGestures(ScrollRect scroll, Button card)
            {
                LobbyInputTestDriver.CheckGestures(events, camera, scroll, card, Flush, Require);
            }
            private Rect ScreenRect(RectTransform rect)
            {
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (var corner in corners)
                {
                    var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
            public void CheckPaintedCenter(RectTransform preview, Rect painted, string context)
            {
                var region = ScreenRect(preview);
                var offset = painted.center - region.center;
                report.AppendLine(context + " painted center offset=" + offset);
                Require(Mathf.Abs(offset.x) <= 8 && Mathf.Abs(offset.y) <= 8,
                    context + " visible pixels are centered within the preview region");
            }
            public Rect CheckPreview(RectTransform preview, ScrollRect scroll, string context)
            {
                Require(preview != null && preview.gameObject.activeInHierarchy, context + " has an active preview");
                Flush();
                int vertices = 0;
                foreach (var graphic in preview.GetComponentsInChildren<Graphic>())
                {
                    Require(!graphic.raycastTarget, context + " decoration cannot capture pointer input: " + graphic.name);
                    var mesh = graphic.canvasRenderer.GetMesh();
                    if (mesh != null) vertices += mesh.vertexCount;
                }
                Require(vertices >= 4, context + " submits actual visible preview geometry");
                var pointer = new PointerEventData(events)
                {
                    position = RectTransformUtility.WorldToScreenPoint(camera, Center(preview)),
                    button = PointerEventData.InputButton.Left
                };
                var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
                Require(hits.Count > 0 && !hits[0].gameObject.transform.IsChildOf(preview) &&
                    ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject) == scroll.gameObject,
                    context + " decoration leaves the real lobby ScrollRect reachable by raycast");

                var bounds = ScreenRect(preview);
                int x = Mathf.Max(0, Mathf.FloorToInt(bounds.xMin)), y = Mathf.Max(0, Mathf.FloorToInt(bounds.yMin));
                int width = Mathf.Min(720, Mathf.CeilToInt(bounds.xMax)) - x;
                int height = Mathf.Min(Height, Mathf.CeilToInt(bounds.yMax)) - y;
                Require(width > 0 && height > 0, context + " has an on-screen pixel region");
                var region = new Rect(x, y, width, height);
                var visible = ReadPixels(region);
                Color32[] hidden;
                try
                {
                    preview.gameObject.SetActive(false);
                    Flush(); hidden = ReadPixels(region);
                }
                finally { preview.gameObject.SetActive(true); Flush(); }
                int changed = 0, left = width, bottom = height, right = -1, top = -1;
                for (int index = 0; index < visible.Length; index++)
                {
                    var a = visible[index]; var b = hidden[index];
                    if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 24) continue;
                    changed++;
                    int px = index % width, py = index / width;
                    left = Mathf.Min(left, px); right = Mathf.Max(right, px);
                    bottom = Mathf.Min(bottom, py); top = Mathf.Max(top, py);
                }
                Require(changed > Mathf.Max(12, width * height / 200), context + " paints visible pixels, compared with its own hidden render");
                report.AppendLine(context + " visible preview pixels=" + changed + ", mesh vertices=" + vertices);
                return Rect.MinMaxRect(x + left, y + bottom, x + right + 1, y + top + 1);
            }
            private Color32[] ReadPixels(Rect region)
            {
                var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D((int)region.width, (int)region.height, TextureFormat.RGB24, false);
                try { image.ReadPixels(region, 0, 0); image.Apply(); return image.GetPixels32(); }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }
            public void CheckReviewLayout(RectTransform page)
            {
                Flush();
                var button = FindCaption(page, "查看结算");
                var bounds = ScreenRect((RectTransform)button.transform);
                foreach (var label in Root.GetComponentsInChildren<Text>())
                {
                    if (!label.enabled || string.IsNullOrWhiteSpace(label.text) || label.transform.IsChildOf(button.transform)) continue;
                    Require(!bounds.Overlaps(ScreenRect(label.rectTransform)), "Review button does not cover visible text: " + label.text);
                }
                foreach (var other in Root.GetComponentsInChildren<Button>())
                {
                    if (!other.enabled || other == button || other.transform.IsChildOf(button.transform)) continue;
                    Require(!bounds.Overlaps(ScreenRect((RectTransform)other.transform)), "Review button does not cover another control: " + other.name);
                }
                foreach (var region in page.GetComponentsInChildren<RectTransform>())
                {
                    if (region.name != "Sudoku board" && region.name != "Swipe board" && region.name != "Bowl") continue;
                    Require(!bounds.Overlaps(ScreenRect(region)), "Review button does not cover the playfield: " + region.name);
                }
                Require(bounds.xMin >= 0 && bounds.yMin >= 0 && bounds.xMax <= 720 && bounds.yMax <= Height,
                    "Review control stays entirely in the rendered frame");
            }

            public void Capture(string name, bool requireVisibleIcons = false)
            {
                Flush();
                var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(720, Height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 720, Height), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + "-720x" + Height + ".png"), image.EncodeToPNG());
                    if (requireVisibleIcons) foreach (var icon in Root.GetComponentsInChildren<UiIconGraphic>())
                    {
                        Require(icon.GetComponent<CanvasRenderer>() != null, icon.Kind + " has the required CanvasRenderer component");
                        var mesh = icon.canvasRenderer.GetMesh();
                        Require(mesh != null && mesh.vertexCount > 0, icon.Kind + " submits an actual render mesh");
                        Require(mesh.bounds.size.x <= icon.rectTransform.rect.width && mesh.bounds.size.y <= icon.rectTransform.rect.height,
                            icon.Kind + " rendered mesh fits its rectangle");
                        var corners = new Vector3[4]; icon.rectTransform.GetWorldCorners(corners);
                        var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                        var max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                        int ink = 0;
                        for (int y = Mathf.Max(0, Mathf.FloorToInt(min.y)); y < Mathf.Min(Height, Mathf.CeilToInt(max.y)); y++)
                        for (int x = Mathf.Max(0, Mathf.FloorToInt(min.x)); x < Mathf.Min(720, Mathf.CeilToInt(max.x)); x++)
                        {
                            var pixel = image.GetPixel(x, y);
                            if (pixel.r < .35f && pixel.g < .5f && pixel.b < .45f) ink++;
                        }
                        Require(ink > 8, icon.Kind + " paints visible ink in actual camera output");
                    }
                }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }
            public void Click(RectTransform rect)
            {
                Flush();
                var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(camera, Center(rect)), button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
                Require(hits.Count > 0, "Drawn control has a raycast hit: " + rect.name);
                pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
                var pressed = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                pointer.pointerPress = pressed != null ? pressed : clicked;
                if (pointer.pointerPress != null) ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
                if (clicked != null && pointer.pointerPress == clicked) ExecuteEvents.Execute(clicked, pointer, ExecuteEvents.pointerClickHandler);
            }
            public void Dispose()
            {
                camera.targetTexture = null;
                if (RenderTexture.active == target) RenderTexture.active = null;
                Object.DestroyImmediate(owner); target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
