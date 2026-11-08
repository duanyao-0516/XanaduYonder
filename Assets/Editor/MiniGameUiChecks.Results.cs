using System;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core;
using XanaduYonder.Core.Watermelon;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.Watermelon;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    public static partial class MiniGameUiChecks
    {
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
                    page.CheckEndState();
                    CaptureHostTerminal(fixture, shell, page, root, "terminal-watermelon-theme-" + theme + (won ? "-goal" : "-lost"));
                    Require(!Get<bool>(page, "manuallyPaused"), "Terminal inspection did not substitute a paused-board cover");
                    Require(game.Score == data.BestScore, "Watermelon inspection does not replay scoring");
                }
                finally { Invoke(shell, "CloseModal"); Object.DestroyImmediate(root.gameObject); Object.DestroyImmediate(inert); }
            }
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
