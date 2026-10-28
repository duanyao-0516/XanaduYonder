using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    internal enum UiIconKind { Home, Settings, Notebook, Close, Book, Restart, Back, Undo, Eraser, Pencil, Bell, Lock }

    // Small UI symbols are meshes rather than font glyphs, so their shape and centering
    // do not depend on the bundled CJK font or platform font fallback.
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class UiIconGraphic : MaskableGraphic
    {
        [SerializeField] private UiIconKind kind;

        public UiIconKind Kind
        {
            get { return kind; }
            set { if (kind == value) return; kind = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var bounds = GetPixelAdjustedRect();
            var size = Mathf.Min(bounds.width, bounds.height);
            if (size <= 0) return;
            var center = bounds.center;
            switch (kind)
            {
                case UiIconKind.Lock:
                    Path(mesh, center, size, new[] {
                        new Vector2(-.30f, .03f), new Vector2(.30f, .03f),
                        new Vector2(.30f, -.36f), new Vector2(-.30f, -.36f), new Vector2(-.30f, .03f)
                    });
                    Path(mesh, center, size, new[] {
                        new Vector2(-.20f, .03f), new Vector2(-.20f, .24f), new Vector2(-.14f, .34f),
                        new Vector2(0, .39f), new Vector2(.14f, .34f), new Vector2(.20f, .24f), new Vector2(.20f, .03f)
                    });
                    Stroke(mesh, center, size, new Vector2(0, -.10f), new Vector2(0, -.23f));
                    break;
                case UiIconKind.Home:
                    Stroke(mesh, center, size, new Vector2(-.42f, .01f), new Vector2(0, .40f));
                    Stroke(mesh, center, size, new Vector2(0, .40f), new Vector2(.42f, .01f));
                    Path(mesh, center, size, new[]
                    {
                        new Vector2(-.30f, .09f), new Vector2(-.30f, -.36f),
                        new Vector2(-.11f, -.36f), new Vector2(-.11f, -.10f),
                        new Vector2(.11f, -.10f), new Vector2(.11f, -.36f),
                        new Vector2(.30f, -.36f), new Vector2(.30f, .09f)
                    });
                    break;
                case UiIconKind.Settings:
                    Gear(mesh, center, size);
                    break;
                case UiIconKind.Notebook:
                    Path(mesh, center, size, new[]
                    {
                        new Vector2(-.27f, -.41f), new Vector2(.39f, -.41f),
                        new Vector2(.39f, .41f), new Vector2(-.27f, .41f),
                        new Vector2(-.27f, -.41f)
                    });
                    Stroke(mesh, center, size, new Vector2(-.14f, -.41f), new Vector2(-.14f, .41f));
                    for (var i = -1; i <= 1; i++)
                    {
                        Stroke(mesh, center, size, new Vector2(-.39f, i * .25f), new Vector2(-.23f, i * .25f));
                        Stroke(mesh, center, size, new Vector2(-.01f, i * .18f), new Vector2(.25f, i * .18f));
                    }
                    break;
                case UiIconKind.Close:
                    Stroke(mesh, center, size, new Vector2(-.30f, -.30f), new Vector2(.30f, .30f));
                    Stroke(mesh, center, size, new Vector2(-.30f, .30f), new Vector2(.30f, -.30f));
                    break;
                case UiIconKind.Book:
                    Path(mesh, center, size, new[]
                    {
                        new Vector2(0, -.32f), new Vector2(-.18f, -.24f), new Vector2(-.42f, -.24f),
                        new Vector2(-.42f, .34f), new Vector2(-.18f, .34f), new Vector2(0, .26f),
                        new Vector2(.18f, .34f), new Vector2(.42f, .34f), new Vector2(.42f, -.24f),
                        new Vector2(.18f, -.24f), new Vector2(0, -.32f), new Vector2(0, .26f)
                    });
                    for (var i = 0; i < 2; i++)
                    {
                        Stroke(mesh, center, size, new Vector2(-.30f, .16f - i * .16f), new Vector2(-.13f, .12f - i * .16f));
                        Stroke(mesh, center, size, new Vector2(.13f, .12f - i * .16f), new Vector2(.30f, .16f - i * .16f));
                    }
                    break;
                case UiIconKind.Restart:
                    CircularArrow(mesh, center, size, 20, 155);
                    CircularArrow(mesh, center, size, 200, 335);
                    break;
                case UiIconKind.Back:
                    Stroke(mesh, center, size, new Vector2(-.36f, 0), new Vector2(.36f, 0));
                    Path(mesh, center, size, new[] {
                        new Vector2(-.08f, .29f), new Vector2(-.37f, 0), new Vector2(-.08f, -.29f)
                    });
                    break;
                case UiIconKind.Undo:
                    Path(mesh, center, size, new[] {
                        new Vector2(-.12f, .38f), new Vector2(-.37f, .14f), new Vector2(-.12f, -.10f)
                    });
                    Path(mesh, center, size, new[] {
                        new Vector2(-.36f, .14f), new Vector2(.10f, .14f), new Vector2(.25f, .10f),
                        new Vector2(.35f, -.01f), new Vector2(.37f, -.16f), new Vector2(.31f, -.29f),
                        new Vector2(.18f, -.35f), new Vector2(-.03f, -.35f)
                    });
                    break;
                case UiIconKind.Eraser:
                    Path(mesh, center, size, new[] {
                        new Vector2(-.38f, -.12f), new Vector2(.10f, .36f), new Vector2(.39f, .07f),
                        new Vector2(-.02f, -.34f), new Vector2(-.16f, -.34f), new Vector2(-.38f, -.12f)
                    });
                    Stroke(mesh, center, size, new Vector2(-.20f, .06f), new Vector2(.09f, -.23f));
                    Stroke(mesh, center, size, new Vector2(-.16f, -.34f), new Vector2(.40f, -.34f));
                    break;
                case UiIconKind.Pencil:
                    Path(mesh, center, size, new[] {
                        new Vector2(-.38f, -.38f), new Vector2(-.28f, -.09f), new Vector2(.20f, .39f),
                        new Vector2(.39f, .20f), new Vector2(-.09f, -.28f), new Vector2(-.38f, -.38f)
                    });
                    Stroke(mesh, center, size, new Vector2(.08f, .27f), new Vector2(.27f, .08f));
                    Stroke(mesh, center, size, new Vector2(-.28f, -.09f), new Vector2(-.09f, -.28f));
                    break;
                case UiIconKind.Bell:
                    Path(mesh, center, size, new[] {
                        new Vector2(-.37f, -.21f), new Vector2(-.27f, -.08f), new Vector2(-.25f, .14f),
                        new Vector2(-.20f, .27f), new Vector2(-.10f, .34f), new Vector2(.10f, .34f),
                        new Vector2(.20f, .27f), new Vector2(.25f, .14f), new Vector2(.27f, -.08f),
                        new Vector2(.37f, -.21f), new Vector2(-.37f, -.21f)
                    });
                    Stroke(mesh, center, size, new Vector2(0, .34f), new Vector2(0, .43f));
                    Path(mesh, center, size, new[] {
                        new Vector2(-.10f, -.32f), new Vector2(-.06f, -.39f),
                        new Vector2(.06f, -.39f), new Vector2(.10f, -.32f)
                    });
                    break;
            }
        }

        private void Path(VertexHelper mesh, Vector2 center, float size, Vector2[] points)
        {
            for (var i = 1; i < points.Length; i++) Stroke(mesh, center, size, points[i - 1], points[i]);
        }

        private void CircularArrow(VertexHelper mesh, Vector2 center, float size, float start, float end)
        {
            const int steps = 18;
            const float radius = .34f;
            var previous = new Vector2(Mathf.Cos(start * Mathf.Deg2Rad), Mathf.Sin(start * Mathf.Deg2Rad)) * radius;
            for (var i = 1; i <= steps; i++)
            {
                var angle = Mathf.Lerp(start, end, (float)i / steps) * Mathf.Deg2Rad;
                var point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Stroke(mesh, center, size, previous, point);
                previous = point;
            }
            var direction = new Vector2(-Mathf.Sin(end * Mathf.Deg2Rad), Mathf.Cos(end * Mathf.Deg2Rad));
            var normal = new Vector2(-direction.y, direction.x);
            Stroke(mesh, center, size, previous, previous - direction * .17f + normal * .12f);
            Stroke(mesh, center, size, previous, previous - direction * .17f - normal * .12f);
        }

        private void Stroke(VertexHelper mesh, Vector2 center, float size, Vector2 start, Vector2 end)
        {
            var a = center + start * size;
            var b = center + end * size;
            var delta = b - a;
            var radius = size * .035f;
            var normal = new Vector2(-delta.y, delta.x).normalized * radius;
            Quad(mesh, a - normal, a + normal, b + normal, b - normal);
            Cap(mesh, a, radius);
            Cap(mesh, b, radius);
        }

        private void Cap(VertexHelper mesh, Vector2 center, float radius)
        {
            const int segments = 10;
            var first = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
            }
            for (var i = 0; i < segments; i++) mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % segments);
        }

        private void Gear(VertexHelper mesh, Vector2 center, float size)
        {
            const int segments = 64;
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                var angle = i * Mathf.PI * 2 / segments;
                var nextAngle = next * Mathf.PI * 2 / segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var nextDirection = new Vector2(Mathf.Cos(nextAngle), Mathf.Sin(nextAngle));
                var radius = i % 8 < 2 || i % 8 > 5 ? .35f : .47f;
                var nextRadius = next % 8 < 2 || next % 8 > 5 ? .35f : .47f;
                Quad(mesh, center + direction * (size * .17f), center + direction * (size * radius),
                    center + nextDirection * (size * nextRadius), center + nextDirection * (size * .17f));
            }
        }

        private void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var first = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddVert(d, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
