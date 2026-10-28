using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Tetris;

namespace XanaduYonder.Runtime.Tetris
{
    internal enum TetrisGraphicKind { Round, Left, Right, Down, CounterClockwise, Clockwise, HardDrop, Preview }

    /// <summary>Page decoration and action symbols, independent of font glyph coverage.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class TetrisGraphic : MaskableGraphic
    {
        internal TetrisGraphicKind Kind;
        internal float Radius;
        private TetrisPiece previewPiece;
        private TetrisCell[] previewCells;

        internal void SetPreview(TetrisPiece piece)
        {
            if (previewPiece == piece) return;
            previewPiece = piece;
            previewCells = piece == TetrisPiece.None ? null : TetrisPieces.GetCells(piece, 0);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            if (bounds.width <= 0 || bounds.height <= 0) return;
            if (Kind == TetrisGraphicKind.Round) { Rounded(mesh, bounds, Radius, color); return; }
            if (Kind == TetrisGraphicKind.Preview) { Preview(mesh, bounds); return; }
            float size = Mathf.Min(bounds.width, bounds.height), stroke = size * .085f;
            if (Kind == TetrisGraphicKind.Clockwise || Kind == TetrisGraphicKind.CounterClockwise)
            {
                float mirror = Kind == TetrisGraphicKind.Clockwise ? -1 : 1;
                Vector2 previous = ArcPoint(bounds.center, size, mirror, -50);
                for (int i = 1; i <= 24; i++)
                {
                    Vector2 next = ArcPoint(bounds.center, size, mirror, -50 + i * 270f / 24);
                    Line(mesh, previous, next, stroke, color); previous = next;
                }
                Vector2 head = ArcPoint(bounds.center, size, mirror, 220);
                Vector2 tangent = new Vector2(Mathf.Sin(220 * Mathf.Deg2Rad) * mirror,
                    -Mathf.Cos(220 * Mathf.Deg2Rad));
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                Line(mesh, head, head + (tangent + normal) * size * .19f, stroke, color);
                Line(mesh, head, head + (tangent - normal) * size * .19f, stroke, color);
                return;
            }
            Vector2 direction = Kind == TetrisGraphicKind.Left ? Vector2.left :
                Kind == TetrisGraphicKind.Right ? Vector2.right : Vector2.down;
            Vector2 end = bounds.center + direction * size * .29f;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Line(mesh, bounds.center - direction * size * .34f, end, stroke, color);
            Line(mesh, end, end - direction * size * .24f + perpendicular * size * .23f, stroke, color);
            Line(mesh, end, end - direction * size * .24f - perpendicular * size * .23f, stroke, color);
            if (Kind == TetrisGraphicKind.HardDrop)
                Line(mesh, bounds.center + new Vector2(-.33f, -.44f) * size,
                    bounds.center + new Vector2(.33f, -.44f) * size, stroke, color);
        }

        private static Vector2 ArcPoint(Vector2 center, float size, float mirror, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(angle) * mirror, Mathf.Sin(angle)) * size * .34f;
        }

        private void Preview(VertexHelper mesh, Rect bounds)
        {
            if (previewCells == null || previewCells.Length == 0) return;
            int minX = 4, minY = 4, maxX = 0, maxY = 0;
            foreach (TetrisCell cell in previewCells)
            {
                minX = Mathf.Min(minX, cell.X); maxX = Mathf.Max(maxX, cell.X);
                minY = Mathf.Min(minY, cell.Y); maxY = Mathf.Max(maxY, cell.Y);
            }
            float unit = Mathf.Min(bounds.width / 4, bounds.height / 2);
            float left = bounds.center.x - (maxX - minX + 1) * unit / 2;
            float top = bounds.center.y + (maxY - minY + 1) * unit / 2;
            foreach (TetrisCell cell in previewCells)
            {
                float gap = unit * .2f;
                Rect tile = new Rect(left + (cell.X - minX) * unit + gap / 2,
                    top - (cell.Y - minY + 1) * unit + gap / 2, unit - gap, unit - gap);
                Rounded(mesh, tile, unit * .1f, PieceColor(previewPiece) * color);
            }
        }

        internal static Color PieceColor(TetrisPiece piece)
        {
            switch (piece)
            {
                case TetrisPiece.I: return UiFactory.Hex("70B7BB");
                case TetrisPiece.O: return UiFactory.Hex("D9BA64");
                case TetrisPiece.T: return UiFactory.Hex("AA91B6");
                case TetrisPiece.J: return UiFactory.Hex("7897BE");
                case TetrisPiece.L: return UiFactory.Hex("D59D6D");
                case TetrisPiece.S: return UiFactory.Hex("8DAC7A");
                case TetrisPiece.Z: return UiFactory.Hex("C9827F");
                default: return Color.clear;
            }
        }

        internal static void Quad(VertexHelper mesh, Rect rect, Color shade)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector2(rect.xMin, rect.yMin), shade, Vector2.zero);
            mesh.AddVert(new Vector2(rect.xMin, rect.yMax), shade, Vector2.zero);
            mesh.AddVert(new Vector2(rect.xMax, rect.yMax), shade, Vector2.zero);
            mesh.AddVert(new Vector2(rect.xMax, rect.yMin), shade, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }

        internal static void Outline(VertexHelper mesh, Rect rect, float width, Color shade)
        {
            Quad(mesh, new Rect(rect.xMin, rect.yMin, rect.width, width), shade);
            Quad(mesh, new Rect(rect.xMin, rect.yMax - width, rect.width, width), shade);
            Quad(mesh, new Rect(rect.xMin, rect.yMin + width, width, rect.height - 2 * width), shade);
            Quad(mesh, new Rect(rect.xMax - width, rect.yMin + width, width, rect.height - 2 * width), shade);
        }

        internal static void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color shade)
        {
            Vector2 delta = to - from, normal = new Vector2(-delta.y, delta.x).normalized * width / 2;
            int first = mesh.currentVertCount;
            mesh.AddVert(from - normal, shade, Vector2.zero); mesh.AddVert(from + normal, shade, Vector2.zero);
            mesh.AddVert(to + normal, shade, Vector2.zero); mesh.AddVert(to - normal, shade, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }

        internal static void Rounded(VertexHelper mesh, Rect rect, float radius, Color shade)
        {
            radius = Mathf.Clamp(radius, 0, Mathf.Min(rect.width, rect.height) / 2);
            if (radius <= 0) { Quad(mesh, rect, shade); return; }
            int first = mesh.currentVertCount;
            mesh.AddVert(rect.center, shade, Vector2.zero);
            const int samples = 24;
            for (int i = 0; i < samples; i++)
            {
                int quadrant = i / 6;
                float angle = (quadrant * 90 + i % 6 * 18) * Mathf.Deg2Rad;
                Vector2 corner = new Vector2(quadrant == 0 || quadrant == 3 ? rect.xMax - radius : rect.xMin + radius,
                    quadrant < 2 ? rect.yMax - radius : rect.yMin + radius);
                mesh.AddVert(corner + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, shade, Vector2.zero);
            }
            for (int i = 0; i < samples; i++) mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % samples);
        }
    }
}
