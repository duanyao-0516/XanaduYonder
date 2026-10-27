using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Minesweeper
{
    internal enum MinesweeperIconKind { Flag, Mine, WrongFlag, Reveal, Overview, Expand, Collapse, Pause, Plus, Minus }

    // Game-local symbols use the same rounded mesh strokes as UiIconGraphic.
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class MinesweeperIconGraphic : MaskableGraphic
    {
        private MinesweeperIconKind kind;
        private bool contrastFlagPole;
        private Color strokeColor;
        public MinesweeperIconKind Kind { get { return kind; } set { kind = value; SetVerticesDirty(); } }
        public bool ContrastFlagPole { get { return contrastFlagPole; } set { contrastFlagPole = value; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            float size = Mathf.Min(rect.width, rect.height);
            Vector2 center = rect.center;
            if (size <= 0) return;
            strokeColor = color;
            switch (kind)
            {
                case MinesweeperIconKind.Flag:
                case MinesweeperIconKind.WrongFlag:
                    if (contrastFlagPole) { strokeColor = UiFactory.Ink; strokeColor.a = color.a; }
                    Line(mesh, center, size, -.22f, -.39f, -.22f, .40f);
                    Line(mesh, center, size, -.37f, -.39f, .14f, -.39f);
                    strokeColor = color;
                    Line(mesh, center, size, -.22f, .38f, .35f, .25f);
                    Line(mesh, center, size, .35f, .25f, -.22f, .07f);
                    if (kind == MinesweeperIconKind.WrongFlag)
                    {
                        Line(mesh, center, size, .02f, -.04f, .37f, -.38f);
                        Line(mesh, center, size, .37f, -.04f, .02f, -.38f);
                    }
                    break;
                case MinesweeperIconKind.Mine:
                    Disc(mesh, center, size * .24f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Stroke(mesh, center + v * size * .23f, center + v * size * .42f, size * .035f);
                    }
                    break;
                case MinesweeperIconKind.Reveal:
                    Line(mesh, center, size, -.32f, .38f, -.10f, -.36f);
                    Line(mesh, center, size, -.10f, -.36f, .05f, -.04f);
                    Line(mesh, center, size, .05f, -.04f, .36f, .12f);
                    Line(mesh, center, size, .36f, .12f, -.32f, .38f);
                    break;
                case MinesweeperIconKind.Overview:
                case MinesweeperIconKind.Collapse:
                case MinesweeperIconKind.Expand:
                    // Four diagonal arrows: overview always points inward, not a scan frame.
                    for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                    {
                        bool outward = kind == MinesweeperIconKind.Expand;
                        float tip = outward ? .42f : .12f;
                        float tail = outward ? .12f : .42f;
                        float wing = outward ? .22f : .32f;
                        Line(mesh, center, size, tail*x, tail*y, tip*x, tip*y);
                        Line(mesh, center, size, wing*x, tip*y, tip*x, tip*y);
                        Line(mesh, center, size, tip*x, wing*y, tip*x, tip*y);
                    }
                    break;
                case MinesweeperIconKind.Pause:
                    Line(mesh, center, size, -.16f, -.34f, -.16f, .34f);
                    Line(mesh, center, size, .16f, -.34f, .16f, .34f);
                    break;
                case MinesweeperIconKind.Plus:
                    Line(mesh, center, size, 0, -.34f, 0, .34f);
                    Line(mesh, center, size, -.34f, 0, .34f, 0);
                    break;
                case MinesweeperIconKind.Minus:
                    Line(mesh, center, size, -.34f, 0, .34f, 0);
                    break;
            }
        }
        private void Line(VertexHelper mesh, Vector2 center, float size, float ax, float ay, float bx, float by)
        { Stroke(mesh, center + new Vector2(ax, ay)*size, center + new Vector2(bx, by)*size, size*.035f); }
        private void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float radius)
        {
            Vector2 delta = b-a, normal = new Vector2(-delta.y, delta.x).normalized * radius;
            int n = mesh.currentVertCount;
            mesh.AddVert(a-normal,strokeColor,Vector2.zero); mesh.AddVert(a+normal,strokeColor,Vector2.zero);
            mesh.AddVert(b+normal,strokeColor,Vector2.zero); mesh.AddVert(b-normal,strokeColor,Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2); mesh.AddTriangle(n,n+2,n+3);
            Disc(mesh,a,radius); Disc(mesh,b,radius);
        }
        private void Disc(VertexHelper mesh, Vector2 center, float radius)
        {
            int n = mesh.currentVertCount;
            mesh.AddVert(center,strokeColor,Vector2.zero);
            const int count = 16;
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;
                mesh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,strokeColor,Vector2.zero);
            }
            for(int i=0;i<count;i++) mesh.AddTriangle(n,n+1+i,n+1+(i+1)%count);
        }
    }
}
