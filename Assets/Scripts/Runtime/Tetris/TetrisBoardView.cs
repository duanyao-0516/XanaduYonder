using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.Tetris;

namespace XanaduYonder.Runtime.Tetris
{
    /// <summary>One reusable mesh for the visible board; hidden spawn rows never reach the canvas.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class TetrisBoardView : MaskableGraphic
    {
        private readonly TetrisPiece[] cells = new TetrisPiece[TetrisRules.BoardWidth * TetrisRules.VisibleRows];
        private TetrisCell[] activeCells;
        private TetrisPiece activePiece;
        private int rotation, activeX, activeY, ghostY;
        private bool gameOver;
        private const float ClearFlashDuration = .12f;
        private uint flashingRows;
        private float flashRemaining;
        private static readonly Color Grid = UiFactory.Hex("B6C2B2") * new Color(1, 1, 1, .34f);

        internal bool HasClearFlash { get { return flashingRows != 0 && flashRemaining > 0; } }

        internal void FlashLines(int[] coreRows)
        {
            uint rows = 0;
            if (coreRows != null)
                for (int i = 0; i < coreRows.Length; i++)
                {
                    int row = coreRows[i];
                    if (row >= TetrisRules.HiddenRows && row < TetrisRules.BoardHeight)
                        rows |= 1u << (row - TetrisRules.HiddenRows);
                }
            flashingRows = rows;
            flashRemaining = rows == 0 ? 0 : ClearFlashDuration;
            SetVerticesDirty();
        }

        internal void TickVisuals(float seconds)
        {
            if (!HasClearFlash || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            flashRemaining = Mathf.Max(0, flashRemaining - seconds);
            if (flashRemaining == 0) flashingRows = 0;
            SetVerticesDirty();
        }

        internal void ClearVisuals()
        {
            if (flashingRows == 0 && flashRemaining == 0) return;
            flashingRows = 0; flashRemaining = 0;
            SetVerticesDirty();
        }

        internal void Render(TetrisSnapshot state, int landingY)
        {
            bool changed = activePiece != state.ActivePiece || rotation != state.Rotation ||
                activeX != state.X || activeY != state.Y || ghostY != landingY || gameOver != state.GameOver;
            if (activePiece != state.ActivePiece || rotation != state.Rotation || activeCells == null)
                activeCells = state.ActivePiece == TetrisPiece.None ? null : TetrisPieces.GetCells(state.ActivePiece, state.Rotation);
            activePiece = state.ActivePiece; rotation = state.Rotation;
            activeX = state.X; activeY = state.Y; ghostY = landingY; gameOver = state.GameOver;
            for (int i = 0; i < cells.Length; i++)
            {
                TetrisPiece piece = state.Cells[i + TetrisRules.HiddenRows * TetrisRules.BoardWidth];
                if (cells[i] != piece) { cells[i] = piece; changed = true; }
            }
            if (changed) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float cell = rect.width / TetrisRules.BoardWidth;
            if (cell <= 0 || rect.height <= 0) return;
            TetrisGraphic.Quad(mesh, rect, UiFactory.Hex("E2E7DC"));
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != TetrisPiece.None)
                    DrawTile(mesh, Tile(rect, cell, i % TetrisRules.BoardWidth, i / TetrisRules.BoardWidth), cells[i]);
            }
            // Keep grid strokes visible at the short portrait size (subpixel lines disappear).
            float gridStroke = cell * .055f;
            for (int x = 0; x <= TetrisRules.BoardWidth; x++)
                TetrisGraphic.Quad(mesh, new Rect(Mathf.Clamp(rect.xMin + x * cell - gridStroke / 2,
                    rect.xMin, rect.xMax - gridStroke), rect.yMin, gridStroke, rect.height), Grid);
            for (int y = 0; y <= TetrisRules.VisibleRows; y++)
                TetrisGraphic.Quad(mesh, new Rect(rect.xMin, Mathf.Clamp(rect.yMin + y * cell - gridStroke / 2,
                    rect.yMin, rect.yMax - gridStroke), rect.width, gridStroke), Grid);
            if (HasClearFlash)
            {
                Color flash = new Color(.92f, .98f, .88f, .62f * flashRemaining / ClearFlashDuration);
                for (int y = 0; y < TetrisRules.VisibleRows; y++)
                    if ((flashingRows & (1u << y)) != 0)
                        TetrisGraphic.Quad(mesh, new Rect(rect.xMin, rect.yMax - (y + 1) * cell, rect.width, cell), flash);
            }
            if (gameOver || activeCells == null) return;
            foreach (TetrisCell shape in activeCells)
            {
                int x = activeX + shape.X, y = ghostY + shape.Y - TetrisRules.HiddenRows;
                if (!Visible(x, y)) continue;
                Rect tile = Tile(rect, cell, x, y);
                float inset = cell * .06f;
                tile = new Rect(tile.x + inset, tile.y + inset, tile.width - 2 * inset, tile.height - 2 * inset);
                Color shade = Color.Lerp(TetrisGraphic.PieceColor(activePiece), UiFactory.Ink, .18f);
                shade.a = .82f;
                TetrisGraphic.Outline(mesh, tile, cell * .065f, shade);
            }
            foreach (TetrisCell shape in activeCells)
            {
                int x = activeX + shape.X, y = activeY + shape.Y - TetrisRules.HiddenRows;
                if (Visible(x, y)) DrawTile(mesh, Tile(rect, cell, x, y), activePiece);
            }
        }

        private static bool Visible(int x, int y)
        { return x >= 0 && x < TetrisRules.BoardWidth && y >= 0 && y < TetrisRules.VisibleRows; }

        private static Rect Tile(Rect bounds, float cell, int x, int y)
        { return new Rect(bounds.xMin + x * cell, bounds.yMax - (y + 1) * cell, cell, cell); }

        private static void DrawTile(VertexHelper mesh, Rect tile, TetrisPiece piece)
        {
            TetrisGraphic.Quad(mesh, tile, TetrisGraphic.PieceColor(piece));
            TetrisGraphic.Outline(mesh, tile, tile.width * .04f, UiFactory.Paper * new Color(1, 1, 1, .53f));
            TetrisGraphic.Quad(mesh, new Rect(tile.xMin, tile.yMin, tile.width, tile.width * .08f),
                UiFactory.Ink * new Color(1, 1, 1, .06f));
        }
    }
}
