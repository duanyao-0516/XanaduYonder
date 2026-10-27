using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Sudoku
{
    internal enum SudokuIconKind { Book, Left, Right, Down }

    // UI geometry keeps the navigation symbols independent of font glyphs and font fallback.
    internal sealed class SudokuIcon : MaskableGraphic
    {
        public SudokuIconKind Kind;
        private static Sprite circle;

        public static SudokuIcon Add(Transform parent, SudokuIconKind kind, float x, float y, float size)
        {
            var icon = UiFactory.Rect(parent, kind + " icon", x, y, size, size).gameObject.AddComponent<SudokuIcon>();
            icon.Kind = kind; icon.color = UiFactory.Ink; icon.raycastTarget = false;
            return icon;
        }

        public static Button Button(Transform parent, string name, SudokuIconKind kind, float x, float y, float size, Action action, bool round = false)
        {
            var button = UiFactory.Button(parent, "", x, y, size, size, action);
            button.name = name;
            if (round)
            {
                if (circle == null)
                {
                    const int n = 96;
                    var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Sudoku circular control", filterMode = FilterMode.Bilinear };
                    var pixels = new Color[n * n];
                    for (int row = 0; row < n; row++)
                    for (int column = 0; column < n; column++)
                    {
                        float distance = new Vector2(column + .5f - n / 2f, row + .5f - n / 2f).magnitude;
                        pixels[row * n + column] = new Color(1, 1, 1, Mathf.Clamp01(n / 2f - distance));
                    }
                    texture.SetPixels(pixels); texture.Apply();
                    circle = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f));
                }
                var background = button.GetComponent<Image>(); background.sprite = circle; background.type = Image.Type.Simple;
            }
            float iconSize = kind == SudokuIconKind.Book ? 34 : 28;
            Add(button.transform, kind, (size - iconSize) / 2, (size - iconSize) / 2, iconSize);
            return button;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Kind == SudokuIconKind.Book)
            {
                Path(vh, new Vector2(.5f, .20f), new Vector2(.37f, .27f), new Vector2(.10f, .27f), new Vector2(.10f, .83f), new Vector2(.34f, .83f), new Vector2(.5f, .75f), new Vector2(.66f, .83f), new Vector2(.90f, .83f), new Vector2(.90f, .27f), new Vector2(.63f, .27f), new Vector2(.5f, .20f), new Vector2(.5f, .75f));
                Line(vh, new Vector2(.22f, .64f), new Vector2(.36f, .64f));
                Line(vh, new Vector2(.64f, .64f), new Vector2(.78f, .64f));
            }
            else if (Kind == SudokuIconKind.Left) Path(vh, new Vector2(.65f, .82f), new Vector2(.33f, .5f), new Vector2(.65f, .18f));
            else if (Kind == SudokuIconKind.Right) Path(vh, new Vector2(.35f, .82f), new Vector2(.67f, .5f), new Vector2(.35f, .18f));
            else Path(vh, new Vector2(.18f, .65f), new Vector2(.5f, .33f), new Vector2(.82f, .65f));
        }

        private void Path(VertexHelper vh, params Vector2[] points)
        {
            for (int i = 1; i < points.Length; i++) Line(vh, points[i - 1], points[i]);
        }
        private void Line(VertexHelper vh, Vector2 a, Vector2 b)
        {
            var rect = rectTransform.rect;
            a = rect.min + Vector2.Scale(a, rect.size); b = rect.min + Vector2.Scale(b, rect.size);
            var delta = (b - a).normalized; var perpendicular = new Vector2(-delta.y, delta.x) * Mathf.Min(rect.width, rect.height) * .037f;
            int start = vh.currentVertCount;
            vh.AddVert(a - perpendicular, color, Vector2.zero); vh.AddVert(a + perpendicular, color, Vector2.zero);
            vh.AddVert(b + perpendicular, color, Vector2.zero); vh.AddVert(b - perpendicular, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
