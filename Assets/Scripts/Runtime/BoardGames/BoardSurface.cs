using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class BoardSurface : MaskableGraphic, IPointerDownHandler, IPointerUpHandler
    {
        private static readonly Color Wood = UiFactory.Hex("EADFC6");
        private static readonly Color LineColor = UiFactory.Hex("967A52");
        private static readonly Color Light = UiFactory.Hex("F2E6CC");
        private static readonly Color Dark = UiFactory.Hex("718C73");
        private static readonly Color Red = UiFactory.Hex("A13A30");
        private static readonly Color[] Seats = { UiFactory.Hex("286B47"), UiFactory.Hex("9B4B37"), UiFactory.Hex("3F6898"),
            UiFactory.Hex("856222"), UiFactory.Hex("705491"), UiFactory.Hex("256D70") };
        private static readonly string[] Symbols = { "•", "—", "×", "▲", "◇", "Ⅱ" };
        private static readonly string[] Army = { "", "工兵", "排长", "连长", "营长", "团长", "旅长", "师长", "军长", "司令", "炸弹", "地雷", "军旗" };
        private static readonly string[] XiangRed = { "", "帥", "仕", "相", "馬", "車", "炮", "兵" };
        private static readonly string[] XiangBlack = { "", "將", "士", "象", "馬", "車", "砲", "卒" };
        private static readonly Vector2[][] ChessShapes = {
            null,
            Points(-.44f,-.48f,.44f,-.48f,.36f,-.30f,.19f,-.20f,.12f,.03f,-.12f,.03f,-.19f,-.20f,-.36f,-.30f),
            Points(-.43f,-.48f,.43f,-.48f,.41f,-.28f,.34f,.21f,.14f,.43f,-.10f,.46f,-.23f,.60f,-.27f,.35f,-.46f,.10f,-.35f,-.03f,-.10f,.10f,.06f,.08f,-.13f,-.15f,-.35f,-.30f),
            Points(-.44f,-.48f,.44f,-.48f,.36f,-.32f,.14f,-.16f,.28f,.10f,.17f,.37f,0,.57f,-.17f,.37f,-.28f,.10f,-.14f,-.16f,-.36f,-.32f),
            Points(-.44f,-.48f,.44f,-.48f,.39f,-.30f,.28f,-.26f,.24f,.24f,.39f,.25f,.39f,.51f,.24f,.51f,.24f,.39f,.10f,.39f,.10f,.51f,-.10f,.51f,-.10f,.39f,-.24f,.39f,-.24f,.51f,-.39f,.51f,-.39f,.25f,-.24f,.24f,-.28f,-.26f,-.39f,-.30f),
            Points(-.44f,-.48f,.44f,-.48f,.36f,-.29f,.23f,-.19f,.44f,.40f,.23f,.12f,.17f,.46f,.01f,.21f,-.16f,.46f,-.24f,.10f,-.44f,.40f,-.23f,-.19f,-.36f,-.29f),
            Points(-.44f,-.48f,.44f,-.48f,.37f,-.32f,.22f,-.12f,.35f,.10f,.24f,.29f,0,.21f,-.24f,.29f,-.35f,.10f,-.22f,-.12f,-.37f,-.32f),
        };
        private readonly HashSet<int> targets = new HashSet<int>();
        private BoardGameId game;
        private IBoardPosition position;
        private Action<int> chosen;
        private int ownSide, viewSide, selected, preview;
        private bool formation;
        private int[] route;
        private BoardMove last;
        private Vector2 press;
        private Camera pressCamera;
        private int pointer = int.MinValue;
        private bool draining;
        private bool enabledInput;

        internal static float Ratio(BoardGameId game)
        {
            switch (game)
            {
                case BoardGameId.Army: return 1.22f;
                case BoardGameId.Xiangqi: return 10f / 9f;
                case BoardGameId.Checkers: return (16f * .8660254f + 1) / 13f;
                default: return 1;
            }
        }

        internal static Color SeatColor(int seat) => Seats[seat];
        internal static string SeatSymbol(int seat) => Symbols[seat];
        internal static string ArmyName(int rank) => rank >= 0 && rank < Army.Length ? Army[rank] : "暗子";

        internal void Bind(BoardGameId game, IBoardPosition position, int ownSide, int viewSide, int selected, int preview,
            IReadOnlyList<int> route, IEnumerable<int> targets, BoardMove last, bool enabledInput, Action<int> chosen, bool formation = false)
        {
            this.game = game;
            this.position = position;
            this.ownSide = ownSide;
            this.viewSide = viewSide;
            this.selected = selected;
            this.preview = preview;
            this.formation = formation;
            this.route = route == null ? Array.Empty<int>() : new List<int>(route).ToArray();
            this.targets.Clear();
            this.targets.UnionWith(targets);
            this.last = last;
            this.enabledInput = enabledInput;
            this.chosen = chosen;
            raycastTarget = enabledInput;
            AddLabels();
            SetVerticesDirty();
        }

        private float Pitch => rectTransform.rect.width / (game == BoardGameId.Checkers ? 13 :
            game == BoardGameId.Xiangqi ? 9 : game == BoardGameId.Army ? 5 : 8);

        private Vector2 Center(int index)
        {
            var area = rectTransform.rect;
            if (game == BoardGameId.Checkers)
                return new Vector2(area.center.x + ChineseCheckersGame.DoubledXAt(index) * Pitch * .5f,
                    area.center.y + (8 - ChineseCheckersGame.RowAt(index)) * Pitch * .8660254f);
            int columns = game == BoardGameId.Army ? 5 : game == BoardGameId.Xiangqi ? 9 : 8;
            int rows = game == BoardGameId.Army ? 12 : game == BoardGameId.Xiangqi ? 10 : 8;
            if (game == BoardGameId.Chess) index = (7 - index / 8) * 8 + index % 8;
            if (viewSide == 1) index = columns * rows - 1 - index;
            int row = index / columns;
            float y = game == BoardGameId.Army ? (row + .66f + (row >= 6 ? .70f : 0)) / 13.02f : (row + .5f) / rows;
            return new Vector2(area.xMin + (index % columns + .5f) * Pitch, area.yMax - y * area.height);
        }

        private void AddLabels()
        {
            var area = rectTransform.rect;
            if (game == BoardGameId.Xiangqi)
            {
                UiFactory.Label(transform, "楚 河", Pitch, area.height / 2 - Pitch * .4f, Pitch * 3, Pitch * .8f,
                    Mathf.RoundToInt(Pitch * .39f), LineColor, TextAnchor.MiddleCenter);
                UiFactory.Label(transform, "漢 界", Pitch * 5, area.height / 2 - Pitch * .4f, Pitch * 3, Pitch * .8f,
                    Mathf.RoundToInt(Pitch * .39f), LineColor, TextAnchor.MiddleCenter);
            }
            if (game == BoardGameId.Chess)
            {
                for (int i = 0; i < 8; i++)
                {
                    string file = ((char)('a' + (viewSide == 0 ? i : 7 - i))).ToString();
                    string rank = (viewSide == 0 ? 8 - i : i + 1).ToString();
                    UiFactory.Label(transform, file, (i + 1) * Pitch - 18, area.height - 20, 16, 17, 14,
                        i % 2 == 0 ? Light : UiFactory.Ink, TextAnchor.MiddleCenter);
                    UiFactory.Label(transform, rank, 3, i * Pitch, 16, 18, 14,
                        i % 2 == 0 ? UiFactory.Ink : Light, TextAnchor.MiddleCenter);
                }
            }
            for (int index = 0; index < position.PointCount && game != BoardGameId.Chess; index++)
            {
                var piece = VisiblePiece(index);
                if (piece.Kind == 0 || (piece.Kind < 0 && game != BoardGameId.Army)) continue;
                Vector2 at = Center(index);
                float w = Pitch * (game == BoardGameId.Army ? .82f : .75f), h = Pitch * (game == BoardGameId.Army ? .38f : .76f);
                string name = game == BoardGameId.Army ? (piece.Kind < 0 ? "" : ArmyName(piece.Kind)) :
                    game == BoardGameId.Xiangqi ? (piece.Side == 0 ? XiangRed : XiangBlack)[piece.Kind] : Symbols[piece.Side];
                if (name.Length == 0) continue;
                int fontSize = Mathf.RoundToInt(Pitch * (game == BoardGameId.Army ? .185f : game == BoardGameId.Xiangqi ? .44f : .45f));
                var tint = game == BoardGameId.Checkers ? Color.white : piece.Side == 0 ? Red : UiFactory.Ink;
                UiFactory.Label(transform, name, at.x - area.xMin - w * .5f, area.yMax - at.y - h * .5f, w, h, fontSize,
                    tint, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        private BoardPiece VisiblePiece(int point)
        {
            if (formation)
            {
                var piece = position.PieceAt(point, ownSide);
                return piece.Side == ownSide ? piece : default;
            }
            if (game != BoardGameId.Checkers || route.Length == 0) return position.PieceAt(point, ownSide);
            if (point == route[route.Length - 1]) return position.PieceAt(selected, ownSide);
            return point == selected ? default : position.PieceAt(point, ownSide);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (position == null || Pitch <= 0) return;
            var bounds = rectTransform.rect;
            Box(mesh, bounds.center, bounds.width, bounds.height, game == BoardGameId.Army ? UiFactory.Hex("DEE7D9") : Wood);
            switch (game)
            {
                case BoardGameId.Army: DrawArmy(mesh); break;
                case BoardGameId.Xiangqi: DrawXiangqi(mesh); break;
                case BoardGameId.Checkers: DrawStar(mesh); break;
                default: DrawChess(mesh); break;
            }
            if (game == BoardGameId.Checkers)
            {
                if (last.From >= 0) DrawTrail(mesh, last.From, last.PathLength == 0 ? new[] { last.To } : last.Path, UiFactory.Hex("82956C"), 3);
                if (route.Length > 0) DrawTrail(mesh, selected, route, UiFactory.Accent, 5);
            }
            for (int index = 0; index < position.PointCount; index++)
            {
                Vector2 at = Center(index);
                var piece = VisiblePiece(index);
                bool marked = index == selected || index == preview || (route.Length != 0 && index == route[route.Length - 1]);
                if (piece.Kind != 0) DrawPiece(mesh, at, piece);
                if (targets.Contains(index))
                {
                    if (piece.Kind == 0) Disc(mesh, at, marked ? Pitch * .21f : Pitch * .115f, UiFactory.Accent);
                    else Ring(mesh, at, Pitch * .43f, 3, UiFactory.Accent);
                }
                if (marked)
                {
                    if (game == BoardGameId.Army) Outline(mesh, at, Pitch * .86f, Pitch * .39f, 3, UiFactory.Accent);
                    else if (game == BoardGameId.Chess) Outline(mesh, at, Pitch * .91f, Pitch * .91f, 3, UiFactory.Accent);
                    else Ring(mesh, at, Pitch * .46f, 2.5f, UiFactory.Accent);
                }
                else if (last.To == index) Disc(mesh, at + new Vector2(Pitch * .29f, Pitch * .28f), Pitch * .063f, UiFactory.Hex("B9783F"));
            }
        }

        private void DrawArmy(VertexHelper mesh)
        {
            for (int from = 0; from < 60; from++)
            {
                Vector2 a = Center(from);
                for (int to = from + 1; to < 60; to++)
                {
                    bool rail = ArmyGame.IsRail(from, to);
                    if (!rail && !ArmyGame.IsRoad(from, to)) continue;
                    Vector2 b = Center(to);
                    Stroke(mesh, a, b, rail ? 6 : 2, rail ? UiFactory.Hex("506953") : UiFactory.Hex("9AAB90"));
                    if (!rail) continue;
                    int marks = Mathf.Max(1, Mathf.FloorToInt(Vector2.Distance(a, b) / 11));
                    for (int i = 0; i < marks; i++)
                    {
                        float p = (i + .2f) / marks, q = (i + .63f) / marks;
                        Stroke(mesh, Vector2.Lerp(a, b, p), Vector2.Lerp(a, b, q), 2, UiFactory.Hex("D8E4D7"));
                    }
                }
            }
            for (int i = 0; i < 60; i++)
            {
                Vector2 center = Center(i);
                if (ArmyGame.IsCamp(i))
                {
                    Disc(mesh, center, Pitch * .175f, UiFactory.Hex("B8CAAA"));
                    Ring(mesh, center, Pitch * .175f, 2, UiFactory.Hex("728C6F"));
                }
                else
                {
                    bool headquarters = ArmyGame.IsHeadquarters(i);
                    Box(mesh, center, Pitch * .64f, Pitch * .29f, headquarters ? UiFactory.Hex("C2D1B5") : UiFactory.Hex("EFF0E4"));
                    Outline(mesh, center, Pitch * .64f, Pitch * .29f, 1.5f, UiFactory.Hex("8C9F86"));
                }
            }
        }

        private void DrawXiangqi(VertexHelper mesh)
        {
            for (int row = 0; row < 10; row++) Stroke(mesh, Center(9 * row), Center(9 * row + 8), 2, LineColor);
            for (int col = 0; col < 9; col++)
            {
                if (col == 0 || col == 8) Stroke(mesh, Center(col), Center(81 + col), 2, LineColor);
                else
                {
                    Stroke(mesh, Center(col), Center(36 + col), 2, LineColor);
                    Stroke(mesh, Center(45 + col), Center(81 + col), 2, LineColor);
                }
            }
            foreach (int row in new[] { 0, 7 })
            {
                Stroke(mesh, Center(row * 9 + 3), Center((row + 2) * 9 + 5), 2, LineColor);
                Stroke(mesh, Center(row * 9 + 5), Center((row + 2) * 9 + 3), 2, LineColor);
            }
        }

        private void DrawChess(VertexHelper mesh)
        {
            for (int point = 0; point < 64; point++)
                Box(mesh, Center(point), Pitch, Pitch, (point / 8 + point % 8) % 2 == 0 ? Light : Dark);
        }

        private void DrawStar(VertexHelper mesh)
        {
            var checkers = (ChineseCheckersGame)position;
            for (int point = 0; point < 121; point++)
            {
                int camp = ChineseCheckersGame.CampAt(point), seat = -1;
                for (int s = 0; s < checkers.Seats; s++) if (checkers.SeatCamp(s) == camp) { seat = s; break; }
                Color tint = seat >= 0 ? Color.Lerp(Wood, Seats[seat], .31f) : UiFactory.Hex("CABA98");
                Disc(mesh, Center(point), Pitch * .165f, tint);
            }
        }

        private void DrawPiece(VertexHelper mesh, Vector2 at, BoardPiece piece)
        {
            switch (game)
            {
                case BoardGameId.Army:
                    var border = piece.Kind < 0 ? UiFactory.Hex("455951") : piece.Side == 0 ? Red : UiFactory.Ink;
                    Box(mesh, at, Pitch * .77f, Pitch * .35f, piece.Kind < 0 ? UiFactory.Hex("596A61") : Light);
                    Outline(mesh, at, Pitch * .77f, Pitch * .35f, 2, border);
                    if (piece.Kind < 0)
                    {
                        Box(mesh, at + Vector2.up * 2, Pitch * .22f, 2, UiFactory.Hex("AAB9AA"));
                        Box(mesh, at - Vector2.up * 3, Pitch * .13f, 2, UiFactory.Hex("AAB9AA"));
                    }
                    break;
                case BoardGameId.Xiangqi:
                    Disc(mesh, at, Pitch * .393f, UiFactory.Hex("FCF1D5"));
                    var ring = piece.Side == 0 ? Red : UiFactory.Ink;
                    Ring(mesh, at, Pitch * .392f, 2.4f, ring);
                    Ring(mesh, at, Pitch * .336f, 1.2f, Color.Lerp(Light, ring, .43f));
                    break;
                case BoardGameId.Checkers:
                    Disc(mesh, at, Pitch * .365f, UiFactory.Hex("F4ECD6"));
                    Disc(mesh, at, Pitch * .327f, Seats[piece.Side]);
                    break;
                default:
                    DrawSilhouette(mesh, at, piece);
                    break;
            }
        }

        private void DrawSilhouette(VertexHelper mesh, Vector2 at, BoardPiece piece)
        {
            float scale = Pitch * .68f;
            Color tint = piece.Side == 0 ? UiFactory.Hex("FFF2D8") : UiFactory.Hex("1D4034");
            Color edge = piece.Side == 0 ? UiFactory.Hex("355146") : UiFactory.Hex("DFD3B9");
            var design = ChessShapes[piece.Kind];
            var shape = new Vector2[design.Length];
            for (int i = 0; i < design.Length; i++) shape[i] = at + design[i] * scale;
            Polygon(mesh, shape, tint);
            for (int i = 0; i < shape.Length; i++) Stroke(mesh, shape[i], shape[(i + 1) % shape.Length], 1.4f, edge);
            if (piece.Kind == 1)
            {
                Disc(mesh, at + Vector2.up * scale * .27f, scale * .189f, edge);
                Disc(mesh, at + Vector2.up * scale * .27f, scale * .16f, tint);
            }
            if (piece.Kind == 2) Disc(mesh, at + new Vector2(-.17f, .27f) * scale, scale * .036f, edge);
            if (piece.Kind == 3) Stroke(mesh, at + new Vector2(.03f, .46f) * scale, at + new Vector2(-.12f, .26f) * scale, 2, edge);
            if (piece.Kind == 6)
            {
                Box(mesh, at + Vector2.up * scale * .43f, scale * .10f, scale * .26f, tint);
                Box(mesh, at + Vector2.up * scale * .45f, scale * .25f, scale * .08f, tint);
            }
        }

        private void DrawTrail(VertexHelper mesh, int from, IReadOnlyList<int> path, Color tint, float width)
        {
            if (from < 0) return;
            foreach (int to in path) { Stroke(mesh, Center(from), Center(to), width, tint); from = to; }
        }

        private int Hit(Vector2 screen, Camera camera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, camera, out Vector2 local) ||
                !rectTransform.rect.Contains(local)) return -1;
            if (game == BoardGameId.Chess)
            {
                Rect rect = rectTransform.rect;
                int square = Mathf.Clamp(Mathf.FloorToInt((rect.yMax - local.y) / Pitch), 0, 7) * 8 +
                    Mathf.Clamp(Mathf.FloorToInt((local.x - rect.xMin) / Pitch), 0, 7);
                if (viewSide == 1) square = 63 - square;
                return (7 - square / 8) * 8 + square % 8;
            }
            float bestDistance = .49f * .49f;
            int best = -1;
            for (int i = 0; i < position.PointCount; i++)
            {
                Vector2 difference = (Center(i) - local) / Pitch;
                if (game == BoardGameId.Army) difference.y /= .50f;
                float distance = difference.sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (!enabledInput || draining || data.button != PointerEventData.InputButton.Left) return;
            if (pointer != int.MinValue || Input.touchCount > 1) { draining = true; pointer = int.MinValue; return; }
            pointer = data.pointerId;
            press = data.position;
            pressCamera = data.pressEventCamera;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId < 0 && pointer == data.pointerId && Input.touchCount == 0) Finish(data.position);
        }

        private void LateUpdate()
        {
            if (Input.touchCount > 1) { pointer = int.MinValue; draining = true; }
            if (draining) { if (Input.touchCount == 0) draining = false; return; }
            if (pointer < 0) return;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId != pointer) continue;
                if (touch.phase == TouchPhase.Canceled) pointer = int.MinValue;
                else if (touch.phase == TouchPhase.Ended) Finish(touch.position);
                else if (Hit(press, pressCamera) != Hit(touch.position, pressCamera)) { pointer = int.MinValue; draining = true; }
                return;
            }
            pointer = int.MinValue;
        }

        private void Finish(Vector2 release)
        {
            pointer = int.MinValue;
            if (!enabledInput || draining) return;
            int from = Hit(press, pressCamera), to = Hit(release, pressCamera);
            if (to >= 0 && to == from) chosen?.Invoke(to);
        }

        private static Vector2[] Points(params float[] coordinates)
        {
            var result = new Vector2[coordinates.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector2(coordinates[2 * i], coordinates[2 * i + 1]);
            return result;
        }

        private static void Box(VertexHelper mesh, Vector2 at, float width, float height, Color tint)
        {
            Vector2 x = Vector2.right * width * .5f, y = Vector2.up * height * .5f;
            Quad(mesh, at - x - y, at - x + y, at + x + y, at + x - y, tint);
        }

        private static void Outline(VertexHelper mesh, Vector2 at, float width, float height, float stroke, Color tint)
        {
            Box(mesh, at + Vector2.up * height * .5f, width, stroke, tint);
            Box(mesh, at - Vector2.up * height * .5f, width, stroke, tint);
            Box(mesh, at + Vector2.right * width * .5f, stroke, height, tint);
            Box(mesh, at - Vector2.right * width * .5f, stroke, height, tint);
        }

        private static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 delta = b - a;
            var normal = new Vector2(-delta.y, delta.x).normalized * width * .5f;
            Quad(mesh, a - normal, a + normal, b + normal, b - normal, tint);
        }

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero); mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }

        private static void Disc(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            const int segments = 22;
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2 * Mathf.PI / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
            }
            for (int i = 0; i < segments; i++) mesh.AddTriangle(start, start + i + 1, start + (i + 1) % segments + 1);
        }

        private static void Ring(VertexHelper mesh, Vector2 center, float radius, float width, Color tint)
        {
            const int segments = 25;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 2 * Mathf.PI / segments, b = (i + 1) * 2 * Mathf.PI / segments;
                var u = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var v = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Quad(mesh, center + u * (radius - width), center + u * radius, center + v * radius, center + v * (radius - width), tint);
            }
        }

        private static void Polygon(VertexHelper mesh, Vector2[] polygon, Color tint)
        {
            var remaining = new List<int>(polygon.Length);
            float area = 0;
            int offset = mesh.currentVertCount;
            for (int i = 0; i < polygon.Length; i++)
            {
                remaining.Add(i);
                area += Cross(polygon[i], polygon[(i + 1) % polygon.Length]);
                mesh.AddVert(polygon[i], tint, Vector2.zero);
            }
            if (area < 0) remaining.Reverse();
            while (remaining.Count > 2)
            {
                bool removed = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross(polygon[b] - polygon[a], polygon[c] - polygon[b]) <= .001f) continue;
                    bool contains = false;
                    foreach (int p in remaining)
                    {
                        if (p == a || p == b || p == c) continue;
                        if (Cross(polygon[b] - polygon[a], polygon[p] - polygon[a]) >= 0 &&
                            Cross(polygon[c] - polygon[b], polygon[p] - polygon[b]) >= 0 &&
                            Cross(polygon[a] - polygon[c], polygon[p] - polygon[c]) >= 0) { contains = true; break; }
                    }
                    if (contains) continue;
                    mesh.AddTriangle(offset + a, offset + b, offset + c);
                    remaining.RemoveAt(i);
                    removed = true;
                    break;
                }
                if (!removed) break;
            }
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
