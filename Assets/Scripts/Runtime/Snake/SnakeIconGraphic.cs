using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Snake
{
    internal enum SnakeIconKind { Up, Right, Down, Left, Pause, Wrap, Lock }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class SnakeIconGraphic : MaskableGraphic
    {
        public SnakeIconKind Kind;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            float size = Mathf.Min(rect.width, rect.height);
            var center = rect.center;
            if (Kind == SnakeIconKind.Pause)
            {
                Line(mesh, center + new Vector2(-.15f, -.3f) * size, center + new Vector2(-.15f, .3f) * size, size * .10f);
                Line(mesh, center + new Vector2(.15f, -.3f) * size, center + new Vector2(.15f, .3f) * size, size * .10f);
                return;
            }
            if (Kind == SnakeIconKind.Wrap)
            {
                Line(mesh, center + Vector2.left * size * .4f, center + Vector2.right * size * .4f, size * .075f);
                Chevron(mesh, center + Vector2.left * size * .25f, size * .55f, SnakeIconKind.Left);
                Chevron(mesh, center + Vector2.right * size * .25f, size * .55f, SnakeIconKind.Right);
                return;
            }
            if (Kind == SnakeIconKind.Lock)
            {
                var points = new[] { new Vector2(-.3f,-.1f),new Vector2(.3f,-.1f),new Vector2(.3f,-.4f),new Vector2(-.3f,-.4f),new Vector2(-.3f,-.1f),new Vector2(-.2f,-.1f),new Vector2(-.2f,.22f),new Vector2(0,.36f),new Vector2(.2f,.22f),new Vector2(.2f,-.1f) };
                for (int i = 1; i < points.Length; i++) Line(mesh, center + points[i-1] * size, center + points[i] * size, size * .07f);
                return;
            }
            Chevron(mesh, center, size, Kind);
        }
        private void Chevron(VertexHelper mesh, Vector2 center, float size, SnakeIconKind direction)
        {
            Vector2 forward = direction == SnakeIconKind.Up ? Vector2.up : direction == SnakeIconKind.Down ? Vector2.down : direction == SnakeIconKind.Left ? Vector2.left : Vector2.right;
            Vector2 side = new Vector2(-forward.y, forward.x);
            var tip = center + forward * size * .17f;
            Line(mesh, center - forward * size * .14f + side * size * .27f, tip, size * .075f);
            Line(mesh, tip, center - forward * size * .14f - side * size * .27f, size * .075f);
        }
        private void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width)
        {
            Vector2 side = new Vector2(-(b-a).y, (b-a).x).normalized * width * .5f;
            int i = mesh.currentVertCount;
            mesh.AddVert(a-side, color, Vector2.zero); mesh.AddVert(a+side, color, Vector2.zero);
            mesh.AddVert(b+side, color, Vector2.zero); mesh.AddVert(b-side, color, Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2); mesh.AddTriangle(i,i+2,i+3);
        }
        public static SnakeIconGraphic Add(Transform parent, SnakeIconKind kind, float x, float y, float size, Color tint)
        {
            var icon = UiFactory.Rect(parent, kind + " icon", x, y, size, size).gameObject.AddComponent<SnakeIconGraphic>();
            icon.Kind = kind; icon.color = tint; icon.raycastTarget = false; return icon;
        }
    }
}
