using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime
{
    // Paint the owning Canvas underneath the content, including space outside the game's safe frame.
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class DialogBackdrop : MaskableGraphic
    {
        private Graphic source;
        private RectTransform boundary;
        private readonly Vector3[] corners = new Vector3[4];
        private readonly Vector2[] vertices = new Vector2[4];

        public static void CoverCanvas(Graphic shade)
        {
            var canvas = shade.GetComponentInParent<Canvas>(true);
            if (canvas == null || shade.GetComponentInChildren<DialogBackdrop>(true) != null) return;
            var rect = UiFactory.Rect(shade.transform, "Canvas backdrop", 0, 0, 1, 1);
            rect.gameObject.layer = shade.gameObject.layer;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.SetAsFirstSibling();
            var backdrop = rect.gameObject.AddComponent<DialogBackdrop>();
            backdrop.source = shade;
            backdrop.boundary = (RectTransform)canvas.transform;
            // Use one emitter for the whole area. Keep the original transform for the safe-area panel and focus scope.
            shade.enabled = false;
            backdrop.RefreshBounds();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Canvas.preWillRenderCanvases += RefreshBounds;
        }

        protected override void OnDisable()
        {
            Canvas.preWillRenderCanvases -= RefreshBounds;
            base.OnDisable();
        }

        private void RefreshBounds()
        {
            // Unity updates Graphic.canvas when a page is reparented or its nearest Canvas changes.
            if (canvas != null) boundary = (RectTransform)canvas.transform;
            if (boundary == null || source == null) return;
            color = source.color;
            raycastTarget = source.raycastTarget;
            boundary.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = source.transform.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, corners[i]);
                max = Vector2.Max(max, corners[i]);
            }
            var center = (min + max) * .5f;
            var size = max - min;
            if (rectTransform.localPosition != (Vector3)center) rectTransform.localPosition = center;
            if (rectTransform.sizeDelta != size) rectTransform.sizeDelta = size;
            for (int i = 0; i < vertices.Length; i++)
            {
                var point = (Vector2)corners[i] - center;
                if (vertices[i] != point) { vertices[i] = point; SetVerticesDirty(); }
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            foreach (var point in vertices) mesh.AddVert(point, color, Vector2.zero);
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(2, 3, 0);
        }

        public override bool Raycast(Vector2 point, Camera camera)
        {
            return boundary != null && RectTransformUtility.RectangleContainsScreenPoint(boundary, point, camera)
                && base.Raycast(point, camera);
        }
    }
}
