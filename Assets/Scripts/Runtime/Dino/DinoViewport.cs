using UnityEngine;

namespace XanaduYonder.Runtime.Dino
{
    // Separate from the existing portrait shell: rotate without shrinking a portrait frame.
    internal sealed class DinoViewport
    {
        private readonly RectTransform canvas, root;
        private Rect lastSafe;
        private Vector2 lastPixels, lastCanvas;
        public float Width { get; private set; }
        public float Height { get; private set; }
        public bool Landscape { get; private set; }
        public DinoViewport(RectTransform canvas, RectTransform root) { this.canvas = canvas; this.root = root; }
        public bool Refresh()
        {
            var pixels = new Vector2(Screen.width, Screen.height);
            var area = Screen.safeArea;
            if (area.width <= 0 || area.height <= 0) area = new Rect(0,0,pixels.x,pixels.y);
            if (pixels.x <= 0 || pixels.y <= 0 || canvas.rect.width <= 0 || canvas.rect.height <= 0) return false;
            if (area == lastSafe && pixels == lastPixels && lastCanvas == canvas.rect.size) return false;
            lastSafe = area; lastPixels = pixels; lastCanvas = canvas.rect.size;
            var shortSide = Mathf.Min(area.width, area.height);
            // Map the short side to 720 units in both orientations, retaining character size.
            var physicalScale = shortSide / 720f;
            Width = area.width / physicalScale;
            Height = area.height / physicalScale;
            Landscape = area.width > area.height;
            var canvasScale = canvas.rect.width / pixels.x;
            root.anchorMin = root.anchorMax = new Vector2(.5f,.5f);
            root.pivot = new Vector2(.5f,.5f);
            root.localScale = Vector3.one * (physicalScale * canvasScale);
            root.sizeDelta = new Vector2(Width,Height);
            root.anchoredPosition = (area.center - pixels * .5f) * canvasScale;
            return true;
        }
    }
}
