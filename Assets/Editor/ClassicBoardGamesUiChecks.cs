using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core.BoardGames;
using XanaduYonder.Runtime;
using XanaduYonder.Runtime.BoardGames;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    [InitializeOnLoad]
    public static class ClassicBoardGamesUiChecks
    {
        private const string Pending = "Xanadu.ClassicBoardGamesUiChecks.Pending";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static int assertions;

        static ClassicBoardGamesUiChecks() { EditorApplication.playModeStateChanged += StateChanged; }

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
            new GameObject("Classic board UI checks runner").AddComponent<Runner>().StartCoroutine(AfterFrame());
        }

        private static IEnumerator AfterFrame()
        {
            yield return null;
            var oldFont = UiFactory.Font;
            int exit = 0;
            try
            {
                UiFactory.Font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                Require(UiFactory.Font != null, "The production Chinese font loads.");
                foreach (Vector2Int viewport in new[] { new Vector2Int(360, 720), new Vector2Int(320, 568) })
                {
                    using (var fixture = new Fixture(viewport))
                    {
                        CheckArmy(fixture);
                        CheckStandardGame(fixture, BoardGameId.Xiangqi, "EnterXiangqi");
                        CheckStandardGame(fixture, BoardGameId.Chess, "EnterChess");
                        CheckCheckers(fixture);
                    }
                    Debug.Log("PASS " + viewport.x + "×" + viewport.y + " board navigation, setup, board layout, and Army handoff");
                }
                Debug.Log("CLASSIC_BOARD_GAMES_UI_CHECKS_PASS " + assertions + " assertions");
            }
            catch (Exception error)
            {
                exit = 1;
                Debug.LogError("CLASSIC_BOARD_GAMES_UI_CHECKS_FAIL after " + assertions + " assertions");
                Debug.LogException(error);
            }
            finally
            {
                UiFactory.Font = oldFont;
                EditorApplication.Exit(exit);
            }
        }

        private static void CheckNavigation(Fixture fixture, BoardGameId game, string route)
        {
            fixture.Enter(game, route);
            var icons = new[] { UiIconKind.Home, UiIconKind.Book, UiIconKind.Settings };
            var navigation = icons.Select(kind => fixture.Navigation(kind)).ToArray();
            for (int i = 0; i < navigation.Length; i++)
            {
                Rect bounds = fixture.Bounds(navigation[i].transform);
                Require(fixture.InViewport(bounds), game + " navigation is in the viewport: " + icons[i]);
                for (int j = 0; j < i; j++)
                    Require(!bounds.Overlaps(fixture.Bounds(navigation[j].transform)), game + " navigation controls do not overlap.");
            }
            fixture.Click(navigation[1]);
            Require(fixture.HasLabel("玩法说明"), game + " Book opens the rules dialog.");
            fixture.Click("返回");
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            Require(fixture.HasLabel("对局设置"), game + " Settings opens the game settings dialog.");
            fixture.Click("完成");
            fixture.Click(fixture.Navigation(UiIconKind.Home));
            Require(!fixture.Page.gameObject.activeInHierarchy, game + " Home leaves the active game page.");
            fixture.Enter(game, route);
            fixture.Click("开始新一局");
            Require(fixture.HasLabel("开局设置"), game + " menu reaches the opening setup.");
        }

        private static void CheckStandardGame(Fixture fixture, BoardGameId game, string route)
        {
            CheckNavigation(fixture, game, route);
            fixture.Click("本地双人");
            fixture.Click("开始对弈");
            var match = fixture.Page.Match;
            Require(match != null && match.Options.Local && match.Position.CurrentSide == 0, game + " starts local play with the first side.");
            var surface = fixture.Surface();
            var footer = fixture.Button("确认走棋");
            fixture.CheckBoardLayout(surface, footer);
            int whiteOrRedKing = game == BoardGameId.Chess ? 4 : 85;
            int blackKing = game == BoardGameId.Chess ? 60 : 4;
            Require(Center(surface, whiteOrRedKing).y < Center(surface, blackKing).y, game + " first side is at the bottom.");
            string first = game == BoardGameId.Chess ? "白方" : "红方";
            Require(fixture.HasLabel(first + " · 玩家 1") && fixture.HasLabel("黑方 · 玩家 2"), game + " both seat labels are present.");
            BoardMove move = match.Position.LegalMoves().First(candidate => candidate.From >= 0);
            fixture.Tap(move.From);
            fixture.Tap(move.To);
            Require(match.MoveCount == 0, game + " board taps preview rather than commit.");
            fixture.Click("确认走棋");
            Require(match.MoveCount == 1 && match.Position.CurrentSide == 1, game + " confirmation commits exactly one legal move.");
            fixture.Click(fixture.Navigation(UiIconKind.Home));
        }

        private static void CheckCheckers(Fixture fixture)
        {
            CheckNavigation(fixture, BoardGameId.Checkers, "EnterChineseCheckers");
            fixture.Click("5 台");
            fixture.Click("开始对弈");
            var match = fixture.Page.Match;
            Require(match != null && !match.Options.Local && match.Options.Seats == 6, "The five-computer option builds a six-seat match.");
            var checkers = match.Position as ChineseCheckersGame;
            Require(checkers != null && checkers.Seats == 6 && checkers.PointCount == 121, "The live board is the full six-seat star.");
            var surface = fixture.Surface();
            fixture.CheckBoardLayout(surface, fixture.Button("结束连跳"));
            Rect board = fixture.Bounds(surface.transform);
            var seats = new List<Rect>();
            for (int i = 1; i <= 6; i++)
            {
                RectTransform badge = fixture.NamedRect("Seat " + i);
                Rect bounds = fixture.Bounds(badge);
                Require(fixture.InViewport(bounds) && bounds.yMin >= board.yMax - .5f, "Seat badge sits above the board.");
                Require(seats.All(other => !other.Overlaps(bounds)), "Six seats have independent indicators.");
                Require(fixture.HasLabel(i == 1 ? "你" : "电脑" + (i - 1)), "The human and each computer are labeled separately.");
                seats.Add(bounds);
            }
            for (int point = 0; point < checkers.PointCount; point++)
                Require(surface.rectTransform.rect.Contains(Center(surface, point)), "Every star hole fits its board.");
            fixture.Click(fixture.Navigation(UiIconKind.Home));
        }

        private static void CheckArmy(Fixture fixture)
        {
            CheckNavigation(fixture, BoardGameId.Army, "EnterArmyChess");
            fixture.Click("本地双人");
            fixture.Click("开始布阵");
            CheckFormation(fixture, 0, "确认并交接");
            fixture.Click("确认并交接");
            fixture.CheckHandoff(1);
            fixture.Reveal(1);

            CheckFormation(fixture, 1, "确认布阵");
            fixture.Click("确认布阵");
            fixture.CheckHandoff(0);
            fixture.Reveal(0);

            var match = fixture.Page.Match;
            Require(match != null && match.Options.Local && match.MoveCount == 0, "Both private formations start one local match.");
            var surface = fixture.Surface();
            fixture.CheckBoardLayout(surface, fixture.Button("确认并交接"));
            int greenFront = ArmyGame.FormationPoint(1, 0);
            Require(Visible(surface, greenFront).Kind == -1, "The opponent rank remains hidden on the board.");
            BoardMove move = match.Position.LegalMoves().First(candidate => match.Position.PieceAt(candidate.To, 0).Kind == 0);
            fixture.Tap(move.From);
            fixture.Tap(move.To);
            Require(match.MoveCount == 0 && match.Position.CurrentSide == 0, "Army destination selection does not advance the turn.");
            Require(Visible(fixture.Surface(), greenFront).Kind == -1, "Army preview does not reveal an opponent rank.");
            fixture.Click("确认并交接");
            Require(match.MoveCount == 1 && match.Position.CurrentSide == 1, "Army confirmation advances exactly one turn.");
            fixture.CheckHandoff(1);
            fixture.Reveal(1);
            Require(Visible(fixture.Surface(), move.To).Kind == -1, "The next player cannot see the just-moved rank.");
            fixture.Click(fixture.Navigation(UiIconKind.Settings));
            fixture.Click("走棋二次确认 · 开启");
            fixture.Click("完成");
            BoardMove direct = match.Position.LegalMoves().First(candidate => match.Position.PieceAt(candidate.To, 1).Kind == 0);
            fixture.Tap(direct.From);
            Require(match.MoveCount == 1, "Selecting a rank in direct mode never commits a turn.");
            fixture.Tap(direct.To);
            Require(match.MoveCount == 2 && match.Position.CurrentSide == 0, "Direct target selection commits one legal Army move.");
            fixture.CheckHandoff(0);
            fixture.Reveal(0);
            fixture.Page.SetForeground(false);
            fixture.CheckHandoff(0);
            fixture.Page.SetForeground(true);
            fixture.CheckHandoff(0);
            fixture.Reveal(0);
            fixture.Click(fixture.Navigation(UiIconKind.Home));
        }

        private static void CheckFormation(Fixture fixture, int side, string action)
        {
            var surface = fixture.Surface();
            fixture.CheckBoardLayout(surface, fixture.Button(action));
            Require(Visible(surface, ArmyGame.FormationPoint(side, 0)).Kind > 0, "Formation renders the active player's own rank.");
            Require(Visible(surface, ArmyGame.FormationPoint(1 - side, 0)).Kind == 0, "Formation omits the other player's pieces.");
        }

        private static BoardPiece Visible(BoardSurface surface, int point) =>
            (BoardPiece)Method(typeof(BoardSurface), "VisiblePiece").Invoke(surface, new object[] { point });

        private static Vector2 Center(BoardSurface surface, int point) =>
            (Vector2)Method(typeof(BoardSurface), "Center").Invoke(surface, new object[] { point });

        private static MethodInfo Method(Type type, string name) => type.GetMethod(name, Members)
            ?? throw new InvalidOperationException(type.Name + " is missing method " + name);

        private static FieldInfo Field(Type type, string name) => type.GetField(name, Members)
            ?? throw new InvalidOperationException(type.Name + " is missing field " + name);

        private static void Require(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class Runner : MonoBehaviour { }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject owner;
            private readonly AppController shell;
            private readonly RectTransform canvasRoot;
            private readonly GameObject lobby;
            private readonly Camera camera;
            private readonly EventSystem events;
            private readonly RenderTexture renderTarget;
            private readonly string directory;
            internal BoardGamePage Page { get; }

            internal Fixture(Vector2Int pixels)
            {
                directory = Path.Combine(Path.GetTempPath(), "xanadu-board-ui-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                owner = new GameObject("Classic board UI fixture");
                var canvasObject = new GameObject("Fixture canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(owner.transform, false);
                canvasRoot = (RectTransform)canvasObject.transform;
                float height = UiFactory.DesignWidth * pixels.y / (float)pixels.x;
                canvasRoot.sizeDelta = new Vector2(UiFactory.DesignWidth, height);
                var canvas = canvasObject.GetComponent<Canvas>();
                camera = new GameObject("Fixture camera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(owner.transform, false);
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = height * .5f;
                camera.aspect = pixels.x / (float)pixels.y;
                renderTarget = new RenderTexture(pixels.x, pixels.y, 24);
                renderTarget.Create();
                camera.targetTexture = renderTarget;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;

                events = new GameObject("Fixture EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
                events.transform.SetParent(owner.transform, false);
                var inactiveHost = new GameObject("Inert AppController");
                inactiveHost.transform.SetParent(owner.transform, false);
                inactiveHost.SetActive(false);
                shell = inactiveHost.AddComponent<AppController>();
                SetHost("frame", canvasRoot);
                SetHost("canvasRoot", canvasRoot);
                canvasObject.AddComponent<SafeDesignFrame>();
                lobby = new GameObject("Fixture lobby");
                lobby.transform.SetParent(owner.transform, false);
                var otherGame = new GameObject("Fixture other game");
                otherGame.transform.SetParent(owner.transform, false);
                otherGame.SetActive(false);
                SetHost("lobbyPage", lobby);
                SetHost("gamePage", otherGame);
                SetHost("data", new SaveData { SoundEnabled = false, HapticsEnabled = false });

                var root = UiFactory.Rect(canvasRoot, "Board game test page", 0, 0, canvasRoot.rect.width, height);
                UiFactory.Stretch(root);
                Page = root.gameObject.AddComponent<BoardGamePage>();
                Page.Initialize(root, directory, () => { Page.Leave(); lobby.SetActive(true); },
                    after => after?.Invoke(), () => true, () => false, () => false, null);
                SetHost("boardPage", Page);
            }

            private void SetHost(string name, object value) => Field(typeof(AppController), name).SetValue(shell, value);

            internal void Enter(BoardGameId game, string route)
            {
                Method(typeof(AppController), route).Invoke(shell, null);
                Flush();
                Require(Page.gameObject.activeInHierarchy && (BoardGameId)Field(typeof(BoardGamePage), "game").GetValue(Page) == game,
                    route + " presents the matching real board-game page.");
                Require(!lobby.activeSelf, route + " hides the lobby.");
            }

            internal bool HasLabel(string text) => Page.GetComponentsInChildren<Text>().Any(label => label.text == text);

            internal Button Navigation(UiIconKind kind)
            {
                var icon = Page.GetComponentsInChildren<UiIconGraphic>().SingleOrDefault(item => item.Kind == kind);
                Require(icon != null, "Shared navigation icon is present: " + kind);
                return icon.GetComponentInParent<Button>();
            }

            internal Button Button(string caption)
            {
                var button = Page.GetComponentsInChildren<Button>()
                    .SingleOrDefault(item => item.GetComponentsInChildren<Text>().Any(label => label.text == caption));
                Require(button != null, "Visible button: " + caption);
                return button;
            }

            internal BoardSurface Surface()
            {
                var surface = Page.GetComponentsInChildren<BoardSurface>().SingleOrDefault();
                Require(surface != null, "The active view contains one production board surface.");
                return surface;
            }

            internal RectTransform NamedRect(string name)
            {
                var rect = Page.GetComponentsInChildren<RectTransform>().SingleOrDefault(item => item.name == name);
                Require(rect != null, "Visible object: " + name);
                return rect;
            }

            internal void CheckBoardLayout(BoardSurface surface, Button footer)
            {
                Rect board = Bounds(surface.transform);
                Rect controls = Bounds(footer.transform);
                Require(board.width > 0 && board.height > 0 && InViewport(board), "The complete board fits the viewport.");
                Require(InViewport(controls) && !controls.Overlaps(board), "Footer controls fit without covering the board.");
            }

            internal void CheckHandoff(int side)
            {
                Require(!Page.GetComponentsInChildren<BoardSurface>().Any(), "No visible board exists during opaque Army handoff.");
                var cover = NamedRect("Army handoff opaque screen");
                var image = cover.GetComponent<Image>();
                Require(image != null && image.raycastTarget && image.color.a == 1, "Handoff is opaque and intercepts pointer input.");
                Rect bounds = Bounds(cover);
                Rect page = Bounds(Page.transform);
                Require(Mathf.Abs(bounds.xMin - page.xMin) < .5f && Mathf.Abs(bounds.xMax - page.xMax) < .5f &&
                    Mathf.Abs(bounds.yMin - page.yMin) < .5f && Mathf.Abs(bounds.yMax - page.yMax) < .5f,
                    "The opaque cover spans the whole page.");
                Require(HasLabel("交给玩家 " + (side + 1)), "The handoff identifies the correct next player.");
            }

            internal void Reveal(int side) => Click("我是玩家 " + (side + 1) + "，揭开棋盘");

            internal bool InViewport(Rect rect) =>
                rect.xMin >= -.5f && rect.yMin >= -.5f && rect.xMax <= renderTarget.width + .5f && rect.yMax <= renderTarget.height + .5f;

            internal Rect Bounds(Transform transform)
            {
                Flush();
                var corners = new Vector3[4];
                ((RectTransform)transform).GetWorldCorners(corners);
                var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }

            internal void Click(string caption) => Click(Button(caption));

            internal void Click(Button button)
            {
                Require(button.IsInteractable(), "The requested button is interactable.");
                var rect = (RectTransform)button.transform;
                Dispatch(rect, rect.rect.center);
            }

            internal void Tap(int boardPoint)
            {
                var surface = Surface();
                Dispatch(surface.rectTransform, Center(surface, boardPoint));
            }

            private void Dispatch(RectTransform expected, Vector2 local)
            {
                Flush();
                var data = new PointerEventData(events)
                {
                    position = RectTransformUtility.WorldToScreenPoint(camera, expected.TransformPoint(local)),
                    pointerId = -1,
                    button = PointerEventData.InputButton.Left,
                    eligibleForClick = true
                };
                var hits = new List<RaycastResult>();
                events.RaycastAll(data, hits);
                Require(hits.Count > 0 && (hits[0].gameObject.transform == expected || hits[0].gameObject.transform.IsChildOf(expected)),
                    "Top raycast reaches " + expected.name + (hits.Count == 0 ? "." : " (hit " + hits[0].gameObject.name + ")."));
                data.pointerPressRaycast = data.pointerCurrentRaycast = hits[0];
                var target = hits[0].gameObject;
                var pressed = ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler);
                var clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
                data.pointerPress = pressed != null ? pressed : clicked;
                if (data.pointerPress != null) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
                if (clicked != null && data.pointerPress == clicked) ExecuteEvents.Execute(clicked, data, ExecuteEvents.pointerClickHandler);
                Flush();
            }

            private void Flush()
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Page.Leave();
                camera.targetTexture = null;
                Object.DestroyImmediate(owner);
                renderTarget.Release();
                Object.DestroyImmediate(renderTarget);
                Directory.Delete(directory, true);
            }
        }
    }
}
