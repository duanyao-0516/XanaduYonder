using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.GoldMiner
{
    /// <summary>A narrow, shaded three-ply cord. Its top pivot is the actual rope attachment.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class GoldMinerHempRope : MaskableGraphic
    {
        private float travel;
        public void SetTravel(float value)
        {
            if (Mathf.Approximately(travel, value)) return;
            travel = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            float width = rect.width, length = rect.height;
            if (width <= 0 || length <= 0) return;
            var edge = new Color32(73, 47, 26, 255);
            var shade = new Color32(161, 120, 64, 255);
            var flax = new Color32(226, 195, 137, 255);
            var light = new Color32(255, 233, 186, 255);
            Quad(mesh, rect.xMin, rect.yMin, rect.xMax, rect.yMax, edge);
            Quad(mesh, rect.xMin + width * .18f, rect.yMin, rect.xMax - width * .18f, rect.yMax, flax);
            // Each diagonal is a strand winding around the cord, with a small lit edge.
            // Phase follows line travel, so the weave stops with the simulation while paused.
            float pitch = width * 1.75f;
            float phase = Mathf.Repeat(travel, pitch);
            // Anchor winding to the fixed upper attachment. Starting at yMin would
            // cancel travel against the changing rope length and freeze the weave.
            for (float y = rect.yMax + pitch - phase; y > rect.yMin - pitch; y -= pitch)
            {
                Diagonal(mesh, rect, y, width * .65f, width * .27f, shade);
                Diagonal(mesh, rect, y + width * .31f, width * .65f, width * .19f, light);
            }
        }

        private static void Quad(VertexHelper mesh, float left, float bottom, float right, float top, Color32 color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(left, bottom), color, Vector2.zero);
            mesh.AddVert(new Vector3(left, top), color, Vector2.zero);
            mesh.AddVert(new Vector3(right, top), color, Vector2.zero);
            mesh.AddVert(new Vector3(right, bottom), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Diagonal(VertexHelper mesh, Rect bounds, float y, float slope, float thickness, Color32 color)
        {
            int start = mesh.currentVertCount;
            float left = bounds.xMin + bounds.width * .18f, right = bounds.xMax - bounds.width * .18f;
            mesh.AddVert(new Vector3(left, Mathf.Clamp(y, bounds.yMin, bounds.yMax)), color, Vector2.zero);
            mesh.AddVert(new Vector3(left, Mathf.Clamp(y + thickness, bounds.yMin, bounds.yMax)), color, Vector2.zero);
            mesh.AddVert(new Vector3(right, Mathf.Clamp(y + slope + thickness, bounds.yMin, bounds.yMax)), color, Vector2.zero);
            mesh.AddVert(new Vector3(right, Mathf.Clamp(y + slope, bounds.yMin, bounds.yMax)), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
