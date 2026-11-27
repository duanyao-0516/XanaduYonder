using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Core.Jump;
using XanaduYonder.Core.Watermelon;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Jump;
using XanaduYonder.Runtime.Watermelon;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    public static partial class MiniGameUiChecks
    {
        private static void CheckLobbySettings(Fixture fixture, AppController host, RectTransform content)
        {
            var opener = content.Find("Lobby header/Lobby settings").GetComponent<Button>();
            fixture.Click((RectTransform)opener.transform);
            var settings = Get<GameObject>(host, "modal");
            Require(settings != null, "The lobby settings button opens its bottom sheet");
            Require(settings.GetComponent<Image>().color == new Color(0f, 0f, 0f, .4f) && settings.GetComponent<Image>().raycastTarget,
                "The settings sheet dims the lobby with a 40% black pointer-blocking scrim");
            Require(!opener.IsInteractable(), "The covered lobby button rejects keyboard and gamepad submission");
            opener.OnSubmit(new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current));
            Require(Get<GameObject>(host, "modal") == settings, "Submitting the background cannot replace the settings sheet");
            var sound = FindCaption(settings.transform, "声音").GetComponent<DialogSwitch>();
            var haptics = FindCaption(settings.transform, "触感").GetComponent<DialogSwitch>();
            Require(sound != null && haptics != null && sound != haptics, "Both feedback preferences have independent switches");
            var initial = haptics.Value;
            fixture.Click((RectTransform)haptics.transform);
            Require(haptics.Value != initial && Get<SaveData>(host, "data").HapticsEnabled == haptics.Value,
                "Tapping the preference row immediately changes the real setting");
            fixture.Capture("dialog-lobby-settings");
            fixture.Click((RectTransform)FindCaption(settings.transform, "声音与触感说明").transform);
            var detail = Get<GameObject>(host, "modal");
            Require(detail != settings && FindCaption(detail.transform, "返回设置") != null,
                "Feedback help has a dedicated detail view and return action");
            Require(detail.transform.parent.GetComponentsInChildren<DialogFocusScope>().Length == 1 &&
                detail.GetComponent<Image>().color == DialogUi.Scrim, "The detail replaces the previous scrim without stacking");
            fixture.Capture("dialog-feedback-details");
            Invoke(host, "HandleBack");
            var reopened = Get<GameObject>(host, "modal");
            Require(FindCaption(reopened.transform, "触感").GetComponent<DialogSwitch>().Value != initial,
                "System Back restores settings with the current value");
            fixture.Click((RectTransform)FindCaption(reopened.transform, "完成").transform);
            Require(Get<GameObject>(host, "modal") == null && opener.IsInteractable(), "Done restores the underlying lobby");
            Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == opener.gameObject,
                "Closing the last sheet restores focus to the lobby settings button");
        }

        // Edit mode allows a real AppController move without Awake opening player saves.
        public static void CheckHostSaveIsolation()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run the save isolation check in edit mode.");
            var directory = Path.Combine(Path.GetTempPath(), "xanadu-host-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var root = new GameObject("Save isolation fixture", typeof(RectTransform));
            var oldFont = UiFactory.Font;
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                var shell = root.AddComponent<AppController>();
                var game = new Game2048(new GameSnapshot { cells = new[] { 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } },
                    Get<IRandomSource>(shell, "random"));
                var store = new LocalSaveStore(directory);
                var data = new SaveData { Game = game.GetSnapshot(), SoundEnabled = false, HapticsEnabled = false };
                Require(store.TrySave(data, out var error), error);
                Set(shell, "data", data);
                Set(shell, "game", game);
                Set(shell, "store", store);
                Set(shell, "frame", (RectTransform)root.transform);
                Set(shell, "canvasRoot", (RectTransform)root.transform);
                Set(shell, "feedback", root.AddComponent<GameFeedback>());
                Set(shell, "activePage", Enum.Parse(typeof(AppController).GetNestedType("Page", System.Reflection.BindingFlags.NonPublic), "Game2048"));
                Invoke(shell, "BuildGame");

                var jump = root.AddComponent<JumpPage>();
                Set(jump, "feedback", Get<GameFeedback>(shell, "feedback"));
                var jumpStore = new JumpSaveStore(directory);
                var jumpGame = new JumpGame(923u);
                var jumpData = new JumpSaveData { Game = jumpGame.GetSnapshot() };
                Require(jumpStore.TrySave(jumpData, out error), error);
                var jumpBefore = File.ReadAllText(jumpStore.SavePath);
                Set(jump, "game", jumpGame);
                Set(jump, "store", jumpStore);
                Set(jump, "data", jumpData);
                Set(shell, "jumpPage", jump);

                Invoke(shell, "Move", Direction.Left);
                Require(store.Load(out _).Game.score == 4, "A valid 2048 swipe persists the merged score");
                Require(File.ReadAllText(jumpStore.SavePath) == jumpBefore && !File.Exists(jumpStore.SavePath + ".bak"),
                    "A 2048 swipe must not rewrite the Jump save or rotate its backup");

                Directory.CreateDirectory(store.SavePath + ".tmp");
                Require(!(bool)Invoke(shell, "SetFeedbackSetting", false, true), "The host reports a failed preference save");
                Require(!store.Load(out _).HapticsEnabled, "Failed writes preserve the previous preference");
                Directory.Delete(store.SavePath + ".tmp");
                Require((bool)Invoke(shell, "SetFeedbackSetting", false, true), "Retrying the preference save succeeds");
                Require(store.Load(out _).HapticsEnabled && File.Exists(store.SavePath + ".bak"), "The retry persists haptics and a recoverable host backup");
                Require(!File.Exists(jumpStore.SavePath + ".bak"), "Preference writes and retries leave other games untouched");
                ClearEditorTiles(shell);
                Invoke(shell, "Restart");
                Require(store.Load(out _).Game.score == 0 && store.Load(out _).BestScore == 4, "Restarting resets the current score and keeps the record");
                Require(!File.Exists(jumpStore.SavePath + ".bak"), "Restarting 2048 keeps the Jump save untouched");
                ClearEditorTiles(shell);
                var gamePage = Get<GameObject>(shell, "gamePage");
                Array.Find(gamePage.GetComponentsInChildren<Button>(), button => button.name == "Settings button").onClick.Invoke();
                var settings = Get<GameObject>(shell, "modal");
                var toggle = Array.Find(settings.GetComponentsInChildren<Button>(), button => button.name.StartsWith("触感", StringComparison.Ordinal));
                toggle.onClick.Invoke();
                Require(!store.Load(out _).HapticsEnabled && !toggle.GetComponent<DialogSwitch>().Value &&
                    toggle.GetComponentInChildren<Text>().text == "触感",
                    "The switch persists the new value and keeps its stable caption");
                Require(Get<GameObject>(shell, "modal") == settings && !File.Exists(jumpStore.SavePath + ".bak"),
                    "The settings sheet remains open and no unrelated save is written");
                Invoke(shell, "OnApplicationQuit");
                Require(File.Exists(jumpStore.SavePath + ".bak"), "The full exit checkpoint still saves the initialized Jump page");
                Debug.Log("HOST_SAVE_ISOLATION_PASS");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UiFactory.Font = oldFont;
                Directory.Delete(directory, true);
            }
        }

        // The engine defers tile destruction in a player. An edit-mode fixture must
        // release the preceding render before invoking another action that redraws.
        private static void ClearEditorTiles(AppController shell)
        {
            var tiles = Get<List<RectTransform>>(Get<BoardView>(shell, "board"), "tiles");
            foreach (var tile in tiles) Object.DestroyImmediate(tile.gameObject);
            tiles.Clear();
        }

        private static void Check2048Terminals(Fixture fixture)
        {
            foreach (string outcome in new[] { "won", "lost", "won-and-blocked" })
            {
                var inert = new GameObject("2048 terminal shell"); inert.SetActive(false);
                var shell = inert.AddComponent<AppController>();
                var pageRoot = fixture.Page("2048 host");
                Set(shell, "frame", pageRoot); Set(shell, "canvasRoot", pageRoot);
                var random = Get<IRandomSource>(shell, "random");
                int[] cells = outcome == "won" ? new[] { 2048, 16, 4, 2, 512, 8, 2, 0, 128, 4, 0, 0, 64, 2, 0, 0 }
                    : new[] { 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2, 4, 8, 16, 32, 64 };
                if (outcome == "won-and-blocked") cells[0] = 2048;
                var game = new Game2048(new GameSnapshot { cells = cells, score = 20148 }, random);
                var data = new SaveData { Game = game.GetSnapshot(), BestScore = 20148, BestTile = outcome == "lost" ? 1024 : 2048 };
                Set(shell, "game", game); Set(shell, "data", data);
                Set(shell, "activePage", Enum.Parse(typeof(AppController).GetNestedType("Page", System.Reflection.BindingFlags.NonPublic), "Game2048"));
                Set(shell, "focused", true);
                Invoke(shell, "BuildGame");
                Get<BoardView>(shell, "board").Render(game.GetSnapshot());
                Get<Text>(shell, "scoreText").text = game.Score.ToString();
                Get<Text>(shell, "bestText").text = data.BestScore.ToString();
                var footer = UiFactory.Label(UiFactory.Band(pageRoot, "Status", 1278, 34, 1), "进度已保存。", 56, 0, 608, 34, 18, UiFactory.Muted, TextAnchor.MiddleCenter);
                Set(shell, "status", footer);
                try
                {
                    if (outcome == "won")
                        CheckRulesNavigation(fixture, shell, pageRoot.Find("2048 page/Game navigation"), "让相同的数字相遇");
                    Invoke(shell, "CheckEndState");
                    CaptureHostTerminal(fixture, shell, shell, pageRoot, "terminal-2048-" + outcome);
                    if (outcome == "won-and-blocked")
                    {
                        fixture.Click((RectTransform)FindCaption(pageRoot, "继续挑战").transform);
                        Require(game.HasAcknowledgedWin && game.IsGameOver, "2048 acknowledged win reveals the same blocked board");
                        CaptureHostTerminal(fixture, shell, shell, pageRoot, "terminal-2048-after-win-blocked");
                    }
                }
                finally { Invoke(shell, "CloseModal"); Object.DestroyImmediate(pageRoot.gameObject); Object.DestroyImmediate(inert); }
            }
        }

        private static void CheckWatermelonTerminals(Fixture fixture)
        {
            for (int theme = 0; theme < 3; theme++)
            foreach (bool won in new[] { true, false })
            {
                var inert = new GameObject("Watermelon terminal shell"); inert.SetActive(false);
                var shell = inert.AddComponent<AppController>();
                var root = fixture.Page("Watermelon terminal page");
                Set(shell, "frame", root); Set(shell, "canvasRoot", root);
                var page = root.gameObject.AddComponent<WatermelonPage>();
                var snapshot = new WatermelonGame(123).GetSnapshot();
                snapshot.Score = 2048; snapshot.NextId = 5;
                snapshot.Balls = new[] {
                    new WatermelonBallState { Id = 1, Level = won ? 9 : 2, X = 2, Y = won ? 1 : 6.1f, Age = 3, Contacted = true },
                    new WatermelonBallState { Id = 2, Level = won ? 9 : 4, X = 4, Y = 1, Age = 3, Contacted = true },
                    new WatermelonBallState { Id = 3, Level = 3, X = 1, Y = .5f, Age = 3, Contacted = true },
                    new WatermelonBallState { Id = 4, Level = 5, X = 5, Y = .7f, Age = 3, Contacted = true }
                };
                var game = new WatermelonGame(snapshot);
                if (won) Require(game.TryMerge(1, 2, out _), "Watermelon fixture reaches the goal through the real merge rule");
                else for (int tick = 0; tick < 8; tick++) game.Advance(.25f);
                Require(won ? game.HasWatermelon : game.GameOver, "Watermelon fixture reaches its actual terminal condition");
                var data = new WatermelonSaveData { Theme = theme, Game = game.GetSnapshot(), BestScore = game.Score };
                Set(page, "game", game); Set(page, "data", data); Set(page, "artwork", new WatermelonArtwork());
                Set(page, "visible", true); Set(page, "lobby", (Action)(() => {}));
                Set(page, "acceptsInput", (Func<bool>)(() => Get<GameObject>(shell, "modal") == null));
                Set(page, "soundEnabled", (Func<bool>)(() => false)); Set(page, "hapticsEnabled", (Func<bool>)(() => false));
                Set(page, "feedback", root.gameObject.AddComponent<GameFeedback>());
                Set(page, "dialog", (Action<string, string, string, Action, string, Action>)((a,b,c,d,e,f) => Invoke(shell, "ShowDialog", a,b,c,d,e,f)));
                Set(page, "openModal", (Func<string,float,Action,RectTransform>)((a,b,c) => (RectTransform)Invoke(shell, "CreateModal", a,b,c)));
                Set(page, "closeModal", (Action)(() => Invoke(shell, "CloseModal")));
                Set(page, "status", (Action<string>)(_ => {}));
                Invoke(page, "Build", root, (Action)(() => {}));
                Invoke(page, "Render", game.GetSnapshot());
                try
                {
                    if (theme == 0 && won)
                        CheckRulesNavigation(fixture, shell, root.Find("Watermelon navigation"), "合成图鉴 · " + WatermelonArtwork.ThemeNames[theme]);
                    page.CheckEndState();
                    CaptureHostTerminal(fixture, shell, page, root, "terminal-watermelon-theme-" + theme + (won ? "-goal" : "-lost"));
                    Require(!Get<bool>(page, "manuallyPaused"), "Terminal inspection did not substitute a paused-board cover");
                    Require(game.Score == data.BestScore, "Watermelon inspection does not replay scoring");
                }
                finally { Invoke(shell, "CloseModal"); Object.DestroyImmediate(root.gameObject); Object.DestroyImmediate(inert); }
            }
        }

        private static void CheckRulesNavigation(Fixture fixture, AppController shell, Transform navigation, string rulesTitle)
        {
            var home = (RectTransform)Icon(navigation, UiIconKind.Home).transform;
            var rules = (RectTransform)Icon(navigation, UiIconKind.Book).transform;
            var settings = (RectTransform)Icon(navigation, UiIconKind.Settings).transform;
            Require(home.anchoredPosition == new Vector2(56, -34) &&
                rules.anchoredPosition == new Vector2(536, -34) && settings.anchoredPosition == new Vector2(606, -34),
                navigation.name + " aligns Home, rules and settings across game pages");
            Require(home.sizeDelta == new Vector2(58, 58) && rules.sizeDelta == home.sizeDelta && settings.sizeDelta == home.sizeDelta,
                navigation.name + " preserves the complete navigation touch targets");
            fixture.Click(rules);
            var modal = Get<GameObject>(shell, "modal");
            Require(modal != null && modal.transform.Find("Dialog safe area/Dialog/" + rulesTitle) != null,
                navigation.name + " opens its own game rules");
            Invoke(shell, "CloseModal");
        }

        private static void CaptureHostTerminal(Fixture fixture, AppController shell, MonoBehaviour page, RectTransform root, string name)
        {
            var result = page.GetComponent<ResultPresentation>();
            Require(result != null && Get<GameObject>(shell, "modal") == null, name + " delays only the result overlay");
            fixture.Capture(name + "-waiting");
            result.Advance(0); result.Advance(.81f);
            Require(Get<GameObject>(shell, "modal") != null, name + " presents a real host result dialog");
            fixture.Capture(name + "-result");
            fixture.Click((RectTransform)FindCaption(root, "查看最终局面").transform);
            fixture.Capture(name + "-inspect");
            fixture.CheckReviewLayout(root);
            fixture.Click((RectTransform)FindCaption(root, "查看结算").transform);
            Require(Get<GameObject>(shell, "modal") != null, name + " reopens the same real result");
            fixture.Capture(name + "-reopen");
        }
    }
}
