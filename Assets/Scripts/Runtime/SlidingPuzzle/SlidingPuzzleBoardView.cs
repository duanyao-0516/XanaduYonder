using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XanaduYonder.Core.SlidingPuzzle;

namespace XanaduYonder.Runtime.SlidingPuzzle
{
    // Presentation only. The game owns the permutation; animation is always cancellable.
    internal sealed class SlidingPuzzleBoardView
    {
        private readonly RectTransform root;
        private readonly Dictionary<int, RectTransform> tiles = new Dictionary<int, RectTransform>();
        private readonly Dictionary<int, Vector2> starts = new Dictionary<int, Vector2>();
        private int[] cells;
        private int size;
        private float elapsed, scale;
        public bool Animating { get; private set; }
        public SlidingPuzzleBoardView(RectTransform parent) { root = parent; }
        public void Render(SlidingPuzzleGame game, float uiScale, bool animate = false)
        {
            bool rebuild = size != game.Size || Math.Abs(scale - uiScale) > .001f;
            scale = uiScale; size = game.Size; cells = game.GetCells();
            if (rebuild)
            {
                for (int i = root.childCount - 1; i >= 0; i--) { var child = root.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child); }
                tiles.Clear();
                for (int i = 0; i < cells.Length; i++)
                {
                    var empty = UiFactory.Panel(root, "Cell " + i, 0, 0, CellSize, CellSize, UiFactory.Hex("D9E1D3"));
                    Round(empty, 8 * scale); Position(empty.rectTransform, i);
                }
                for (int value = 1; value < cells.Length; value++)
                {
                    var tile = UiFactory.Panel(root, "Tile " + value, 0, 0, CellSize, CellSize, UiFactory.Hex("F5F1D9"));
                    Round(tile, 8 * scale);
                    var label = UiFactory.Label(tile.transform, value.ToString(), 0, 0, CellSize, CellSize,
                        Mathf.RoundToInt((size == 3 ? 36 : size == 4 ? 29 : 24) * scale), UiFactory.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                    label.name = "Tile number"; label.supportRichText = false;
                    tiles.Add(value, tile.rectTransform);
                }
            }
            starts.Clear();
            foreach (var pair in tiles) starts[pair.Key] = pair.Value.anchoredPosition;
            elapsed = 0; Animating = animate && !rebuild;
            if (!Animating) Finish();
        }
        public bool Tick(float seconds)
        {
            if (!Animating) return false;
            elapsed += Mathf.Max(0, seconds);
            float t = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / .13f), 3);
            foreach (var pair in tiles) pair.Value.anchoredPosition = Vector2.Lerp(starts[pair.Key], Location(Array.IndexOf(cells, pair.Key)), t);
            if (elapsed < .13f) return false;
            Finish(); return true;
        }
        public void Finish()
        {
            Animating = false;
            foreach (var pair in tiles) Position(pair.Value, Array.IndexOf(cells, pair.Key));
        }
        public int IndexAt(Vector2 local)
        {
            float x = local.x - Padding, y = -local.y - Padding;
            if (x < 0 || y < 0) return -1;
            int col = Mathf.FloorToInt(x / (CellSize + Gap)), row = Mathf.FloorToInt(y / (CellSize + Gap));
            if (col >= size || row >= size || x - col * (CellSize + Gap) > CellSize || y - row * (CellSize + Gap) > CellSize) return -1;
            return row * size + col;
        }
        private float Padding { get { return root.rect.width * .0263f; } }
        private float Gap { get { return root.rect.width * .0197f; } }
        private float CellSize { get { return (root.rect.width - Padding * 2 - Gap * (size - 1)) / size; } }
        private Vector2 Location(int index) { return new Vector2(Padding + index % size * (CellSize + Gap), -Padding - index / size * (CellSize + Gap)); }
        private void Position(RectTransform tile, int index) { tile.anchoredPosition = Location(index); tile.sizeDelta = Vector2.one * CellSize; }
        internal static void Round(Image image, float radius) { image.pixelsPerUnitMultiplier = 16 / Mathf.Max(1, radius); }
    }
}
