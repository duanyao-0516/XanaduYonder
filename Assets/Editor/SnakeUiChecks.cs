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
using XanaduYonder.Core.Snake;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Snake;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Production UGUI rendering only. Every page receives a disposable save directory.
    [InitializeOnLoad]
    public static class SnakeUiChecks
    {
        private const string Pending = "Xanadu.SnakeUiChecks.Pending";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly StringBuilder report = new StringBuilder();
        private static string output;
        private static int checks;

        static SnakeUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }

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
            new GameObject("Snake checks runner").AddComponent<Runner>().StartCoroutine(RunAfterFrame());
        }

        private static IEnumerator RunAfterFrame()
        {
            yield return null;
            int exit = 0;
            var oldFont = UiFactory.Font;
            checks = 0;
            report.Clear();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/validation/snake-ui"));
            Directory.CreateDirectory(output);
            report.AppendLine("Unity " + Application.unityVersion + "; production UGUI + Camera.Render; temporary save directories");
            UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            var routine = CheckAll();
            while (true)
            {
                bool moved = false;
                try { moved = routine.MoveNext(); }
                catch (Exception error)
                {
                    exit = 1;
                    report.AppendLine("SNAKE_UI_CHECKS_FAIL after " + checks + " assertions\n" + error);
                    Debug.LogException(error);
                }
                if (!moved || exit != 0) break;
                yield return routine.Current;
            }
            (routine as IDisposable)?.Dispose();
            if (exit == 0) report.AppendLine("SNAKE_UI_CHECKS_PASS: " + checks + " assertions");
            UiFactory.Font = oldFont;
            File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
            Debug.Log(report.ToString());
            EditorApplication.Exit(exit);
        }

        private static IEnumerator CheckAll()
        {
            Require(UiFactory.Font != null, "Bundled Noto CJK font loads");
            var type = typeof(AppController).Assembly.GetType("XanaduYonder.Runtime.Snake.SnakePage");
            Require(type != null, "MISSING_FEATURE: production SnakePage is required for real UGUI validation");
            foreach (int height in new[] { 1320, 1582 })
            using (var f = new Fixture(height))
            {
                CheckLobby(f);
                using (var p = new PageFixture(f, type))
                {
                    // UGUI Dropdown initializes its tween runner in Start, not Awake.
                    // Let the real lifecycle run before sending any pointer events.
                    yield return null;
                    CheckImmediateCountdown(p, true, "Lobby entry");
                    Require(p.Session.Game.Rules.Mode == SnakeMode.Classic && p.Session.Game.Rules.Boundary == SnakeBoundary.Wrap,
                        "Initial mode is classic with four-edge wrap");
                    f.Capture("classic-ready");
                    Require(p.Session.CountdownSeconds > 0 && !p.Session.CanPlay, "Start uses a countdown before movement");
                    long tick = p.Session.Game.TickCount;
                    p.Advance(.25f);
                    Require(p.Session.Game.TickCount == tick, "Countdown does not move the snake");
                    f.Capture("classic-countdown");
                    p.CompleteCountdown();
                    CheckLayout(f, p);
                    CheckNavigation(f, p);
                    CheckRenderedBoard(f, p);
                    f.Capture("classic-running");
                    CheckFoodGrowth(f, p);
                    CheckMultiTickRendering(f, p);
                    CheckPauseAndLifecycle(f, p);

                    var dropdown = Get<Dropdown>(p.Page, "levelDropdown");
                    Require(dropdown != null && dropdown.options.Count == 3, "Classic dropdown has three difficulty choices");
                    RequireColor(dropdown.GetComponent<Image>().color, "E7EDE4", "Dropdown surface matches shared palette");
                    RequireColor(dropdown.template.GetComponent<Image>().color, "D6C9AE", "Dropdown template matches Sudoku palette");
                    int oldDifficulty = p.Session.Game.Rules.Difficulty;
                    string oldSession = p.Session.SessionId;
                    p.ResumeForChecks();
                    f.Click(dropdown.GetComponent<RectTransform>());
                    Require(p.Session.Paused, "Opening difficulty dropdown pauses before changing a rule");
                    yield return null;
                    var list = Find(p.Root, "Dropdown List");
                    Require(list != null, "Real pointer opens production dropdown list");
                    var choices = list.GetComponentsInChildren<Toggle>();
                    Require(choices.Length == 3, "Expanded classic list renders all three choices");
                    f.Capture("classic-difficulty-list");
                    int next = oldDifficulty == 0 ? 1 : 0;
                    f.Click(choices[next].GetComponent<RectTransform>());
                    foreach (var step in WaitForCondition(() => !((SnakeDropdown)dropdown).IsExpanded, "Changed difficulty dropdown finishes closing")) yield return step;
                    Require(p.Session.Game.Rules.Difficulty == next && p.Session.SessionId != oldSession && dropdown.value == next,
                        "Selecting another difficulty starts that slot directly without a redundant confirmation");
                    CheckImmediateCountdown(p, true, "Difficulty selection");
                    f.Capture("classic-difficulty-selected-countdown");
                    p.CompleteCountdown();
                    string selectedSession = p.Session.SessionId;
                    f.Click(dropdown.GetComponent<RectTransform>());
                    yield return null;
                    choices = Find(p.Root, "Dropdown List").GetComponentsInChildren<Toggle>();
                    f.Click(choices[next].GetComponent<RectTransform>());
                    foreach (var step in WaitForCondition(() => !((SnakeDropdown)dropdown).IsExpanded, "Unchanged difficulty dropdown finishes closing")) yield return step;
                    Require(p.Session.SessionId == selectedSession, "Selecting the current difficulty preserves the same session");
                    CheckImmediateCountdown(p, false, "Unchanged difficulty selection");
                    p.CompleteCountdown();
                    f.Click(dropdown.GetComponent<RectTransform>());
                    yield return null;
                    f.Click(Find(f.Root, "Blocker"));
                    foreach (var step in WaitForCondition(() => !((SnakeDropdown)dropdown).IsExpanded, "Dismissed difficulty dropdown finishes closing")) yield return step;
                    CheckImmediateCountdown(p, false, "Dropdown outside dismissal");

                    p.ResumeForChecks();
                    var oldBoundary = p.Session.Game.Rules.Boundary;
                    oldSession = p.Session.SessionId;
                    f.Click(Find(p.Root, "Boundary"));
                    Require(p.Session.Paused && p.Session.Game.Rules.Boundary == oldBoundary,
                        "Boundary control pauses and asks before applying another boundary");
                    p.ClickDialog("取消");
                    Require(p.Session.SessionId == oldSession && p.Session.Game.Rules.Boundary == oldBoundary,
                        "Boundary cancellation preserves the complete current game identity");
                    f.Capture("classic-boundary-cancelled");

                    var profile = Get<SnakeProfile>(p.Page, "profile");
                    var catalog = Get<SnakeCatalog>(p.Page, "catalog");
                    for (int level = 0; level < 7; level++)
                    {
                        profile.CampaignRecords[level].Wins = 1;
                        profile.CampaignRecords[level].BestScore = catalog.Get(level).TargetFood * 10;
                        profile.CampaignRecords[level].BestElapsedSeconds = 30;
                    }
                    profile.UnlockedLevel = 7;
                    Invoke(p.Page, "StartCampaign", 7, true);
                    Require(p.Session.Game.Rules.Mode == SnakeMode.Campaign, "Campaign starts a real obstacle session");
                    Require(p.Session.Game.Rules.Obstacles.Length > 0, "Obstacle screenshot uses a level with actual internal obstacles");
                    Require(Get<Dropdown>(p.Page, "levelDropdown").options.Count == 12, "Campaign dropdown exposes the 12 local levels");
                    Require(!Find(p.Root, "Boundary").GetComponent<Button>().interactable,
                        "Campaign boundary remains visible and read-only");
                    CheckLayout(f, p);
                    f.Capture("campaign-ready");
                    p.ResumeForChecks();
                    Require(p.Session.Game.Rules.TargetFood > 0, "Campaign carries its food target into the production session");
                    CheckRenderedBoard(f, p);
                    f.Capture("campaign-running");
                    var fixedBoundary = p.Session.Game.Rules.Boundary;
                    Find(p.Root, "Boundary").GetComponent<Button>().onClick.Invoke();
                    Require(p.Session.Game.Rules.Boundary == fixedBoundary, "Even a direct boundary callback cannot alter a campaign rule");
                    f.Click(Get<Button>(p.Page, "modeButton").GetComponent<RectTransform>());
                    p.ClickDialog("经典无限");
                    Require(p.Session.Game.Rules.Mode == SnakeMode.Classic, "Real mode selection returns to classic");
                    CheckLayout(f, p);
                    p.ResumeForChecks();
                    CheckRenderedBoard(f, p);
                    f.Capture("classic-after-mode-switch");
                    f.Click(Get<Button>(p.Page, "modeButton").GetComponent<RectTransform>());
                    p.ClickDialog("障碍闯关");
                    Require(p.Session.Game.Rules.Mode == SnakeMode.Campaign, "Real mode selection restores the campaign");
                    CheckImmediateCountdown(p, false, "Mode selects the existing campaign");
                    CheckLayout(f, p);
                    p.ResumeForChecks();
                    CheckRenderedBoard(f, p);
                    f.Capture("campaign-after-mode-switch");
                    Invoke(p.Page, "Save");
                    Require(Directory.GetFiles(p.SaveDirectory).Length > 0, "Production Save writes only the injected temporary directory");
                    Invoke(p.Page, "Back");
                    Require(p.LobbyRequests == 0 && p.Session.Paused && Get<GameObject>(p.Page, "dialog") != null,
                        "Back first pauses without silently abandoning the page");
                    p.ClickDialog("返回大厅");
                    Require(p.LobbyRequests == 1 && p.Session.Paused, "Explicit lobby action returns to the host with a paused game");
                }
                CheckFullBoardResult(f, type);
                CheckCampaignResult(f, type);
                CheckRestartConfirmation(f, type);
                CheckCampaignPreviewStartAndContinue(f, type);
                CheckLightweightOverlayReturns(f, type);
                CheckLateSettingsCallbacks(f, type);
                CheckCompletedClassicEntryAndSelection(f, type);
                CheckLoadWarningAcknowledgement(f, type);
                CheckPointerCancellationBeforeTick(f, type);
                foreach (var step in CheckCampaignPickerCancellation(f, type)) yield return step;
                report.AppendLine("PASS 720x" + height + ": production pages, layout, actual dropdown clicks, rule rollback, countdown, lifecycle and growth");
            }
        }

        private static void CheckLobby(Fixture f)
        {
            var inert = new GameObject("Inert Snake lobby shell");
            inert.SetActive(false);
            var shell = inert.AddComponent<AppController>();
            Set(shell, "frame", f.Root);
            Invoke(shell, "BuildLobby");
            var lobby = Get<GameObject>(shell, "lobbyPage");
            try
            {
                var content = (RectTransform)lobby.transform.Find("Lobby list viewport/Lobby list content");
                Require(content != null, "Real host lobby retains scrolling content");
                string[] names = { "2048", "Jump", "Sheep", "Watermelon", "Idiom", "Sudoku", "Minesweeper", "Zuma", "Dino", "Gomoku", "HappyBall", "Sokoban", "Snake" };
                float previous = float.PositiveInfinity;
                for (int i = 0; i < names.Length; i++)
                {
                    var card = content.Find(names[i] + " game card") as RectTransform;
                    Require(card != null, "Host has game card " + names[i]);
                    Require(card.anchoredPosition.y < previous, "Host preserves the twelve master cards then appends Snake");
                    Require(Math.Abs(card.rect.height - 350) < .1f, "Card height remains 350 for " + names[i]);
                    Require(card.GetComponentInChildren<Text>().text.StartsWith((i + 1).ToString("00")), "Visible ordinal matches " + names[i]);
                    previous = card.anchoredPosition.y;
                }
                int count = 0;
                foreach (Transform child in content) if (child.name.EndsWith(" game card", StringComparison.Ordinal)) count++;
                Require(count == 13, "Lobby contains exactly thirteen playable game cards");
                content.anchoredPosition = new Vector2(0, Mathf.Max(0, content.rect.height - ((RectTransform)content.parent).rect.height));
                f.Capture("lobby-thirteenth-game");
            }
            finally { Object.DestroyImmediate(lobby); Object.DestroyImmediate(inert); }
        }

        private static void CheckLayout(Fixture f, PageFixture p)
        {
            var rules = p.Session.Game.Rules;
            Require(rules.Width == 20 && rules.Height == 24, "Production board has 20 columns and 24 rows");
            var board = f.Bounds(Find(p.Root, "Snake board"));
            Require(Math.Abs(board.width / 20 - board.height / 24) < .05f, "All 20x24 cells remain square");
            Require(board.xMin >= 0 && board.xMax <= 720 && board.yMin >= 0 && board.yMax <= f.Height,
                "Entire board is visible without cropping or fullscreen");
            Require(Math.Abs(board.width - 648) < 1 && Math.Abs(board.height - 777.6f) < 1,
                "Both screen heights preserve the enlarged 648 by 777.6 board");
            float extraHeight = f.Height - 1320;
            Require(Math.Abs(board.xMin - 36) < 1 && Math.Abs(f.Height - board.yMax - (268 + extraHeight * .45f)) < 1,
                "Board starts at x36/y268 and receives its balanced share of extra height");
            var boardBand = Find(p.Root, "Snake board band");
            var controlBand = f.Bounds(Find(p.Root, "Snake controls"));
            Require(Math.Abs(boardBand.rect.height - 778) < .5f && Math.Abs(controlBand.height - 256) < .5f &&
                Math.Abs(f.Height - controlBand.yMax - (1032 + extraHeight * .80f)) < 1,
                "Board and control bands preserve their approved heights and vertical spacing");
            var frame = Find(p.Root, "Snake board frame");
            var clip = Find(p.Root, "Snake board clip");
            var mask = clip.GetComponent<Mask>();
            var maskImage = clip.GetComponent<Image>();
            Require(frame.parent == boardBand && clip.parent == frame && Find(p.Root, "Snake board").parent == clip,
                "Only the board is clipped, while wrap exits remain outside the clipping subtree");
            Require(mask != null && mask.enabled && !mask.showMaskGraphic && maskImage != null && maskImage.type == Image.Type.Sliced &&
                maskImage.sprite != null && maskImage.sprite.border.x > 0,
                "The board uses a real rounded UGUI image mask without drawing the mask itself");
            var controls = new List<Rect>();
            foreach (string name in new[] { "Pause", "Restart", "Boundary", "Up", "Left", "Right", "Down" })
            {
                var rect = f.Bounds(Find(p.Root, name));
                Require(rect.width >= 95.5f && rect.height >= 95.5f, "Primary control provides at least 96 design units: " + name);
                Require(rect.xMin >= 0 && rect.xMax <= 720.5f && rect.yMin >= 0 && rect.yMax <= f.Height,
                    "Control remains inside page: " + name);
                Require(!board.Overlaps(rect), "Controls do not obscure board: " + name);
                foreach (var other in controls) Require(!rect.Overlaps(other), "Control hit regions do not overlap: " + name);
                controls.Add(rect);
            }
            var left = f.Bounds(Find(p.Root, "Snake left tools"));
            var right = f.Bounds(Find(p.Root, "Snake direction pad"));
            Require(left.xMax <= right.xMin, "Left tools and right direction pad are separate regions");
            var up = f.Bounds(Find(p.Root, "Up"));
            var down = f.Bounds(Find(p.Root, "Down"));
            float upperGap = f.Bounds(Find(p.Root, "Snake progress")).yMin - board.yMax;
            float lowerGap = board.yMin - up.yMax;
            Require(upperGap > 0 && lowerGap > 0 && Math.Abs(upperGap - lowerGap) < 4,
                "Board has balanced empty space between its statistics and the first control row");
            Require(Math.Abs(up.center.y - f.Bounds(Find(p.Root, "Restart")).center.y) < .5f &&
                Math.Abs(down.center.y - f.Bounds(Find(p.Root, "Boundary")).center.y) < .5f,
                "Up aligns with restart and down aligns with the boundary control");
            foreach (var pair in new[] { new KeyValuePair<string, float>("Up", 48), new KeyValuePair<string, float>("Left", 104),
                new KeyValuePair<string, float>("Right", 104), new KeyValuePair<string, float>("Down", 160) })
            {
                var button = Find(p.Root, pair.Key);
                Require(Math.Abs(button.anchoredPosition.y + pair.Value) < .5f && Math.Abs(button.rect.width - 96) < .5f && Math.Abs(button.rect.height - 96) < .5f,
                    "Direction button preserves its 96-unit target and compact vertical placement: " + pair.Key);
            }
            Require(Math.Abs(Find(p.Root, "Snake left tools").anchoredPosition.y + 48) < .5f &&
                Math.Abs(Find(p.Root, "Boundary").anchoredPosition.y + 112) < .5f, "Left tools retain the approved row offsets");
            var modeButton = Get<Button>(p.Page, "modeButton");
            var mode = f.Bounds(modeButton.GetComponent<RectTransform>());
            var modeImage = modeButton.GetComponent<Image>();
            RequireColor(modeImage.color, "E7EDE4", "Mode selector has the shared visible green surface");
            Require(Math.Abs(mode.width - 210) < .5f && Math.Abs(mode.height - 54) < .5f && modeImage.type == Image.Type.Sliced && modeImage.sprite != null,
                "Mode selector is a 210 by 54 rounded control");
            var picker = Get<Dropdown>(p.Page, "levelDropdown");
            var dropdown = f.Bounds(picker.GetComponent<RectTransform>());
            float pickerWidth = rules.Mode == SnakeMode.Classic ? 164 : 260;
            Require(Math.Abs(dropdown.width - pickerWidth) < .5f && Math.Abs(dropdown.height - 54) < .5f &&
                Math.Abs(picker.template.rect.width - pickerWidth) < .5f,
                "Picker and its options resize for the selected mode: " + rules.Mode);
            Require(Math.Abs(picker.captionText.rectTransform.rect.width - (pickerWidth - 58)) < .5f,
                "Picker caption leaves consistent space for its arrow after mode changes");
            Require(mode.center.x > 360 && mode.yMin > board.yMax && !mode.Overlaps(dropdown),
                "Mode entry sits right of the heading above the board without overlapping the level dropdown");
            foreach (string field in new[] { "scoreText", "progressText" })
            {
                var label = Get<Text>(p.Page, field);
                Require(label.fontSize == 25 && label.supportRichText && label.text.Contains("<size=32><b>") && label.text.Contains("</b></size>"),
                    "HUD keeps 25-point labels and emphasizes 32-point bold numbers: " + field);
            }
            var progress = Get<Text>(p.Page, "progressText").text;
            Require(rules.Mode != SnakeMode.Classic || (progress.StartsWith("长度 ", StringComparison.Ordinal) && !progress.Contains("蛇长")),
                "Classic HUD uses the requested length label");
            CheckWrapExitCenters(f, p, board, frame);
            foreach (var t in p.Root.GetComponentsInChildren<Transform>())
            {
                Require(t.name.IndexOf("fullscreen", StringComparison.OrdinalIgnoreCase) < 0 && t.name.IndexOf("footer", StringComparison.OrdinalIgnoreCase) < 0,
                    "Snake page has no fullscreen or footer control: " + t.name);
                Require(t.name != "Direction center", "Direction pad has no center dot object");
            }
            foreach (var label in p.Root.GetComponentsInChildren<Text>()) Require(label.font == UiFactory.Font, "Production labels use bundled Noto CJK");
        }

        private static void CheckWrapExitCenters(Fixture f, PageFixture p, Rect board, RectTransform frame)
        {
            var centers = new[] { new Vector2(board.xMin, board.center.y), new Vector2(board.xMax, board.center.y),
                new Vector2(board.center.x, board.yMax), new Vector2(board.center.x, board.yMin) };
            for (int i = 0; i < centers.Length; i++)
            {
                var exit = Find(p.Root, "Wrap exit " + i);
                var icon = exit.GetComponentInChildren<SnakeIconGraphic>(true);
                Require(exit.parent == frame && Math.Abs(exit.rect.width - 32) < .5f && Math.Abs(exit.rect.height - 32) < .5f &&
                    Vector2.Distance(f.Bounds(exit).center, centers[i]) < .5f,
                    "Wrap exit container is centered on the actual board edge: " + i);
                Require(exit.gameObject.activeSelf == (p.Session.Game.Rules.Boundary == SnakeBoundary.Wrap),
                    "Wrap exits follow the selected boundary: " + i);
                Require(icon != null && Vector2.Distance(icon.rectTransform.pivot, new Vector2(.5f, .5f)) < .001f &&
                    Vector2.Distance(f.Bounds(icon.rectTransform).center, centers[i]) < .5f &&
                    Math.Abs(Mathf.DeltaAngle(icon.rectTransform.localEulerAngles.z, i < 2 ? 0 : 90)) < .5f,
                    "Wrap icon stays centered after its top/bottom rotation: " + i);
                if (!exit.gameObject.activeInHierarchy) continue;
                var mesh = icon.canvasRenderer.GetMesh();
                Require(mesh != null && mesh.vertexCount > 0 && Vector2.Distance(f.ScreenPoint(icon.transform, mesh.bounds.center), centers[i]) < .5f,
                    "Rendered wrap-arrow geometry is centered on its board edge: " + i);
            }
        }

        private static void CheckRenderedBoard(Fixture f, PageFixture p)
        {
            Require(Get<GameObject>(p.Page, "dialog") == null && !Get<GameObject>(p.Page, "countdown").activeSelf,
                "Rounded-board pixel checks inspect the unobscured production page");
            var board = f.Bounds(Find(p.Root, "Snake board"));
            var points = new List<Vector2>();
            foreach (var corner in new[] { new Vector2(board.xMin, board.yMin), new Vector2(board.xMax, board.yMin),
                new Vector2(board.xMin, board.yMax), new Vector2(board.xMax, board.yMax) })
            {
                var inward = new Vector2(corner.x < board.center.x ? 1 : -1, corner.y < board.center.y ? 1 : -1);
                points.Add(corner + inward * 3);
                points.Add(corner + inward * 18);
            }
            var up = f.Bounds(Find(p.Root, "Up"));
            var down = f.Bounds(Find(p.Root, "Down"));
            points.Add((up.center + down.center) * .5f);
            var pixels = f.Pixels(points.ToArray());
            for (int i = 0; i < 8; i += 2)
            {
                RequireColor(pixels[i], "F6F4EC", "Rendered rounded corner exposes the page background: " + i / 2);
                Require(ColorDistance(pixels[i + 1], UiFactory.Paper) > .04f, "Board content remains visible inside the rounded corner: " + i / 2);
            }
            RequireColor(pixels[8], "F6F4EC", "Rendered direction pad center is empty, without a decorative dot");
        }

        private static float ColorDistance(Color a, Color b)
        { return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b); }

        private static void CheckNavigation(Fixture f, PageFixture p)
        {
            var nav = Find(p.Root, "Snake navigation");
            foreach (var pair in new[] { new KeyValuePair<UiIconKind, float>(UiIconKind.Home, 56), new KeyValuePair<UiIconKind, float>(UiIconKind.Book, 536), new KeyValuePair<UiIconKind, float>(UiIconKind.Settings, 606) })
            {
                var button = Icon(nav, pair.Key);
                var area = f.Bounds(button.GetComponent<RectTransform>());
                Require(Math.Abs(area.xMin - pair.Value) < .5 && Math.Abs(f.Height - area.yMax - 34) < .5 && Math.Abs(area.width - 58) < .5,
                    "Shared navigation position and size match " + pair.Key);
            }
            Text brand = null;
            foreach (var text in nav.GetComponentsInChildren<Text>()) if (text.text.Contains("XANADU")) brand = text;
            Require(brand != null && Math.Abs(f.Bounds(brand.rectTransform).center.x - 360) < .5, "Brand remains centered in navigation");
            RequireColor(UiFactory.Paper, "F6F4EC", "Shared paper color is fixed");
            RequireColor(UiFactory.Ink, "173D35", "Shared ink color is fixed");
        }

        private static void CheckFoodGrowth(Fixture f, PageFixture p)
        {
            var rules = SnakeRules.Classic(1, SnakeBoundary.Wrap);
            var snapshot = new SnakeSnapshot { Body = new[] { 406, 405, 404, 403 }, Direction = SnakeDirection.Right,
                Food = 407, RandomState = 1234567, State = SnakeState.Playing };
            var seeded = new SnakeSession(new SnakeGame(rules, snapshot));
            Set(p.Page, "session", seeded);
            Invoke(p.Page, "Refresh");
            p.ResumeForChecks();
            string before = TextFingerprint(p.Root);
            long beforeTick = seeded.Game.TickCount;
            p.Advance(.2f);
            long advanced = seeded.Game.TickCount - beforeTick;
            Require(advanced == 1 && seeded.Game.Body.Length == 5 && seeded.Game.FoodEaten == 1,
                "A production session grows from four to five cells when eating; beforeTick=" + beforeTick +
                ", advanced=" + advanced + ", tick=" + seeded.Game.TickCount + ", length=" + seeded.Game.Body.Length +
                ", food=" + seeded.Game.FoodEaten + ", paused=" + seeded.Paused + ", frameDelta=" + Time.unscaledDeltaTime);
            Require(TextFingerprint(p.Root) != before, "Eating updates visible production HUD text");
            Require(TextFingerprint(Find(p.Root, "Snake progress")).Contains("10"), "HUD shows the ten points awarded for the first food");
            f.Capture("classic-after-food");
        }

        private static void CheckPauseAndLifecycle(Fixture f, PageFixture p)
        {
            f.Click(Find(p.Root, "Pause"));
            Require(p.Session.Paused && !p.Session.CanPlay, "Pause control stops the production session");
            var dialog = Get<GameObject>(p.Page, "dialog");
            Require(dialog != null, "Pause uses a visible modal");
            f.Capture("classic-paused");
            long tick = p.Session.Game.TickCount;
            p.Advance(.2f);
            Require(p.Session.Game.TickCount == tick, "Paused session does not tick");
            p.ClickDialog("继续", "开始");
            Require(p.Session.CountdownSeconds > 0, "Resume starts a fresh countdown");
            p.CompleteCountdown();
            f.Click(Icon(Find(p.Root, "Snake navigation"), UiIconKind.Settings).GetComponent<RectTransform>());
            Require(p.SettingsOpened == 1 && p.Session.Paused, "Settings host callback receives a paused game");
            p.AcceptInput = false;
            p.CloseSettings();
            Require(p.Session.Paused && !p.Session.CanPlay, "Settings close never implicitly resumes movement");
            p.AcceptInput = true;
            p.ResumeForChecks();
            p.AcceptInput = false;
            var oldDirection = p.Session.Game.Direction;
            f.Click(Find(p.Root, "Up"));
            p.Advance(.2f);
            Require(p.Session.Game.Direction == oldDirection, "Host input gate prevents a direction button from queueing a turn");
            p.AcceptInput = true;
            Invoke(p.Page, "SetForeground", false);
            Require(p.Session.Paused, "Losing foreground pauses");
            Invoke(p.Page, "SetForeground", true);
            Require(p.Session.Paused, "Returning to foreground remains paused");
            p.ResumeForChecks();
            Invoke(p.Page, "Suspend");
            Require(p.Session.Paused, "Application suspension pauses and clears motion");
            f.Capture("classic-returned-from-background");
        }

        private static void CheckMultiTickRendering(Fixture f, PageFixture p)
        {
            var rules = SnakeRules.Classic(1, SnakeBoundary.Wrap);
            var snapshot = new SnakeSnapshot { Body = new[] { 406, 405, 404, 403 }, Direction = SnakeDirection.Right,
                Food = 100, RandomState = 1234567, State = SnakeState.Playing };
            Set(p.Page, "session", new SnakeSession(new SnakeGame(rules, snapshot)));
            Invoke(p.Page, "Refresh");
            p.ResumeForChecks();
            p.Advance(.2f);
            var board = Get<object>(p.Page, "board");
            Require(p.Session.Game.TickCount == 1 && Get<SnakeSnapshot>(board, "previous") != null,
                "A normal one-tick frame keeps the interpolation snapshot");
            Set(p.Page, "session", new SnakeSession(new SnakeGame(rules, snapshot)));
            Invoke(p.Page, "Refresh");
            p.ResumeForChecks();
            p.Advance(.15f);
            Require(p.Session.Game.TickCount == 0, "Partial frame preserves the fixed-step accumulator");
            p.Advance(.25f);
            Require(p.Session.Game.TickCount == 2 && p.Session.Game.Body[0] == 408,
                "A .15 plus .25 second frame sequence legitimately advances two standard-speed ticks");
            f.Capture("classic-two-tick-frame");
            Require(Get<SnakeSnapshot>(board, "previous") == null,
                "A multi-tick frame renders the final snapshot without one-cell wrap interpolation");
        }

        private static void CheckLoadWarningAcknowledgement(Fixture f, Type type)
        {
            using (var p = new PageFixture(f, type, directory =>
                File.WriteAllText(Path.Combine(directory, "snake-v1.json"), "{broken-save")))
            {
                var dialog = Get<GameObject>(p.Page, "dialog");
                Require(dialog != null && TextFingerprint(dialog.transform).Contains("进度无法恢复"),
                    "A corrupt temporary save presents the production recovery warning");
                f.Capture("corrupt-save-warning");
                p.ClickDialog("进入游戏");
                CheckImmediateCountdown(p, true, "Acknowledged save recovery");
                Invoke(p.Page, "Leave");
                Invoke(p.Page, "Enter");
                dialog = Get<GameObject>(p.Page, "dialog");
                f.Capture("corrupt-save-reentered");
                Require(dialog == null, "An acknowledged load warning never repeats when reentering the same page");
                CheckImmediateCountdown(p, false, "Lobby reentry after save recovery");
            }
        }

        private static void CheckFullBoardResult(Fixture f, Type type)
        {
            using (var p = new PageFixture(f, type))
            {
                var rules = SnakeRules.Classic(1, SnakeBoundary.Wrap);
                var body = new int[rules.Capacity];
                for (int y = 0; y < rules.Height; y++)
                for (int x = 0; x < rules.Width; x++)
                    body[y * rules.Width + x] = y * rules.Width + (y % 2 == 0 ? x : rules.Width - 1 - x);
                var data = new SnakeSessionData
                {
                    SessionId = Guid.NewGuid().ToString("D"), ElapsedSeconds = 120, ResultRecorded = false,
                    Board = new SnakeSnapshot { Body = body, Direction = SnakeDirection.Left, State = SnakeState.WonFullBoard,
                        Food = -1, FoodEaten = 476, Score = 4760, TickCount = 476, RandomState = 123 }
                };
                Require(data.TryValidate(rules, out var reason), "Full-board fixture follows the production snapshot contract: " + reason);
                var profile = Get<SnakeProfile>(p.Page, "profile");
                profile.SelectedMode = 0; profile.SelectedDifficulty = 1; profile.SelectedBoundary = SnakeBoundary.Wrap;
                Set(p.Page, "session", new SnakeSession(rules, data));
                Set(p.Page, "sessionStarted", true);
                Invoke(p.Page, "Refresh");
                Invoke(p.Page, "CheckEndState");
                string text = TextFingerprint(Get<GameObject>(p.Page, "dialog").transform);
                Require(text.Contains("满盘通关") && text.Contains("100%") && text.Contains("480 / 480") && text.Contains("4760") && text.Contains("476") && text.Contains("02:00") && text.Contains("四边相通"),
                    "Full-board result shows complete capacity, food, score, time and boundary");
                Require(p.Session.ResultRecorded && profile.GetClassicRecord(1, SnakeBoundary.Wrap).Wins == 1,
                    "Full-board UI records the completed game exactly once");
                Invoke(p.Page, "CheckEndState");
                Require(profile.GetClassicRecord(1, SnakeBoundary.Wrap).Wins == 1, "Repeated end-state checks do not count another full-board win");
                Require(SnakeSaveCodec.TryDecode(File.ReadAllText(Path.Combine(p.SaveDirectory, "snake-v1.json")),
                    Get<SnakeCatalog>(p.Page, "catalog"), out var saved, out reason), "Full-board UI saves a valid profile: " + reason);
                Require(saved.GetClassic(1, SnakeBoundary.Wrap).Board.State == SnakeState.WonFullBoard && saved.GetClassicRecord(1, SnakeBoundary.Wrap).Wins == 1,
                    "The saved file contains the completed full board and its single win");
                f.Capture("classic-full-board-result");
                p.ClickDialog("再来一局");
                f.Capture("classic-play-again-countdown");
                Require(p.Session.SessionId != data.SessionId, "Play again creates a new session identity");
                CheckImmediateCountdown(p, true, "Play again");
                CheckCountdownStartsMovement(p);
            }
        }

        private static void CheckCampaignResult(Fixture f, Type type)
        {
            using (var p = new PageFixture(f, type))
            {
                var catalog = Get<SnakeCatalog>(p.Page, "catalog");
                var rules = catalog.Get(0);
                var body = new int[4 + rules.TargetFood];
                for (int i = 0; i < body.Length; i++) body[i] = rules.Width * (rules.Height - 1) + i;
                var data = new SnakeSessionData
                {
                    SessionId = Guid.NewGuid().ToString("D"), ElapsedSeconds = 30,
                    Board = new SnakeSnapshot { Body = body, Direction = SnakeDirection.Left, State = SnakeState.WonTarget,
                        Food = -1, FoodEaten = rules.TargetFood, Score = rules.TargetFood * 10, TickCount = rules.TargetFood, RandomState = 456 }
                };
                Require(data.TryValidate(rules, out var reason), "Campaign result fixture follows the production snapshot contract: " + reason);
                var profile = Get<SnakeProfile>(p.Page, "profile");
                profile.SelectedMode = 1; profile.SelectedLevel = 0;
                Set(p.Page, "session", new SnakeSession(rules, data));
                Set(p.Page, "sessionStarted", true);
                Invoke(p.Page, "Refresh");
                Invoke(p.Page, "CheckEndState");
                string text = TextFingerprint(Get<GameObject>(p.Page, "dialog").transform);
                Require(text.Contains("关卡通关") && text.Contains("得分 80") && text.Contains("果实 8") && text.Contains("00:30") && text.Contains("环游起步"),
                    "Target result shows real level, score, collected food and time");
                Require(p.Session.ResultRecorded && profile.GetCampaignRecord(0).Wins == 1 && profile.UnlockedLevel == 1,
                    "Campaign UI records the win and unlocks exactly the next level");
                Require(SnakeSaveCodec.TryDecode(File.ReadAllText(Path.Combine(p.SaveDirectory, "snake-v1.json")), catalog, out var saved, out reason),
                    "Campaign UI saves a valid profile: " + reason);
                Require(saved.CampaignSlot.Board.State == SnakeState.WonTarget && saved.UnlockedLevel == 1 && saved.GetCampaignRecord(0).Wins == 1,
                    "The saved file contains the target win and unlocked next level");
                f.Capture("campaign-target-result");
                p.ClickDialog("下一关");
                f.Capture("campaign-next-countdown");
                Require(p.Session.SessionId != data.SessionId && p.Session.Game.Rules.LevelId == catalog.Get(1).LevelId,
                    "Next level immediately creates the next campaign session without another preview");
                CheckImmediateCountdown(p, true, "Next level");
                CheckCountdownStartsMovement(p);
            }
        }

        private static void CheckImmediateCountdown(PageFixture p, bool fresh, string action)
        {
            Require(!p.Session.Paused && Math.Abs(p.Session.CountdownSeconds - 3) < .0001 && !p.Session.CanPlay &&
                Get<GameObject>(p.Page, "dialog") == null && Get<GameObject>(p.Page, "countdown").activeSelf,
                action + " immediately starts its visible three-second countdown without a pause dialog");
            Require(Get<IList>(p.Session, "directions").Count == 0, action + " clears queued input before the countdown");
            if (fresh)
                Require(p.Session.Game.Body.Length == 4 && p.Session.Game.Score == 0 && p.Session.Game.TickCount == 0 && p.Session.ElapsedSeconds == 0,
                    action + " begins with four cells, zero score, zero ticks and zero effective elapsed time");
        }

        private static void CheckCountdownStartsMovement(PageFixture p)
        {
            long tick = p.Session.Game.TickCount;
            double elapsed = p.Session.ElapsedSeconds;
            p.CompleteCountdown();
            Require(p.Session.Game.TickCount == tick && p.Session.ElapsedSeconds == elapsed,
                "Countdown consumes no game ticks or effective game time");
            p.Advance(.25f);
            Require(p.Session.Game.TickCount == tick + 1 && p.Session.ElapsedSeconds > elapsed && Get<GameObject>(p.Page, "dialog") == null,
                "The session automatically moves after countdown completion without another confirmation");
        }

        private static void CheckRestartConfirmation(Fixture f, Type type)
        {
            using (var p = new PageFixture(f, type))
            {
                p.CompleteCountdown(); p.Advance(.2f);
                string original = p.Session.SessionId;
                long tick = p.Session.Game.TickCount;
                int head = p.Session.Game.Body[0], score = p.Session.Game.Score;
                double elapsed = p.Session.ElapsedSeconds;
                Require(p.Session.QueueDirection(SnakeDirection.Up), "Restart fixture includes an input awaiting its next tick");
                f.Click(Find(p.Root, "Restart"));
                p.ClickDialog("取消");
                Require(p.Session.SessionId == original && p.Session.Paused && p.Session.CountdownSeconds == 0 &&
                    p.Session.Game.TickCount == tick && p.Session.Game.Body[0] == head && p.Session.Game.Score == score && p.Session.ElapsedSeconds == elapsed,
                    "Cancel restart preserves the same board and effective time while staying paused");
                f.Capture("classic-restart-cancelled");
                p.ClickDialog("继续"); p.CompleteCountdown();
                Require(p.Session.QueueDirection(SnakeDirection.Up), "Confirmed restart starts with an old queued direction to clear");
                f.Click(Find(p.Root, "Restart"));
                p.ClickDialog("重新开始");
                f.Capture("classic-restart-countdown");
                Require(p.Session.SessionId != original, "Confirmed restart replaces the old session identity");
                CheckImmediateCountdown(p, true, "Confirmed restart");
                CheckCountdownStartsMovement(p);
            }
        }

        private static void CheckCampaignPreviewStartAndContinue(Fixture f, Type type)
        {
            using (var p = new PageFixture(f, type))
            {
                p.CompleteCountdown();
                string classicId = p.Session.SessionId;
                f.Click(Get<Button>(p.Page, "modeButton").GetComponent<RectTransform>());
                p.ClickDialog("障碍闯关");
                Require(p.Session.SessionId == classicId && TextFingerprint(Get<GameObject>(p.Page, "dialog").transform).Contains("开始关卡"),
                    "Normal campaign selection still previews its level before explicit start");
                f.Capture("campaign-start-preview");
                p.ClickDialog("开始关卡");
                Require(p.Session.SessionId != classicId && p.Session.Game.Rules.Mode == SnakeMode.Campaign,
                    "Preview start creates the selected campaign session");
                CheckImmediateCountdown(p, true, "Campaign preview start");
                f.Capture("campaign-preview-start-countdown");
                CheckCountdownStartsMovement(p);
                string campaignId = p.Session.SessionId;
                long tick = p.Session.Game.TickCount;
                double elapsed = p.Session.ElapsedSeconds;
                f.Click(Get<Button>(p.Page, "modeButton").GetComponent<RectTransform>());
                p.ClickDialog("经典无限"); p.ResumeForChecks();
                f.Click(Get<Button>(p.Page, "modeButton").GetComponent<RectTransform>());
                p.ClickDialog("障碍闯关");
                Require(p.Session.SessionId == campaignId && p.Session.Game.TickCount == tick && p.Session.ElapsedSeconds == elapsed,
                    "Mode selection directly restores an active campaign without an extra preview");
                CheckImmediateCountdown(p, false, "Existing campaign mode selection");
                p.CompleteCountdown();
                // Exercise the production preview's continue action as well as the
                // direct active-campaign route above; the button itself is raycast.
                Invoke(p.Page, "PreviewCampaign", 0, null);
                Require(TextFingerprint(Get<GameObject>(p.Page, "dialog").transform).Contains("继续关卡"),
                    "An existing selected campaign offers continuation from its normal preview");
                p.ClickDialog("继续关卡");
                Require(p.Session.SessionId == campaignId && p.Session.Game.TickCount == tick && p.Session.ElapsedSeconds == elapsed,
                    "Preview continue preserves the existing campaign board and effective time");
                CheckImmediateCountdown(p, false, "Campaign preview continue");
                f.Capture("campaign-preview-continue-countdown");
                CheckCountdownStartsMovement(p);
                tick = p.Session.Game.TickCount;
                Invoke(p.Page, "Leave"); Invoke(p.Page, "Enter");
                Require(p.Session.SessionId == campaignId && p.Session.Game.TickCount == tick,
                    "Lobby reentry preserves an existing campaign session");
                CheckImmediateCountdown(p, false, "Lobby reentry into an existing game");
                Invoke(p.Page, "SetForeground", false); Invoke(p.Page, "SetForeground", true);
                Require(p.Session.Paused && p.Session.CountdownSeconds == 0 && Get<GameObject>(p.Page, "dialog") != null,
                    "Returning from background keeps the restored game paused");
                p.Advance(.25f);
                Require(p.Session.Game.TickCount == tick, "A foreground-restored paused game does not advance");
            }
        }

        private static void CheckLightweightOverlayReturns(Fixture f, Type type)
        {
            using (var p = new PageFixture(f, type))
            {
                p.CompleteCountdown();
                string sessionId = p.Session.SessionId;
                f.Click(Icon(Find(p.Root, "Snake navigation"), UiIconKind.Book).GetComponent<RectTransform>());
                p.ClickDialog("知道了");
                CheckImmediateCountdown(p, false, "Help acknowledgement from play");
                p.CompleteCountdown();
                f.Click(Icon(Find(p.Root, "Snake navigation"), UiIconKind.Book).GetComponent<RectTransform>());
                Invoke(p.Page, "Back");
                CheckImmediateCountdown(p, false, "Help Back from play");
                p.Advance(.25f);
                // The page adapter also preserves a countdown-origin return; pointer
                // input itself remains covered by the countdown overlay.
                Invoke(p.Page, "ShowRules"); p.ClickDialog("知道了");
                CheckImmediateCountdown(p, false, "Help return from an interrupted countdown");
                p.CompleteCountdown();
                f.Click(Icon(Find(p.Root, "Snake navigation"), UiIconKind.Settings).GetComponent<RectTransform>());
                p.CloseSettings();
                CheckImmediateCountdown(p, false, "Settings completion from play");
                p.CompleteCountdown();
                f.Click(Get<Button>(p.Page, "modeButton").GetComponent<RectTransform>());
                p.ClickDialog("取消");
                CheckImmediateCountdown(p, false, "Mode selection cancellation from play");
                p.CompleteCountdown();
                Invoke(p.Page, "Suspend");
                f.Click(Icon(Find(p.Root, "Snake navigation"), UiIconKind.Book).GetComponent<RectTransform>());
                p.ClickDialog("知道了");
                Require(p.Session.Paused && p.Session.CountdownSeconds == 0 && Get<GameObject>(p.Page, "dialog") != null,
                    "Help opened from a paused session returns to its paused state");
                Invoke(p.Page, "CloseDialog");
                f.Click(Icon(Find(p.Root, "Snake navigation"), UiIconKind.Settings).GetComponent<RectTransform>());
                p.CloseSettings();
                Require(p.Session.Paused && p.Session.CountdownSeconds == 0 && Get<GameObject>(p.Page, "dialog") != null,
                    "Settings opened from a paused session does not start it");
                Invoke(p.Page, "Back");
                Require(p.LobbyRequests == 1 && p.Session.Paused && p.Session.SessionId == sessionId,
                    "Back from pause returns to the lobby while retaining the session");
            }
        }

        private static void CheckLateSettingsCallbacks(Fixture f, Type type)
        {
            using (var p = new PageFixture(f, type))
            {
                p.CompleteCountdown();
                f.Click(Icon(Find(p.Root, "Snake navigation"), UiIconKind.Settings).GetComponent<RectTransform>());
                Invoke(p.Page, "SetForeground", false); Invoke(p.Page, "SetForeground", true);
                p.CloseSettings();
                Require(p.Session.Paused && p.Session.CountdownSeconds == 0 && Get<GameObject>(p.Page, "dialog") != null,
                    "A settings callback invalidated by backgrounding cannot resume the game");
                p.ResumeForChecks();
                f.Click(Icon(Find(p.Root, "Snake navigation"), UiIconKind.Settings).GetComponent<RectTransform>());
                Invoke(p.Page, "Leave"); p.CloseSettings();
                Require(p.Session.Paused && p.Session.CountdownSeconds == 0 && !p.Root.gameObject.activeSelf,
                    "A late settings callback after Leave cannot start a hidden session");
                Invoke(p.Page, "Enter"); p.CompleteCountdown();
                f.Click(Icon(Find(p.Root, "Snake navigation"), UiIconKind.Settings).GetComponent<RectTransform>());
                Invoke(p.Page, "Leave"); Invoke(p.Page, "Enter");
                p.Advance(.25f);
                double countdown = p.Session.CountdownSeconds;
                p.CloseSettings();
                Require(countdown > 0 && p.Session.CountdownSeconds == countdown && Get<GameObject>(p.Page, "dialog") == null,
                    "An old settings callback cannot alter a later explicit lobby entry");
            }
        }

        private static IEnumerable<object> CheckCampaignPickerCancellation(Fixture f, Type type)
        {
            using (var p = new PageFixture(f, type))
            {
                yield return null;
                p.CompleteCountdown();
                f.Click(Get<Button>(p.Page, "modeButton").GetComponent<RectTransform>());
                p.ClickDialog("障碍闯关"); p.ClickDialog("取消");
                Require(TextFingerprint(Get<GameObject>(p.Page, "dialog").transform).Contains("选择玩法"),
                    "Cancelling the first campaign preview returns to mode selection");
                p.ClickDialog("取消");
                CheckImmediateCountdown(p, false, "Cancelling the nested mode and preview flow");
                p.CompleteCountdown();
                f.Click(Get<Button>(p.Page, "modeButton").GetComponent<RectTransform>());
                p.ClickDialog("障碍闯关"); p.ClickDialog("开始关卡"); p.CompleteCountdown();
                var picker = Get<Dropdown>(p.Page, "levelDropdown");
                string sessionId = p.Session.SessionId;
                foreach (bool useBack in new[] { false, true })
                {
                    f.Click(picker.GetComponent<RectTransform>());
                    yield return null;
                    var choices = Find(p.Root, "Dropdown List").GetComponentsInChildren<Toggle>();
                    f.Click(choices[1].GetComponent<RectTransform>());
                    var dialog = Get<GameObject>(p.Page, "dialog");
                    Button locked = null;
                    foreach (var button in dialog.GetComponentsInChildren<Button>())
                        if (button.GetComponentInChildren<Text>().text.Contains("尚未解锁")) locked = button;
                    Require(locked != null && !locked.interactable && p.Session.SessionId == sessionId,
                        "A locked campaign preview disables its primary action and preserves the active game");
                    // Deliberately act before UGUI's 0.15-second old-list fade finishes.
                    if (useBack) Invoke(p.Page, "Back");
                    else { yield return null; p.ClickDialog("取消"); }
                    foreach (var step in WaitForCondition(() => Get<GameObject>(p.Page, "dialog") == null && ((SnakeDropdown)picker).IsExpanded,
                        "Preview returns to its visible level picker")) yield return step;
                    // This is a stability observation after the reopen, not a wait for
                    // completion: a late old Hide must not destroy the replacement list.
                    yield return new WaitForSecondsRealtime(.2f);
                    report.AppendLine("Campaign preview " + (useBack ? "Back" : "Cancel") + ": dialog=" +
                        (Get<GameObject>(p.Page, "dialog") != null) + ", picker=" + Get<SnakeDropdown>(p.Page, "levelDropdown").IsExpanded +
                        ", paused=" + p.Session.Paused + ", countdown=" + p.Session.CountdownSeconds);
                    Require(Get<GameObject>(p.Page, "dialog") == null && Get<SnakeDropdown>(p.Page, "levelDropdown").IsExpanded,
                        "Immediate preview " + (useBack ? "Back" : "Cancel") + " restores the level picker after its fading list is destroyed");
                    Invoke(p.Page, "Back");
                    foreach (var step in WaitForCondition(() => !((SnakeDropdown)picker).IsExpanded, "Restored level picker finishes closing")) yield return step;
                    CheckImmediateCountdown(p, false, "Closing the restored level picker");
                    Require(p.Session.SessionId == sessionId, "Picker preview cancellation preserves the original session identity");
                    p.CompleteCountdown();
                }
            }
        }

        private static IEnumerable<object> WaitForCondition(Func<bool> condition, string reason)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Require(condition(), reason + " within two seconds");
        }

        private static void CheckCompletedClassicEntryAndSelection(Fixture f, Type type)
        {
            using (var p = new PageFixture(f, type, directory => WriteCompletedClassicSave(directory, false)))
            {
                Require(p.Session.Game.State == SnakeState.WonFullBoard && p.Session.CountdownSeconds == 0 &&
                    TextFingerprint(Get<GameObject>(p.Page, "dialog").transform).Contains("满盘通关"),
                    "Lobby entry into a completed slot shows its result without starting a new game");
                string completedId = p.Session.SessionId;
                Invoke(p.Page, "ShowRules"); p.ClickDialog("知道了");
                Require(p.Session.SessionId == completedId && p.Session.Game.State == SnakeState.WonFullBoard &&
                    TextFingerprint(Get<GameObject>(p.Page, "dialog").transform).Contains("满盘通关"),
                    "A lightweight overlay opened from a result restores that same result");
                Invoke(p.Page, "Leave"); Invoke(p.Page, "Enter");
                Require(p.Session.SessionId == completedId && Get<SnakeProfile>(p.Page, "profile").GetClassicRecord(1, SnakeBoundary.Wrap).Wins == 1,
                    "Repeated entry into a completed slot preserves its result and counts its win once");
            }
            using (var p = new PageFixture(f, type, directory => WriteCompletedClassicSave(directory, true)))
            {
                p.CompleteCountdown();
                var profile = Get<SnakeProfile>(p.Page, "profile");
                string completedId = profile.GetClassic(1, SnakeBoundary.Wrap).SessionId;
                f.Click(Get<Button>(p.Page, "modeButton").GetComponent<RectTransform>());
                p.ClickDialog("经典无限");
                Require(p.Session.SessionId != completedId && p.Session.Game.Rules.Mode == SnakeMode.Classic,
                    "Selecting classic with a completed slot creates a fresh game instead of reopening the result");
                CheckImmediateCountdown(p, true, "Selecting a completed classic slot");
                Require(profile.GetClassicRecord(1, SnakeBoundary.Wrap).Wins == 1 && profile.GetClassicRecord(1, SnakeBoundary.Wrap).BestScore == 4760,
                    "Replacing the completed slot preserves its earned records");
                Require(SnakeSaveCodec.TryDecode(File.ReadAllText(Path.Combine(p.SaveDirectory, "snake-v1.json")),
                    Get<SnakeCatalog>(p.Page, "catalog"), out var saved, out var reason), "Reselected classic saves a valid profile: " + reason);
                Require(saved.GetClassic(1, SnakeBoundary.Wrap).Board.State == SnakeState.Playing && saved.GetClassicRecord(1, SnakeBoundary.Wrap).Wins == 1,
                    "Persisted classic slot advances to the new game without rolling back its record");
            }
        }

        private static void WriteCompletedClassicSave(string directory, bool enterCampaign)
        {
            var catalog = SnakeCatalog.Parse(Resources.Load<TextAsset>("Snake/levels").text);
            var rules = SnakeRules.Classic(1, SnakeBoundary.Wrap);
            var body = new int[rules.Capacity];
            for (int y = 0; y < rules.Height; y++)
            for (int x = 0; x < rules.Width; x++) body[y * rules.Width + x] = y * rules.Width + (y % 2 == 0 ? x : rules.Width - 1 - x);
            var data = new SnakeSessionData { SessionId = Guid.NewGuid().ToString("D"), ElapsedSeconds = 120,
                Board = new SnakeSnapshot { Body = body, Direction = SnakeDirection.Left, State = SnakeState.WonFullBoard,
                    Food = -1, FoodEaten = 476, Score = 4760, TickCount = 476, RandomState = 123 } };
            var profile = new SnakeProfile(catalog);
            profile.Capture(new SnakeSession(rules, data));
            profile.SelectedMode = enterCampaign ? 1 : 0;
            File.WriteAllText(Path.Combine(directory, "snake-v1.json"), SnakeSaveCodec.Encode(profile));
        }

        private static void CheckPointerCancellationBeforeTick(Fixture f, Type type)
        {
            bool allCanceled = true;
            var frames = new[] {
                new SnakePointerFrame { TouchCount = 2, PrimaryPressed = true },
                new SnakePointerFrame { TouchCount = 1, Canceled = true, PrimaryPressed = true }
            };
            foreach (var frame in frames)
            using (var p = new PageFixture(f, type))
            {
                var rules = SnakeRules.Classic(1, SnakeBoundary.Wrap);
                var snapshot = new SnakeSnapshot { Body = new[] { 406, 405, 404, 403 }, Direction = SnakeDirection.Right,
                    Food = 100, RandomState = 1234567, State = SnakeState.Playing };
                Set(p.Page, "session", new SnakeSession(new SnakeGame(rules, snapshot)));
                Invoke(p.Page, "Refresh");
                p.ResumeForChecks();
                Require(p.Session.QueueDirection(SnakeDirection.Up) && p.Session.QueueDirection(SnakeDirection.Left),
                    "Pointer cancellation fixture has two valid queued turns before its next tick");
                Invoke(p.Page, "AdvancePointerFrame", .2f, frame);
                int pending = Get<IList>(p.Session, "directions").Count;
                bool canceled = p.Session.Game.TickCount == 1 && p.Session.Game.Direction == SnakeDirection.Right && p.Session.Game.Body[0] == 407 && pending == 0;
                report.AppendLine("Pointer cancellation " + (frame.Canceled ? "cancel" : "two-finger") + ": " +
                    "direction=" + p.Session.Game.Direction + ", head=" + p.Session.Game.Body[0] + ", queued=" + pending);
                allCanceled &= canceled;
            }
            Require(allCanceled, "Canceled and multi-touch frames clear queued turns before the same frame advances the snake");
        }

        private static string TextFingerprint(Transform root)
        {
            var value = new StringBuilder();
            foreach (var text in root.GetComponentsInChildren<Text>()) value.Append(text.text).Append('|');
            return value.ToString();
        }

        private static void RequireColor(Color actual, string hex, string reason)
        {
            Color expected = UiFactory.Hex(hex);
            Require(Math.Abs(actual.r - expected.r) < .01 && Math.Abs(actual.g - expected.g) < .01 && Math.Abs(actual.b - expected.b) < .01, reason);
        }

        private static RectTransform Find(Transform root, string name)
        {
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true)) if (rect.name == name) return rect;
            throw new InvalidOperationException("Missing production UI element: " + name);
        }

        private static Button Icon(Transform root, UiIconKind kind)
        {
            foreach (var icon in root.GetComponentsInChildren<UiIconGraphic>()) if (icon.Kind == kind) return icon.GetComponentInParent<Button>();
            throw new InvalidOperationException("Missing navigation icon " + kind);
        }

        private static T Get<T>(object value, string name)
        {
            var field = value.GetType().GetField(name, Members);
            Require(field != null, "Expected production field " + name);
            return (T)field.GetValue(value);
        }

        private static void Set(object value, string name, object state)
        {
            var field = value.GetType().GetField(name, Members);
            Require(field != null, "Expected production field " + name);
            field.SetValue(value, state);
        }

        private static object Invoke(object value, string name, params object[] args)
        {
            var method = value.GetType().GetMethod(name, Members);
            Require(method != null, "Expected production method " + name);
            return method.Invoke(value, args);
        }

        private sealed class PageFixture : IDisposable
        {
            public readonly RectTransform Root;
            public readonly Component Page;
            public readonly string SaveDirectory;
            public bool AcceptInput = true;
            public int LobbyRequests, SettingsOpened;
            private readonly Fixture fixture;
            private Action settingsClosed;
            public SnakeSession Session { get { return Get<SnakeSession>(Page, "session"); } }

            public PageFixture(Fixture f, Type type, Action<string> prepareSave = null)
            {
                fixture = f;
                SaveDirectory = Path.Combine(Path.GetTempPath(), "XanaduSnakeUi-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(SaveDirectory);
                prepareSave?.Invoke(SaveDirectory);
                Root = f.Page();
                Page = Root.gameObject.AddComponent(type);
                // Disable automatic frames before initialization, while OnDisable has
                // no session to pause. Enter may intentionally start a countdown.
                ((Behaviour)Page).enabled = false;
                var feedback = Root.gameObject.AddComponent<GameFeedback>();
                Invoke(Page, "Initialize", Root, (Action)(() => LobbyRequests++), (Action<Action>)(closed => { SettingsOpened++; settingsClosed = closed; }),
                    (Func<bool>)(() => AcceptInput), (Func<bool>)(() => false), (Func<bool>)(() => false), feedback, (Action<string>)(_ => { }), SaveDirectory);
                Invoke(Page, "Enter");
                // Keep all game timing explicit while child UGUI components run their
                // normal lifecycle and receive real EventSystem pointer events.
            }

            public void CloseSettings() { var closed = settingsClosed; settingsClosed = null; closed?.Invoke(); }
            public void Advance(float seconds) { Invoke(Page, "AdvanceFrame", seconds); }

            public void ClickDialog(params string[] labels)
            {
                var dialog = Get<GameObject>(Page, "dialog");
                Require(dialog != null, "Expected production confirmation dialog");
                foreach (string label in labels)
                foreach (var button in dialog.GetComponentsInChildren<Button>())
                {
                    var text = button.GetComponentInChildren<Text>();
                    if (text == null || !text.text.Contains(label)) continue;
                    fixture.Click(button.GetComponent<RectTransform>());
                    return;
                }
                throw new InvalidOperationException("Missing dialog action: " + string.Join("/", labels));
            }

            public void CompleteCountdown()
            {
                for (int i = 0; i < 13 && Session.CountdownSeconds > 0; i++) Advance(.25f);
                Require(Session.CanPlay, "Session becomes playable only after countdown; paused=" + Session.Paused +
                    ", countdown=" + Session.CountdownSeconds + ", state=" + Session.Game.State +
                    ", picker=" + Get<SnakeDropdown>(Page, "levelDropdown").IsExpanded + ", dialog=" + (Get<GameObject>(Page, "dialog") != null) +
                    ", visible=" + Get<bool>(Page, "visible") + ", foreground=" + Get<bool>(Page, "foreground") + ", accepts=" + AcceptInput);
                Advance(0f);
                Require(!Get<GameObject>(Page, "countdown").activeSelf, "Production update removes countdown input cover after completion");
            }

            public void ResumeForChecks()
            {
                var dialog = Get<GameObject>(Page, "dialog");
                if (dialog != null) ClickDialog("继续", "开始");
                if (Session.Paused) Invoke(Page, "ContinueGame");
                CompleteCountdown();
            }

            public void Dispose()
            {
                if (Page != null) Invoke(Page, "Leave");
                if (Root != null) Object.DestroyImmediate(Root.gameObject);
                if (Directory.Exists(SaveDirectory)) Directory.Delete(SaveDirectory, true);
            }
        }

        private static void Require(bool condition, string reason)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(reason);
        }

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
                owner = new GameObject("Snake UI fixture");
                Root = new GameObject("Fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
                Root.SetParent(owner.transform, false);
                Root.sizeDelta = new Vector2(720, height);
                camera = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(owner.transform, false);
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = height * .5f;
                camera.aspect = 720f / height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = UiFactory.Paper;
                target = new RenderTexture(720, height, 24);
                target.Create();
                camera.targetTexture = target;
                var canvas = Root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                events = new GameObject("Fixture events", typeof(EventSystem)).GetComponent<EventSystem>();
                events.transform.SetParent(owner.transform, false);
            }

            public RectTransform Page()
            {
                var rect = UiFactory.Rect(Root, "Snake fixture page", 0, 0, 720, Height);
                UiFactory.Stretch(rect);
                return rect;
            }

            public void Flush() { Canvas.ForceUpdateCanvases(); camera.Render(); Canvas.ForceUpdateCanvases(); }

            public Rect Bounds(RectTransform rect)
            {
                Flush();
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                var low = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var high = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (var corner in corners)
                {
                    var screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
                    low = Vector2.Min(low, screen); high = Vector2.Max(high, screen);
                }
                return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
            }

            public Vector2 ScreenPoint(Transform transform, Vector3 local)
            { return RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(local)); }

            public Color[] Pixels(params Vector2[] positions)
            {
                Flush();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(720, Height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 720, Height), 0, 0); image.Apply();
                    var pixels = new Color[positions.Length];
                    for (int i = 0; i < positions.Length; i++)
                        pixels[i] = image.GetPixel(Mathf.Clamp(Mathf.FloorToInt(positions[i].x), 0, 719),
                            Mathf.Clamp(Mathf.FloorToInt(positions[i].y), 0, Height - 1));
                    return pixels;
                }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }

            public void Capture(string name)
            {
                Flush();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(720, Height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 720, Height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + "-720x" + Height + ".png"), image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }

            public void Click(RectTransform rect)
            {
                Flush();
                var pointer = new PointerEventData(events)
                {
                    position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                    pointerId = -1,
                    button = PointerEventData.InputButton.Left
                };
                var hits = new List<RaycastResult>();
                events.RaycastAll(pointer, hits);
                Require(hits.Count > 0, "Rendered control has a raycast hit: " + rect.name);
                Require(hits[0].gameObject.transform == rect || hits[0].gameObject.transform.IsChildOf(rect),
                    "Rendered pointer reaches requested control: " + rect.name + "; actual " + hits[0].gameObject.name);
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
                Object.DestroyImmediate(owner);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
