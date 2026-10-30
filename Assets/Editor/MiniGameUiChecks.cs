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
    public static class MiniGameUiChecks
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
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Require(UiFactory.Font != null, "Bundled font loads");
                foreach (int height in new[] { 1320, 1582 })
                using (var fixture = new Fixture(height))
                {
                    CheckLobby(fixture);
                    CheckZumaHostInput(fixture);
                    CheckJump(fixture);
                    CheckSudoku(fixture);
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
                string[] names = { "Sudoku", "Minesweeper", "SlidingPuzzle", "Sokoban", "Gomoku", "AgeOfWar", "2048", "Watermelon", "Sheep", "LinkMatch", "Zuma", "Tetris", "Jump", "Dino", "FlappyBird", "HappyBall", "Gold Miner", "Idiom" };
                string[] routes = { "EnterSudoku", "EnterMinesweeper", "EnterSlidingPuzzle", "EnterSokoban", "EnterGomoku", "ShowAgeOfWarSelection", "EnterGame", "EnterWatermelon", "EnterSheep", "EnterLinkMatch", "EnterZuma", "EnterTetris", "EnterJump", "EnterDino", "EnterFlappyBird", "EnterHappyBall", "EnterGoldMiner", "EnterIdiom" };
                float lastBottom = 0;
                for (int i = 0; i < names.Length; i++)
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
                Require(cardCount == names.Length, "Lobby contains exactly eighteen ordered game cards");
                var fruit = LobbyDesignChecks.FindCard(content, "LinkMatch").GetComponentsInChildren<RawImage>();
                Require(fruit.Length == 4, "Link Match lobby preview shows four fruit tiles");
                foreach (var tile in fruit) Require(tile.texture != null && !tile.raycastTarget, "Decorative Link Match fruit loads without intercepting input");
                Require(fruit[0].texture == fruit[2].texture && fruit[0].texture != fruit[1].texture && fruit[1].texture != fruit[3].texture,
                    "Link Match preview retains the approved strawberry pair, banana and grapes");
                CheckLobbyPreviews(f, content);
                content.anchoredPosition = Vector2.zero; f.Capture("lobby-top");
                f.ShowLobbyCard(content, LobbyDesignChecks.FindCard(content, "Watermelon")); f.Capture("lobby-watermelon");
                var status = Get<Text>(shell, "lobbyStatus");
                Require(status.transform.parent == content, "Lobby status belongs to scrolling content");
                Invoke(shell, "SetStatus", "所有玩法离线可用 · 进度自动保存");
                var viewport = (RectTransform)content.parent;
                content.anchoredPosition = new Vector2(0, Mathf.Max(0, content.rect.height - viewport.rect.height));
                f.Capture("lobby-footer");
                var finalCard = LobbyDesignChecks.FindCard(content, "Idiom");
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
            page.Initialize(root, () => { }, () => { }, () => true, () => false, () => false, feedback, _ => { },
                (title, message, primary, action, secondary, cancel) => dialog = title);
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
                f.Click((RectTransform)book.transform);
                Require(dialog == "找到跳跃的节奏", "Jump book icon opens existing rules");
                var scene = Get<JumpSceneView>(page, "scene");
                Get<Camera>(scene, "sceneCamera").Render();
                f.Capture("jump");
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
                f.Click((RectTransform)restart.transform);
                Require(Get<GameObject>(page, "dialog") == overlay, "Modal raycast blocks underlying restart");
                Require(!Get<CanvasGroup>(page, "gameInput").interactable, "Modal disables underlying keyboard navigation");
                f.Capture("sudoku-help");
                Button close = null;
                foreach (var button in overlay.GetComponentsInChildren<Button>())
                    if (button.GetComponentInChildren<Text>()?.text == "返回对局") close = button;
                Require(close != null, "Rules exposes return action");
                f.Click((RectTransform)close.transform);
                Require(Get<GameObject>(page, "dialog") == null && session.CanPlay, "Closing help resumes live game");
                session.Tick(1);
                Require(session.ElapsedSeconds == seconds + 1, "Timer advances after rules close");
                Require(Get<SudokuProfileStore>(page, "store") == null, "Sudoku fixture never accesses player saves");
            }
            finally { Object.DestroyImmediate(root.gameObject); }
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
