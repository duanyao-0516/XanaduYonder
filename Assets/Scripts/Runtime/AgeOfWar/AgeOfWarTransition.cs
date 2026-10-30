using System;
using UnityEngine;
using UnityEngine.UI;

namespace XanaduYonder.Runtime.AgeOfWar
{
    /// <summary>Original Moon bitmap transitions. Takes ownership of snapshot; Dispose destroys it.</summary>
    internal sealed class AgeOfWarTransition : IDisposable
    {
        private readonly RectTransform root;
        private readonly Texture2D snapshot;
        private readonly AgeOfWarTransitionState state;
        private readonly RawImage full;
        private readonly RectTransform[] pieces;
        private bool disposed;

        internal AgeOfWarTransition(Transform parent, Texture2D snapshot, int stageW, int stageH, bool fade)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            if (snapshot.width != stageW || snapshot.height != stageH)
                throw new ArgumentException("Transition snapshot must use the original stage pixel dimensions.", "snapshot");
            state = new AgeOfWarTransitionState(stageW, stageH, fade);
            this.snapshot = snapshot;
            // Flash Bitmap and BitmapData.draw use unsmoothed samples in these source classes.
            snapshot.filterMode = FilterMode.Point;
            snapshot.wrapMode = TextureWrapMode.Clamp;
            root = AgeOfWarDrawing.Rect(parent, "Original bitmap transition", 0, 0, stageW, stageH);
            full = Image(root, "Snapshot", snapshot, 0, 0, stageW, stageH, new Rect(0, 0, 1, 1));
            pieces = new RectTransform[state.Tiles.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                var tile = state.Tiles[i];
                var pivot = AgeOfWarDrawing.Rect(root, "Bitmap tile " + i, (float)tile.X, (float)tile.Y);
                pieces[i] = pivot;
                // UVs keep the source's fractional crop translation while BitmapData dimensions
                // truncate to integers. Snapshot texture coordinates originate at the bottom left.
                var uv = new Rect((float)(tile.SourceX / stageW),
                    (float)(1 - (tile.SourceY + tile.Height) / stageH),
                    (float)tile.Width / stageW, (float)tile.Height / stageH);
                Image(pivot, "Cropped snapshot", snapshot, (float)tile.ImageX, (float)tile.ImageY,
                    tile.Width, tile.Height, uv);
            }
            // TransitionsManager keeps an invisible click blocker above the screenshot.
            var blocker = AgeOfWarDrawing.Rect(root, "Transition click blocker", 0, 0, stageW, stageH).gameObject.AddComponent<Image>();
            blocker.color = Color.clear;
            blocker.raycastTarget = true;
        }

        internal bool Done { get { return state.Done; } }

        internal void Advance(float dt)
        {
            if (disposed) return;
            state.Advance(dt);
            full.gameObject.SetActive(state.FullBitmapVisible);
            full.color = new Color(1, 1, 1, (float)state.Alpha);
            for (int i = 0; i < pieces.Length; i++)
            {
                var tile = state.Tiles[i];
                pieces[i].gameObject.SetActive(tile.Visible);
                pieces[i].localScale = new Vector3((float)tile.Scale, (float)tile.Scale, 1);
                pieces[i].localRotation = Quaternion.Euler(0, 0, -(float)tile.Rotation);
            }
        }

        private static RawImage Image(Transform parent, string name, Texture texture, float x, float y, float width, float height, Rect uv)
        {
            var image = AgeOfWarDrawing.Rect(parent, name, x, y, width, height).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.raycastTarget = false;
            return image;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            root.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(root.gameObject);
            UnityEngine.Object.Destroy(snapshot);
        }
    }
}
