using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using XanaduYonder.Runtime;
using XanaduYonder.Core;
using XanaduYonder.Core.Sudoku;
using XanaduYonder.Runtime.Jump;
using XanaduYonder.Runtime.Sheep;
using XanaduYonder.Runtime.Sudoku;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Uses the real host builder without running Awake or opening any game store.
    [InitializeOnLoad]
    public static class LobbyDesignChecks
    {
        private const string Pending = "Xanadu.LobbyDesignChecks.Pending";
        private const string PreferenceKey = "xanadu.lobby.display.v1";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly StringBuilder report = new StringBuilder();
        private static int checks;
        private static string output;

        static LobbyDesignChecks() { EditorApplication.playModeStateChanged += StateChanged; }

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
            new GameObject("Lobby design checks runner").AddComponent<Runner>().StartCoroutine(RunAfterFrame());
        }

        private static IEnumerator RunAfterFrame() { yield return null; Execute(); }

        private static void Execute()
        {
            int exit = 0;
            bool hadPreference = PlayerPrefs.HasKey(PreferenceKey);
            int previousPreference = PlayerPrefs.GetInt(PreferenceKey, 0);
            const string noString = "__lobby_preference_type_probe__";
            string previousString = PlayerPrefs.GetString(PreferenceKey, noString);
            float previousFloat = PlayerPrefs.GetFloat(PreferenceKey, float.NaN);
            var previousFont = UiFactory.Font;
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/lobby-design"));
            Directory.CreateDirectory(output);
            try
            {
                report.Length = 0; checks = 0;
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Require(UiFactory.Font != null, "Bundled font loads");
                CheckPreferences();
                foreach (int height in new[] { 1320, 1582 })
                using (var fixture = new Fixture(height))
                {
                    CheckModes(fixture);
                    CheckRecords(fixture);
                    CheckOptions(fixture);
                    report.AppendLine("PASS production lobby at 720x" + height);
                }
                report.AppendLine("LOBBY_DESIGN_CHECKS_PASS: " + checks + " assertions");
            }
            catch (Exception error)
            {
                exit = 1;
                report.AppendLine("LOBBY_DESIGN_CHECKS_FAIL after " + checks + " assertions\n" + error);
                Debug.LogException(error);
            }
            finally
            {
                if (hadPreference && previousString != noString) PlayerPrefs.SetString(PreferenceKey, previousString);
                else if (hadPreference && !float.IsNaN(previousFloat)) PlayerPrefs.SetFloat(PreferenceKey, previousFloat);
                else if (hadPreference) PlayerPrefs.SetInt(PreferenceKey, previousPreference);
                else PlayerPrefs.DeleteKey(PreferenceKey);
                PlayerPrefs.Save();
                UiFactory.Font = previousFont;
                File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
                Debug.Log(report.ToString());
                EditorApplication.Exit(exit);
            }
        }

        private static readonly string[] Names = { "Sudoku", "Minesweeper", "SlidingPuzzle", "Sokoban", "Gomoku", "AgeOfWar",
            "2048", "Watermelon", "Sheep", "LinkMatch", "Zuma", "Tetris", "Jump", "Dino", "FlappyBird", "HappyBall", "Gold Miner", "Idiom" };
        private static readonly string[] Routes = { "EnterSudoku", "EnterMinesweeper", "EnterSlidingPuzzle", "EnterSokoban", "EnterGomoku", "ShowAgeOfWarSelection",
            "EnterGame", "EnterWatermelon", "EnterSheep", "EnterLinkMatch", "EnterZuma", "EnterTetris", "EnterJump", "EnterDino", "EnterFlappyBird", "EnterHappyBall", "EnterGoldMiner", "EnterIdiom" };
        private static readonly int[] Ordinals = { 6, 7, 14, 12, 10, 18, 1, 4, 3, 17, 8, 15, 2, 9, 16, 11, 13, 5 };
        private static string Category(int i) { return i < 6 ? "逻辑策略" : i < 12 ? "合成消除" : i < 17 ? "敏捷挑战" : "文字语言"; }

        private static object Property(object owner, string name)
        {
            var property = owner.GetType().GetProperty(name, Members);
            return property != null ? property.GetValue(owner) : owner.GetType().GetField(name, Members).GetValue(owner);
        }
        private static object Invoke(object owner, string name, params object[] args)
        {
            var method = owner.GetType().GetMethods(Members).Single(m => m.Name == name && m.GetParameters().Length == args.Length);
            return method.Invoke(owner, args);
        }
        internal static Component View(AppController host)
        {
            var field = typeof(AppController).GetField("lobbyView", Members);
            Require(field != null && field.GetValue(host) is Component,
                "Production lobby exposes a real display-mode view and selectable Double/Directory/Single entry");
            return (Component)field.GetValue(host);
        }
        internal static void SetModeForCheck(AppController host, int mode)
        {
            var view = View(host);
            var method = view.GetType().GetMethod("SetMode", Members);
            method.Invoke(view, new[] { Enum.ToObject(method.GetParameters()[0].ParameterType, mode) });
        }
        internal static RectTransform FindCard(Transform root, string name)
        {
            return root.GetComponentsInChildren<RectTransform>(true).Single(r => r.name == name + " game card" && ActiveWithin(root, r));
        }
        internal static bool ActiveWithin(Transform root, Transform child)
        {
            for (var current = child; current != null && current != root; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return true;
        }
        internal static Text RecordForCheck(AppController host, string name)
        {
            return FindCard(View(host).transform, name).GetComponentsInChildren<Text>(true).Single(t => t.name == "Game record");
        }
        internal static float CardTop(RectTransform content, RectTransform card)
        {
            return -content.InverseTransformPoint(card.TransformPoint(new Vector3(card.rect.xMin, card.rect.yMax))).y;
        }
        private static RectTransform Named(Transform root, string name)
        {
            return root.GetComponentsInChildren<RectTransform>().Single(r => r.name == name);
        }

        private static void CheckPreferences()
        {
            PlayerPrefs.DeleteKey(PreferenceKey);
            using (var f = new Fixture(1320))
            {
                Require(Convert.ToInt32(Property(f.View, "Mode")) == 0, "Missing preference defaults to Double");
                Require(!PlayerPrefs.HasKey(PreferenceKey), "Building the default lobby does not write display preferences");
            }
            foreach (int stored in new[] { 0, 1, 2, -1, 99 })
            {
                PlayerPrefs.SetInt(PreferenceKey, stored);
                using (var f = new Fixture(1320))
                    Require(Convert.ToInt32(Property(f.View, "Mode")) == (stored >= 0 && stored <= 2 ? stored : 0),
                        "Saved display mode is restored or safely defaulted: " + stored);
                Require(PlayerPrefs.GetInt(PreferenceKey) == stored, "Reading preferences never rewrites their stored value");
            }
            PlayerPrefs.SetString(PreferenceKey, "");
            using (var f = new Fixture(1320)) Require(Convert.ToInt32(Property(f.View, "Mode")) == 0, "An empty legacy preference defaults to Double");
            PlayerPrefs.DeleteKey(PreferenceKey);
        }

        private static void CheckModes(Fixture f)
        {
            Require(Named(f.View.transform, "Display options button").GetComponent<Button>() != null, "Visible display entry is a real UGUI button");
            var count = f.View.GetComponentsInChildren<Text>().Single(t => t.text == "全部游戏 · 18 款");
            Require(count.GetComponentInParent<Button>() == null, "All-games count is static text");
            Require(!f.View.GetComponentsInChildren<Text>(true).Any(t => t.text.Contains("自定义排序")), "Lobby has no custom-order action");
            var scroll = f.Scroll;
            var status = Property(f.View, "StatusLabel");
            for (int mode = 0; mode < 3; mode++)
            {
                SetModeForCheck(f.Host, mode); f.Flush();
                Require(ReferenceEquals(scroll, f.Scroll) && ReferenceEquals(status, Property(f.View, "StatusLabel")), "Changing mode keeps the scroll and status owners stable");
                var cards = f.View.GetComponentsInChildren<Button>().Where(b => b.name.EndsWith(" game card", StringComparison.Ordinal)).ToArray();
                Require(cards.Length == 18, "Display mode " + mode + " contains all eighteen actual entries");
                for (int i = 0; i < cards.Length; i++)
                {
                    var card = (RectTransform)cards[i].transform;
                    Require(card.name == Names[i] + " game card", "Fixed category order retains " + Names[i] + " in mode " + mode);
                    Require(card.GetComponentsInChildren<Button>().Length == 1, Names[i] + " has a single whole-card entry");
                    LobbyInputTestDriver.CheckBinding(cards[i], f.Host, Routes[i], Require);
                    var title = Named(card, "Game name").GetComponent<Text>();
                    Require(Mathf.Abs(title.fontSize * title.transform.localScale.y - (mode == 2 ? 24 : mode == 1 ? 13 : 14)) < .1f, "Approved title font for " + Names[i]);
                    Require(card.rect.height >= (mode == 2 ? 143 : mode == 1 ? 48 : 66) - .01f, "Approved minimum card height for " + Names[i]);
                    if (mode != 2)
                    {
                        var icon = Named(card, "Game icon");
                        var iconImage = icon.GetComponent<Image>();
                        Require(iconImage != null && iconImage.sprite != null, Names[i] + " uses an imported icon or explicit fallback");
                        Near(icon.rect.width, 20, Names[i] + " icon width"); Near(icon.rect.height, 20, Names[i] + " icon height");
                        var iconCenter = card.InverseTransformPoint(icon.TransformPoint(icon.rect.center));
                        var nameCenter = card.InverseTransformPoint(title.rectTransform.TransformPoint(title.rectTransform.rect.center));
                        Near(iconCenter.y, nameCenter.y - 1, Names[i] + " icon has the approved one-unit optical offset");
                        Near(icon.anchoredPosition.x, 10, Names[i] + " icon respects ten-unit card padding");
                        Near(title.rectTransform.rect.height * title.transform.localScale.y, 20, Names[i] + " uses a twenty-unit name row");
                        Near(title.rectTransform.anchoredPosition.x - icon.anchoredPosition.x - icon.rect.width, 8, Names[i] + " icon-name gap");
                        if (mode == 1) Require(!card.GetComponentsInChildren<Text>().Any(t => t.name == "Game record"), "Directory omits record rows");
                        else
                        {
                            var record = Named(card, "Game record").GetComponent<Text>();
                            Require(record.text.IndexOfAny(new[] { '\r', '\n' }) < 0, "Double mode uses one compact record line");
                            Near(record.rectTransform.anchoredPosition.x, 38, "Compact record aligns with the name");
                        }
                    }
                    else
                    {
                        Require(!card.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Game icon"), "Single mode removes small icons");
                        string caption = Named(card, "Game caption").GetComponent<Text>().text;
                        Require(caption.StartsWith(Ordinals[i].ToString("00")) && caption.Contains(Category(i)) && caption.Split('/').Length == 3,
                            Names[i] + " retains original ordinal, category and gameplay tag");
                        Require(card.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Game logo") == (Names[i] != "Idiom"),
                            Names[i] + " uses its real large logo, with the approved text-only Idiom exception");
                    }
                    foreach (var decoration in card.GetComponentsInChildren<Graphic>())
                        if (decoration.transform != card) Require(!decoration.raycastTarget, "Decorative children cannot steal taps: " + decoration.name);
                }
                var groupTitles = f.View.GetComponentsInChildren<Text>().Where(t => t.name == "Group title").ToArray();
                Require(groupTitles.Length == (mode == 2 ? 0 : 4), "Single has no redundant category headers; compact modes retain four groups");
                var subtitle = Named(f.View.transform, "Lobby subtitle");
                var toolbar = Named(f.View.transform, "Lobby toolbar");
                var subtitleBottom = f.Scroll.content.InverseTransformPoint(subtitle.TransformPoint(new Vector3(subtitle.rect.xMin, subtitle.rect.yMin)));
                Near(-toolbar.anchoredPosition.y + subtitleBottom.y, 9, "Toolbar keeps the approved nine-unit upper gap");
                var first = (RectTransform)cards[0].transform;
                var logicalFrame = Named(f.View.transform, "Lobby logical frame");
                var firstBounds = HorizontalBounds(first, logicalFrame);
                Near(firstBounds.x, 18, "All modes share the same left card margin in mode " + mode);
                foreach (var name in new[] { "Lobby header", "Lobby toolbar", "Toolbar rule", "Lobby cards", "Lobby footer", "Lobby status" })
                {
                    var bounds = HorizontalBounds(Named(f.View.transform, name), logicalFrame);
                    Near(bounds.x, firstBounds.x, name + " aligns with the left card edge in mode " + mode);
                    Near(bounds.y, logicalFrame.rect.width - 18, name + " retains the shared right margin in mode " + mode);
                }
                foreach (var name in new[] { "Lobby settings", "Display options button" })
                    Near(HorizontalBounds(Named(f.View.transform, name), logicalFrame).y,
                        logicalFrame.rect.width - 18, name + " keeps its right edge when switching modes");
                if (mode < 2)
                {
                    var second = (RectTransform)cards[1].transform;
                    Near(second.anchoredPosition.x - first.anchoredPosition.x - first.rect.width, 10, "Two-column gap stays ten units");
                    Near(second.anchoredPosition.y, first.anchoredPosition.y, "First pair shares the same row");
                    Near(HorizontalBounds(second, logicalFrame).y, logicalFrame.rect.width - 18,
                        "Second-column cards retain the shared right margin");
                }
                else Near(firstBounds.y, logicalFrame.rect.width - 18, "Single cards retain the shared right margin");
                f.Scroll.content.anchoredPosition = Vector2.zero; f.Flush();
                f.Capture("mode-" + mode + "-top");
                // Every route receives actual module-driven taps/jitter/drags after each rebuild.
                foreach (var card in cards)
                {
                    f.ShowCard((RectTransform)card.transform);
                    LobbyInputTestDriver.CheckGestures(f.Events, f.Camera, f.Scroll, card, f.Flush, Require);
                }
                f.ShowCard((RectTransform)cards.Last().transform); f.Capture("mode-" + mode + "-bottom");
            }
        }

        private static void CheckRecords(Fixture f)
        {
            SetModeForCheck(f.Host, 0);
            var entries = ((IEnumerable)Property(f.View, "Entries")).Cast<object>().ToArray();
            Require(entries.Length == 18, "View owns a complete production-entry snapshot");
            var sudoku = entries.Single(e => (string)Property(e, "ObjectName") == "Sudoku");
            string id = (string)Property(sudoku, "Id");
            Invoke(f.View, "SetRecord", id, "已完成 2000/2000 · 入门 500 · 简单 500 · 中等 500 · 困难 500", "已完成 2000/2000");
            Require(RecordForCheck(f.Host, "Sudoku").text == "已完成 2000/2000", "Double renders the compact live record");
            SetModeForCheck(f.Host, 2); f.Flush();
            var maximum = RecordForCheck(f.Host, "Sudoku");
            Require(maximum.text == "已完成 2000/2000 · 入门 500 · 简单 500 · 中等 500 · 困难 500", "Single retains every maximum Sudoku count without ellipsis");
            Require(maximum.preferredWidth <= maximum.rectTransform.rect.width + 1, "The complete maximum Sudoku record fits its rendered width");
            SetModeForCheck(f.Host, 1);
            Invoke(f.View, "SetRecord", id, "存档暂不可用 · 测试旧版本", "存档暂不可用");
            SetModeForCheck(f.Host, 2);
            Require(RecordForCheck(f.Host, "Sudoku").text == "存档暂不可用 · 测试旧版本", "An update while Directory is shown survives rebuilding Single");
            SetModeForCheck(f.Host, 0);
            Require(RecordForCheck(f.Host, "Sudoku").text == "存档暂不可用", "Warning meaning survives rebuilding Double");
            typeof(AppController).GetField("flappyBest", Members).SetValue(f.Host, 987654321L);
            Invoke(f.Host, "RefreshFlappyRecord");
            Require(RecordForCheck(f.Host, "FlappyBird").text.Contains("987654321"), "Actual host refresh reaches the visible compact record");
            SetModeForCheck(f.Host, 2);
            Require(RecordForCheck(f.Host, "FlappyBird").text.Contains("987654321"), "Actual host record survives mode switches without stale Text references");
            typeof(AppController).GetField("flappyBest", Members).SetValue(f.Host, long.MaxValue);
            typeof(AppController).GetField("flappyLoadWarning", Members).SetValue(f.Host, "测试存档未写入");
            Invoke(f.Host, "RefreshFlappyRecord");
            var flappy = entries.Single(e => (string)Property(e, "ObjectName") == "FlappyBird");
            foreach (int mode in new[] { 0, 1, 2, 0 })
            {
                SetModeForCheck(f.Host, mode); f.Flush();
                Require(((string)Property(flappy, "Record")).Contains(long.MaxValue.ToString()) &&
                    ((string)Property(flappy, "Record")).Contains("未保存"), "Full Flappy record model retains exact maximum score and warning in mode " + mode);
                Require(((string)Property(flappy, "CompactRecord")).StartsWith("未保存"), "Compact record prioritizes the unsaved warning over a long score");
                if (mode == 0) Require(RecordForCheck(f.Host, "FlappyBird").text.Contains("未保存"),
                    "The visible compact record keeps its warning even when the long score is ellipsized");
            }
        }

        private static void CheckOptions(Fixture f)
        {
            SetModeForCheck(f.Host, 0); f.Scroll.content.anchoredPosition = Vector2.zero; f.Flush();
            var trigger = Named(f.View.transform, "Display options button").GetComponent<Button>();
            var first = FindCard(f.View.transform, "Sudoku").GetComponent<Button>();
            var original = first.onClick; int clicks = 0;
            first.onClick = new Button.ButtonClickedEvent(); first.onClick.AddListener(() => clicks++);
            try
            {
                f.Tap(trigger);
                Require((bool)Property(f.View, "HasDisplayOptions"), "Real display-entry tap opens the options panel");
                Require(!first.IsInteractable(), "Options block background keyboard navigation and submission");
                first.OnSubmit(new BaseEventData(f.Events));
                Require(clicks == 0, "Submitting a covered card cannot enter its game");
                var overlay = Named(f.View.transform, "Display options overlay");
                var pointer = new PointerEventData(f.Events) { position = f.ScreenPoint(FindCard(f.View.transform, "Sudoku")) };
                var hits = new List<RaycastResult>(); f.Events.RaycastAll(pointer, hits);
                Require(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(overlay), "Overlay owns pointer rays above covered cards");
                Require(!overlay.GetComponentsInChildren<Text>().Any(t => t.text.Contains("排序")), "Display options contain no sorting capability");
                f.Capture("display-options");
                f.Tap(Named(overlay, "Close display options").GetComponent<Button>());
                Require(!(bool)Property(f.View, "HasDisplayOptions") && first.IsInteractable(), "Close restores the underlying lobby input");
                f.Tap(first); Require(clicks == 1, "First fresh card tap works immediately after closing options");
            }
            finally { if (first != null) first.onClick = original; }
            foreach (var name in new[] { "Directory", "Single", "Double" })
            {
                f.Scroll.content.anchoredPosition = Vector2.zero; f.Flush(); f.Tap(trigger);
                f.Tap(Named(f.View.transform, "Display mode " + name).GetComponent<Button>());
                int expected = name == "Double" ? 0 : name == "Directory" ? 1 : 2;
                Require(Convert.ToInt32(Property(f.View, "Mode")) == expected && !(bool)Property(f.View, "HasDisplayOptions"), "Actual option tap switches to " + name + " and closes the sheet");
                Require(PlayerPrefs.GetInt(PreferenceKey, -1) == expected, "User choice persists independently for " + name);
                var card = FindCard(f.View.transform, "Sudoku").GetComponent<Button>(); f.ShowCard((RectTransform)card.transform);
                LobbyInputTestDriver.CheckGestures(f.Events, f.Camera, f.Scroll, card, f.Flush, Require);
            }
            f.Scroll.content.anchoredPosition = Vector2.zero; f.Flush(); f.Tap(trigger);
            Invoke(f.Host, "HandleBack");
            Require(!(bool)Property(f.View, "HasDisplayOptions"), "Host Back closes display options before opening exit confirmation");
            f.Tap(trigger); f.View.gameObject.SetActive(false); f.View.gameObject.SetActive(true); f.Flush();
            Require(!(bool)Property(f.View, "HasDisplayOptions"), "Disabling the lobby clears options and restores modal input ownership");
            f.Tap(trigger); Invoke(f.View, "CloseDisplayOptions"); Invoke(f.View, "CloseDisplayOptions");
            Require(!(bool)Property(f.View, "HasDisplayOptions"), "Repeated display close is safe");
        }

        private static Vector2 HorizontalBounds(RectTransform item, RectTransform frame)
        {
            return new Vector2(
                frame.InverseTransformPoint(item.TransformPoint(new Vector3(item.rect.xMin, 0))).x,
                frame.InverseTransformPoint(item.TransformPoint(new Vector3(item.rect.xMax, 0))).x);
        }

        private static void Near(float actual, float expected, string context)
        { Require(Mathf.Abs(actual - expected) <= .1f, context + ": " + actual + " vs " + expected); }

        private sealed class Fixture : IDisposable
        {
            internal readonly int Height;
            internal readonly AppController Host;
            internal readonly Component View;
            internal readonly Camera Camera;
            internal readonly EventSystem Events;
            private readonly GameObject owner, inert;
            private readonly RenderTexture target;
            internal ScrollRect Scroll => (ScrollRect)Property(View, "Scroll");
            internal Fixture(int height)
            {
                Height = height;
                owner = new GameObject("Lobby design fixture");
                var root = new GameObject("Fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
                root.SetParent(owner.transform, false); root.sizeDelta = new Vector2(720, height);
                Camera = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>();
                Camera.transform.SetParent(owner.transform, false); Camera.enabled = false;
                Camera.transform.position = new Vector3(0, 0, -10); Camera.orthographic = true;
                Camera.orthographicSize = height * .5f; Camera.aspect = 720f / height;
                Camera.nearClipPlane = .1f; Camera.farClipPlane = 100;
                Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = UiFactory.Paper;
                target = new RenderTexture(720, height, 24); target.Create(); Camera.targetTexture = target;
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera;
                Events = new GameObject("Fixture events", typeof(EventSystem)).GetComponent<EventSystem>(); Events.transform.SetParent(owner.transform, false);
                inert = new GameObject("Inert lobby host"); inert.SetActive(false);
                Host = inert.AddComponent<AppController>();
                typeof(AppController).GetField("frame", Members).SetValue(Host, root);
                typeof(AppController).GetField("canvasRoot", Members).SetValue(Host, root);
                try
                {
                    Invoke(Host, "BuildLobby"); View = LobbyDesignChecks.View(Host);
                    // In-memory fresh profiles exercise the real host record refresh without initializing stores.
                    var game = new Game2048((IRandomSource)typeof(AppController).GetField("random", Members).GetValue(Host));
                    typeof(AppController).GetField("game", Members).SetValue(Host, game);
                    typeof(AppController).GetField("data", Members).SetValue(Host, new SaveData { Game = game.GetSnapshot() });
                    typeof(AppController).GetField("scoreText", Members).SetValue(Host, UiFactory.Label(inert.transform, "", 0, 0, 1, 1, 10, Color.white));
                    typeof(AppController).GetField("bestText", Members).SetValue(Host, UiFactory.Label(inert.transform, "", 0, 0, 1, 1, 10, Color.white));
                    typeof(AppController).GetField("jumpPage", Members).SetValue(Host, inert.AddComponent<JumpPage>());
                    typeof(AppController).GetField("sheepPage", Members).SetValue(Host, inert.AddComponent<SheepPage>());
                    var sudoku = inert.AddComponent<SudokuPage>();
                    typeof(SudokuPage).GetField("profile", Members).SetValue(sudoku, new SudokuProfile());
                    typeof(AppController).GetField("sudokuPage", Members).SetValue(Host, sudoku);
                    Invoke(Host, "RefreshScores"); Flush();
                }
                catch { Dispose(); throw; }
            }
            internal void Flush() { Canvas.ForceUpdateCanvases(); Camera.Render(); Canvas.ForceUpdateCanvases(); }
            internal Vector2 ScreenPoint(RectTransform rect) { return RectTransformUtility.WorldToScreenPoint(Camera, rect.TransformPoint(rect.rect.center)); }
            internal void Tap(Button button)
            {
                Flush(); using (var driver = new LobbyInputTestDriver(Events)) driver.Tap(ScreenPoint((RectTransform)button.transform)); Flush();
            }
            internal void ShowCard(RectTransform card)
            {
                Scroll.StopMovement(); Flush();
                float top = CardTop(Scroll.content, card);
                Scroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(top - (Scroll.viewport.rect.height - card.rect.height) * .5f,
                    0, Mathf.Max(0, Scroll.content.rect.height - Scroll.viewport.rect.height))); Flush();
            }
            internal void Capture(string name)
            {
                Flush(); var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(720, Height, TextureFormat.RGB24, false);
                try { image.ReadPixels(new Rect(0, 0, 720, Height), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(output, name + "-720x" + Height + ".png"), image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); RenderTexture.active = previous; }
            }
            public void Dispose()
            {
                Object.DestroyImmediate(owner); Object.DestroyImmediate(inert); target.Release(); Object.DestroyImmediate(target);
            }
        }

        private static void Require(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
