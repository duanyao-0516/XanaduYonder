using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Gomoku;

namespace XanaduYonder.Runtime.Gomoku
{
    // One mesh contains the board and up to 225 stones; axis labels stay real text.
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class GomokuBoardGraphic : MaskableGraphic
    {
        private static readonly Color Grid = UiFactory.Hex("8C907A"), Star = UiFactory.Hex("62745E");
        private static readonly Color Black = UiFactory.Hex("293A31"), White = UiFactory.Hex("FFFDF5");
        private static readonly Color WhiteEdge = UiFactory.Hex("8A937E"), Victory = UiFactory.Hex("BD8450");
        private GomokuGame game;
        private int selected = -1;
        private Rect bounds;

        public void Render(GomokuGame value, int preview)
        { game = value.Copy(); selected = preview; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); bounds = rectTransform.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            const float margin = (float)GomokuBoardGeometry.Margin;
            const float end = (float)(GomokuBoardGeometry.Size - GomokuBoardGeometry.Margin);
            const float pitch = (float)GomokuBoardGeometry.Pitch;
            if (selected >= 0)
            {
                var p = Position(selected); var tint = UiFactory.Accent; tint.a = .10f;
                Stroke(mesh, new Vector2(margin, p.y), new Vector2(end, p.y), pitch * .75f, tint);
                Stroke(mesh, new Vector2(p.x, margin), new Vector2(p.x, end), pitch * .75f, tint);
                Rectangle(mesh, new Vector2(p.x, 22), 32, 30, tint);
                Rectangle(mesh, new Vector2(p.x, 626), 32, 30, tint);
                Rectangle(mesh, new Vector2(22, p.y), 36, 30, tint);
                Rectangle(mesh, new Vector2(626, p.y), 36, 30, tint);
            }
            for (int i = 0; i < GomokuGame.Size; i++)
            {
                float at = margin + i * pitch;
                Stroke(mesh, new Vector2(margin, at), new Vector2(end, at), 1.3f, Grid);
                Stroke(mesh, new Vector2(at, margin), new Vector2(at, end), 1.3f, Grid);
            }
            foreach (int star in new[] { 48, 56, 112, 168, 176 }) Disc(mesh, Position(star), 4, Star);
            if (game == null) return;
            for (int i = 0; i < GomokuGame.CellCount; i++)
            {
                var stone = game.GetCell(i);
                if (stone == GomokuStone.Empty) continue;
                var at = Position(i); bool black = stone == GomokuStone.Black;
                Disc(mesh, at, pitch * .40f, black ? UiFactory.Ink : WhiteEdge);
                Disc(mesh, at, pitch * .40f - 1.4f, black ? Black : White);
                if (i == game.LastMove) Disc(mesh, at, 3.6f, UiFactory.Hex(black ? "E8BC80" : "A76837"));
            }
            var winning = game.WinningLine;
            if (winning.Length > 1)
            {
                var a = Position(winning[0]); var b = Position(winning[winning.Length - 1]);
                Stroke(mesh, a, b, 6, Victory); Disc(mesh, a, 3, Victory); Disc(mesh, b, 3, Victory);
            }
            if (selected >= 0)
            {
                var at = Position(selected); bool black = game.ToPlay == GomokuStone.Black;
                var preview = black ? Black : White; preview.a = black ? .5f : .7f;
                Disc(mesh, at, pitch * .43f, preview);
                Ring(mesh, at, pitch * .43f, 3, UiFactory.Accent);
                var cross = black ? Color.white : UiFactory.Accent;
                Stroke(mesh, at + new Vector2(-6, 0), at + new Vector2(6, 0), 2, cross);
                Stroke(mesh, at + new Vector2(0, -6), at + new Vector2(0, 6), 2, cross);
            }
        }

        private static Vector2 Position(int cell)
        { return new Vector2((float)GomokuBoardGeometry.XAt(cell), (float)GomokuBoardGeometry.YAt(cell)); }

        private Vector2 Local(Vector2 point)
        { return new Vector2(bounds.xMin + point.x * bounds.width / 648, bounds.yMax - point.y * bounds.height / 648); }

        private void Rectangle(VertexHelper mesh, Vector2 at, float width, float height, Color tint)
        {
            var r = new Vector2(width, height) * .5f;
            Quad(mesh, at - r, at + new Vector2(r.x, -r.y), at + r, at + new Vector2(-r.x, r.y), tint);
        }

        private void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var delta = b - a; var n = new Vector2(-delta.y, delta.x).normalized * (width * .5f);
            Quad(mesh, a - n, a + n, b + n, b - n, tint);
        }

        private void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(Local(a), tint, Vector2.zero); mesh.AddVert(Local(b), tint, Vector2.zero);
            mesh.AddVert(Local(c), tint, Vector2.zero); mesh.AddVert(Local(d), tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }

        private void Disc(VertexHelper mesh, Vector2 at, float radius, Color tint)
        {
            const int count = 32;
            int first = mesh.currentVertCount;
            mesh.AddVert(Local(at), tint, Vector2.zero);
            for (int i = 0; i < count; i++)
            {
                float angle = 2 * Mathf.PI * i / count;
                mesh.AddVert(Local(at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius), tint, Vector2.zero);
            }
            for (int i = 0; i < count; i++) mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % count);
        }

        private void Ring(VertexHelper mesh, Vector2 at, float radius, float width, Color tint)
        {
            const int count = 40;
            for (int i = 0; i < count; i++)
            {
                float a = 2 * Mathf.PI * i / count, b = 2 * Mathf.PI * (i + 1) / count;
                var da = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Quad(mesh, at + da * (radius - width * .5f), at + da * (radius + width * .5f),
                    at + db * (radius + width * .5f), at + db * (radius - width * .5f), tint);
            }
        }
    }
}
