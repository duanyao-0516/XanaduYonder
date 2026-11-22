using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XanaduYonder.Core.BoardGames;

namespace XanaduYonder.Runtime.BoardGames
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed partial class BoardSurface : MaskableGraphic, IPointerDownHandler, IPointerUpHandler
    {
        private static readonly Color Wood = UiFactory.Hex("EADFC6");
        private static readonly Color LineColor = UiFactory.Hex("664320");
        private static readonly Color Light = UiFactory.Hex("F2E6CC");
        private static readonly Color Dark = UiFactory.Hex("718C73");
        private static readonly Color Red = UiFactory.Hex("A13A30");
        private static readonly Color ChessGold = UiFactory.Hex("E7B354");
        private static readonly Color ChessIvory = UiFactory.Hex("FFF2CD");
        private static readonly Color ChessMark = UiFactory.Hex("283E33");
        private static readonly Color WhitePiece = UiFactory.Hex("FFF2D8");
        private static readonly Color WhitePieceEdge = UiFactory.Hex("355146");
        private static readonly Color BlackPiece = UiFactory.Hex("1D4034");
        private static readonly Color BlackPieceEdge = UiFactory.Hex("DFD3B9");
        private static readonly Color SuggestionGold = UiFactory.Hex("FFD461");
        private static readonly Color SuggestionEdge = UiFactory.Hex("6D3E10");
        private const float ChessLabelOffset = .31f;
        private static Texture2D xiangqiArtwork;
        private static readonly Color[] Seats = { UiFactory.Hex("286B47"), UiFactory.Hex("9B4B37"), UiFactory.Hex("3F6898"),
            UiFactory.Hex("856222"), UiFactory.Hex("705491"), UiFactory.Hex("256D70") };
        private static readonly string[] Symbols = { "•", "—", "×", "▲", "◇", "Ⅱ" };
        private static readonly string[] Army = { "", "工兵", "排长", "连长", "营长", "团长", "旅长", "师长", "军长", "司令", "炸弹", "地雷", "军旗" };
        private static readonly string[] XiangRed = { "", "帥", "仕", "相", "馬", "車", "炮", "兵" };
        private static readonly string[] XiangBlack = { "", "將", "士", "象", "馬", "車", "砲", "卒" };
        private static readonly string[] ChessNames = { "", "兵", "马", "象", "车", "后", "王" };
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
        private readonly List<Text> labels = new List<Text>();
        private BoardGameId game;
        private IBoardPosition position;
        private Action<int> chosen;
        private int ownSide, viewSide, selected, preview;
        private bool formation, revealArmy, showCoordinates, showChessNames;
        private int[] route;
        private BoardMove last;
        private Vector2 press;
        private Camera pressCamera;
        private int pointer = int.MinValue;
        private bool draining;
        private bool enabledInput;

        private bool HasXiangqiArtwork => game == BoardGameId.Xiangqi && xiangqiArtwork != null;
        public override Texture mainTexture => HasXiangqiArtwork ? xiangqiArtwork : base.mainTexture;
        internal BoardMove? Suggestion { get; private set; }

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
            IReadOnlyList<int> route, IEnumerable<int> targets, BoardMove last, bool enabledInput, Action<int> chosen,
            bool formation = false, bool revealArmy = false, BoardMove? suggestion = null,
            bool showCoordinates = false, bool showChessNames = true)
        {
            this.game = game;
            if (game == BoardGameId.Xiangqi && xiangqiArtwork == null)
                xiangqiArtwork = Resources.Load<Texture2D>("BoardGames/XiangqiArtwork");
            this.position = position;
            this.ownSide = ownSide;
            this.viewSide = viewSide;
            this.selected = selected;
            this.preview = preview;
            this.formation = formation;
            this.revealArmy = revealArmy;
            this.showCoordinates = showCoordinates;
            this.showChessNames = showChessNames;
            Suggestion = suggestion;
            this.route = route == null ? Array.Empty<int>() : new List<int>(route).ToArray();
            this.targets.Clear();
            this.targets.UnionWith(targets);
            this.last = last;
            this.enabledInput = enabledInput;
            this.chosen = chosen;
            raycastTarget = enabledInput;
            AddLabels();
            SetMaterialDirty();
            SetVerticesDirty();
        }

        private int Columns => game == BoardGameId.Checkers ? 13 : game == BoardGameId.Xiangqi ? 9 : game == BoardGameId.Army ? 5 : 8;
        private int Rows => game == BoardGameId.Checkers ? 17 : game == BoardGameId.Xiangqi ? 10 : game == BoardGameId.Army ? 12 : 8;
        private float AxisGutter => showCoordinates
            ? Mathf.Min(game == BoardGameId.Checkers ? 28 : 32, rectTransform.rect.width * .15f) : 0;
        private float Pitch => (rectTransform.rect.width - AxisGutter) / Columns;

        private Rect BoardArea
        {
            get
            {
                Rect area = rectTransform.rect;
                float gutter = AxisGutter;
                return Rect.MinMaxRect(area.xMin + gutter, area.yMin + gutter, area.xMax, area.yMax);
            }
        }

        private Vector2 Center(int index)
        {
            Rect area = BoardArea;
            if (game == BoardGameId.Checkers)
                return new Vector2(area.center.x + ChineseCheckersGame.DoubledXAt(index) * Pitch * .5f,
                    CheckersRowY(area, ChineseCheckersGame.RowAt(index)));
            int columns = Columns;
            int rows = Rows;
            if (game == BoardGameId.Chess) index = (7 - index / 8) * 8 + index % 8;
            if (viewSide == 1) index = columns * rows - 1 - index;
            int row = index / columns;
            float y = game == BoardGameId.Army ? (row + .66f + (row >= 6 ? .70f : 0)) / 13.02f : (row + .5f) / rows;
            return new Vector2(area.xMin + (index % columns + .5f) * Pitch, area.yMax - y * area.height);
        }

        private float CheckersRowY(Rect area, int row) => area.center.y + (8 - row) * Pitch * .8660254f;

        private void AddLabels()
        {
            foreach (Text label in labels)
            {
                label.gameObject.SetActive(false);
                Destroy(label.gameObject);
            }
            labels.Clear();

            if (xiangqiPieces != null && game != BoardGameId.Xiangqi) xiangqiPieces.gameObject.SetActive(false);
            if (showCoordinates) AddCoordinates();
            if (game == BoardGameId.Chess && !showChessNames) return;
            if (game == BoardGameId.Xiangqi)
            {
                Rect board = BoardArea;
                int fontSize = Mathf.RoundToInt(Pitch * .39f);
                AddCenteredLabel("楚 河", new Vector2(board.xMin + Pitch * 2.5f, board.center.y), Pitch * 3, Pitch * .8f, fontSize, LineColor);
                AddCenteredLabel("漢 界", new Vector2(board.xMin + Pitch * 6.5f, board.center.y), Pitch * 3, Pitch * .8f, fontSize, LineColor);
                BindXiangqiPieceLayer();
                if (HasXiangqiArtwork) return;
            }
            for (int index = 0; index < position.PointCount; index++)
            {
                var piece = VisiblePiece(index);
                if (piece.Kind <= 0) continue;
                Vector2 at = Center(index);
                if (game == BoardGameId.Chess)
                {
                    var label = AddCenteredLabel(ChessNames[piece.Kind], at - Vector2.up * (Pitch * ChessLabelOffset),
                        Pitch * .45f, Pitch * .32f, Mathf.RoundToInt(Pitch * .29f),
                        piece.Side == ChessGame.White ? WhitePieceEdge : WhitePiece, FontStyle.Bold);
                    label.alignByGeometry = true;
                    label.name = "Chess piece label " + index;
                    continue;
                }
                float w = Pitch * (game == BoardGameId.Army ? .82f : .75f);
                float h = Pitch * (game == BoardGameId.Army ? .38f : .76f);
                string name = game == BoardGameId.Army ? ArmyName(piece.Kind) :
                    game == BoardGameId.Xiangqi ? (piece.Side == 0 ? XiangRed : XiangBlack)[piece.Kind] : Symbols[piece.Side];
                int font = Mathf.RoundToInt(Pitch * (game == BoardGameId.Army ? .185f : game == BoardGameId.Xiangqi ? .44f : .45f));
                Color tint = game == BoardGameId.Checkers ? Color.white : piece.Side == 0 ? Red : UiFactory.Ink;
                var pieceLabel = AddCenteredLabel(name, at, w, h, font, tint, FontStyle.Bold);
                if (game == BoardGameId.Xiangqi) pieceLabel.name = XiangqiPieceLabelName(index);
            }
        }

        private void AddCoordinates()
        {
            Rect area = rectTransform.rect;
            Rect board = BoardArea;
            float gutter = AxisGutter;
            float pitch = Pitch;
            int fontSize = Mathf.RoundToInt(Mathf.Min(gutter * .72f, pitch * .60f));
            bool checkers = game == BoardGameId.Checkers;
            for (int col = 0; col < Columns; col++)
            {
                // Odd numbers are the file ticks shared by the board's 13 evenly spaced major columns.
                string file = game == BoardGameId.Chess ? ((char)('a' + col)).ToString() :
                    (checkers ? col * 2 + 1 : col + 1).ToString(CultureInfo.InvariantCulture);
                float x = checkers ? board.xMin + (col + .5f) * pitch : Center(col).x;
                Vector2 at = new Vector2(x, area.yMin + gutter * .5f);
                var label = AddCenteredLabel(file, at, pitch, gutter, fontSize, UiFactory.Ink, FontStyle.Bold);
                label.alignByGeometry = true;
                label.name = "Board file " + file;
            }
            for (int row = 0; row < Rows; row++)
            {
                int rank = game == BoardGameId.Chess || checkers ? row + 1 : Rows - row;
                string digits = rank.ToString(CultureInfo.InvariantCulture);
                string text = game == BoardGameId.Xiangqi ? BoardCatalog.XiangqiRankName(rank) : digits;
                float y = checkers ? CheckersRowY(board, row) : Center(row * Columns).y;
                Vector2 at = new Vector2(area.xMin + gutter * .5f, y);
                var label = AddCenteredLabel(text, at, gutter, gutter, fontSize, UiFactory.Ink, FontStyle.Bold);
                label.alignByGeometry = true;
                label.name = "Board rank " + digits;
            }
        }

        private Text AddCenteredLabel(string text, Vector2 at, float width, float height, int fontSize, Color tint,
            FontStyle style = FontStyle.Normal)
        {
            Rect area = rectTransform.rect;
            Text label = UiFactory.Label(transform, text, at.x - area.xMin - width * .5f, area.yMax - at.y - height * .5f,
                width, height, fontSize, tint, TextAnchor.MiddleCenter, style);
            labels.Add(label);
            return label;
        }

        private BoardPiece VisiblePiece(int point)
        {
            if (game == BoardGameId.Army)
            {
                var piece = position.PieceAt(point, ownSide);
                if (formation) return piece.Side == ownSide ? piece : default;
                if (revealArmy && piece.Kind < 0) return position.PieceAt(point, piece.Side);
                if (!revealArmy && ownSide < 0 && piece.Kind > 0) return new BoardPiece(piece.Side, -1);
                return piece;
            }
            if (game != BoardGameId.Checkers || route.Length == 0) return position.PieceAt(point, ownSide);
            if (point == route[route.Length - 1]) return position.PieceAt(selected, ownSide);
            return point == selected ? default : position.PieceAt(point, ownSide);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (position == null || Pitch <= 0) return;
            Rect outer = rectTransform.rect;
            Rect board = BoardArea;
            if (showCoordinates) Box(mesh, outer.center, outer.width, outer.height, UiFactory.Paper);
            if (HasXiangqiArtwork) ArtworkQuad(mesh, board, new RectInt(8, 8, 600, 668));
            else Box(mesh, board.center, board.width, board.height, game == BoardGameId.Army ? UiFactory.Hex("DEE7D9") : Wood);
            switch (game)
            {
                case BoardGameId.Army: DrawArmy(mesh); break;
                case BoardGameId.Xiangqi: DrawXiangqi(mesh); return;
                case BoardGameId.Checkers: DrawStar(mesh); break;
                default: DrawChess(mesh); break;
            }
            DrawPieces(mesh);
        }

        internal void DrawPieces(VertexHelper mesh)
        {
            if (position == null || Pitch <= 0) return;
            if (game == BoardGameId.Checkers)
            {
                if (last.From >= 0) DrawTrail(mesh, last.From, last.PathLength == 0 ? new[] { last.To } : last.Path, UiFactory.Hex("82956C"), 3);
                if (route.Length > 0) DrawTrail(mesh, selected, route, UiFactory.Accent, 5);
                if (Suggestion is BoardMove jumpHint && jumpHint.PathLength > 1)
                {
                    var landings = jumpHint.Path;
                    DrawTrail(mesh, jumpHint.From, landings, SuggestionEdge, 7);
                    DrawTrail(mesh, jumpHint.From, landings, SuggestionGold, 3);
                }
            }
            int animatedDestination = XiangqiMotionDestination;
            for (int index = 0; index < position.PointCount; index++)
            {
                Vector2 at = Center(index);
                var piece = VisiblePiece(index);
                bool marked = index == selected || index == preview || (route.Length != 0 && index == route[route.Length - 1]);
                if (piece.Kind != 0 && index != animatedDestination) DrawPiece(mesh, at, piece);
                bool target = targets.Contains(index);
                if (game == BoardGameId.Chess && (marked || target))
                {
                    DrawChessMark(mesh, at, index, piece.Kind != 0, marked);
                    continue;
                }
                if (target)
                {
                    if (piece.Kind == 0 && game == BoardGameId.Checkers)
                    {
                        Disc(mesh, at, Pitch * .235f, UiFactory.Hex("B9D1A7"));
                        Ring(mesh, at, Pitch * .235f, 2.4f, UiFactory.Accent);
                    }
                    else if (piece.Kind == 0) Disc(mesh, at, marked ? Pitch * .21f : Pitch * .115f, UiFactory.Accent);
                    else Ring(mesh, at, Pitch * .43f, 3, UiFactory.Accent);
                }
                if (marked)
                {
                    if (game == BoardGameId.Army) Outline(mesh, at, Pitch * .86f, Pitch * .39f, 3, UiFactory.Accent);
                    else Ring(mesh, at, Pitch * .46f, 2.5f, UiFactory.Accent);
                }
                else if (last.To == index && index != animatedDestination)
                    Disc(mesh, at + new Vector2(Pitch * .29f, Pitch * .28f), Pitch * .063f, UiFactory.Hex("B9783F"));
            }
            if (Suggestion is BoardMove hint && hint.From != hint.To &&
                (uint)hint.From < position.PointCount && (uint)hint.To < position.PointCount)
            {
                DrawSuggestionMark(mesh, Center(hint.From), false);
                DrawSuggestionMark(mesh, Center(hint.To), true);
                if (game == BoardGameId.Checkers)
                    for (int i = 0; i < hint.PathLength - 1; i++)
                    {
                        Vector2 landing = Center(hint.PathAt(i));
                        Disc(mesh, landing, Pitch * .16f, SuggestionEdge);
                        Disc(mesh, landing, Pitch * .10f, SuggestionGold);
                    }
            }
            DrawXiangqiMotion(mesh, animatedDestination);
        }

        private void DrawSuggestionMark(VertexHelper mesh, Vector2 at, bool destination)
        {
            float width = Pitch * .91f;
            float height = game == BoardGameId.Army ? Pitch * .42f : width;
            float stroke = Mathf.Clamp(Pitch * .07f, 3.5f, 7);
            float inner = stroke * .57f;
            if (destination)
            {
                if (game == BoardGameId.Xiangqi || game == BoardGameId.Checkers)
                {
                    float radius = Pitch * .49f;
                    Ring(mesh, at, radius, stroke, SuggestionEdge);
                    Ring(mesh, at, radius - (stroke - inner) * .5f, inner, SuggestionGold);
                }
                else
                {
                    Outline(mesh, at, width, height, stroke, SuggestionEdge);
                    Outline(mesh, at, width, height, inner, SuggestionGold);
                }
                return;
            }
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                {
                    var corner = at + new Vector2(x * width, y * height) * .5f;
                    var horizontal = corner - Vector2.right * (x * width * .24f);
                    var vertical = corner - Vector2.up * (y * height * .24f);
                    Stroke(mesh, horizontal, corner, stroke, SuggestionEdge);
                    Stroke(mesh, corner, vertical, stroke, SuggestionEdge);
                    Stroke(mesh, horizontal, corner, inner, SuggestionGold);
                    Stroke(mesh, corner, vertical, inner, SuggestionGold);
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
            const float line = 2.4f;
            for (int row = 0; row < 10; row++) Stroke(mesh, Center(9 * row), Center(9 * row + 8), line, LineColor);
            for (int col = 0; col < 9; col++)
            {
                if (col == 0 || col == 8) Stroke(mesh, Center(col), Center(81 + col), line, LineColor);
                else
                {
                    Stroke(mesh, Center(col), Center(36 + col), line, LineColor);
                    Stroke(mesh, Center(45 + col), Center(81 + col), line, LineColor);
                }
            }
            foreach (int row in new[] { 0, 7 })
            {
                Stroke(mesh, Center(row * 9 + 3), Center((row + 2) * 9 + 5), line, LineColor);
                Stroke(mesh, Center(row * 9 + 5), Center((row + 2) * 9 + 3), line, LineColor);
            }
            for (int col = 0; col < 9; col += 2)
            {
                DrawXiangqiPositionMark(mesh, Center(27 + col));
                DrawXiangqiPositionMark(mesh, Center(54 + col));
            }
            foreach (int point in new[] { 19, 25, 64, 70 }) DrawXiangqiPositionMark(mesh, Center(point));
        }

        private void DrawXiangqiPositionMark(VertexHelper mesh, Vector2 point)
        {
            Rect board = BoardArea;
            for (int x = -1; x <= 1; x += 2)
            {
                float neighbor = point.x + x * Pitch;
                if (neighbor < board.xMin || neighbor > board.xMax) continue;
                for (int y = -1; y <= 1; y += 2)
                {
                    var corner = point + new Vector2(x, y) * (Pitch * .10f);
                    Stroke(mesh, corner, corner + Vector2.right * (x * Pitch * .13f), 1.8f, LineColor);
                    Stroke(mesh, corner, corner + Vector2.up * (y * Pitch * .13f), 1.8f, LineColor);
                }
            }
        }

        private void DrawChess(VertexHelper mesh)
        {
            for (int point = 0; point < 64; point++)
            {
                var square = (point / 8 + point % 8) % 2 == 0 ? Light : Dark;
                if (point == selected) square = ChessGold;
                else if (point == preview) square = UiFactory.Hex("F5D67F");
                Box(mesh, Center(point), Pitch, Pitch, square);
            }
        }

        private void DrawChessMark(VertexHelper mesh, Vector2 at, int index, bool occupied, bool marked)
        {
            if (marked)
            {
                Outline(mesh, at, Pitch * .90f, Pitch * .90f, 5, ChessMark);
                Outline(mesh, at, Pitch * .81f, Pitch * .81f, 2.5f, ChessIvory);
                if (index == selected || occupied) return;
            }
            if (!occupied)
            {
                Disc(mesh, at, Pitch * .217f, ChessMark);
                Disc(mesh, at, Pitch * .172f, ChessIvory);
                Disc(mesh, at, Pitch * .108f, ChessMark);
                return;
            }
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                {
                    var corner = at + new Vector2(x, y) * (Pitch * .40f);
                    var horizontal = corner - Vector2.right * (x * Pitch * .18f);
                    var vertical = corner - Vector2.up * (y * Pitch * .18f);
                    Stroke(mesh, horizontal, corner, 7, ChessMark);
                    Stroke(mesh, corner, vertical, 7, ChessMark);
                    Stroke(mesh, horizontal, corner, 3.5f, ChessIvory);
                    Stroke(mesh, corner, vertical, 3.5f, ChessIvory);
                }
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
                    DrawXiangqiPiece(mesh, at, piece, Pitch);
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
            float pitch = Pitch;
            float scale = pitch * (showChessNames ? .54f : .68f);
            Color tint = piece.Side == ChessGame.White ? WhitePiece : BlackPiece;
            Color edge = piece.Side == ChessGame.White ? WhitePieceEdge : BlackPieceEdge;
            if (showChessNames)
            {
                Vector2 caption = at - Vector2.up * (pitch * ChessLabelOffset);
                Box(mesh, caption, pitch * .40f, pitch * .31f, tint);
                Outline(mesh, caption, pitch * .40f, pitch * .31f, 1.2f, edge);
                at += Vector2.up * (pitch * .15f);
            }
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
                !BoardArea.Contains(local)) return -1;
            if (game == BoardGameId.Chess)
            {
                Rect rect = BoardArea;
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
            UpdateXiangqiMotion();
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

        private static void ArtworkQuad(VertexHelper mesh, Rect bounds, RectInt region)
        {
            // Artwork uses top-left pixel coordinates. The atlas also has an opaque white swatch at UV (0, 0) for the board line mesh.
            float u0 = (region.xMin + .5f) / xiangqiArtwork.width;
            float u1 = (region.xMax - .5f) / xiangqiArtwork.width;
            float v0 = 1 - (region.yMax - .5f) / xiangqiArtwork.height;
            float v1 = 1 - (region.yMin + .5f) / xiangqiArtwork.height;
            int index = mesh.currentVertCount;
            mesh.AddVert(new Vector2(bounds.xMin, bounds.yMin), Color.white, new Vector2(u0, v0));
            mesh.AddVert(new Vector2(bounds.xMin, bounds.yMax), Color.white, new Vector2(u0, v1));
            mesh.AddVert(new Vector2(bounds.xMax, bounds.yMax), Color.white, new Vector2(u1, v1));
            mesh.AddVert(new Vector2(bounds.xMax, bounds.yMin), Color.white, new Vector2(u1, v0));
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
