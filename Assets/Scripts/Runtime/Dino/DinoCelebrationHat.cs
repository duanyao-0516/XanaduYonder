using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.Dino
{
    // Original pixel geometry, drawn independently of the Chromium sprite palette.
    // This is decoration only: the simulation never reads this graphic or its bounds.
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class DinoCelebrationHat : MaskableGraphic
    {
        internal const float PixelWidth = 22;
        internal const float PixelHeight = 14;
        private DinoCelebration kind;
        internal DinoCelebration Kind
        {
            get { return kind; }
            set { if (kind == value) return; kind = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var bounds = GetPixelAdjustedRect();
            if (bounds.width <= 0 || bounds.height <= 0) return;
            Color32 red = new Color32(200, 55, 52, 255);
            Color32 gold = new Color32(235, 180, 59, 255);
            Color32 cream = new Color32(246, 235, 207, 255);
            Color32 purple = new Color32(121, 83, 170, 255);
            Color32 orange = new Color32(232, 144, 54, 255);
            Color32 turquoise = new Color32(46, 158, 167, 255);
            Color32 pink = new Color32(215, 91, 136, 255);
            switch (kind)
            {
                case DinoCelebration.Festival:
                    PixelRect(mesh, bounds, 5, 2, 12, 11, red);
                    PixelRect(mesh, bounds, 1, 0, 20, 2, gold);
                    PixelRect(mesh, bounds, 5, 3, 12, 3, gold);
                    PixelRect(mesh, bounds, 7, 11, 2, 2, cream);
                    break;
                case DinoCelebration.Halloween:
                    PixelRect(mesh, bounds, 1, 0, 20, 2, orange);
                    PixelRect(mesh, bounds, 4, 2, 14, 2, purple);
                    PixelRect(mesh, bounds, 6, 4, 10, 3, purple);
                    PixelRect(mesh, bounds, 8, 7, 7, 3, purple);
                    PixelRect(mesh, bounds, 10, 10, 5, 2, purple);
                    PixelRect(mesh, bounds, 13, 12, 5, 2, purple);
                    PixelRect(mesh, bounds, 6, 3, 10, 2, orange);
                    PixelRect(mesh, bounds, 10, 3, 3, 3, gold);
                    break;
                case DinoCelebration.Christmas:
                    PixelRect(mesh, bounds, 5, 3, 12, 5, red);
                    PixelRect(mesh, bounds, 7, 8, 9, 3, red);
                    PixelRect(mesh, bounds, 9, 11, 8, 2, red);
                    PixelRect(mesh, bounds, 4, 1, 14, 3, cream);
                    PixelRect(mesh, bounds, 15, 8, 4, 4, cream);
                    break;
                case DinoCelebration.Birthday:
                    PixelRect(mesh, bounds, 3, 0, 16, 2, pink);
                    PixelRect(mesh, bounds, 5, 2, 12, 3, turquoise);
                    PixelRect(mesh, bounds, 7, 5, 8, 3, turquoise);
                    PixelRect(mesh, bounds, 9, 8, 4, 3, turquoise);
                    PixelRect(mesh, bounds, 10, 11, 3, 3, gold);
                    PixelRect(mesh, bounds, 6, 3, 3, 2, gold);
                    PixelRect(mesh, bounds, 10, 6, 3, 2, pink);
                    break;
            }
        }

        private static void PixelRect(VertexHelper mesh, Rect bounds, float x, float y, float width, float height, Color32 tint)
        {
            float left = bounds.xMin + x / PixelWidth * bounds.width;
            float bottom = bounds.yMin + y / PixelHeight * bounds.height;
            float right = left + width / PixelWidth * bounds.width;
            float top = bottom + height / PixelHeight * bounds.height;
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(left, bottom), tint, Vector2.zero);
            mesh.AddVert(new Vector3(left, top), tint, Vector2.zero);
            mesh.AddVert(new Vector3(right, top), tint, Vector2.zero);
            mesh.AddVert(new Vector3(right, bottom), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
